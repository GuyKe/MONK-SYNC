using System.Collections.Generic;
using Monk.Audio;
using Monk.Cosmetics;
using Monk.Gameplay;
using Monk.Hub;
using Monk.Locomotion;
using Monk.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Monk.EditorTools
{
    /// <summary>
    /// Menu: Monk > Build Backrooms Map. Generates a seeded maze with identical start rooms, landmarks, doors,
    /// hazards, lighting, audio and the player rig in the open scene. Run it again to rebuild.
    /// </summary>
    public static partial class MapBuilder
    {
        private const float C = MapLayout.Cell;
        private const float T = 0.3f;            // wall thickness
        private const float Pit = MapLayout.PitDepth;
        private const int BlockCells = 6;        // culling block size in cells
        private const int MaxRun = 4;            // longest merged floor / wall run in cells

        private static BackroomsAssets.Set M;
        private static Transform _root;
        private static int _mix;
        private static readonly Dictionary<string, Transform> Groups = new Dictionary<string, Transform>();

        private struct Hole { public float along, width, y0, y1; }
        private static readonly Dictionary<(int x, int z, int dir), Hole> Holes = new Dictionary<(int, int, int), Hole>();
        private static readonly List<(Vector3 c, bool alongZ)> _doors = new List<(Vector3, bool)>();

        // ------------------------------------------------------------------ entry

        [MenuItem("Monk/Build Backrooms Map")]
        public static void BuildDefault() => Build(MapLayout.DefaultSeed);

        [MenuItem("Monk/Build Backrooms Map (new random layout)")]
        public static void BuildRandomLayout() => Build(System.Environment.TickCount & 0x7fffffff);

        public static void Build(int seed)
        {
            try
            {
                Progress("Generating the layout", 0.02f);
                MapLayout.Generate(seed);
                _mix = MapLayout.Seed * 31;

                Progress("Generating textures and materials", 0.05f);
                M = BackroomsAssets.Build();

                var old = GameObject.Find("Backrooms Map");
                if (old != null) Object.DestroyImmediate(old);
                Groups.Clear();
                Holes.Clear();
                _doors.Clear();
                _fuseCells.Clear();
                for (int z = 0; z < MapLayout.Height; z++)
                    for (int x = 0; x < MapLayout.Width; x++)
                        if (MapLayout.IsDoor(MapLayout.At(x, z)))
                            _doors.Add((MapLayout.CellCenter(x, z), MapLayout.DoorRunsAlongZ(x, z)));

                _root = new GameObject("Backrooms Map").transform;
                Undo.RegisterCreatedObjectUndo(_root.gameObject, "Build Backrooms Map");

                Progress("Floors, ceilings and lights", 0.15f);
                RegisterHoles();
                BuildFloorsAndCeilings();
                BuildPanels();

                Progress("Walls and doors", 0.3f);
                BuildWalls();
                BuildDoors();

                Progress("Rooms and landmarks", 0.4f);
                foreach (var r in MapLayout.Rooms)
                {
                    if (r.kind == RoomKind.Pit) BuildPitRoom(r);
                    else if (r.kind == RoomKind.Server) BuildServerRoom(r);
                }
                BuildLobbies();
                BuildFlooded();

                Progress("Props and details", 0.55f);
                LockDoorsAndPlaceFuses();
                BuildMazeProps();
                BuildDeadEnds();
                BuildDucts();
                BuildDetails();
                BuildExit();
                BuildAtrium();

                Progress("Facility hub", 0.7f);
                var cctv = BuildMazeCctv();
                BuildHub(cctv);

                Progress("Interactive systems", 0.82f);
                var loco = BuildPlayerAndGameplay();
                BuildAudioAndLights(loco);
                ApplyAtmosphere(loco);

                Progress("Culling blocks", 0.93f);
                BuildBlocks();
                MarkStatic();

                Selection.activeGameObject = _root.gameObject;
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"Facility hub + Level 0 built (seed {MapLayout.Seed}): {MapLayout.Width}x{MapLayout.Height} cells. Save the scene (Ctrl+S).");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void Progress(string what, float t) => EditorUtility.DisplayProgressBar("Building the Backrooms", what, t);

        // ---------------------------------------------------------------- helpers

        private static float Rand(int x, int z, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093 ^ z * 19349663 ^ (salt + _mix) * 83492791);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static bool NearDoor(Vector3 p)
        {
            foreach (var d in _doors)
            {
                float dx = Mathf.Abs(p.x - d.c.x), dz = Mathf.Abs(p.z - d.c.z);
                float along = C * 0.5f + 1.8f, across = C * 0.5f + 0.2f;
                if (d.alongZ ? (dx < across && dz < along) : (dx < along && dz < across)) return true;
            }
            return false;
        }

        private static Transform Group(string name)
        {
            if (Groups.TryGetValue(name, out var t)) return t;
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);

            SurfaceKind kind;
            float slip;
            switch (name)
            {
                case "Carpet": kind = SurfaceKind.Carpet; slip = 0.02f; break;
                case "Wallpaper": kind = SurfaceKind.Wallpaper; slip = 0.03f; break;
                case "Concrete": kind = SurfaceKind.Concrete; slip = 0.05f; break;
                case "Ceiling": kind = SurfaceKind.Concrete; slip = 0.05f; break;
                case "Metal": kind = SurfaceKind.Metal; slip = 0.1f; break;
                case "Water": kind = SurfaceKind.Water; slip = 0.7f; break; // slick floor under shallow water
                case "Crates": kind = SurfaceKind.Crate; slip = 0f; break;
                case "Dividers": kind = SurfaceKind.Crate; slip = 0.02f; break;
                case "Glass": kind = SurfaceKind.Glass; slip = 0.1f; break;
                case "Hub:Concrete": kind = SurfaceKind.Concrete; slip = 0.05f; break;
                case "Hub:Metal": kind = SurfaceKind.Metal; slip = 0.08f; break;
                case "Hub:Floor": kind = SurfaceKind.Metal; slip = 0.1f; break;
                case "Hub:Props": kind = SurfaceKind.Crate; slip = 0.02f; break;
                case "Hub:Glass": kind = SurfaceKind.Glass; slip = 0.1f; break;
                default: Groups[name] = go.transform; return go.transform;
            }
            var sp = go.AddComponent<SurfaceProperties>();
            sp.kind = kind;
            sp.slipPercentage = slip;
            Groups[name] = go.transform;
            return go.transform;
        }

        private static void NoShadows(GameObject go)
        {
            var r = go.GetComponent<MeshRenderer>();
            if (r == null) return;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>A box with world-space UVs. center is in world space; the parent must not be scaled.</summary>
        private static GameObject Part(string name, Transform parent, Vector3 center, Vector3 size, Material mat,
            float tile = 2f, bool collider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.AddComponent<MeshFilter>().sharedMesh = MeshKit.Box(center, size, tile);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<BoxCollider>().size = size;
            NoShadows(go);
            return go;
        }

        /// <summary>A flat quad facing `facing`. size.x may be negative to mirror it.</summary>
        private static GameObject Decal(string name, Vector3 pos, Vector3 facing, Vector2 size, Material mat, float roll = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(Group("Decor"), false);
            go.transform.position = pos;
            Vector3 up = Mathf.Abs(facing.y) > 0.9f ? Vector3.forward : Vector3.up;
            go.transform.rotation = Quaternion.LookRotation(-facing, up) * Quaternion.Euler(0f, 0f, roll);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            NoShadows(go);
            return go;
        }

        private static GameObject Pipe(Vector3 a, Vector3 b, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Pipe";
            go.transform.SetParent(Group("Metal"), false);
            Vector3 d = b - a;
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            go.transform.localScale = new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = M.Metal;
            NoShadows(go);
            return go;
        }

        private static GameObject Cylinder(string name, Vector3 pos, Vector3 scale, Material mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(Group("Decor"), false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            NoShadows(go);
            return go;
        }

        private static void CrateStack(Vector3 basePos, int count, float yaw = 0f)
        {
            if (NearDoor(basePos)) return;
            for (int i = 0; i < count; i++)
            {
                var c = Part("Crate", Group("Crates"),
                    new Vector3(basePos.x + (i % 2 == 0 ? 0f : 0.07f), basePos.y + 0.45f + i * 0.9f, basePos.z),
                    new Vector3(0.9f, 0.9f, 0.9f), M.Crate, 1f);
                c.transform.rotation = Quaternion.Euler(0f, yaw + i * 6f, 0f);
            }
        }

        private static void Divider(Vector3 pos, float yaw, bool knocked)
        {
            if (NearDoor(pos)) return;
            const float len = 2.4f;
            var g = Group("Dividers");
            if (!knocked)
            {
                var panel = Part("Divider", g, pos + Vector3.up * 0.85f, new Vector3(len, 1.6f, 0.12f), M.Fabric, 0.5f);
                var trim = Part("DividerTrim", g, pos + Vector3.up * 1.67f, new Vector3(len, 0.05f, 0.15f), M.Metal, 1f, false);
                panel.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                trim.transform.rotation = panel.transform.rotation;
            }
            else
            {
                var panel = Part("DividerKnocked", g, pos + Vector3.up * 0.38f, new Vector3(len, 1.6f, 0.12f), M.Fabric, 0.5f);
                panel.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(70f, 0f, 0f);
            }
        }

        private static void Desk(Vector3 pos, float yaw, float width = 1.8f, bool monitor = true)
        {
            var g = Group("Crates");
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 R(Vector3 local) => pos + q * local;

            var top = Part("DeskTop", g, R(new Vector3(0f, 0.74f, 0f)), new Vector3(width, 0.05f, 0.8f), M.Crate, 1f);
            top.transform.rotation = q;
            foreach (float sx in new[] { -1f, 1f })
            {
                var leg = Part("DeskLeg", g, R(new Vector3(sx * (width * 0.5f - 0.04f), 0.36f, 0f)), new Vector3(0.05f, 0.72f, 0.7f), M.Metal, 1f);
                leg.transform.rotation = q;
            }
            if (!monitor) return;
            var body = Part("Monitor", Group("Decor"), R(new Vector3(0f, 1.0f, 0.15f)), new Vector3(0.5f, 0.34f, 0.05f), M.Rack, 1f, false);
            body.transform.rotation = q;
            var screen = Part("Screen", Group("Decor"), R(new Vector3(0f, 1.0f, 0.118f)), new Vector3(0.44f, 0.28f, 0.01f),
                Rand((int)pos.x, (int)pos.z, 77) > 0.5f ? M.LedGreen : M.LedAmber, 1f, false);
            screen.transform.rotation = q;
        }

        private static void Chair(Vector3 pos, float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            var seat = Part("ChairSeat", Group("Crates"), pos + q * new Vector3(0f, 0.46f, 0f), new Vector3(0.46f, 0.07f, 0.46f), M.Fabric, 0.5f);
            seat.transform.rotation = q;
            var back = Part("ChairBack", Group("Crates"), pos + q * new Vector3(0f, 0.76f, -0.21f), new Vector3(0.46f, 0.5f, 0.06f), M.Fabric, 0.5f);
            back.transform.rotation = q;
            Cylinder("ChairPost", pos + Vector3.up * 0.22f, new Vector3(0.06f, 0.22f, 0.06f), M.Metal);
        }

        private static void Shelf(Vector3 pos, float yaw)
        {
            if (NearDoor(pos)) return;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 R(Vector3 local) => pos + q * local;
            const float w = 1.6f, d = 0.5f;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var post = Part("ShelfPost", Group("Metal"), R(new Vector3(sx * (w * 0.5f - 0.03f), 1f, sz * (d * 0.5f - 0.03f))), new Vector3(0.05f, 2f, 0.05f), M.Metal, 1f);
                    post.transform.rotation = q;
                }
            for (int i = 0; i < 4; i++)
            {
                float y = 0.3f + i * 0.55f;
                var board = Part("ShelfBoard", Group("Metal"), R(new Vector3(0f, y, 0f)), new Vector3(w, 0.04f, d), M.Metal, 1f);
                board.transform.rotation = q;
                for (int k = 0; k < 3; k++)
                {
                    if (Rand((int)pos.x + i, (int)pos.z + k, 400) < 0.45f) continue;
                    var box = Part("ShelfBox", Group("Crates"), R(new Vector3(-0.55f + k * 0.55f, y + 0.2f, 0f)), new Vector3(0.38f, 0.36f, 0.38f), M.Crate, 1f);
                    box.transform.rotation = q;
                }
            }
        }

        // ----------------------------------------------------------- theme lookups

        private static Material WallpaperFor(Theme t) => M.Wallpapers[t == Theme.Default ? 0 : t == Theme.Office ? 1 : 2];
        private static Material CarpetFor(Theme t) => M.Carpets[t == Theme.Default ? 0 : t == Theme.Mold ? 1 : 2];
        private static Material CeilingFor(Theme t) => M.Ceilings[t == Theme.Mold ? 1 : 0];
        private static bool RoomGlyph(char c) => c == 'S' || c == 'X' || c == 'A' || c == 'R' || MapLayout.IsPitSpace(c) || c == 'o';

        private static bool IsAtrium(int x, int z)
        {
            int id = MapLayout.LobbyIdAt(x, z);
            return id >= 0 && MapLayout.Lobbies[id].kind == LobbyKind.Atrium;
        }

        // ------------------------------------------------------------------ holes

        private static void RegisterHoles()
        {
            // The hub's south face: a huge observation window (cells 4..7 of the hub) and the airlock opening (cell 8).
            var atrium = MapLayout.AtriumRect;
            int top = atrium.z + atrium.h - 1;
            for (int i = 4; i <= 7; i++)
                Holes[(atrium.x + i, top, 0)] = new Hole { along = (atrium.x + i + 0.5f) * C, width = 3.8f, y0 = MapLayout.HubFloorY + 0.9f, y1 = MapLayout.HubFloorY + 3.9f };
            Holes[(atrium.x + 8, top, 0)] = new Hole { along = (atrium.x + 8.5f) * C, width = 3.2f, y0 = MapLayout.HubFloorY, y1 = MapLayout.HubFloorY + 3.4f };

            // Broken drywall: holes cut through ordinary partitions, with crates to climb up to them.
            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                    for (int dir = 0; dir < 2; dir++)
                    {
                        int nx = x + MapLayout.DX[dir], nz = z + MapLayout.DZ[dir];
                        char a = MapLayout.At(x, z), b = MapLayout.At(nx, nz);
                        if (!MapLayout.IsFloor(a) || !MapLayout.IsFloor(b) || RoomGlyph(a) || RoomGlyph(b)) continue;
                        if (MapLayout.IsDoor(a) || MapLayout.IsDoor(b) || a == 'F' || b == 'F' || a == 'E' || b == 'E') continue;
                        if (!MapLayout.NeedsWall(x, z, dir) || Rand(x, z, 500 + dir) > 0.045f) continue;

                        bool horizontal = dir == 0;
                        Vector3 mid = horizontal ? new Vector3((x + 0.5f) * C, 0f, (z + 1) * C) : new Vector3((x + 1) * C, 0f, (z + 0.5f) * C);
                        if (NearDoor(mid)) continue;

                        float along = (horizontal ? mid.x : mid.z) + (Rand(x, z, 502 + dir) - 0.5f) * 1.6f;
                        Holes[(x, z, dir)] = new Hole { along = along, width = 1.2f, y0 = 1.6f, y1 = 2.6f };

                        float side = Rand(x, z, 504 + dir) > 0.5f ? 0.8f : -0.8f;
                        Vector3 crate = horizontal ? new Vector3(along, 0f, mid.z + side) : new Vector3(mid.x + side, 0f, along);
                        CrateStack(crate, 2, Rand(x, z, 506) * 40f);
                    }
        }

        // --------------------------------------------------------- floors, ceilings

        private struct SlabKind { public Material mat; public string group; public float h; }

        private static SlabKind FloorOf(int x, int z)
        {
            char ch = MapLayout.At(x, z);
            switch (ch)
            {
                case ' ': case 'o': case 'p': case '=': case '|': case '+': return new SlabKind();
                case 'F': return new SlabKind { mat = M.PoolTile, group = "Water" };
                case 'S': case 'X': case 'A': case 'D': case 'B': return new SlabKind { mat = M.Concrete, group = "Concrete" };
                case 'R': return new SlabKind { mat = M.Linoleum, group = "Concrete" };
                default: return new SlabKind { mat = CarpetFor(MapLayout.ThemeAt(x, z)), group = "Carpet" };
            }
        }

        private static SlabKind CeilingOf(int x, int z)
        {
            char ch = MapLayout.At(x, z);
            if (!MapLayout.IsFloor(ch)) return new SlabKind();
            return new SlabKind { mat = CeilingFor(MapLayout.ThemeAt(x, z)), group = "Ceiling", h = MapLayout.CeilingHeight(ch) };
        }

        private static void BuildFloorsAndCeilings()
        {
            int W = MapLayout.Width, H = MapLayout.Height;
            for (int z = 0; z < H; z++)
            {
                int x = 0;
                while (x < W)
                {
                    var k = FloorOf(x, z);
                    if (k.mat == null) { x++; continue; }
                    int n = 1;
                    while (n < MaxRun && x + n < W)
                    {
                        var k2 = FloorOf(x + n, z);
                        if (k2.mat != k.mat || k2.group != k.group) break;
                        n++;
                    }
                    Vector3 center = new Vector3((x + n * 0.5f) * C, -0.15f, (z + 0.5f) * C);
                    Part("Floor", Group(k.group), center, new Vector3(n * C, 0.3f, C), k.mat, k.mat == M.PoolTile ? 2f : 2f);
                    if (k.mat == M.PoolTile) WaterQuad(new Vector3(center.x, 0.06f, center.z), n * C, C);
                    x += n;
                }

                x = 0;
                while (x < W)
                {
                    var k = CeilingOf(x, z);
                    if (k.mat == null) { x++; continue; }
                    int n = 1;
                    while (n < MaxRun && x + n < W)
                    {
                        var k2 = CeilingOf(x + n, z);
                        if (k2.mat != k.mat || !Mathf.Approximately(k2.h, k.h)) break;
                        n++;
                    }
                    Part("Ceiling", Group("Ceiling"), new Vector3((x + n * 0.5f) * C, k.h + 0.1f, (z + 0.5f) * C),
                        new Vector3(n * C, 0.2f, C), k.mat, 0.6f);
                    x += n;
                }

                for (x = 0; x < W; x++) BuildSpecialFloor(MapLayout.At(x, z), MapLayout.CellCenter(x, z));
            }
        }

        private static void WaterQuad(Vector3 pos, float width, float depth)
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.name = "Water";
            water.transform.SetParent(Group("Decor"), false);
            water.transform.position = pos;
            water.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            water.transform.localScale = new Vector3(width, depth, 1f);
            water.GetComponent<MeshRenderer>().sharedMaterial = M.Water;
            NoShadows(water);
        }

        private static void BuildSpecialFloor(char ch, Vector3 c)
        {
            Vector3 slab = new Vector3(c.x, -0.15f, c.z);
            switch (ch)
            {
                case 'o':
                    Part("Ledge", Group("Concrete"), new Vector3(c.x, -Pit * 0.5f, c.z), new Vector3(C, Pit, C), M.Concrete);
                    break;
                case 'p':
                    PitFloor(c);
                    break;
                case '=':
                    PitFloor(c);
                    Part("Walkway", Group("Metal"), slab, new Vector3(C, 0.3f, 1.2f), M.Metal, 1f);
                    break;
                case '|':
                    PitFloor(c);
                    Part("Walkway", Group("Metal"), slab, new Vector3(1.2f, 0.3f, C), M.Metal, 1f);
                    break;
                case '+':
                    PitFloor(c);
                    Part("Walkway", Group("Metal"), slab, new Vector3(C, 0.3f, 1.2f), M.Metal, 1f);
                    Part("Walkway", Group("Metal"), slab, new Vector3(1.2f, 0.3f, C), M.Metal, 1f);
                    break;
            }
        }

        private static void PitFloor(Vector3 c) =>
            Part("PitFloor", Group("Concrete"), new Vector3(c.x, -Pit - 0.15f, c.z), new Vector3(C, 0.3f, C), M.Concrete);

        // ----------------------------------------------------------------- panels

        private static LightPanel.Mode PanelMode(int x, int z, char ch)
        {
            float r = Rand(x, z, 1);
            switch (ch)
            {
                case 'S': case 'X': case 'A': case 'E': case 'D': case 'B': return LightPanel.Mode.Steady;
                case 'R': return LightPanel.Mode.Dead;
                case 'F': return r < 0.35f ? LightPanel.Mode.Flicker : r < 0.55f ? LightPanel.Mode.Dead : LightPanel.Mode.Steady;
                case 'o': case 'p': case '=': case '|': case '+':
                    return r < 0.3f ? LightPanel.Mode.Flicker : r < 0.6f ? LightPanel.Mode.Dead : LightPanel.Mode.Steady;
            }
            // Atrium cells beside a start room stay lit.
            if (IsAtrium(x, z)) return LightPanel.Mode.Steady;
            switch (MapLayout.ThemeAt(x, z))
            {
                case Theme.Mold: return r < 0.3f ? LightPanel.Mode.Dead : r < 0.55f ? LightPanel.Mode.Flicker : LightPanel.Mode.Steady;
                case Theme.Office: return r < 0.1f ? LightPanel.Mode.Dead : r < 0.25f ? LightPanel.Mode.Flicker : LightPanel.Mode.Steady;
                default: return r < 0.15f ? LightPanel.Mode.Dead : r < 0.35f ? LightPanel.Mode.Flicker : LightPanel.Mode.Steady;
            }
        }

        private static void BuildPanels()
        {
            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                {
                    char ch = MapLayout.At(x, z);
                    if (!MapLayout.IsFloor(ch)) continue;
                    Vector3 c = MapLayout.CellCenter(x, z);
                    float ceil = MapLayout.CeilingHeight(ch);
                    var mode = PanelMode(x, z, ch);
                    Material mat = mode == LightPanel.Mode.Dead ? M.PanelDead : mode == LightPanel.Mode.Flicker ? M.PanelFlicker : M.PanelOn;
                    var go = Part("Panel", Group("Fixtures"), new Vector3(c.x, ceil - 0.025f, c.z), new Vector3(1.2f, 0.05f, 0.6f), mat, 1f, false);
                    if ((x + z) % 2 == 1) go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    var lp = go.AddComponent<LightPanel>();
                    lp.mode = mode;
                    lp.rend = go.GetComponent<MeshRenderer>();
                    lp.emitColor = M.PanelEmit;
                    lp.seed = Rand(x, z, 2) * 100f;
                }
        }

        // ------------------------------------------------------------------ walls

        private struct WallSpec { public bool valid, wallpaper; public float baseY, topY; public Material mat; public string group; }

        private static WallSpec EdgeSpec(int x, int z, int dir)
        {
            var s = new WallSpec();
            int nx = x + MapLayout.DX[dir], nz = z + MapLayout.DZ[dir];
            char a = MapLayout.At(x, z), b = MapLayout.At(nx, nz);
            bool fa = MapLayout.IsFloor(a), fb = MapLayout.IsFloor(b);
            if (!fa && !fb) return s;
            if (fa && fb && !MapLayout.NeedsWall(x, z, dir)) return s;

            float baseY = 100f, top = 0f;
            if (fa) { baseY = Mathf.Min(baseY, MapLayout.IsPitSpace(a) ? -Pit : -0.3f); top = Mathf.Max(top, MapLayout.CeilingHeight(a)); }
            if (fb) { baseY = Mathf.Min(baseY, MapLayout.IsPitSpace(b) ? -Pit : -0.3f); top = Mathf.Max(top, MapLayout.CeilingHeight(b)); }
            s.valid = true;
            s.baseY = baseY;
            s.topY = top + 0.2f;

            bool pool = (fa && a == 'F') || (fb && b == 'F');
            bool room = (fa && RoomGlyph(a)) || (fb && RoomGlyph(b));
            int cx = fa ? x : nx, cz = fa ? z : nz;
            bool facade = (fa && a == 'H' && !fb) || (fb && b == 'H' && !fa);   // the hub's south face
            if (facade) { s.mat = M.HubConcrete; s.group = "Concrete"; }
            else if (pool) { s.mat = M.PoolTile; s.group = "Concrete"; }
            else if (room) { s.mat = M.Concrete; s.group = "Concrete"; }
            else { s.mat = WallpaperFor(MapLayout.ThemeAt(cx, cz)); s.group = "Wallpaper"; s.wallpaper = true; }
            return s;
        }

        private static bool SameSpec(WallSpec a, WallSpec b) =>
            a.mat == b.mat && a.group == b.group && Mathf.Approximately(a.baseY, b.baseY) && Mathf.Approximately(a.topY, b.topY);

        private static void BuildWalls()
        {
            for (int z = -1; z < MapLayout.Height; z++) WallLine(z, true);
            for (int x = -1; x < MapLayout.Width; x++) WallLine(x, false);
        }

        /// <summary>Walks one row of horizontal edges (or column of vertical edges), merging neighbours into longer walls.</summary>
        private static void WallLine(int line, bool horizontal)
        {
            int count = horizontal ? MapLayout.Width : MapLayout.Height;
            int dir = horizontal ? 0 : 1;
            int runStart = 0, runLen = 0;
            var runSpec = new WallSpec();

            for (int i = 0; i < count; i++)
            {
                int x = horizontal ? i : line, z = horizontal ? line : i;
                var spec = EdgeSpec(x, z, dir);
                if (!spec.valid)
                {
                    if (runLen > 0) EmitWall(line, horizontal, runStart, runLen, runSpec, null);
                    runLen = 0;
                    continue;
                }
                if (Holes.TryGetValue((x, z, dir), out var hole))
                {
                    if (runLen > 0) EmitWall(line, horizontal, runStart, runLen, runSpec, null);
                    runLen = 0;
                    EmitWall(line, horizontal, i, 1, spec, hole);
                    continue;
                }
                if (runLen > 0 && runLen < MaxRun && SameSpec(runSpec, spec)) { runLen++; continue; }
                if (runLen > 0) EmitWall(line, horizontal, runStart, runLen, runSpec, null);
                runStart = i; runLen = 1; runSpec = spec;
            }
            if (runLen > 0) EmitWall(line, horizontal, runStart, runLen, runSpec, null);
        }

        private static void EmitWall(int line, bool horizontal, int start, int len, WallSpec s, Hole? hole)
        {
            float edge = (line + 1) * C;                    // coordinate across the wall
            float a0 = start * C, a1 = (start + len) * C;   // range along it
            Transform group = Group(s.group);

            void Seg(float from, float to, float y0, float y1)
            {
                if (to - from < 0.01f || y1 - y0 < 0.01f) return;
                float mid = (from + to) * 0.5f, my = (y0 + y1) * 0.5f;
                Vector3 center = horizontal ? new Vector3(mid, my, edge) : new Vector3(edge, my, mid);
                Vector3 size = horizontal ? new Vector3(to - from, y1 - y0, T) : new Vector3(T, y1 - y0, to - from);
                Part("Wall", group, center, size, s.mat, 2.4f);
            }

            float lo = a0 - T * 0.5f, hi = a1 + T * 0.5f;   // overlap the corners
            if (hole.HasValue)
            {
                var h = hole.Value;
                float hs = Mathf.Max(lo, h.along - h.width * 0.5f), he = Mathf.Min(hi, h.along + h.width * 0.5f);
                Seg(lo, hs, s.baseY, s.topY);
                Seg(he, hi, s.baseY, s.topY);
                Seg(hs, he, s.baseY, h.y0);
                Seg(hs, he, h.y1, s.topY);
                return;
            }
            Seg(lo, hi, s.baseY, s.topY);

            if (!s.wallpaper) return;
            // Stained baseboards (and wood wainscot in the office areas) on both faces.
            bool office = s.mat == M.Wallpapers[1];
            foreach (float side in new[] { -1f, 1f })
            {
                float off = side * (T * 0.5f + 0.025f);
                float mid = (lo + hi) * 0.5f, length = hi - lo;
                Vector3 pos = horizontal ? new Vector3(mid, 0.07f, edge + off) : new Vector3(edge + off, 0.07f, mid);
                Vector3 size = horizontal ? new Vector3(length, 0.14f, 0.05f) : new Vector3(0.05f, 0.14f, length);
                Part("Baseboard", Group("Decor"), pos, size, M.Baseboard, 0.3f, false);
                if (office)
                {
                    float off2 = side * (T * 0.5f + 0.015f);
                    Vector3 wp = horizontal ? new Vector3(mid, 0.5f, edge + off2) : new Vector3(edge + off2, 0.5f, mid);
                    Vector3 ws = horizontal ? new Vector3(length, 1.0f, 0.03f) : new Vector3(0.03f, 1.0f, length);
                    Part("Wainscot", Group("Decor"), wp, ws, M.Wood, 1f, false);
                }
            }
        }

        // ---------------------------------------------------------------- doors

        private struct MazeDoor { public IndustrialDoor door; public Vector2Int cell; }
        private static readonly List<MazeDoor> _mazeDoors = new List<MazeDoor>();

        private static void BuildDoors()
        {
            _mazeDoors.Clear();
            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                    if (MapLayout.IsDoor(MapLayout.At(x, z))) BuildDoor(x, z);
        }

        /// <summary>A heavy fire door in a corridor: buttons on both sides, warning lamps, a lock lamp, hazard framing.</summary>
        private static void BuildDoor(int x, int z)
        {
            Vector3 c = MapLayout.CellCenter(x, z);
            bool alongZ = MapLayout.DoorRunsAlongZ(x, z);
            float ceil = MapLayout.CeilingHeight(MapLayout.At(x, z));
            const float thick = 0.4f;

            var root = new GameObject("Fire Door");
            root.transform.SetParent(Group("Doors"), false);
            root.transform.position = c;

            Vector3 panelSize = alongZ ? new Vector3(C - 0.02f, ceil, thick) : new Vector3(thick, ceil, C - 0.02f);
            var panel = Part("Panel", root.transform, new Vector3(c.x, ceil * 0.5f, c.z), panelSize, M.Metal, 1f);
            Vector3 stripeSize = alongZ ? new Vector3(panelSize.x, 0.5f, thick + 0.02f) : new Vector3(thick + 0.02f, 0.5f, panelSize.z);
            Part("HazardStripe", panel.transform, new Vector3(c.x, 0.3f, c.z), stripeSize, M.Hazard, 0.5f, false);

            panel.AddComponent<Rigidbody>().isKinematic = true;
            var sp = panel.AddComponent<SurfaceProperties>();
            sp.kind = SurfaceKind.Metal;
            sp.slipPercentage = 0.1f;

            // Doors rise into a housing above the ceiling (nothing else is behind the thin walls to slide into).
            Vector3 hs = alongZ ? new Vector3(C + 0.2f, ceil + 0.1f, thick + 0.4f) : new Vector3(thick + 0.4f, ceil + 0.1f, C + 0.2f);
            Part("Housing", root.transform, new Vector3(c.x, ceil + 0.2f + (ceil + 0.1f) * 0.5f, c.z), hs, M.Metal, 1f, false);

            var lamps = new List<Renderer>();
            for (int s = -1; s <= 1; s += 2)
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector3 p = alongZ
                        ? new Vector3(c.x + k * 1.3f, ceil - 0.3f, c.z + s * (thick * 0.5f + 0.05f))
                        : new Vector3(c.x + s * (thick * 0.5f + 0.05f), ceil - 0.3f, c.z + k * 1.3f);
                    Vector3 sz = alongZ ? new Vector3(0.3f, 0.15f, 0.08f) : new Vector3(0.08f, 0.15f, 0.3f);
                    lamps.Add(Part("WarningLamp", Group("Decor"), p, sz, M.LampRed, 1f, false).GetComponent<Renderer>());
                }

            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = alongZ ? new Vector3(c.x, 0.011f, c.z + s * (thick * 0.5f + 0.3f)) : new Vector3(c.x + s * (thick * 0.5f + 0.3f), 0.011f, c.z);
                Vector3 sz = alongZ ? new Vector3(C - 0.7f, 0.02f, 0.4f) : new Vector3(0.4f, 0.02f, C - 0.7f);
                Part("FloorStripe", Group("Decor"), p, sz, M.Hazard, 0.5f, false);
            }

            var door = root.AddComponent<IndustrialDoor>();
            door.panel = panel.transform;
            door.panelCollider = panel.GetComponent<Collider>();
            door.warningLamps = lamps.ToArray();
            door.sparkMaterial = M.Spark;
            door.style = DoorStyle.Rise;
            door.openOffset = new Vector3(0f, ceil + 0.1f, 0f);
            door.startOpen = true;
            door.automatic = false;                 // fire doors are worked by hand
            door.warningSeconds = 1.4f;
            door.closeSeconds = 0.75f;

            // Two buttons per wall (one each side of the door), plus a lock lamp, exit sign and hazard frame.
            var lockLamps = new List<Renderer>();
            float wallOff = C * 0.5f - T * 0.5f;
            for (int wall = -1; wall <= 1; wall += 2)
            {
                Vector3 outward = alongZ ? new Vector3(-wall, 0f, 0f) : new Vector3(0f, 0f, -wall);
                for (int s = -1; s <= 1; s += 2)
                {
                    float along = s * 1.1f;
                    Vector3 pt = alongZ ? new Vector3(c.x + wall * wallOff, 1.3f, c.z + along) : new Vector3(c.x + along, 1.3f, c.z + wall * wallOff);
                    MountButton(door, pt, outward);

                    Vector3 stripeAt = alongZ ? new Vector3(c.x + wall * wallOff, ceil * 0.5f, c.z + s * (thick * 0.5f + 0.32f)) : new Vector3(c.x + s * (thick * 0.5f + 0.32f), ceil * 0.5f, c.z + wall * wallOff);
                    Vector3 stripeSz = alongZ ? new Vector3(0.02f, ceil, 0.16f) : new Vector3(0.16f, ceil, 0.02f);
                    Part("FrameStripe", Group("Decor"), stripeAt + outward * 0.012f, stripeSz, M.Hazard, 0.5f, false);
                }
                Vector3 lampAt = alongZ ? new Vector3(c.x + wall * wallOff, 1.95f, c.z) : new Vector3(c.x, 1.95f, c.z + wall * wallOff);
                Vector3 lampSize = alongZ ? new Vector3(0.04f, 0.14f, 0.3f) : new Vector3(0.3f, 0.14f, 0.04f);
                lockLamps.Add(Part("LockLamp", Group("Decor"), lampAt + outward * 0.03f, lampSize, M.LedRed, 1f, false).GetComponent<Renderer>());
                Vector3 signAt = new Vector3(lampAt.x, 2.55f, lampAt.z);
                Vector3 signSize = alongZ ? new Vector3(0.04f, 0.18f, 0.5f) : new Vector3(0.5f, 0.18f, 0.04f);
                Part("ExitSign", Group("Decor"), signAt + outward * 0.03f, signSize, M.LedGreen, 1f, false);
            }
            door.lockLamps = lockLamps.ToArray();
            _mazeDoors.Add(new MazeDoor { door = door, cell = new Vector2Int(x, z) });
        }

        private static void MountButton(IndustrialDoor door, Vector3 wallPoint, Vector3 outward)
        {
            bool onX = Mathf.Abs(outward.x) > 0.5f;
            Vector3 plateSize = onX ? new Vector3(0.06f, 0.32f, 0.22f) : new Vector3(0.22f, 0.32f, 0.06f);
            Vector3 capSize = onX ? new Vector3(0.05f, 0.12f, 0.12f) : new Vector3(0.12f, 0.12f, 0.05f);
            Part("ButtonPlate", Group("Decor"), wallPoint + outward * 0.03f, plateSize, M.Rack, 1f, false);
            var cap = Part("DoorButton", Group("Decor"), wallPoint + outward * 0.075f, capSize, M.LedRed, 1f);
            var b = cap.AddComponent<DoorButton>();
            b.door = door;
            b.cap = cap.transform;
            b.glow = cap.GetComponent<Renderer>();
            b.pressDirection = -outward;
        }

        // -------------------------------------------------------------- pit rooms

        private static void BuildPitRoom(RoomInfo r)
        {
            Vector3 O = new Vector3(r.rect.x * C, 0f, r.rect.z * C);
            Vector3 L(float x, float y, float z) => O + new Vector3(x, y, z);

            Pipe(L(0.6f, 3f, 6f), L(19.4f, 3f, 6f), 0.1f);
            Pipe(L(0.6f, 3.3f, 14f), L(19.4f, 3.3f, 14f), 0.1f);
            foreach (var p in new[] { new Vector2(6f, 10f), new Vector2(14f, 10f), new Vector2(10f, 14f), new Vector2(10f, 6f) })
                Pipe(L(p.x, 2f, p.y), L(p.x, 6.3f, p.y), 0.07f);

            Part("PitStripe", Group("Decor"), L(2f, 0.011f, 0.5f), new Vector3(3.4f, 0.02f, 0.5f), M.Hazard, 0.5f, false);
            Part("PitStripe", Group("Decor"), L(18f, 0.011f, 19.5f), new Vector3(3.4f, 0.02f, 0.5f), M.Hazard, 0.5f, false);

            for (int tz = 0; tz < 5; tz++)
                for (int tx = 0; tx < 5; tx++)
                {
                    char w = MapLayout.At(r.rect.x + tx, r.rect.z + tz);
                    Vector3 c = MapLayout.CellCenter(r.rect.x + tx, r.rect.z + tz);
                    if (w == '=' || w == '+')
                        foreach (float side in new[] { -0.55f, 0.55f })
                            Part("WalkwayEdge", Group("Decor"), new Vector3(c.x, 0.011f, c.z + side), new Vector3(C, 0.02f, 0.1f), M.Hazard, 0.3f, false);
                    if (w == '|' || w == '+')
                        foreach (float side in new[] { -0.55f, 0.55f })
                            Part("WalkwayEdge", Group("Decor"), new Vector3(c.x + side, 0.011f, c.z), new Vector3(0.1f, 0.02f, C), M.Hazard, 0.3f, false);
                }
            Cylinder("Bollard", L(0.9f, 0.45f, 0.7f), new Vector3(0.24f, 0.45f, 0.24f), M.Hazard, true);
            Cylinder("Bollard", L(3.1f, 0.45f, 0.7f), new Vector3(0.24f, 0.45f, 0.24f), M.Hazard, true);

            var hazard = new GameObject("Pitfalls Kill Zone");
            hazard.transform.SetParent(Group("Zones"), false);
            var hz = hazard.AddComponent<HazardZone>();
            hz.bounds = new Bounds(L(10f, -5.2f, 10f), new Vector3(20f, 2f, 20f));
            hz.damage = 100f;
            hz.cause = "Fell into the Pitfalls";
        }

        // ------------------------------------------------------------ server rooms

        private static void BuildServerRoom(RoomInfo r)
        {
            // The room proper starts one row above the door row.
            Vector3 O = new Vector3(r.rect.x * C, 0f, (r.rect.z + 1) * C);
            Vector3 L(float x, float y, float z) => O + new Vector3(x, y, z);

            float[] xs = { 2f, 4.4f, 6.8f, 13.2f, 15.6f, 18f };
            for (int row = 0; row < 2; row++)
            {
                float zRow = row == 0 ? 3f : 8f;
                float face = row == 0 ? -1f : 1f;
                for (int i = 0; i < xs.Length; i++)
                {
                    Part("Rack", Group("Metal"), L(xs[i], 1.05f, zRow), new Vector3(0.8f, 2.1f, 0.9f), M.Rack, 1f);
                    for (int k = 0; k < 3; k++)
                    {
                        float rr = Rand(i + r.rect.x, row * 7 + k + r.rect.z, 200);
                        Material led = rr < 0.55f ? M.LedGreen : rr < 0.8f ? M.LedAmber : M.LedRed;
                        Part("LED", Group("Decor"), L(xs[i] - 0.15f + k * 0.15f, 0.7f + Rand(i, k + r.rect.x, 201 + row) * 1.2f, zRow + face * 0.46f),
                            new Vector3(0.08f, 0.04f, 0.02f), led, 1f, false);
                    }
                }
            }
            foreach (float tz in new[] { 1.5f, 4.3f, 9.8f })
                Part("CableTray", Group("Metal"), L(10f, 2.6f, tz), new Vector3(19f, 0.1f, 0.5f), M.Metal, 1f);
            CrateStack(L(18.8f, 0f, 11f), 2);
            CrateStack(L(1.2f, 0f, 11f), 3);

            Desk(L(18.6f, 0f, 5.6f), 90f, 2.0f);
            Chair(L(17.4f, 0f, 5.6f), 90f);
            for (int i = 0; i < 6; i++)
            {
                float cx = 1.5f + i * 3.1f + Rand(i, 3, 210);
                Part("FloorCable", Group("Decor"), L(cx, 0.04f, 5.6f), new Vector3(0.07f, 0.07f, 5.2f - Rand(i, 4, 211) * 1.5f), M.Rack, 1f, false);
                Part("RackCable", Group("Decor"), L(xs[i % xs.Length] + 0.2f, 2.35f, 3f), new Vector3(0.05f, 0.5f, 0.05f), M.Rack, 1f, false);
            }

            foreach (float x in new[] { 4f, 16f })
            {
                var go = new GameObject("EmergencyLight");
                go.transform.SetParent(Group("Fixtures"), false);
                go.transform.position = L(x, 2.5f, 6f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.1f, 0.05f);
                l.range = 7f;
                l.intensity = 2.2f;
                l.shadows = LightShadows.None;
            }
        }

        // ---------------------------------------------------------------- lobbies

        private static void BuildLobbies()
        {
            foreach (var lobby in MapLayout.Lobbies)
            {
                var r = lobby.rect;
                if (lobby.kind == LobbyKind.Pillars)
                {
                    // A checkerboard of columns, only at interior lattice points so no doorway is blocked.
                    for (int i = 1; i < r.w; i++)
                        for (int j = 1; j < r.h; j++)
                        {
                            if ((i + j) % 2 != 0) continue;
                            float h = MapLayout.CeilingHeight('.');
                            Material wall = WallpaperFor(MapLayout.ThemeAt(r.x + i - 1, r.z + j - 1));
                            Part("Pillar", Group("Wallpaper"), new Vector3((r.x + i) * C, h * 0.5f, (r.z + j) * C), new Vector3(1f, h, 1f), wall, 2.4f);
                        }
                    if (Rand(r.x, r.z, 800) < 0.5f)
                    {
                        Vector3 c = new Vector3((r.x + r.w * 0.5f) * C, 0f, (r.z + r.h * 0.5f) * C + 1.8f);
                        Divider(c, 0f, false);
                        Divider(c + new Vector3(3f, 0f, 0.5f), 70f, true);
                    }
                }
                else if (lobby.kind == LobbyKind.Tall)
                {
                    BuildTallLobby(r);
                }
            }
        }

        private static void BuildTallLobby(CellRect r)
        {
            const float cwY = 2.4f;
            float x0 = r.x * C, z0 = r.z * C, width = r.w * C, depth = r.h * C;
            float cz = z0 + depth - 1.6f;

            Part("Catwalk", Group("Metal"), new Vector3(x0 + width * 0.5f, cwY - 0.075f, cz), new Vector3(width - 8f, 0.15f, 1.6f), M.Metal, 1f);
            foreach (float px in new[] { 4.6f, width * 0.5f, width - 4.6f })
                Part("CatwalkPost", Group("Metal"), new Vector3(x0 + px, cwY * 0.5f, cz), new Vector3(0.15f, cwY, 0.15f), M.Metal, 1f);
            CrateStack(new Vector3(x0 + 5.2f, 0f, cz - 1.7f), 2);
            CrateStack(new Vector3(x0 + width - 5.2f, 0f, cz - 1.7f), 2);
            CrateStack(new Vector3(x0 + width * 0.5f, cwY, cz), 2);
            Pipe(new Vector3(x0 + 0.5f, 4.3f, z0 + depth - 2.8f), new Vector3(x0 + width - 0.5f, 4.3f, z0 + depth - 2.8f), 0.12f);
            Pipe(new Vector3(x0 + 0.5f, 3.9f, z0 + depth - 4.5f), new Vector3(x0 + width - 0.5f, 3.9f, z0 + depth - 4.5f), 0.1f);
            Shelf(new Vector3(x0 + 3f, 0f, z0 + 1.2f), 0f);
        }

        // ---------------------------------------------------------------- flooded

        private static void BuildFlooded()
        {
            foreach (var box in MapLayout.FloodBoxes)
            {
                var go = new GameObject("FloodReflection");
                go.transform.SetParent(Group("Fixtures"), false);
                go.transform.position = new Vector3((box.x + box.w * 0.5f) * C, 1.6f, (box.z + box.h * 0.5f) * C);
                var probe = go.AddComponent<ReflectionProbe>();
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
                probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                probe.resolution = 64;
                probe.size = new Vector3(box.w * C, 3.2f, box.h * C);
                probe.boxProjection = true;
                probe.hdr = false;

                // Half-sunk crates, a fallen pipe and hanging cables on flooded cells.
                int placed = 0;
                for (int i = 0; i < 40 && placed < 5; i++)
                {
                    int cx = box.x + (int)(Rand(box.x, box.z, 900 + i) * box.w);
                    int cz = box.z + (int)(Rand(box.z, box.x, 940 + i) * box.h);
                    if (MapLayout.At(cx, cz) != 'F') continue;
                    Vector3 c = MapLayout.CellCenter(cx, cz);
                    if (placed < 2) CrateStack(new Vector3(c.x + 1.1f, 0f, c.z + 1.1f), 1 + placed, i * 13f);
                    else if (placed == 2) Pipe(c + new Vector3(-1.5f, 2.5f, -1.5f), c + new Vector3(1.2f, 0.35f, 1.2f), 0.1f);
                    else Part("HangingCable", Group("Decor"), new Vector3(c.x, 2.6f, c.z), new Vector3(0.04f, 1.2f, 0.04f), M.Rack, 1f, false);
                    placed++;
                }
            }
        }

        // -------------------------------------------------------------- maze props

        private static int PickWall(int x, int z, int salt)
        {
            var walls = new List<int>(4);
            for (int d = 0; d < 4; d++) if (MapLayout.NeedsWall(x, z, d)) walls.Add(d);
            return walls.Count == 0 ? -1 : walls[(int)(Rand(x, z, salt) * walls.Count) % walls.Count];
        }

        private static void BuildMazeProps()
        {
            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                {
                    char ch = MapLayout.At(x, z);
                    if ((ch != '.' && ch != ',') || MapLayout.RoomIdAt(x, z) >= 0 || IsAtrium(x, z)) continue;
                    Vector3 c = MapLayout.CellCenter(x, z);
                    if (NearDoor(c)) continue;

                    float r = Rand(x, z, 600);
                    if (r > 0.22f) continue;
                    int wall = PickWall(x, z, 601);
                    if (wall < 0) continue;

                    Vector3 n = new Vector3(MapLayout.DX[wall], 0f, MapLayout.DZ[wall]);
                    bool alongX = wall == 0 || wall == 2;
                    Vector3 along = alongX ? Vector3.right : Vector3.forward;
                    float yaw = alongX ? 0f : 90f;
                    float side = Rand(x, z, 602) > 0.5f ? 1f : -1f;

                    if (r < 0.04f)
                        CrateStack(c + n * 1.3f + along * (side * 1.2f), 1 + (int)(Rand(x, z, 603) * 3f), Rand(x, z, 604) * 40f);
                    else if (r < 0.07f)
                        Divider(c + n * (C * 0.5f - T * 0.5f - 0.5f) + along * (side * 0.6f), yaw, false);
                    else if (r < 0.10f)
                    {
                        Vector3 deskPos = c + n * (C * 0.5f - T * 0.5f - 0.5f);
                        Desk(deskPos, yaw, 1.4f, Rand(x, z, 605) > 0.5f);
                        Chair(deskPos - n * 0.7f, yaw + (alongX ? 0f : 90f) + 150f);
                    }
                    else if (r < 0.12f)
                        FallenDrywall(c + n * (C * 0.5f - T * 0.5f - 0.9f), yaw);
                    else if (r < 0.15f)
                        CrossingPipe(c, n, wall);
                    else if (r < 0.18f)
                        RackRow(c + n * (C * 0.5f - T * 0.5f - 0.6f) - along * 1.0f, yaw, 2 + (int)(Rand(x, z, 610) * 2f));
                    else
                        Shelf(c + n * (C * 0.5f - T * 0.5f - 0.3f), yaw);
                }
        }

        private static void BuildDeadEnds()
        {
            var cells = new List<Vector2Int>(MapLayout.DeadEnds);
            cells.Add(MapLayout.ExitCell);
            foreach (var cell in cells)
            {
                if (_fuseCells.Contains(cell)) continue;   // a fuse box lives here
                int x = cell.x, z = cell.y;
                int open = -1;
                for (int d = 0; d < 4; d++) if (!MapLayout.NeedsWall(x, z, d)) { open = d; break; }
                if (open < 0) continue;
                int back = (open + 2) % 4;

                Vector3 c = MapLayout.CellCenter(x, z);
                Vector3 n = new Vector3(MapLayout.DX[back], 0f, MapLayout.DZ[back]); // points at the back wall
                bool alongX = back == 0 || back == 2;
                Vector3 along = alongX ? Vector3.right : Vector3.forward;
                float yaw = alongX ? 0f : 90f;
                Vector3 backFace = c + n * (C * 0.5f - T * 0.5f);

                if (x == MapLayout.ExitCell.x && z == MapLayout.ExitCell.y) continue; // the exit has its own treatment

                float r = Rand(x, z, 700);
                if (r < 0.34f)
                {
                    Vector3 deskPos = backFace - n * 0.5f;
                    Desk(deskPos, yaw, 1.6f, true);
                    Chair(deskPos - n * 0.8f, yaw + (alongX ? 0f : 90f) + 170f);
                    Part("Papers", Group("Decor"), deskPos + Vector3.up * 0.78f, new Vector3(0.21f, 0.01f, 0.3f), M.Paper, 1f, false);
                }
                else if (r < 0.67f)
                {
                    CrateStack(backFace - n * 0.6f + along * 1.0f, 3, Rand(x, z, 701) * 30f);
                    CrateStack(backFace - n * 0.6f - along * 1.0f, 2, Rand(x, z, 702) * 30f);
                    Divider(c - n * 0.2f, yaw + 25f, true);
                }
                else
                {
                    Chair(c, Rand(x, z, 703) * 360f);
                    Decal("Arrow", backFace - n * 0.012f + Vector3.up * 1.2f, -n, new Vector2(0.7f, 0.7f), M.Arrow);
                }

                if (Rand(x, z, 704) < 0.6f)
                    Decal("Tally", backFace - n * 0.012f + Vector3.up * 1.5f + along * 0.8f, -n, new Vector2(1.0f, 1.0f), M.Scratches);
            }
        }

        // ------------------------------------------------------------------ ducts

        private static bool StraightCell(int x, int z, bool eastWest)
        {
            char ch = MapLayout.At(x, z);
            if ((ch != '.') || MapLayout.RoomIdAt(x, z) >= 0 || MapLayout.LobbyIdAt(x, z) >= 0) return false;
            Vector3 c = MapLayout.CellCenter(x, z);
            if (NearDoor(c)) return false;
            return eastWest
                ? MapLayout.NeedsWall(x, z, 0) && MapLayout.NeedsWall(x, z, 2) && !MapLayout.NeedsWall(x, z, 1) && !MapLayout.NeedsWall(x, z, 3)
                : MapLayout.NeedsWall(x, z, 1) && MapLayout.NeedsWall(x, z, 3) && !MapLayout.NeedsWall(x, z, 0) && !MapLayout.NeedsWall(x, z, 2);
        }

        /// <summary>Narrow ventilation ducts along straight corridors: a route above sightlines.</summary>
        private static void BuildDucts()
        {
            int made = 0;
            for (int pass = 0; pass < 2 && made < 18; pass++)
            {
                bool eastWest = pass == 0;
                int lines = eastWest ? MapLayout.Height : MapLayout.Width;
                int length = eastWest ? MapLayout.Width : MapLayout.Height;
                for (int line = 0; line < lines && made < 18; line++)
                {
                    int i = 0;
                    while (i < length)
                    {
                        int x = eastWest ? i : line, z = eastWest ? line : i;
                        if (!StraightCell(x, z, eastWest)) { i++; continue; }
                        int run = 1;
                        while (i + run < length && run < 6 &&
                               StraightCell(eastWest ? i + run : line, eastWest ? line : i + run, eastWest)) run++;
                        if (run >= 4 && Rand(x, z, 950) < 0.35f)
                        {
                            BuildDuct(x, z, run, eastWest);
                            made++;
                        }
                        i += run;
                    }
                }
            }
        }

        private static void BuildDuct(int x, int z, int cells, bool eastWest)
        {
            var metal = Group("Metal");
            float len = cells * C;
            Vector3 start = eastWest ? new Vector3(x * C, 0f, (z + 0.5f) * C) : new Vector3((x + 0.5f) * C, 0f, z * C);
            Vector3 mid = start + (eastWest ? Vector3.right : Vector3.forward) * (len * 0.5f);

            Vector3 sizeFlat = eastWest ? new Vector3(len, 0.06f, 0.9f) : new Vector3(0.9f, 0.06f, len);
            Vector3 sizeSide = eastWest ? new Vector3(len, 0.9f, 0.06f) : new Vector3(0.06f, 0.9f, len);
            Part("DuctFloor", metal, mid + Vector3.up * 1.9f, sizeFlat, M.Metal, 1f);
            Part("DuctRoof", metal, mid + Vector3.up * 2.8f, sizeFlat, M.Metal, 1f);
            Vector3 sideOff = eastWest ? new Vector3(0f, 0f, 0.42f) : new Vector3(0.42f, 0f, 0f);
            Part("DuctSide", metal, mid + Vector3.up * 2.35f + sideOff, sizeSide, M.Metal, 1f);
            Part("DuctSide", metal, mid + Vector3.up * 2.35f - sideOff, sizeSide, M.Metal, 1f);

            for (int k = 0; k < cells; k++)
            {
                Vector3 p = start + (eastWest ? Vector3.right : Vector3.forward) * ((k + 0.5f) * C);
                Part("DuctHanger", Group("Decor"), p + Vector3.up * 3.0f, new Vector3(0.05f, 0.4f, 0.05f), M.Metal, 1f, false);
            }

            var audio = new GameObject("VentAudio");
            audio.transform.SetParent(Group("Decor"), false);
            audio.transform.position = mid + Vector3.up * 2.35f;
            audio.AddComponent<VentEmitter>();

            // Crates under the open end so arms can reach it.
            Vector3 dir = eastWest ? Vector3.right : Vector3.forward;
            CrateStack(start + dir * 0.9f, 2);
        }

        // ---------------------------------------------------------------- details

        private static void BuildDetails()
        {
            var decor = Group("Decor");
            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                {
                    char ch = MapLayout.At(x, z);
                    if (!MapLayout.IsFloor(ch)) continue;
                    Vector3 c = MapLayout.CellCenter(x, z);
                    float ceil = MapLayout.CeilingHeight(ch);
                    Theme theme = MapLayout.ThemeAt(x, z);

                    if (Rand(x, z, 300) < 0.45f)
                        Part("Sprinkler", decor, new Vector3(c.x + (Rand(x, z, 301) - 0.5f) * 3f, ceil - 0.04f, c.z + (Rand(x, z, 302) - 0.5f) * 3f),
                            new Vector3(0.07f, 0.08f, 0.07f), M.Metal, 1f, false);
                    if (Rand(x, z, 303) < 0.2f)
                        Part("CeilingGrate", decor, new Vector3(c.x + (Rand(x, z, 304) - 0.5f) * 2f, ceil - 0.015f, c.z + (Rand(x, z, 305) - 0.5f) * 2f),
                            new Vector3(0.6f, 0.03f, 0.6f), M.Rack, 1f, false);

                    for (int d = 0; d < 4; d++) WallDecor(x, z, d, ch, theme);

                    if (ch != '.' && ch != ',') continue;
                    float mess = theme == Theme.Mold ? 1.6f : 1f;

                    if (Rand(x, z, 310) < 0.25f * mess)
                    {
                        var p = new Vector3(c.x + (Rand(x, z, 312) - 0.5f) * 2.8f, 0.013f, c.z + (Rand(x, z, 314) - 0.5f) * 2.8f);
                        if (!NearDoor(p))
                        {
                            float s = 1.1f + Rand(x, z, 316) * 1.3f;
                            Decal("FloorStain", p, Vector3.up, new Vector2(s, s), M.Stain, Rand(x, z, 318) * 360f);
                        }
                    }
                    if (theme == Theme.Mold && Rand(x, z, 330) < 0.25f)
                    {
                        var p = new Vector3(c.x + (Rand(x, z, 331) - 0.5f) * 2.8f, 0.014f, c.z + (Rand(x, z, 332) - 0.5f) * 2.8f);
                        if (!NearDoor(p)) Decal("FloorMold", p, Vector3.up, new Vector2(1.6f, 1.6f), M.MoldDecal, Rand(x, z, 333) * 360f);
                    }
                    if (Rand(x, z, 320) < 0.22f)
                        for (int k = 0; k < 3; k++)
                        {
                            var p = new Vector3(c.x + (Rand(x, z, 321 + k) - 0.5f) * 3f, 0.0125f, c.z + (Rand(x, z, 340 + k) - 0.5f) * 3f);
                            if (NearDoor(p)) continue;
                            var paper = Part("Paper", decor, p, new Vector3(0.21f, 0.004f, 0.3f), M.Paper, 1f, false);
                            paper.transform.rotation = Quaternion.Euler(0f, Rand(x, z, 350 + k) * 360f, 0f);
                        }
                    if (Rand(x, z, 360) < 0.1f)
                    {
                        var p = new Vector3(c.x + (Rand(x, z, 361) - 0.5f) * 2.4f, 0.04f, c.z + (Rand(x, z, 362) - 0.5f) * 2.4f);
                        if (!NearDoor(p))
                        {
                            var tile = Part("FallenTile", decor, p, new Vector3(0.6f, 0.03f, 0.6f), M.Ceilings[0], 0.6f, false);
                            tile.transform.rotation = Quaternion.Euler(Rand(x, z, 363) * 14f - 7f, Rand(x, z, 364) * 360f, Rand(x, z, 365) * 14f - 7f);
                        }
                    }
                }
        }

        /// <summary>Surface detail on one face of a wall: peeling paper, stains, mold, cracks, graffiti, fittings.</summary>
        private static void WallDecor(int x, int z, int dir, char ch, Theme theme)
        {
            if (!MapLayout.NeedsWall(x, z, dir)) return;
            int ex = dir == 3 ? x - 1 : x, ez = dir == 2 ? z - 1 : z;
            int edgeDir = (dir == 0 || dir == 2) ? 0 : 1;
            if (!EdgeSpec(ex, ez, edgeDir).wallpaper) return;

            Vector3 c = MapLayout.CellCenter(x, z);
            Vector3 normal = new Vector3(MapLayout.DX[dir], 0f, MapLayout.DZ[dir]);
            Vector3 inner = c + normal * (C * 0.5f - T * 0.5f);
            if (NearDoor(inner - normal * 0.5f)) return;

            Vector3 inward = -normal;
            bool alongX = dir == 0 || dir == 2;
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            float ceil = MapLayout.CeilingHeight(ch);
            int s = dir * 7;

            float Roll(int salt) => Rand(x, z, salt + s);

            if (Roll(10) < 0.12f)
            {
                float side = Roll(11) > 0.5f ? 1f : -1f;
                Decal("Peel", inner + inward * 0.012f + along * (side * (C * 0.5f - 0.35f)) + Vector3.up * (ceil - 0.35f),
                    inward, new Vector2(-side * 0.6f, 0.6f), M.Peel);
            }
            if (Roll(20) < 0.15f)
                Decal("WaterStain", inner + inward * 0.008f + along * ((Roll(21) * 2f - 1f) * 1.1f) + Vector3.up * (ceil - 0.8f),
                    inward, new Vector2(1.0f, 1.5f), M.Stain);
            if (Roll(30) < (theme == Theme.Mold ? 0.3f : 0.03f))
                Decal("Mold", inner + inward * 0.009f + along * ((Roll(31) * 2f - 1f) * 1.2f) + Vector3.up * (0.4f + Roll(32) * 1.2f),
                    inward, new Vector2(1.3f, 1.3f), M.MoldDecal, Roll(33) * 360f);
            if (Roll(40) < 0.05f)
                Decal("Crack", inner + inward * 0.01f + along * ((Roll(41) * 2f - 1f) * 1.2f) + Vector3.up * (1f + Roll(42) * 1.4f),
                    inward, new Vector2(1.0f, 1.0f), M.WallCrack, Roll(43) * 360f);
            if (Roll(50) < 0.03f)
                Decal("Tally", inner + inward * 0.011f + along * ((Roll(51) * 2f - 1f) * 1.2f) + Vector3.up * 1.4f,
                    inward, new Vector2(0.9f, 0.9f), M.Scratches);
            if (Roll(60) < 0.02f)
                Decal("Arrow", inner + inward * 0.011f + along * ((Roll(61) * 2f - 1f) * 1.2f) + Vector3.up * 1.2f,
                    inward, new Vector2(0.6f, 0.6f), M.Arrow);

            float jitter = (Roll(70) - 0.5f) * 2.4f;
            if (Roll(71) < 0.1f)
                Part("Outlet", Group("Decor"), inner + inward * 0.012f + along * jitter + Vector3.up * 0.3f,
                    alongX ? new Vector3(0.08f, 0.12f, 0.02f) : new Vector3(0.02f, 0.12f, 0.08f), M.Paper, 1f, false);
            if (Roll(80) < 0.06f)
                Part("LightSwitch", Group("Decor"), inner + inward * 0.012f - along * jitter + Vector3.up * 1.2f,
                    alongX ? new Vector3(0.08f, 0.12f, 0.02f) : new Vector3(0.02f, 0.12f, 0.08f), M.Paper, 1f, false);
            if (Roll(90) < 0.1f)
                Part("Conduit", Group("Decor"), inner + inward * 0.04f + Vector3.up * (ceil - 0.35f),
                    alongX ? new Vector3(C, 0.06f, 0.06f) : new Vector3(0.06f, 0.06f, C), M.Metal, 1f, false);
            if (Roll(100) < 0.03f)
            {
                Vector3 p = inner + inward * 0.12f + along * ((Roll(101) - 0.5f) * 2f) + Vector3.up * 0.95f;
                Cylinder("Extinguisher", p, new Vector3(0.16f, 0.22f, 0.16f), M.Red);
                Part("ExtinguisherBracket", Group("Decor"), p + normal * 0.07f,
                    alongX ? new Vector3(0.22f, 0.08f, 0.04f) : new Vector3(0.04f, 0.08f, 0.22f), M.Rack, 1f, false);
            }
        }

        // ------------------------------------------------------------------- exit

        private static void BuildExit()
        {
            int x = MapLayout.ExitCell.x, z = MapLayout.ExitCell.y;
            Vector3 c = MapLayout.CellCenter(x, z);
            int open = 0;
            for (int d = 0; d < 4; d++) if (!MapLayout.NeedsWall(x, z, d)) { open = d; break; }
            int back = (open + 2) % 4;
            Vector3 n = new Vector3(MapLayout.DX[back], 0f, MapLayout.DZ[back]);
            Vector3 along = (back == 0 || back == 2) ? Vector3.right : Vector3.forward;
            Vector3 face = c + n * (C * 0.5f - T * 0.5f - 0.02f);

            Decal("NoclipSeam", face + Vector3.up * 1.6f, -n, new Vector2(3f, 3f), M.ExitGlow);
            for (int i = 0; i < 9; i++)
            {
                float w = 0.15f + Rand(i, 1, 230) * 0.7f, h = 0.2f + Rand(i, 2, 230) * 1.1f;
                Vector3 p = face - n * 0.1f + along * ((Rand(i, 3, 230) - 0.5f) * 3.2f) + Vector3.up * (0.4f + Rand(i, 4, 230) * 2.4f);
                var slab = Part("SeamShard", Group("Decor"), p, new Vector3(w, h, 0.12f), M.ExitGlow, 1f, false);
                slab.transform.rotation = Quaternion.LookRotation(-n, Vector3.up) * Quaternion.Euler(0f, 0f, Rand(i, 5, 230) * 90f - 45f);
            }

            var go = new GameObject("Exit Zone");
            go.transform.SetParent(Group("Zones"), false);
            go.AddComponent<ExitZone>().bounds = new Bounds(c + Vector3.up * 1.5f, new Vector3(C, 3f, C) * 0.8f);
        }

        private static void BuildAudioAndLights(ArmLocomotion loco)
        {
            var lights = new GameObject("Light Manager");
            lights.transform.SetParent(_root, false);
            lights.AddComponent<LightPanelManager>().target = loco.head;

            var ambience = new GameObject("Ambience");
            ambience.transform.SetParent(_root, false);
            ambience.AddComponent<AmbienceController>().listener = loco.head;

            var reverb = new GameObject("Reverb Zone");
            reverb.transform.SetParent(_root, false);
            reverb.transform.position = new Vector3(MapLayout.Width * C * 0.5f, 2f, MapLayout.Height * C * 0.5f);
            var rz = reverb.AddComponent<AudioReverbZone>();
            rz.reverbPreset = AudioReverbPreset.Hallway;
            rz.minDistance = MapLayout.Width * C * 0.7f;
            rz.maxDistance = MapLayout.Width * C * 0.9f;
        }

        private static void ApplyAtmosphere(ArmLocomotion loco)
        {
            var fog = new Color(0.34f, 0.29f, 0.13f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.fogColor = fog;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.17f, 0.09f);
            RenderSettings.skybox = null;

            var cam = loco.head.GetComponent<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = fog;
                cam.farClipPlane = 90f;
            }
        }

        // ------------------------------------------------------------ culling blocks

        /// <summary>Groups everything into 24 m blocks so ChunkVisibility can switch far-away parts off.</summary>
        private static void BuildBlocks()
        {
            var blocksRoot = new GameObject("Blocks");
            blocksRoot.transform.SetParent(_root, false);
            var blocks = new Dictionary<(int, int), GameObject>();
            var subs = new Dictionary<(int, int, string), Transform>();
            float size = BlockCells * C;

            foreach (var kv in new List<KeyValuePair<string, Transform>>(Groups))
            {
                if (kv.Key == "Zones" || kv.Key.StartsWith("Hub:")) continue;
                Transform group = kv.Value;
                var src = group.GetComponent<SurfaceProperties>();
                var children = new List<Transform>();
                foreach (Transform child in group) children.Add(child);

                foreach (var child in children)
                {
                    Vector3 p = child.position;
                    var key = (Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.z / size));
                    if (!blocks.TryGetValue(key, out var block))
                    {
                        block = new GameObject($"Block {key.Item1},{key.Item2}");
                        block.transform.SetParent(blocksRoot.transform, false);
                        blocks[key] = block;
                    }
                    var subKey = (key.Item1, key.Item2, kv.Key);
                    if (!subs.TryGetValue(subKey, out var sub))
                    {
                        var go = new GameObject(kv.Key);
                        go.transform.SetParent(block.transform, false);
                        if (src != null)
                        {
                            var sp = go.AddComponent<SurfaceProperties>();
                            sp.kind = src.kind;
                            sp.slipPercentage = src.slipPercentage;
                        }
                        sub = go.transform;
                        subs[subKey] = sub;
                    }
                    child.SetParent(sub, true);
                }
                Object.DestroyImmediate(group.gameObject);
            }

            var vis = _root.gameObject.AddComponent<ChunkVisibility>();
            vis.blockSize = size;
            var list = new List<GameObject>();
            var centers = new List<Vector3>();
            foreach (var kv in blocks)
            {
                list.Add(kv.Value);
                centers.Add(new Vector3((kv.Key.Item1 + 0.5f) * size, 0f, (kv.Key.Item2 + 0.5f) * size));
            }
            vis.blocks = list.ToArray();
            vis.centers = centers.ToArray();
            if (_cctvSystem != null) _cctvSystem.chunks = vis;
        }

        private static void MarkStatic()
        {
            var names = new HashSet<string> { "Carpet", "Wallpaper", "Concrete", "Ceiling", "Metal", "Water", "Crates", "Dividers" };
            foreach (var kv in Groups)
                if (kv.Key.StartsWith("Hub:") && kv.Value != null && kv.Key != "Hub:Decor" && kv.Key != "Hub:Fixtures" && kv.Key != "Hub:Doors")
                    foreach (var t in kv.Value.GetComponentsInChildren<Transform>())
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            var blocksRoot = _root.Find("Blocks");
            if (blocksRoot == null) return;
            foreach (Transform block in blocksRoot)
                foreach (Transform sub in block)
                {
                    if (!names.Contains(sub.name)) continue;
                    foreach (var t in sub.GetComponentsInChildren<Transform>())
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                }
        }
    }
}
