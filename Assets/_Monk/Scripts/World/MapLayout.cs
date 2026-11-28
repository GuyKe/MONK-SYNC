using System;
using System.Collections.Generic;
using UnityEngine;

namespace Monk.World
{
    public enum RoomKind { Hub, Pit, Server }
    public enum LobbyKind { Atrium, Tall, Pillars }
    public enum Theme { Default, Mold, Office }

    public struct CellRect
    {
        public int x, z, w, h;
        public CellRect(int x, int z, int w, int h) { this.x = x; this.z = z; this.w = w; this.h = h; }
        public bool Contains(int cx, int cz) => cx >= x && cx < x + w && cz >= z && cz < z + h;
        public bool Overlaps(CellRect o, int margin) =>
            x - margin < o.x + o.w && x + w + margin > o.x && z - margin < o.z + o.h && z + h + margin > o.z;
    }

    public sealed class RoomInfo
    {
        public RoomKind kind;
        public CellRect rect;
    }

    public sealed class LobbyInfo
    {
        public LobbyKind kind;
        public CellRect rect;
    }

    /// <summary>
    /// The Level 0 map as data, generated from a seed. One character per 4 m cell. Every cell is floor except the solid
    /// block the facility hub sits on; thin partition walls separate neighbouring cells unless a passage was carved
    /// between them (a maze). Hand-designed rooms are stamped in, plus open lobbies:
    ///   .  carpet   ,  tall hall   F  flooded   E  exit seam   H  atrium (the big hall the hub overlooks)
    ///   D  fire door (manual)   B  blast door   A  airlock
    ///   R  server room   o  pit-room ledge   p  pit   =  walkway along X   |  walkway along Z   +  junction
    /// Rows are indexed by z (row 0 is the south edge).
    /// </summary>
    public static class MapLayout
    {
        public const float Cell = 4f;
        public const float PitDepth = 6f;
        public const int DefaultSize = 44;
        public const int DefaultSeed = 20260101;

        /// <summary>Height of the hub floor above the Level 0 floor (0 = the hub is level with the maze).</summary>
        public const float HubFloorY = 0f;
        public const float AtriumCeiling = 12.2f;

        private static readonly string[] PitTemplate =
        {
            "o=o=o",
            "|p|p|",
            "+=+=+",
            "|ppp|",
            "o===o",
        };

        private static readonly string[] ServerTemplate =
        {
            "__D__",
            "RRRRR",
            "RRRRR",
            "RRRRR",
        };

        public static readonly int[] DX = { 0, 1, 0, -1 }; // N, E, S, W
        public static readonly int[] DZ = { 1, 0, -1, 0 };

        public static int Width { get; private set; }
        public static int Height { get; private set; }
        public static int Seed { get; private set; }
        public static Vector2Int ExitCell { get; private set; }

        /// <summary>The solid block the facility hub is built on (10 x 6 cells). Its south face looks into the atrium.</summary>
        public static CellRect HubRect { get; private set; }
        /// <summary>The big open hall directly south of the hub (10 x 8 cells).</summary>
        public static CellRect AtriumRect { get; private set; }

        public static readonly List<RoomInfo> Rooms = new List<RoomInfo>();
        public static readonly List<LobbyInfo> Lobbies = new List<LobbyInfo>();
        public static readonly List<Vector2Int> DeadEnds = new List<Vector2Int>();
        public static readonly List<CellRect> FloodBoxes = new List<CellRect>();

        private static char[,] _cell;
        private static int[,] _roomId, _lobbyId;
        private static bool[,] _doorAlongZ;
        private static Theme[,] _theme;
        private static HashSet<int> _pass;

        // ------------------------------------------------------------------ queries

        public static char At(int x, int z) =>
            (_cell == null || x < 0 || z < 0 || x >= Width || z >= Height) ? ' ' : _cell[x, z];

        public static Theme ThemeAt(int x, int z) =>
            (_theme == null || x < 0 || z < 0 || x >= Width || z >= Height) ? Theme.Default : _theme[x, z];

        public static bool IsFloor(char c) => c != ' ';
        public static bool IsWalkable(char c) => c != ' ' && c != 'p';
        public static bool IsPitSpace(char c) => c == 'p' || c == '=' || c == '|' || c == '+';
        public static bool IsDoor(char c) => c == 'A' || c == 'D' || c == 'B';

        public static float CeilingHeight(char c)
        {
            switch (c)
            {
                case 'H': return AtriumCeiling;
                case ',': return 5.2f;
                case 'R': return 3.0f;
                case 'o': case 'p': case '=': case '|': case '+': return 6.4f;
                default: return 3.2f;
            }
        }

        public static Vector3 CellCenter(int x, int z) => new Vector3((x + 0.5f) * Cell, 0f, (z + 0.5f) * Cell);

        /// <summary>True if a door cell's passage runs along Z (the door panel then spans X).</summary>
        public static bool DoorRunsAlongZ(int x, int z) =>
            _doorAlongZ != null && x >= 0 && z >= 0 && x < Width && z < Height && _doorAlongZ[x, z];

        public static int RoomIdAt(int x, int z) =>
            (_roomId == null || x < 0 || z < 0 || x >= Width || z >= Height) ? -1 : _roomId[x, z];

        public static int LobbyIdAt(int x, int z) =>
            (_lobbyId == null || x < 0 || z < 0 || x >= Width || z >= Height) ? -1 : _lobbyId[x, z];

        private static bool AllowsDirection(char c, int x, int z, int dx, int dz)
        {
            if (c == '=') return dz == 0;
            if (c == '|') return dx == 0;
            if (IsDoor(c)) return DoorRunsAlongZ(x, z) ? dx == 0 : dz == 0;
            return true;
        }

        // Edge key: each shared edge is stored once, from its south/west cell, as north (0) or east (1).
        private static int Key(int x, int z, int dir)
        {
            switch (dir)
            {
                case 2: z -= 1; dir = 0; break;
                case 3: x -= 1; dir = 1; break;
            }
            return ((z * Width) + x) * 2 + dir;
        }

        /// <summary>Does the edge of cell (x,z) in direction dir (0=N,1=E,2=S,3=W) need a wall?</summary>
        public static bool NeedsWall(int x, int z, int dir)
        {
            int nx = x + DX[dir], nz = z + DZ[dir];
            char a = At(x, z), b = At(nx, nz);
            if (!IsFloor(a) || !IsFloor(b)) return true;
            if (IsPitSpace(a) && IsPitSpace(b)) return false;
            if (!_pass.Contains(Key(x, z, dir))) return true;
            return !AllowsDirection(a, x, z, DX[dir], DZ[dir]) || !AllowsDirection(b, nx, nz, -DX[dir], -DZ[dir]);
        }

        /// <summary>Can a player walk from (x,z) one step in (dx,dz)?</summary>
        public static bool Open(int x, int z, int dx, int dz)
        {
            int dir = dx == 1 ? 1 : dx == -1 ? 3 : dz == 1 ? 0 : 2;
            return IsWalkable(At(x, z)) && IsWalkable(At(x + dx, z + dz)) && !NeedsWall(x, z, dir);
        }

        public static HashSet<Vector2Int> ReachableFrom(Vector2Int start, HashSet<Vector2Int> blocked = null)
        {
            var seen = new HashSet<Vector2Int> { start };
            var stack = new Stack<Vector2Int>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                for (int d = 0; d < 4; d++)
                {
                    if (!Open(p.x, p.y, DX[d], DZ[d])) continue;
                    var n = new Vector2Int(p.x + DX[d], p.y + DZ[d]);
                    if (blocked != null && blocked.Contains(n)) continue;
                    if (seen.Add(n)) stack.Push(n);
                }
            }
            return seen;
        }

        /// <summary>
        /// Picks up to `max` doors that can be locked without cutting off any part of the map: with all of them
        /// closed, every walkable cell outside the locked doors is still reachable from the atrium.
        /// </summary>
        public static List<Vector2Int> ChooseLockableDoors(List<Vector2Int> candidates, int max)
        {
            var locked = new List<Vector2Int>();
            var blocked = new HashSet<Vector2Int>();
            int walkable = 0;
            for (int z = 0; z < Height; z++)
                for (int x = 0; x < Width; x++) if (IsWalkable(_cell[x, z])) walkable++;

            foreach (var door in candidates)
            {
                if (locked.Count >= max) break;
                blocked.Add(door);
                // every walkable cell except the locked door cells must still be reachable
                if (ReachableFrom(AtriumCentre, blocked).Count == walkable - blocked.Count) locked.Add(door);
                else blocked.Remove(door);
            }
            return locked;
        }

        public static int PassageCount(int x, int z)
        {
            int n = 0;
            for (int d = 0; d < 4; d++)
                if (IsFloor(At(x + DX[d], z + DZ[d])) && !NeedsWall(x, z, d)) n++;
            return n;
        }

        public static Vector2Int AtriumCentre => new Vector2Int(AtriumRect.x + AtriumRect.w / 2, AtriumRect.z + AtriumRect.h / 2);

        // --------------------------------------------------------------- generation

        public static void Generate(int seed = DefaultSeed, int size = DefaultSize)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                if (TryGenerate(seed + attempt * 7919, size)) { Seed = seed; return; }
            }
            throw new InvalidOperationException("MapLayout: could not generate a valid map for seed " + seed);
        }

        private static bool TryGenerate(int seed, int size)
        {
            var rng = new System.Random(seed);
            Width = Height = size;
            _cell = new char[size, size];
            _roomId = new int[size, size];
            _lobbyId = new int[size, size];
            _doorAlongZ = new bool[size, size];
            _theme = new Theme[size, size];
            _pass = new HashSet<int>();
            Rooms.Clear();
            Lobbies.Clear();
            DeadEnds.Clear();
            FloodBoxes.Clear();
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++) { _cell[x, z] = '.'; _roomId[x, z] = -1; _lobbyId[x, z] = -1; }

            // 1. The hub block with the atrium directly in front (south) of it, then everything else, with gaps.
            var placed = new List<CellRect>();
            int roomCount = 0;
            if (!FindSpot(rng, placed, 10, 14, out var unit)) return false;
            placed.Add(unit);
            AtriumRect = new CellRect(unit.x, unit.z, 10, 8);
            HubRect = new CellRect(unit.x, unit.z + 8, 10, 6);
            for (int z = HubRect.z; z < HubRect.z + HubRect.h; z++)
                for (int x = HubRect.x; x < HubRect.x + HubRect.w; x++) { _cell[x, z] = ' '; _roomId[x, z] = roomCount; }
            roomCount++;
            Rooms.Add(new RoomInfo { kind = RoomKind.Hub, rect = HubRect });
            MarkLobby(LobbyKind.Atrium, AtriumRect);

            for (int i = 0; i < 3; i++) if (!StampRoom(rng, placed, RoomKind.Pit, PitTemplate, ref roomCount)) return false;
            for (int i = 0; i < 2; i++) if (!StampRoom(rng, placed, RoomKind.Server, ServerTemplate, ref roomCount)) return false;
            for (int i = 0; i < 2; i++) if (!StampLobby(rng, placed, LobbyKind.Tall, 7, 4)) return false;
            for (int i = 0; i < 7; i++) if (!StampLobby(rng, placed, LobbyKind.Pillars, 5, 4)) return false;

            // 2. A random maze (spanning tree) over every free cell.
            int free = 0;
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++) if (IsFree(x, z)) free++;

            var visited = new bool[size, size];
            var stack = new Stack<Vector2Int>();
            Vector2Int start;
            do { start = new Vector2Int(rng.Next(size), rng.Next(size)); } while (!IsFree(start.x, start.y));
            visited[start.x, start.y] = true;
            stack.Push(start);
            int seen = 1;
            var options = new List<int>(4);
            while (stack.Count > 0)
            {
                var p = stack.Peek();
                options.Clear();
                for (int d = 0; d < 4; d++)
                {
                    int nx = p.x + DX[d], nz = p.y + DZ[d];
                    if (nx >= 0 && nz >= 0 && nx < size && nz < size && IsFree(nx, nz) && !visited[nx, nz]) options.Add(d);
                }
                if (options.Count == 0) { stack.Pop(); continue; }
                int dir = options[rng.Next(options.Count)];
                Carve(p.x, p.y, dir);
                var next = new Vector2Int(p.x + DX[dir], p.y + DZ[dir]);
                visited[next.x, next.y] = true;
                seen++;
                stack.Push(next);
            }
            if (seen != free) return false; // free space was split by the rooms: try another layout

            // 3. Open lobbies and room interiors, plus a few extra loops.
            int[] eastNorth = { 1, 0 };
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                    foreach (int dd in eastNorth)
                    {
                        int nx = x + DX[dd], nz = z + DZ[dd];
                        if (nx >= size || nz >= size) continue;
                        bool sameLobby = _lobbyId[x, z] >= 0 && _lobbyId[x, z] == _lobbyId[nx, nz];
                        bool sameRoom = _roomId[x, z] >= 0 && _roomId[x, z] == _roomId[nx, nz];
                        bool braid = IsFree(x, z) && IsFree(nx, nz) && rng.NextDouble() < 0.09;
                        if (sameLobby || sameRoom || braid) Carve(x, z, dd);
                    }

            // Wide doorways into the atrium from the maze on its west, east and south sides.
            var a = AtriumRect;
            for (int k = 3; k <= 4; k++)
            {
                OpenToFree(a.x, a.z + k, 3);
                OpenToFree(a.x + a.w - 1, a.z + k, 1);
            }
            for (int k = 4; k <= 5; k++) OpenToFree(a.x + k, a.z, 2);

            // 4. Flood a few areas.
            for (int f = 0; f < 3; f++)
            {
                int w = 5 + rng.Next(4), h = 5 + rng.Next(4);
                var box = new CellRect(rng.Next(1, size - w), rng.Next(1, size - h), w, h);
                if (box.Overlaps(unit, 0)) { f--; if (rng.Next(100) < 3) break; continue; }
                for (int z = box.z; z < box.z + box.h; z++)
                    for (int x = box.x; x < box.x + box.w; x++)
                        if (_cell[x, z] == '.' && _roomId[x, z] < 0 && _lobbyId[x, z] < 0) _cell[x, z] = 'F';
                FloodBoxes.Add(box);
            }

            // 5. Tall halls and the atrium.
            foreach (var l in Lobbies)
                if (l.kind == LobbyKind.Tall || l.kind == LobbyKind.Atrium)
                    for (int z = l.rect.z; z < l.rect.z + l.rect.h; z++)
                        for (int x = l.rect.x; x < l.rect.x + l.rect.w; x++)
                            if (_cell[x, z] == '.') _cell[x, z] = l.kind == LobbyKind.Atrium ? 'H' : ',';

            // 6. Themes: nearest of a few random centres.
            var centres = new List<(int x, int z, Theme t)>();
            for (int i = 0; i < 10; i++)
                centres.Add((rng.Next(size), rng.Next(size), (Theme)(i % 3)));
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++)
                {
                    int best = int.MaxValue; Theme t = Theme.Default;
                    foreach (var c in centres)
                    {
                        int d = (c.x - x) * (c.x - x) + (c.z - z) * (c.z - z);
                        if (d < best) { best = d; t = c.t; }
                    }
                    _theme[x, z] = t;
                }
            for (int z = a.z - 2; z < a.z + a.h + 2; z++)
                for (int x = a.x - 2; x < a.x + a.w + 2; x++)
                    if (x >= 0 && z >= 0 && x < size && z < size) _theme[x, z] = Theme.Default;

            // 7. Fire doors in straight corridor cells, kept apart.
            var candidates = new List<Vector2Int>();
            for (int z = 1; z < size - 1; z++)
                for (int x = 1; x < size - 1; x++)
                {
                    char c = _cell[x, z];
                    if ((c != '.' && c != 'F') || _roomId[x, z] >= 0 || _lobbyId[x, z] >= 0) continue;
                    bool n = !NeedsWall(x, z, 0), e = !NeedsWall(x, z, 1), s = !NeedsWall(x, z, 2), w = !NeedsWall(x, z, 3);
                    if ((n && s && !e && !w) || (e && w && !n && !s)) candidates.Add(new Vector2Int(x, z));
                }
            Shuffle(rng, candidates);
            var doors = new List<Vector2Int>();
            foreach (var cd in candidates)
            {
                if (doors.Count >= 20) break;
                bool tooClose = false;
                foreach (var d in doors) if (Math.Abs(d.x - cd.x) + Math.Abs(d.y - cd.y) < 6) tooClose = true;
                foreach (var r in Rooms) if (Near(r.rect, cd, 3)) tooClose = true;
                if (tooClose) continue;
                bool alongZ = !NeedsWall(cd.x, cd.y, 0);
                _cell[cd.x, cd.y] = 'D';
                _doorAlongZ[cd.x, cd.y] = alongZ;
                doors.Add(cd);
            }

            // 8. Dead ends, and the exit in the one farthest from the atrium.
            for (int z = 1; z < size - 1; z++)
                for (int x = 1; x < size - 1; x++)
                {
                    char c = _cell[x, z];
                    if ((c != '.' && c != 'F') || _roomId[x, z] >= 0 || _lobbyId[x, z] >= 0) continue;
                    if (PassageCount(x, z) == 1) DeadEnds.Add(new Vector2Int(x, z));
                }
            if (DeadEnds.Count < 20) return false;
            Vector2Int exit = DeadEnds[0];
            int bestDist = -1;
            var centre = AtriumCentre;
            foreach (var de in DeadEnds)
            {
                int dist = Math.Abs(de.x - centre.x) + Math.Abs(de.y - centre.y);
                if (dist > bestDist) { bestDist = dist; exit = de; }
            }
            ExitCell = exit;
            _cell[exit.x, exit.y] = 'E';
            DeadEnds.Remove(exit);

            // 9. Everything must be reachable from the atrium.
            int walkable = 0;
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++) if (IsWalkable(_cell[x, z])) walkable++;
            return ReachableFrom(centre).Count == walkable;
        }

        private static void OpenToFree(int x, int z, int dir)
        {
            int nx = x + DX[dir], nz = z + DZ[dir];
            if (nx < 0 || nz < 0 || nx >= Width || nz >= Height) return;
            if (!IsFree(nx, nz)) return;
            Carve(x, z, dir);
        }

        private static bool IsFree(int x, int z) => _cell[x, z] == '.' && _roomId[x, z] < 0;

        private static void Carve(int x, int z, int dir) => _pass.Add(Key(x, z, dir));

        private static bool Near(CellRect r, Vector2Int c, int margin) =>
            c.x >= r.x - margin && c.x < r.x + r.w + margin && c.y >= r.z - margin && c.y < r.z + r.h + margin;

        private static void Shuffle<T>(System.Random rng, List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }

        private static bool FindSpot(System.Random rng, List<CellRect> placed, int w, int h, out CellRect rect)
        {
            for (int i = 0; i < 600; i++)
            {
                var r = new CellRect(rng.Next(1, Width - w), rng.Next(1, Height - h), w, h);
                bool ok = true;
                foreach (var p in placed) if (r.Overlaps(p, 2)) { ok = false; break; }
                if (ok) { rect = r; return true; }
            }
            rect = new CellRect();
            return false;
        }

        private static bool StampRoom(System.Random rng, List<CellRect> placed, RoomKind kind, string[] template, ref int roomCount)
        {
            int h = template.Length, w = template[0].Length;
            if (!FindSpot(rng, placed, w, h, out var r)) return false;
            placed.Add(r);
            int id = roomCount++;
            for (int tz = 0; tz < h; tz++)
                for (int tx = 0; tx < w; tx++)
                {
                    char c = template[tz][tx];
                    if (c == '.') continue;
                    int x = r.x + tx, z = r.z + tz;
                    _cell[x, z] = c == '_' ? ' ' : c;
                    _roomId[x, z] = id;
                }

            Rooms.Add(new RoomInfo { kind = kind, rect = r });
            switch (kind)
            {
                case RoomKind.Pit:
                    Carve(r.x, r.z, 2);
                    Carve(r.x + 4, r.z + 4, 0);
                    break;
                case RoomKind.Server:
                    _doorAlongZ[r.x + 2, r.z] = true;
                    Carve(r.x + 2, r.z, 2);
                    break;
            }
            return true;
        }

        private static bool StampLobby(System.Random rng, List<CellRect> placed, LobbyKind kind, int w, int h)
        {
            if (!FindSpot(rng, placed, w, h, out var r)) return false;
            placed.Add(r);
            MarkLobby(kind, r);
            return true;
        }

        private static void MarkLobby(LobbyKind kind, CellRect r)
        {
            int id = Lobbies.Count;
            Lobbies.Add(new LobbyInfo { kind = kind, rect = r });
            for (int z = r.z; z < r.z + r.h; z++)
                for (int x = r.x; x < r.x + r.w; x++) _lobbyId[x, z] = id;
        }
    }
}
