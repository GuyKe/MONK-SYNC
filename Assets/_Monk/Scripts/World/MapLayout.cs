using System;
using System.Collections.Generic;
using UnityEngine;

namespace Monk.World
{
    public enum RoomKind { Start, Pit, Server }
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
        /// <summary>For a start room: the cell you appear in.</summary>
        public Vector2Int Spawn => new Vector2Int(rect.x + 2, rect.z + 1);
    }

    public sealed class LobbyInfo
    {
        public LobbyKind kind;
        public CellRect rect;
    }

    /// <summary>
    /// The map as data, generated from a seed. One character per 4 m cell. Every cell is floor except a few solid cells;
    /// thin partition walls separate neighbouring cells unless a passage was carved between them (a maze), with these
    /// hand-designed rooms stamped in and open lobbies:
    ///   .  carpet   ,  tall hall   F  flooded   E  exit seam
    ///   S  start room   X  start room (window east)   A  airlock door
    ///   D  fire door (manual)   B  blast door (automatic)
    ///   R  server room   o  pit-room ledge   p  pit   =  walkway along X   |  walkway along Z   +  junction
    /// Rows are indexed by z (row 0 is the south edge).
    /// </summary>
    public static class MapLayout
    {
        public const float Cell = 4f;
        public const float PitDepth = 6f;
        public const int DefaultSize = 42;
        public const int DefaultSeed = 20260101;

        // 9 x 5 footprint: a 6 x 4 room (window on the east side), the airlock above it, and a 3-wide open atrium
        // east of it that the window looks into. Row 0 is the south edge.
        private static readonly string[] StartTemplate =
        {
            "SSSSSS...",
            "SSSSSX...",
            "SSSSSX...",
            "SSSSSS...",
            "..A......",
        };

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
                case 'S': case 'X': case 'A': return 4.2f;
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

        public static HashSet<Vector2Int> ReachableFrom(Vector2Int start)
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
                    if (seen.Add(n)) stack.Push(n);
                }
            }
            return seen;
        }

        public static int PassageCount(int x, int z)
        {
            int n = 0;
            for (int d = 0; d < 4; d++)
                if (IsFloor(At(x + DX[d], z + DZ[d])) && !NeedsWall(x, z, d)) n++;
            return n;
        }

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

            // 1. Stamp rooms and lobbies, largest first, with a gap between everything.
            var placed = new List<CellRect>();
            int roomCount = 0;
            for (int i = 0; i < 7; i++) if (!StampRoom(rng, placed, RoomKind.Start, StartTemplate, ref roomCount)) return false;
            for (int i = 0; i < 2; i++) if (!StampRoom(rng, placed, RoomKind.Pit, PitTemplate, ref roomCount)) return false;
            for (int i = 0; i < 2; i++) if (!StampRoom(rng, placed, RoomKind.Server, ServerTemplate, ref roomCount)) return false;
            for (int i = 0; i < 2; i++) if (!StampLobby(rng, placed, LobbyKind.Tall, 7, 4)) return false;
            for (int i = 0; i < 6; i++) if (!StampLobby(rng, placed, LobbyKind.Pillars, 5, 4)) return false;

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

            // 4. Flood a few areas.
            var startFootprints = new List<CellRect>();
            foreach (var r in Rooms) if (r.kind == RoomKind.Start) startFootprints.Add(r.rect);
            for (int f = 0; f < 3; f++)
            {
                int w = 5 + rng.Next(4), h = 5 + rng.Next(4);
                var box = new CellRect(rng.Next(1, size - w), rng.Next(1, size - h), w, h);
                bool nearStart = false;
                foreach (var s in startFootprints) if (box.Overlaps(s, 0)) nearStart = true;
                if (nearStart) { f--; if (rng.Next(100) < 3) break; continue; }
                for (int z = box.z; z < box.z + box.h; z++)
                    for (int x = box.x; x < box.x + box.w; x++)
                        if (_cell[x, z] == '.' && _roomId[x, z] < 0 && _lobbyId[x, z] < 0) _cell[x, z] = 'F';
                FloodBoxes.Add(box);
            }

            // 5. Tall halls.
            foreach (var l in Lobbies)
                if (l.kind == LobbyKind.Tall)
                    for (int z = l.rect.z; z < l.rect.z + l.rect.h; z++)
                        for (int x = l.rect.x; x < l.rect.x + l.rect.w; x++)
                            if (_cell[x, z] == '.') _cell[x, z] = ',';

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
            foreach (var r in Rooms)
                if (r.kind == RoomKind.Start)
                    for (int z = r.rect.z; z < r.rect.z + r.rect.h; z++)
                        for (int x = r.rect.x; x < r.rect.x + r.rect.w; x++) _theme[x, z] = Theme.Default;

            // 7. Doors in straight corridor cells, kept apart.
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
                if (doors.Count >= 18) break;
                bool tooClose = false;
                foreach (var d in doors) if (Math.Abs(d.x - cd.x) + Math.Abs(d.y - cd.y) < 6) tooClose = true;
                foreach (var r in Rooms) if (Near(r.rect, cd, 3)) tooClose = true;
                if (tooClose) continue;
                bool alongZ = !NeedsWall(cd.x, cd.y, 0);
                _cell[cd.x, cd.y] = doors.Count % 3 == 2 ? 'B' : 'D';
                _doorAlongZ[cd.x, cd.y] = alongZ;
                doors.Add(cd);
            }

            // 8. Dead ends, and the exit in the one farthest from every start room.
            for (int z = 1; z < size - 1; z++)
                for (int x = 1; x < size - 1; x++)
                {
                    char c = _cell[x, z];
                    if ((c != '.' && c != 'F') || _roomId[x, z] >= 0 || _lobbyId[x, z] >= 0) continue;
                    if (PassageCount(x, z) == 1) DeadEnds.Add(new Vector2Int(x, z));
                }
            if (DeadEnds.Count == 0) return false;
            Vector2Int exit = DeadEnds[0];
            int bestDist = -1;
            foreach (var de in DeadEnds)
            {
                int nearest = int.MaxValue;
                foreach (var r in Rooms)
                    if (r.kind == RoomKind.Start)
                        nearest = Math.Min(nearest, Math.Abs(de.x - r.Spawn.x) + Math.Abs(de.y - r.Spawn.y));
                if (nearest > bestDist) { bestDist = nearest; exit = de; }
            }
            ExitCell = exit;
            _cell[exit.x, exit.y] = 'E';
            DeadEnds.Remove(exit);

            // 9. Everything must be reachable from every start room.
            int walkable = 0;
            for (int z = 0; z < size; z++)
                for (int x = 0; x < size; x++) if (IsWalkable(_cell[x, z])) walkable++;
            foreach (var r in Rooms)
                if (r.kind == RoomKind.Start && ReachableFrom(r.Spawn).Count != walkable) return false;

            return true;
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
                case RoomKind.Start:
                    _doorAlongZ[r.x + 2, r.z + 4] = true;
                    Carve(r.x + 2, r.z + 4, 0); // airlock opens north
                    MarkLobby(LobbyKind.Atrium, new CellRect(r.x + 6, r.z, 3, 5));
                    break;
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
