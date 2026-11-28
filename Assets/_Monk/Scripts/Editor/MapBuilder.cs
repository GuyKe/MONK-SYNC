using System.Collections.Generic;
using Monk.AI;
using Monk.Audio;
using Monk.Gameplay;
using Monk.Locomotion;
using Monk.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Monk.EditorTools
{
    /// <summary>
    /// Menu: Monk > Build Backrooms Map.
    /// Builds the whole level in the open scene from MapLayout: textures, geometry, doors, hazards, lights,
    /// vents, props, monster, audio and the player rig. Run it again to rebuild.
    /// </summary>
    public static class MapBuilder
    {
        private const float C = MapLayout.Cell;
        private const float T = 0.3f;            // wall thickness
        private const float Pit = MapLayout.PitDepth;

        private static BackroomsAssets.Set M;
        private static Transform _root;
        private static readonly Dictionary<string, Transform> Groups = new Dictionary<string, Transform>();

        private struct Hole { public float along, width, y0, y1; }
        private static readonly Dictionary<(int x, int z, int dir), Hole> Holes = new Dictionary<(int, int, int), Hole>();

        // Vent shafts between two rooms (both ends are on the same row, running east-west).
        private static readonly (int x1, int z1, int x2, int z2)[] VentSpecs =
        {
            (1, 8, 4, 8),    // west hall  <-> flooded hall (bypasses the fire door)
            (7, 11, 9, 11),  // central hall <-> pit room
            (8, 18, 10, 18), // north hall <-> server room
        };

        // Hiding nooks cut into walls: cell, wall direction (1=E, 3=W), offset along the wall.
        private static readonly (int x, int z, int dir, float off)[] NookSpecs =
        {
            (1, 7, 3, 0.6f), (1, 12, 3, -0.6f), (15, 8, 1, 0.6f), (15, 12, 1, -0.6f), (7, 7, 3, 0.6f),
            (9, 6, 1, -0.6f), (8, 14, 1, 0.6f), (7, 17, 1, -0.6f), (1, 16, 3, 0.6f), (14, 2, 1, -0.6f),
        };

        // ------------------------------------------------------------------ entry

        [MenuItem("Monk/Build Backrooms Map")]
        public static void Build()
        {
            var old = GameObject.Find("Backrooms Map");
            if (old != null) Object.DestroyImmediate(old);

            M = BackroomsAssets.Build();
            Groups.Clear();
            Holes.Clear();
            _doors.Clear();
            for (int dz = 0; dz < MapLayout.Height; dz++)
                for (int dx = 0; dx < MapLayout.Width; dx++)
                    if (MapLayout.IsDoor(MapLayout.At(dx, dz)))
                        _doors.Add((MapLayout.CellCenter(dx, dz), MapLayout.DoorRunsAlongZ(dx, dz)));

            _root = new GameObject("Backrooms Map").transform;
            Undo.RegisterCreatedObjectUndo(_root.gameObject, "Build Backrooms Map");

            RegisterHoles();
            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                    BuildCell(x, z);

            BuildVents();
            BuildNooks();
            BuildProps();
            BuildStartZone();
            BuildPitRoom();
            BuildServerRoom();
            BuildFloodedHall();
            BuildExit();
            BuildDetails();

            var loco = BuildPlayerAndGameplay();
            BuildMonster();
            BuildAudioAndLights(loco);
            ApplyAtmosphere(loco);
            MarkStatic();

            var surface = _root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << 2);
            var runtime = _root.gameObject.AddComponent<MapRuntime>();
            runtime.surface = surface;
            runtime.monsters = new[] { _root.Find("Stalker").gameObject };

            Selection.activeGameObject = _root.gameObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Backrooms map built. Save the scene (Ctrl+S). See README for lighting and VR setup.");
        }

        // ---------------------------------------------------------------- helpers

        private static readonly List<(Vector3 c, bool alongZ)> _doors = new List<(Vector3, bool)>();

        /// <summary>True inside the keep-clear zone in front of (and behind) any door.</summary>
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

        private static float Rand(int x, int z, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093 ^ z * 19349663 ^ salt * 83492791);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
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
                case "Water": kind = SurfaceKind.Water; slip = 0.7f; break; // slick floor under the shallow water
                case "Crates": kind = SurfaceKind.Crate; slip = 0f; break;
                case "Glass": kind = SurfaceKind.Glass; slip = 0.1f; break;
                case "Dividers": kind = SurfaceKind.Crate; slip = 0.02f; break;
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
            GameObject panel, trim;
            if (!knocked)
            {
                panel = Part("Divider", g, pos + Vector3.up * 0.85f, new Vector3(len, 1.6f, 0.12f), M.Fabric, 0.5f);
                trim = Part("DividerTrim", g, pos + Vector3.up * 1.67f, new Vector3(len, 0.05f, 0.15f), M.Metal, 1f, false);
                panel.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                trim.transform.rotation = panel.transform.rotation;
            }
            else
            {
                panel = Part("DividerKnocked", g, pos + Vector3.up * 0.38f, new Vector3(len, 1.6f, 0.12f), M.Fabric, 0.5f);
                panel.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(70f, 0f, 0f);
            }
        }

        // -------------------------------------------------------------- holes

        private static void RegisterHoles()
        {
            foreach (var v in VentSpecs)
            {
                float zc = (v.z1 + 0.5f) * C;
                var h = new Hole { along = zc, width = 0.9f, y0 = 1.8f, y1 = 2.7f };
                Holes[(v.x1, v.z1, 1)] = h;
                Holes[(v.x2, v.z2, 3)] = h;
            }

            foreach (var n in NookSpecs)
                Holes[(n.x, n.z, n.dir)] = new Hole
                    { along = (n.z + 0.5f) * C + n.off, width = 1.2f, y0 = 1.6f, y1 = 2.6f };

            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                    if (MapLayout.At(x, z) == 'X' && MapLayout.IsFloor(MapLayout.At(x + 1, z)) && MapLayout.NeedsWall(x, z, 1))
                        Holes[(x, z, 1)] = new Hole { along = (z + 0.5f) * C, width = 3.2f, y0 = 1.1f, y1 = 2.7f };
        }

        // --------------------------------------------------------------- cells

        private static void BuildCell(int x, int z)
        {
            char ch = MapLayout.At(x, z);
            if (!MapLayout.IsFloor(ch)) return;

            Vector3 c = MapLayout.CellCenter(x, z);
            float ceil = MapLayout.CeilingHeight(ch);

            BuildFloor(ch, c);
            Part("Ceiling", Group("Ceiling"), new Vector3(c.x, ceil + 0.1f, c.z), new Vector3(C, 0.2f, C), M.CeilingTile, 0.6f);
            BuildPanel(x, z, ch, c, ceil);

            for (int d = 0; d < 4; d++)
                if (MapLayout.NeedsWall(x, z, d)) BuildWall(x, z, d, ch);

            if (MapLayout.IsDoor(ch)) BuildDoor(x, z, ch);
            if (ch == 'X' && Holes.ContainsKey((x, z, 1))) BuildWindow(x, z);
        }

        private static void BuildFloor(char ch, Vector3 c)
        {
            Vector3 slab = new Vector3(c.x, -0.15f, c.z);
            Vector3 full = new Vector3(C, 0.3f, C);
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
                case 'F':
                    Part("FloodFloor", Group("Water"), slab, full, M.Carpet, 2f);
                    var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.DestroyImmediate(water.GetComponent<Collider>());
                    water.name = "Water";
                    water.transform.SetParent(Group("Decor"), false);
                    water.transform.position = new Vector3(c.x, 0.06f, c.z);
                    water.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    water.transform.localScale = new Vector3(C, C, 1f);
                    water.GetComponent<MeshRenderer>().sharedMaterial = M.Water;
                    NoShadows(water);
                    break;
                case 'S': case 'X': case 'A': case 'R': case 'D': case 'B':
                    Part("Floor", Group("Concrete"), slab, full, M.Concrete, 2f);
                    break;
                default:
                    Part("Floor", Group("Carpet"), slab, full, M.Carpet, 2f);
                    break;
            }
        }

        private static void PitFloor(Vector3 c) =>
            Part("PitFloor", Group("Concrete"), new Vector3(c.x, -Pit - 0.15f, c.z), new Vector3(C, 0.3f, C), M.Concrete);

        private static LightPanel.Mode PanelMode(int x, int z, char ch)
        {
            float r = Rand(x, z, 1);
            switch (ch)
            {
                case 'S': case 'X': case 'A': case 'E': return LightPanel.Mode.Steady;
                case 'R': return LightPanel.Mode.Dead;
                case 'F': return r < 0.35f ? LightPanel.Mode.Flicker : r < 0.55f ? LightPanel.Mode.Dead : LightPanel.Mode.Steady;
                case 'o': case 'p': case '=': case '|': case '+':
                    return r < 0.3f ? LightPanel.Mode.Flicker : r < 0.6f ? LightPanel.Mode.Dead : LightPanel.Mode.Steady;
                default:
                    return r < 0.2f ? LightPanel.Mode.Dead : r < 0.45f ? LightPanel.Mode.Flicker : LightPanel.Mode.Steady;
            }
        }

        private static void BuildPanel(int x, int z, char ch, Vector3 c, float ceil)
        {
            var mode = PanelMode(x, z, ch);
            Material mat = mode == LightPanel.Mode.Dead ? M.PanelDead : mode == LightPanel.Mode.Flicker ? M.PanelFlicker : M.PanelOn;
            var go = Part("Panel", Group("Fixtures"), new Vector3(c.x, ceil - 0.025f, c.z), new Vector3(1.2f, 0.05f, 0.6f), mat, 1f, false);
            var lp = go.AddComponent<LightPanel>();
            lp.mode = mode;
            lp.rend = go.GetComponent<MeshRenderer>();
            lp.emitColor = M.PanelEmit;
            lp.seed = Rand(x, z, 2) * 100f;
        }

        // ---------------------------------------------------------------- walls

        private static Material WallMaterial(char ch)
        {
            switch (ch)
            {
                case '.': case ',': case 'F': case 'E': case 'D': case 'B': return M.Wallpaper;
                default: return M.Concrete;
            }
        }

        private static void BuildWall(int x, int z, int dir, char ch)
        {
            int nx = x + MapLayout.DX[dir], nz = z + MapLayout.DZ[dir];
            char n = MapLayout.At(nx, nz);
            bool floorNeighbor = MapLayout.IsFloor(n);
            if (floorNeighbor && dir != 0 && dir != 1) return; // each shared edge is built once

            Vector3 c = MapLayout.CellCenter(x, z);
            Vector3 normal = new Vector3(MapLayout.DX[dir], 0f, MapLayout.DZ[dir]);
            float ceil = MapLayout.CeilingHeight(ch);
            float baseY = MapLayout.IsPitSpace(ch) ? -Pit : -0.3f;
            float topY = Mathf.Max(ceil, floorNeighbor ? MapLayout.CeilingHeight(n) : 0f) + 0.2f;

            // Walls against solid sit just inside the cell; walls between two cells sit on the shared edge.
            Vector3 edge = c + normal * (C * 0.5f) - (floorNeighbor ? Vector3.zero : normal * (T * 0.5f));
            bool alongX = dir == 0 || dir == 2;
            float centerAlong = alongX ? edge.x : edge.z;
            float len = C + 2f * T;
            Material mat = WallMaterial(ch);
            Transform group = Group(mat == M.Concrete ? "Concrete" : "Wallpaper");

            void Seg(float a0, float a1, float y0, float y1)
            {
                if (a1 - a0 < 0.01f || y1 - y0 < 0.01f) return;
                float mid = centerAlong + (a0 + a1) * 0.5f;
                float my = (y0 + y1) * 0.5f;
                Vector3 center = alongX ? new Vector3(mid, my, edge.z) : new Vector3(edge.x, my, mid);
                Vector3 size = alongX ? new Vector3(a1 - a0, y1 - y0, T) : new Vector3(T, y1 - y0, a1 - a0);
                Part("Wall", group, center, size, mat, 2.4f);
            }

            if (Holes.TryGetValue((x, z, dir), out var hole))
            {
                float ho = hole.along - centerAlong, hw = hole.width * 0.5f;
                Seg(-len * 0.5f, ho - hw, baseY, topY);
                Seg(ho + hw, len * 0.5f, baseY, topY);
                Seg(ho - hw, ho + hw, baseY, hole.y0);
                Seg(ho - hw, ho + hw, hole.y1, topY);
            }
            else
            {
                Seg(-len * 0.5f, len * 0.5f, baseY, topY);
            }

            if (mat == M.Wallpaper) WallDecor(x, z, dir, edge, normal, alongX, ceil);
        }

        private static void WallDecor(int x, int z, int dir, Vector3 edge, Vector3 normal, bool alongX, float ceil)
        {
            Vector3 inner = edge - normal * (T * 0.5f);   // the wall's visible face
            Vector3 inward = -normal;
            Vector3 along = alongX ? Vector3.right : Vector3.forward;

            // stained baseboard
            Part("Baseboard", Group("Decor"), inner + inward * 0.025f + Vector3.up * 0.07f,
                alongX ? new Vector3(C, 0.14f, 0.05f) : new Vector3(0.05f, 0.14f, C), M.Baseboard, 0.3f, false);

            // peeling corner near the ceiling
            if (Rand(x, z, 10 + dir) < 0.3f)
            {
                float side = Rand(x, z, 30 + dir) > 0.5f ? 1f : -1f;
                Decal("Peel", inner + inward * 0.012f + along * (side * (C * 0.5f - 0.35f)) + Vector3.up * (ceil - 0.35f),
                    inward, new Vector2(-side * 0.6f, 0.6f), M.Peel);
            }

            // water damage hanging from the ceiling line
            if (Rand(x, z, 20 + dir) < 0.4f)
            {
                float off = (Rand(x, z, 40 + dir) * 2f - 1f) * 1.1f;
                Decal("WaterStain", inner + inward * 0.008f + along * off + Vector3.up * (ceil - 0.8f),
                    inward, new Vector2(1.0f, 1.5f), M.Stain);
            }

            // Small fittings, kept off the door approaches.
            if (NearDoor(inner + inward * 0.5f)) return;
            float jitter = (Rand(x, z, 51 + dir) - 0.5f) * 2.4f;
            if (Rand(x, z, 50 + dir) < 0.3f)
                Part("Outlet", Group("Decor"), inner + inward * 0.012f + along * jitter + Vector3.up * 0.3f,
                    alongX ? new Vector3(0.08f, 0.12f, 0.02f) : new Vector3(0.02f, 0.12f, 0.08f), M.Paper, 1f, false);
            if (Rand(x, z, 55 + dir) < 0.2f)
                Part("LightSwitch", Group("Decor"), inner + inward * 0.012f - along * jitter + Vector3.up * 1.2f,
                    alongX ? new Vector3(0.08f, 0.12f, 0.02f) : new Vector3(0.02f, 0.12f, 0.08f), M.Paper, 1f, false);
            if (Rand(x, z, 60 + dir) < 0.25f)
                Part("Conduit", Group("Decor"), inner + inward * 0.04f + Vector3.up * (ceil - 0.35f),
                    alongX ? new Vector3(C, 0.06f, 0.06f) : new Vector3(0.06f, 0.06f, C), M.Metal, 1f, false);
            if (Rand(x, z, 65 + dir) < 0.1f)
            {
                Vector3 p = inner + inward * 0.12f + along * ((Rand(x, z, 66 + dir) - 0.5f) * 2f) + Vector3.up * 0.95f;
                Cylinder("Extinguisher", p, new Vector3(0.16f, 0.22f, 0.16f), M.Red);
                Part("ExtinguisherBracket", Group("Decor"), p + normal * 0.07f, alongX ? new Vector3(0.22f, 0.08f, 0.04f) : new Vector3(0.04f, 0.08f, 0.22f), M.Rack, 1f, false);
            }
        }

        // ---------------------------------------------------------------- doors

        private static void BuildDoor(int x, int z, char ch)
        {
            Vector3 c = MapLayout.CellCenter(x, z);
            bool alongZ = MapLayout.DoorRunsAlongZ(x, z);
            float ceil = MapLayout.CeilingHeight(ch);
            float thick = ch == 'A' ? 0.7f : ch == 'B' ? 0.5f : 0.35f;
            string label = ch == 'A' ? "Airlock Door" : ch == 'B' ? "Blast Door" : "Fire Door";

            var root = new GameObject(label);
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
            var obstacle = panel.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = panelSize;

            // Rising blast doors slide up into a housing above the ceiling.
            if (ch == 'B')
            {
                Vector3 hs = alongZ ? new Vector3(C + 0.2f, ceil + 0.1f, thick + 0.4f) : new Vector3(thick + 0.4f, ceil + 0.1f, C + 0.2f);
                Part("Housing", root.transform, new Vector3(c.x, ceil + 0.2f + (ceil + 0.1f) * 0.5f, c.z), hs, M.Metal, 1f, false);
            }

            // Warning lamps on both faces.
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

            // Hazard stripes on the floor either side.
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
            door.style = ch == 'B' ? DoorStyle.Rise : DoorStyle.SlideSideways;
            door.openOffset = ch == 'B' ? new Vector3(0f, ceil + 0.1f, 0f)
                : (alongZ ? new Vector3(C * 0.98f, 0f, 0f) : new Vector3(0f, 0f, C * 0.98f));
            door.startOpen = ch != 'A';
            door.automatic = ch == 'B';
            if (ch == 'A') { door.warningSeconds = 1.2f; door.closeSeconds = 1.4f; door.openSeconds = 3f; }

            // Buttons on one wall, levers on the other (airlock: buttons on both).
            float wallOff = C * 0.5f - T;
            for (int s = -1; s <= 1; s += 2)
            {
                float along = s * 1.1f;
                Vector3 east = alongZ ? new Vector3(c.x + wallOff, 1.3f, c.z + along) : new Vector3(c.x + along, 1.3f, c.z + wallOff);
                Vector3 west = alongZ ? new Vector3(c.x - wallOff, 1.3f, c.z + along) : new Vector3(c.x + along, 1.3f, c.z - wallOff);
                Vector3 outE = alongZ ? Vector3.left : Vector3.back;
                Vector3 outW = -outE;

                MountButton(door, east, outE);
                if (ch == 'A') MountButton(door, west, outW);
                else MountLever(door, west, outW);

                // green exit sign above each button, hazard-striped frame beside the opening
                Vector3 signSize = alongZ ? new Vector3(0.04f, 0.18f, 0.5f) : new Vector3(0.5f, 0.18f, 0.04f);
                Part("ExitSign", Group("Decor"), new Vector3(east.x, 2.55f, east.z) + outE * 0.03f, signSize, M.LedGreen, 1f, false);
                Part("ExitSign", Group("Decor"), new Vector3(west.x, 2.55f, west.z) + outW * 0.03f, signSize, M.LedGreen, 1f, false);
                Vector3 stripeE = alongZ ? new Vector3(c.x + wallOff, ceil * 0.5f, c.z + s * (thick * 0.5f + 0.32f)) : new Vector3(c.x + s * (thick * 0.5f + 0.32f), ceil * 0.5f, c.z + wallOff);
                Vector3 stripeW = alongZ ? new Vector3(c.x - wallOff, ceil * 0.5f, c.z + s * (thick * 0.5f + 0.32f)) : new Vector3(c.x + s * (thick * 0.5f + 0.32f), ceil * 0.5f, c.z - wallOff);
                Vector3 stripeSz = alongZ ? new Vector3(0.02f, ceil, 0.16f) : new Vector3(0.16f, ceil, 0.02f);
                Part("FrameStripe", Group("Decor"), stripeE + outE * 0.012f, stripeSz, M.Hazard, 0.5f, false);
                Part("FrameStripe", Group("Decor"), stripeW + outW * 0.012f, stripeSz, M.Hazard, 0.5f, false);
            }

            if (ch != 'A')
            {
                var zone = new GameObject("DoorwaySlowZone");
                zone.transform.SetParent(Group("Zones"), false);
                var sz = zone.AddComponent<MonsterSlowZone>();
                sz.bounds = new Bounds(new Vector3(c.x, 1.5f, c.z), new Vector3(C, 3f, C));
                sz.speedMultiplier = 0.5f;
            }
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
            b.pressDirection = -outward;
        }

        private static void MountLever(IndustrialDoor door, Vector3 wallPoint, Vector3 outward)
        {
            bool onX = Mathf.Abs(outward.x) > 0.5f;
            Vector3 plateSize = onX ? new Vector3(0.06f, 0.5f, 0.22f) : new Vector3(0.22f, 0.5f, 0.06f);
            Part("LeverPlate", Group("Decor"), wallPoint + outward * 0.03f, plateSize, M.Rack, 1f, false);

            var root = new GameObject("DoorLever");
            root.transform.SetParent(Group("Decor"), false);
            root.transform.position = wallPoint + outward * 0.1f;
            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);
            var arm = Part("Arm", pivot.transform, root.transform.position + Vector3.up * 0.22f, new Vector3(0.07f, 0.45f, 0.07f), M.LedAmber, 1f);
            Part("Knob", pivot.transform, root.transform.position + Vector3.up * 0.46f, new Vector3(0.14f, 0.1f, 0.14f), M.LedRed, 1f, false);

            var lever = root.AddComponent<DoorLever>();
            lever.door = door;
            lever.pivot = pivot.transform;
        }

        // --------------------------------------------------------------- window

        private static void BuildWindow(int x, int z)
        {
            Vector3 c = MapLayout.CellCenter(x, z);
            float ex = c.x + C * 0.5f;

            Part("WindowSill", Group("Metal"), new Vector3(ex, 1.1f, c.z), new Vector3(T + 0.1f, 0.12f, 3.4f), M.Metal, 1f);
            Part("WindowHead", Group("Metal"), new Vector3(ex, 2.7f, c.z), new Vector3(T + 0.1f, 0.12f, 3.4f), M.Metal, 1f);
            Part("WindowMullion", Group("Metal"), new Vector3(ex, 1.9f, c.z), new Vector3(0.14f, 1.6f, 0.12f), M.Metal, 1f);

            // Two clear panes. A hard enough hand slap shatters one into physical shards.
            for (int side = -1; side <= 1; side += 2)
            {
                var pane = Part("GlassPane", Group("Glass"), new Vector3(ex, 1.9f, c.z + side * 0.82f),
                    new Vector3(0.05f, 1.6f, 1.56f), M.Glass, 1f);
                var glass = pane.AddComponent<BreakableGlass>();
                glass.rend = pane.GetComponent<MeshRenderer>();
                glass.col = pane.GetComponent<BoxCollider>();
                glass.shardMaterial = M.Glass;
            }
        }

        // ---------------------------------------------------------------- vents

        private static void BuildVents()
        {
            var metal = Group("Metal");
            foreach (var v in VentSpecs)
            {
                float zc = (v.z1 + 0.5f) * C;
                float x0 = (v.x1 + 1) * C - T, x1 = v.x2 * C + T;
                float len = x1 - x0, xm = (x0 + x1) * 0.5f;

                Part("VentFloor", metal, new Vector3(xm, 1.75f, zc), new Vector3(len, 0.1f, 1.1f), M.Metal, 1f);
                Part("VentRoof", metal, new Vector3(xm, 2.75f, zc), new Vector3(len, 0.1f, 1.1f), M.Metal, 1f);
                Part("VentSide", metal, new Vector3(xm, 2.25f, zc - 0.5f), new Vector3(len, 0.9f, 0.1f), M.Metal, 1f);
                Part("VentSide", metal, new Vector3(xm, 2.25f, zc + 0.5f), new Vector3(len, 0.9f, 0.1f), M.Metal, 1f);

                var audio = new GameObject("VentAudio");
                audio.transform.SetParent(Group("Decor"), false);
                audio.transform.position = new Vector3(xm, 2.25f, zc);
                audio.AddComponent<VentEmitter>();

                // Crates under each opening so arms can reach the sill.
                CrateStack(new Vector3(x0 - 0.55f, 0f, zc), 2);
                CrateStack(new Vector3(x1 + 0.55f, 0f, zc), 2);
            }
        }

        private static void BuildNooks()
        {
            foreach (var n in NookSpecs)
            {
                Vector3 c = MapLayout.CellCenter(n.x, n.z);
                float zc = c.z + n.off;
                float sign = n.dir == 1 ? 1f : -1f;
                float edge = n.dir == 1 ? (n.x + 1) * C : n.x * C;
                float x1 = edge + sign * 2.2f, xm = (edge + x1) * 0.5f;
                var g = Group("Concrete");

                Part("NookFloor", g, new Vector3(xm, 1.45f, zc), new Vector3(2.2f, 0.1f, 2.4f), M.Concrete);
                Part("NookRoof", g, new Vector3(xm, 3.15f, zc), new Vector3(2.2f, 0.1f, 2.4f), M.Concrete);
                Part("NookBack", g, new Vector3(x1 + sign * 0.05f, 2.3f, zc), new Vector3(0.1f, 1.8f, 2.4f), M.Concrete);
                Part("NookSide", g, new Vector3(xm, 2.3f, zc - 1.15f), new Vector3(2.2f, 1.8f, 0.1f), M.Concrete);
                Part("NookSide", g, new Vector3(xm, 2.3f, zc + 1.15f), new Vector3(2.2f, 1.8f, 0.1f), M.Concrete);

                // Crates below the opening.
                float cx = edge - sign * (T + 0.55f);
                CrateStack(new Vector3(cx, 0f, zc), 2);
            }
        }

        // ---------------------------------------------------------------- props

        private static void BuildProps()
        {
            // Crate stacks in corners: cell, corner offset, height.
            (int x, int z, float ox, float oz, int n)[] stacks =
            {
                (3, 5, -1, -1, 2), (12, 5, 1, -1, 3), (1, 10, -1, 1, 3), (15, 14, 1, -1, 2), (1, 17, 1, 1, 2),
                (7, 16, -1, 1, 3), (11, 2, -1, -1, 2), (13, 3, 1, 1, 3), (9, 7, -1, 1, 2), (12, 15, -1, -1, 3),
                (5, 18, 1, -1, 2), (4, 11, 1, 0, 1),
            };
            foreach (var s in stacks)
            {
                Vector3 c = MapLayout.CellCenter(s.x, s.z);
                CrateStack(new Vector3(c.x + s.ox * 1.2f, 0f, c.z + s.oz * 1.2f), s.n, Rand(s.x, s.z, 5) * 40f);
            }

            // Office dividers: cell, offset, yaw, knocked over.
            (int x, int z, float ox, float oz, float yaw, bool down)[] dividers =
            {
                (6, 5, 0, 0.8f, 90, false), (11, 5, 0, -0.8f, 90, false), (13, 5, 0, 0.8f, 90, false),
                (1, 9, 0.8f, 0, 0, false), (1, 13, -0.8f, 0, 0, false), (15, 8, -0.8f, 0, 0, false),
                (15, 12, 0.8f, 0, 0, false), (10, 2, 0, 0, 0, false), (13, 2, 0, 0, 90, false),
                (7, 7, 0.8f, 0, 0, false), (7, 12, -0.8f, 0, 0, false), (1, 7, 0.3f, 0.3f, -30, true),
                (8, 5, 0.5f, -0.5f, 20, true), (15, 10, 0, 0, 70, true), (11, 3, 0, 0.7f, 40, true),
                (11, 15, 0, 0.8f, 90, false), (4, 18, 0, 0.8f, 90, false),
            };
            foreach (var d in dividers)
            {
                Vector3 c = MapLayout.CellCenter(d.x, d.z);
                Divider(c + new Vector3(d.ox, 0f, d.oz), d.yaw, d.down);
            }

            // Exposed ceiling pipes along the halls.
            Pipe(new Vector3(4.5f, 2.65f, 20.7f), new Vector3(62f, 2.65f, 20.7f), 0.12f);
            Pipe(new Vector3(5.8f, 2.65f, 22f), new Vector3(5.8f, 2.65f, 76f), 0.12f);
            Pipe(new Vector3(62.7f, 2.65f, 22f), new Vector3(62.7f, 2.65f, 62f), 0.12f);
            Pipe(new Vector3(19f, 2.65f, 24f), new Vector3(19f, 2.65f, 54f), 0.1f);
            Pipe(new Vector3(8f, 4.3f, 57.2f), new Vector3(28f, 4.3f, 57.2f), 0.12f);
            Pipe(new Vector3(8f, 3.9f, 58.7f), new Vector3(28f, 3.9f, 58.7f), 0.1f);

            // Office cubicles in the atrium (the pillars are gone: the spawn view is open).
            Cubicle(new Vector3(44f, 0f, 12f), 0f);
            Cubicle(new Vector3(53f, 0f, 9f), 90f);
            Cubicle(new Vector3(56f, 0f, 15.5f), 0f);

            // Metal shelving with boxes: more ledges to climb.
            Shelf(new Vector3(4.6f, 0f, 26f), 90f);
            Shelf(new Vector3(4.6f, 0f, 62f), 90f);
            Shelf(new Vector3(63.4f, 0f, 28f), 90f);
            Shelf(new Vector3(63.4f, 0f, 56f), 90f);
            Shelf(new Vector3(12f, 0f, 75.4f), 0f);
            Shelf(new Vector3(20f, 0f, 75.4f), 0f);

            // Catwalk in the tall hall: a ledge to climb onto and hide above sightlines.
            const float cwY = 2.4f;
            Part("Catwalk", Group("Metal"), new Vector3(18f, cwY - 0.075f, 58.9f), new Vector3(20f, 0.15f, 1.6f), M.Metal, 1f);
            foreach (float px in new[] { 8.6f, 18f, 27.4f })
                Part("CatwalkPost", Group("Metal"), new Vector3(px, cwY * 0.5f, 58.9f), new Vector3(0.15f, cwY, 0.15f), M.Metal, 1f);
            CrateStack(new Vector3(9.3f, 0f, 57.2f), 2);
            CrateStack(new Vector3(26.7f, 0f, 57.2f), 2);
            CrateStack(new Vector3(14f, cwY, 59f), 2);
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

        private static void Cubicle(Vector3 pos, float yaw)
        {
            if (NearDoor(pos)) return;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Divider(pos + q * new Vector3(0f, 0f, 1.2f), yaw, false);
            Divider(pos + q * new Vector3(-1.2f, 0f, 0f), yaw + 90f, false);
            Divider(pos + q * new Vector3(1.2f, 0f, 0f), yaw + 90f, false);
            Desk(pos + q * new Vector3(0f, 0f, 0.7f), yaw);
            Chair(pos + q * new Vector3(0.2f, 0f, -0.1f), yaw + 160f);
            // a few papers on the desk
            Part("Papers", Group("Decor"), pos + q * new Vector3(-0.5f, 0.78f, 0.6f), new Vector3(0.21f, 0.01f, 0.3f), M.Paper, 1f, false);
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

        private static void BuildStartZone()
        {
            // Lockers along the west wall and a concrete bench: a research threshold.
            for (int i = 0; i < 6; i++)
                Part("Locker", Group("Metal"), new Vector3(12.6f, 1f, 3f + i * 1.25f), new Vector3(0.5f, 2f, 0.6f), M.Rack, 1f);
            Part("Bench", Group("Concrete"), new Vector3(24f, 0.25f, 11f), new Vector3(3f, 0.5f, 0.8f), M.Concrete, 2f);
            Part("Bench", Group("Concrete"), new Vector3(20f, 0.25f, 11f), new Vector3(1.5f, 0.5f, 0.8f), M.Concrete, 2f);
            CrateStack(new Vector3(33.5f, 0f, 14.5f), 2);

            // Hazard band approaching the airlock.
            Part("ApproachStripe", Group("Decor"), new Vector3(26f, 0.011f, 14.8f), new Vector3(3.4f, 0.02f, 0.8f), M.Hazard, 0.5f, false);
            Part("WallBand", Group("Decor"), new Vector3(24.2f, 0.3f, 15.84f), new Vector3(3.4f, 0.4f, 0.04f), M.Hazard, 0.5f, false);

            // Control console against the south wall with dead and live screens.
            Desk(new Vector3(20f, 0f, 1.3f), 180f, 3.6f, false);
            Desk(new Vector3(27f, 0f, 1.3f), 180f, 3.0f, false);
            for (int i = 0; i < 5; i++)
            {
                float sx = 18.6f + i * 1.65f;
                Part("ConsoleScreen", Group("Decor"), new Vector3(sx, 1.15f, 1.0f), new Vector3(0.7f, 0.45f, 0.05f), M.Rack, 1f, false);
                Part("ConsoleGlow", Group("Decor"), new Vector3(sx, 1.15f, 1.03f), new Vector3(0.62f, 0.37f, 0.01f),
                    i % 3 == 1 ? M.PanelDead : (i % 2 == 0 ? M.LedGreen : M.LedAmber), 1f, false);
            }
            Chair(new Vector3(20f, 0f, 2.6f), 0f);
            Chair(new Vector3(26.5f, 0f, 2.6f), 20f);

            // Pipes, a floor drain, warning boards and an equipment cabinet.
            Pipe(new Vector3(12.5f, 3.7f, 0.45f), new Vector3(35.5f, 3.7f, 0.45f), 0.1f);
            Pipe(new Vector3(12.5f, 3.35f, 0.45f), new Vector3(35.5f, 3.35f, 0.45f), 0.07f);
            Part("FloorDrain", Group("Decor"), new Vector3(29f, 0.011f, 8f), new Vector3(0.7f, 0.02f, 0.7f), M.Rack, 1f, false);
            Part("WarningBoard", Group("Decor"), new Vector3(16f, 2.1f, 15.68f), new Vector3(1.2f, 0.8f, 0.03f), M.Hazard, 0.5f, false);
            Part("WarningBoard", Group("Decor"), new Vector3(19f, 2.1f, 15.68f), new Vector3(0.8f, 0.8f, 0.03f), M.Hazard, 0.5f, false);
            Part("Cabinet", Group("Metal"), new Vector3(34.9f, 1f, 14f), new Vector3(0.6f, 2f, 1.2f), M.Rack, 1f);
            Cylinder("Extinguisher", new Vector3(34.5f, 0.45f, 12.4f), new Vector3(0.18f, 0.24f, 0.18f), M.Red);
        }

        private static void BuildPitRoom()
        {
            // Pipes to swing across the drops, and hanging pipes to grab.
            Pipe(new Vector3(36.6f, 3f, 42f), new Vector3(55.4f, 3f, 42f), 0.1f);
            Pipe(new Vector3(36.6f, 3.3f, 50f), new Vector3(55.4f, 3.3f, 50f), 0.1f);
            Pipe(new Vector3(42f, 2f, 46f), new Vector3(42f, 6.3f, 46f), 0.07f);
            Pipe(new Vector3(50f, 2f, 46f), new Vector3(50f, 6.3f, 46f), 0.07f);
            Pipe(new Vector3(46f, 2f, 50f), new Vector3(46f, 6.3f, 50f), 0.07f);
            Pipe(new Vector3(46f, 2f, 42f), new Vector3(46f, 6.3f, 42f), 0.07f);

            // Warning stripes where the ledge begins.
            Part("PitStripe", Group("Decor"), new Vector3(38f, 0.011f, 36.5f), new Vector3(3.4f, 0.02f, 0.5f), M.Hazard, 0.5f, false);

            // Hazard edging along every walkway, and bollards where the ledge begins.
            for (int cz = 9; cz <= 13; cz++)
                for (int cx = 9; cx <= 13; cx++)
                {
                    char w = MapLayout.At(cx, cz);
                    Vector3 c = MapLayout.CellCenter(cx, cz);
                    if (w == '=' || w == '+')
                        foreach (float side in new[] { -0.55f, 0.55f })
                            Part("WalkwayEdge", Group("Decor"), new Vector3(c.x, 0.011f, c.z + side), new Vector3(C, 0.02f, 0.1f), M.Hazard, 0.3f, false);
                    if (w == '|' || w == '+')
                        foreach (float side in new[] { -0.55f, 0.55f })
                            Part("WalkwayEdge", Group("Decor"), new Vector3(c.x + side, 0.011f, c.z), new Vector3(0.1f, 0.02f, C), M.Hazard, 0.3f, false);
                }
            Cylinder("Bollard", new Vector3(36.9f, 0.45f, 36.7f), new Vector3(0.24f, 0.45f, 0.24f), M.Hazard, true);
            Cylinder("Bollard", new Vector3(39.1f, 0.45f, 36.7f), new Vector3(0.24f, 0.45f, 0.24f), M.Hazard, true);

            var hazard = new GameObject("Pitfalls Kill Zone");
            hazard.transform.SetParent(Group("Zones"), false);
            var hz = hazard.AddComponent<HazardZone>();
            hz.bounds = new Bounds(new Vector3(46f, -5.2f, 46f), new Vector3(20f, 2f, 20f));
            hz.damage = 100f;
            hz.cause = "Fell into the Pitfalls";
        }

        private static void BuildServerRoom()
        {
            float[] xs = { 42f, 44.4f, 46.8f, 53.2f, 55.6f, 58f };
            for (int row = 0; row < 2; row++)
            {
                float zRow = row == 0 ? 71f : 76f;
                float face = row == 0 ? -1f : 1f;
                for (int i = 0; i < xs.Length; i++)
                {
                    Part("Rack", Group("Metal"), new Vector3(xs[i], 1.05f, zRow), new Vector3(0.8f, 2.1f, 0.9f), M.Rack, 1f);
                    for (int k = 0; k < 3; k++)
                    {
                        float r = Rand(i, row * 7 + k, 200);
                        Material led = r < 0.55f ? M.LedGreen : r < 0.8f ? M.LedAmber : M.LedRed;
                        Part("LED", Group("Decor"), new Vector3(xs[i] - 0.15f + k * 0.15f, 0.7f + Rand(i, k, 201 + row) * 1.2f, zRow + face * 0.46f),
                            new Vector3(0.08f, 0.04f, 0.02f), led, 1f, false);
                    }
                }
            }
            Part("CableTray", Group("Metal"), new Vector3(50f, 2.6f, 69.5f), new Vector3(19f, 0.1f, 0.5f), M.Metal, 1f);
            Part("CableTray", Group("Metal"), new Vector3(50f, 2.6f, 72.3f), new Vector3(19f, 0.1f, 0.5f), M.Metal, 1f);
            Part("CableTray", Group("Metal"), new Vector3(50f, 2.6f, 77.8f), new Vector3(19f, 0.1f, 0.5f), M.Metal, 1f);
            CrateStack(new Vector3(58.8f, 0f, 79f), 2);
            CrateStack(new Vector3(41.2f, 0f, 79f), 3);

            // Workstation, floor cabling and cables dropping from the trays into the racks.
            Desk(new Vector3(58.6f, 0f, 73.6f), 90f, 2.0f);
            Chair(new Vector3(57.4f, 0f, 73.6f), 90f);
            for (int i = 0; i < 6; i++)
            {
                float cx = 41.5f + i * 3.1f + Rand(i, 3, 210);
                Part("FloorCable", Group("Decor"), new Vector3(cx, 0.04f, 73.6f), new Vector3(0.07f, 0.07f, 5.2f - Rand(i, 4, 211) * 1.5f), M.Rack, 1f, false);
                Part("RackCable", Group("Decor"), new Vector3(xs[i % xs.Length] + 0.2f, 2.35f, 71f), new Vector3(0.05f, 0.5f, 0.05f), M.Rack, 1f, false);
            }

            // Dim red emergency lights: the only real light in here.
            foreach (float x in new[] { 44f, 56f })
            {
                var go = new GameObject("EmergencyLight");
                go.transform.SetParent(Group("Fixtures"), false);
                go.transform.position = new Vector3(x, 2.5f, 74f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.1f, 0.05f);
                l.range = 7f;
                l.intensity = 2.2f;
                l.shadows = LightShadows.None;
            }
        }

        private static void BuildFloodedHall()
        {
            var go = new GameObject("FloodReflection");
            go.transform.SetParent(Group("Fixtures"), false);
            go.transform.position = new Vector3(18f, 1.6f, 40f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 64;
            probe.size = new Vector3(4.5f, 3.2f, 32.5f);
            probe.boxProjection = true;
            probe.hdr = false;

            // A fallen pipe leaning across the water, half-sunk crates and hanging cables.
            Pipe(new Vector3(16.4f, 2.5f, 31f), new Vector3(19.4f, 0.35f, 33f), 0.1f);
            CrateStack(new Vector3(18.4f, 0f, 45f), 2, 15f);
            CrateStack(new Vector3(17.2f, 0f, 29f), 1, 40f);
            for (int i = 0; i < 6; i++)
                Part("HangingCable", Group("Decor"), new Vector3(17.2f + Rand(i, 1, 220) * 2.6f, 2.6f, 26f + i * 4.6f), new Vector3(0.04f, 1.2f, 0.04f), M.Rack, 1f, false);
        }

        private static void BuildExit()
        {
            Vector3 c = MapLayout.CellCenter(MapLayout.ExitCell.x, MapLayout.ExitCell.y);
            Decal("NoclipSeam", new Vector3(c.x, 1.6f, c.z + C * 0.5f - T - 0.02f), Vector3.back, new Vector2(3f, 3f), M.ExitGlow);

            for (int i = 0; i < 9; i++)
            {
                float w = 0.15f + Rand(i, 1, 230) * 0.7f, h = 0.2f + Rand(i, 2, 230) * 1.1f;
                var slab = Part("SeamShard", Group("Decor"),
                    new Vector3(c.x + (Rand(i, 3, 230) - 0.5f) * 3.2f, 0.4f + Rand(i, 4, 230) * 2.4f, c.z + C * 0.5f - T - 0.1f),
                    new Vector3(w, h, 0.12f), M.ExitGlow, 1f, false);
                slab.transform.rotation = Quaternion.Euler(0f, 0f, Rand(i, 5, 230) * 90f - 45f);
            }

            var go = new GameObject("Exit Zone");
            go.transform.SetParent(Group("Zones"), false);
            go.AddComponent<ExitZone>().bounds = new Bounds(new Vector3(c.x, 1.5f, c.z + 0.5f), new Vector3(C, 3f, 3f));
        }

        // ------------------------------------------------------------ cell details

        /// <summary>Ceiling fittings everywhere; stains, litter and fallen ceiling tiles on carpet. Door approaches stay clear.</summary>
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

                    if (Rand(x, z, 300) < 0.55f)
                        Part("Sprinkler", decor, new Vector3(c.x + (Rand(x, z, 301) - 0.5f) * 3f, ceil - 0.04f, c.z + (Rand(x, z, 302) - 0.5f) * 3f),
                            new Vector3(0.07f, 0.08f, 0.07f), M.Metal, 1f, false);
                    if (Rand(x, z, 303) < 0.3f)
                        Part("CeilingGrate", decor, new Vector3(c.x + (Rand(x, z, 304) - 0.5f) * 2f, ceil - 0.015f, c.z + (Rand(x, z, 305) - 0.5f) * 2f),
                            new Vector3(0.6f, 0.03f, 0.6f), M.Rack, 1f, false);

                    if (ch != '.' && ch != ',') continue;

                    for (int i = 0; i < 2; i++)
                    {
                        if (Rand(x, z, 310 + i) > 0.35f) continue;
                        var p = new Vector3(c.x + (Rand(x, z, 312 + i) - 0.5f) * 2.8f, 0.013f, c.z + (Rand(x, z, 314 + i) - 0.5f) * 2.8f);
                        if (NearDoor(p)) continue;
                        float s = 1.1f + Rand(x, z, 316 + i) * 1.3f;
                        Decal("FloorStain", p, Vector3.up, new Vector2(s, s), M.Stain, Rand(x, z, 318 + i) * 360f);
                    }

                    if (Rand(x, z, 320) < 0.35f)
                        for (int k = 0; k < 4; k++)
                        {
                            var p = new Vector3(c.x + (Rand(x, z, 321 + k) - 0.5f) * 3f, 0.0125f, c.z + (Rand(x, z, 330 + k) - 0.5f) * 3f);
                            if (NearDoor(p)) continue;
                            var paper = Part("Paper", decor, p, new Vector3(0.21f, 0.004f, 0.3f), M.Paper, 1f, false);
                            paper.transform.rotation = Quaternion.Euler(0f, Rand(x, z, 340 + k) * 360f, 0f);
                        }

                    if (Rand(x, z, 350) < 0.18f)
                    {
                        var p = new Vector3(c.x + (Rand(x, z, 351) - 0.5f) * 2.4f, 0.04f, c.z + (Rand(x, z, 352) - 0.5f) * 2.4f);
                        if (NearDoor(p)) continue;
                        var tile = Part("FallenTile", decor, p, new Vector3(0.6f, 0.03f, 0.6f), M.CeilingTile, 0.6f, false);
                        tile.transform.rotation = Quaternion.Euler(Rand(x, z, 353) * 14f - 7f, Rand(x, z, 354) * 360f, Rand(x, z, 355) * 14f - 7f);
                        var chunk = Part("TileChunk", decor, p + new Vector3(0.5f, -0.02f, 0.3f), new Vector3(0.22f, 0.04f, 0.18f), M.CeilingTile, 0.6f, false);
                        chunk.transform.rotation = Quaternion.Euler(0f, Rand(x, z, 356) * 360f, 0f);
                    }
                }
        }

        // ------------------------------------------------------- player, gameplay

        private static ArmLocomotion BuildPlayerAndGameplay()
        {
            Vector3 start = MapLayout.CellCenter(MapLayout.StartCell.x, MapLayout.StartCell.y);
            var loco = GymBuilder.BuildPlayer(_root, new Vector3(start.x, 0.1f, start.z));

            var health = loco.gameObject.AddComponent<PlayerHealth>();
            health.locomotion = loco;
            health.startCheckpoint = new Vector3(start.x, 1.3f, start.z);
            loco.gameObject.AddComponent<HandInteractor>().locomotion = loco;
            loco.gameObject.AddComponent<HandImpactAudio>().locomotion = loco;

            foreach (var cell in new[] { new Vector2Int(5, 1), new Vector2Int(9, 8), new Vector2Int(12, 15), new Vector2Int(6, 18) })
            {
                Vector3 c = MapLayout.CellCenter(cell.x, cell.y);
                var go = new GameObject("Checkpoint " + cell.x + "," + cell.y);
                go.transform.SetParent(Group("Zones"), false);
                var cp = go.AddComponent<CheckpointZone>();
                cp.bounds = new Bounds(new Vector3(c.x, 1.5f, c.z), new Vector3(C, 4f, C));
                cp.spawn = new Vector3(c.x, 1.3f, c.z);
            }
            return loco;
        }

        private static void BuildMonster()
        {
            var route = new GameObject("Patrol Route");
            route.transform.SetParent(_root, false);
            var cells = new[]
            {
                new Vector2Int(11, 2), new Vector2Int(14, 5), new Vector2Int(15, 10), new Vector2Int(15, 15),
                new Vector2Int(11, 15), new Vector2Int(7, 7), new Vector2Int(7, 13), new Vector2Int(4, 14),
                new Vector2Int(1, 12), new Vector2Int(1, 6), new Vector2Int(7, 5), new Vector2Int(9, 6),
            };
            var waypoints = new Transform[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                var wp = new GameObject("Waypoint " + i);
                wp.transform.SetParent(route.transform, false);
                wp.transform.position = MapLayout.CellCenter(cells[i].x, cells[i].y);
                waypoints[i] = wp.transform;
            }

            var m = new GameObject("Stalker");
            m.SetActive(false); // wakes after the NavMesh is baked
            m.transform.SetParent(_root, false);
            m.transform.position = waypoints[0].position;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "Body";
            body.transform.SetParent(m.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            body.transform.localScale = new Vector3(1.2f, 1.1f, 1f);
            body.GetComponent<MeshRenderer>().sharedMaterial = M.Monster;

            foreach (float side in new[] { -0.22f, 0.22f })
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(eye.GetComponent<Collider>());
                eye.name = "Eye";
                eye.transform.SetParent(m.transform, false);
                eye.transform.localPosition = new Vector3(side, 1.75f, 0.45f);
                eye.transform.localScale = Vector3.one * 0.13f;
                eye.GetComponent<MeshRenderer>().sharedMaterial = M.MonsterEye;
            }

            var agent = m.AddComponent<NavMeshAgent>();
            agent.radius = 0.5f;   // wide: closed off from vents and narrow gaps
            agent.height = 2.2f;
            agent.speed = 2.2f;
            agent.acceleration = 10f;
            agent.angularSpeed = 240f;
            agent.stoppingDistance = 0.4f;
            m.AddComponent<PatrolMonster>().waypoints = waypoints;
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
            reverb.transform.position = new Vector3(32f, 2f, 40f);
            var rz = reverb.AddComponent<AudioReverbZone>();
            rz.reverbPreset = AudioReverbPreset.Hallway;
            rz.minDistance = 45f;
            rz.maxDistance = 60f;
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
                cam.farClipPlane = 80f;
            }
        }

        private static void MarkStatic()
        {
            foreach (var name in new[] { "Carpet", "Wallpaper", "Concrete", "Ceiling", "Metal", "Water", "Crates", "Dividers" })
            {
                if (!Groups.TryGetValue(name, out var g)) continue;
                foreach (var t in g.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }
    }
}
