using System.Collections.Generic;
using Monk.Audio;
using Monk.Hub;
using Monk.Locomotion;
using Monk.World;
using UnityEngine;

namespace Monk.EditorTools
{
    public static partial class MapBuilder
    {
        private static readonly HashSet<Vector2Int> _fuseCells = new HashSet<Vector2Int>();

        // ------------------------------------------------------- verticality props

        /// <summary>Two sheets of fallen drywall: one leaning as a ramp, one lying flat, with chunks of debris.</summary>
        private static void FallenDrywall(Vector3 pos, float yaw)
        {
            if (NearDoor(pos)) return;
            var g = Group("Crates");
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            var lean = Part("FallenDrywall", g, pos + q * new Vector3(0f, 0.6f, 0f), new Vector3(1.2f, 0.05f, 2.4f), M.Drywall, 1f);
            lean.transform.rotation = q * Quaternion.Euler(-30f, 0f, 0f);
            var flat = Part("FallenDrywall", g, pos + q * new Vector3(0.9f, 0.06f, -0.4f), new Vector3(1.2f, 0.05f, 2.4f), M.Drywall, 1f);
            flat.transform.rotation = q * Quaternion.Euler(3f, 14f, 2f);
            for (int i = 0; i < 4; i++)
            {
                var chunk = Part("DrywallChunk", Group("Decor"), pos + q * new Vector3(Rand((int)pos.x + i, (int)pos.z, 620) * 1.8f - 0.6f, 0.05f, Rand((int)pos.x, (int)pos.z + i, 621) * 2f - 1.8f),
                    new Vector3(0.3f + Rand(i, 1, 622) * 0.3f, 0.08f, 0.25f), M.Drywall, 1f, false);
                chunk.transform.rotation = Quaternion.Euler(Rand(i, 2, 623) * 20f, Rand(i, 3, 623) * 360f, Rand(i, 4, 623) * 20f);
            }
        }

        /// <summary>A bar across the corridor to swing from.</summary>
        private static void CrossingPipe(Vector3 cellCentre, Vector3 towardWall, int wallDir)
        {
            int x = Mathf.FloorToInt(cellCentre.x / C), z = Mathf.FloorToInt(cellCentre.z / C);
            if (!MapLayout.NeedsWall(x, z, (wallDir + 2) % 4)) return; // only where it can span wall to wall
            Vector3 a = cellCentre - towardWall * (C * 0.5f - T * 0.5f - 0.05f) + Vector3.up * 2.1f;
            Vector3 b = cellCentre + towardWall * (C * 0.5f - T * 0.5f - 0.05f) + Vector3.up * 2.1f;
            Pipe(a, b, 0.07f);
        }

        /// <summary>One server rack. tilt leans it as if it had been knocked.</summary>
        private static void ServerRack(Vector3 pos, float yaw, float tilt)
        {
            if (NearDoor(pos)) return;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            var rack = Part("Rack", Group("Metal"), pos + Vector3.up * 1.05f, new Vector3(0.8f, 2.1f, 0.9f), M.Rack, 1f);
            rack.transform.rotation = q * Quaternion.Euler(0f, 0f, tilt);
            for (int k = 0; k < 3; k++)
            {
                float rr = Rand((int)pos.x + k, (int)pos.z, 630);
                Material led = rr < 0.55f ? M.LedGreen : rr < 0.8f ? M.LedAmber : M.LedRed;
                var l = Part("LED", Group("Decor"), pos + q * new Vector3(-0.2f + k * 0.2f, 0.7f + Rand(k, (int)pos.x, 631) * 1.2f, 0.46f),
                    new Vector3(0.08f, 0.04f, 0.02f), led, 1f, false);
                l.transform.rotation = q;
            }
        }

        /// <summary>Racks side by side with an uneven skyline of crates on top: ledges to climb.</summary>
        private static void RackRow(Vector3 start, float yaw, int count, bool topCrates = false)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = start + q * new Vector3(i * 0.9f, 0f, 0f);
                bool knocked = Rand((int)p.x, (int)p.z, 640) < 0.18f;
                ServerRack(p, yaw, knocked ? 14f : 0f);
                if (topCrates && !knocked && Rand((int)p.x + 1, (int)p.z, 641) < 0.55f)
                    CrateStack(p + Vector3.up * 2.1f, 1 + (int)(Rand((int)p.x, (int)p.z + 1, 642) * 2f), yaw);
            }
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
            Part("Papers", Group("Decor"), pos + q * new Vector3(-0.5f, 0.78f, 0.6f), new Vector3(0.21f, 0.01f, 0.3f), M.Paper, 1f, false);
        }

        // ------------------------------------------------------------------ atrium

        /// <summary>
        /// The main Backrooms floor the hub looks down on: tall columns, hanging fluorescents, cubicles, server rack
        /// islands, fallen drywall and cross pipes. The drop zone in front of the airlock is kept clear.
        /// </summary>
        private static void BuildAtrium()
        {
            var a = MapLayout.AtriumRect;
            float ax = a.x * C, az = a.z * C;
            bool Clear(float x, float z) => x > ax + 27f && x < ax + 39f && z > az + 19f; // in front of the airlock

            // Tall columns on an 8 m grid, with pipes strung between them to swing on.
            for (int i = 1; i <= 4; i++)
                for (int j = 1; j <= 3; j++)
                {
                    float px = ax + i * 8f, pz = az + j * 8f;
                    if (Clear(px, pz)) continue;
                    Part("Column", Group("Concrete"), new Vector3(px, MapLayout.AtriumCeiling * 0.5f, pz), new Vector3(1.2f, MapLayout.AtriumCeiling, 1.2f), M.HubConcrete, 2f);
                    Part("ColumnBase", Group("Decor"), new Vector3(px, 0.2f, pz), new Vector3(1.4f, 0.4f, 1.4f), M.Hazard, 0.5f, false);
                    if (i < 4 && !Clear(px + 8f, pz)) Pipe(new Vector3(px + 0.6f, 4.3f + (j % 2) * 0.5f, pz), new Vector3(px + 7.4f, 4.3f + (j % 2) * 0.5f, pz), 0.09f);
                }

            // Hanging fluorescent fixtures high over the floor.
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 4; j++)
                {
                    float px = ax + 4f + i * 8f, pz = az + 4f + j * 8f;
                    float r = Rand(i, j, 1300);
                    var mode = r < 0.2f ? LightPanel.Mode.Dead : r < 0.5f ? LightPanel.Mode.Flicker : LightPanel.Mode.Steady;
                    Material mat = mode == LightPanel.Mode.Dead ? M.PanelDead : mode == LightPanel.Mode.Flicker ? M.PanelFlicker : M.PanelOn;
                    var panel = Part("HangingPanel", Group("Fixtures"), new Vector3(px, 9.4f, pz), new Vector3(2.4f, 0.08f, 0.8f), mat, 1f, false);
                    var lp = panel.AddComponent<LightPanel>();
                    lp.mode = mode;
                    lp.rend = panel.GetComponent<MeshRenderer>();
                    lp.emitColor = M.PanelEmit;
                    lp.seed = Rand(i, j, 1301) * 100f;
                    foreach (float cx in new[] { -1f, 1f })
                        Pipe(new Vector3(px + cx, 9.4f, pz), new Vector3(px + cx, MapLayout.AtriumCeiling, pz), 0.012f);
                }

            // The hanging panels are far above the floor, so a few real lights fill it in.
            foreach (var p in new[] { new Vector2(10f, 10f), new Vector2(10f, 24f), new Vector2(24f, 10f), new Vector2(26f, 26f) })
            {
                var go = new GameObject("AtriumLight");
                go.transform.SetParent(Group("Fixtures"), false);
                go.transform.position = new Vector3(ax + p.x, 6f, az + p.y);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 19f;
                l.intensity = 1.5f;
                l.color = new Color(1f, 0.9f, 0.6f);
                l.shadows = LightShadows.None;
            }

            // Office cubicles.
            Cubicle(new Vector3(ax + 4f, 0f, az + 4.5f), 0f);
            Cubicle(new Vector3(ax + 12f, 0f, az + 13f), 90f);
            Cubicle(new Vector3(ax + 20f, 0f, az + 4.5f), 0f);
            Cubicle(new Vector3(ax + 4f, 0f, az + 21f), 180f);

            // Server rack islands with crates on top: staircases of ledges.
            RackRow(new Vector3(ax + 14f, 0f, az + 20f), 0f, 5, true);
            RackRow(new Vector3(ax + 14f, 0f, az + 21.2f), 180f, 5, true);
            RackRow(new Vector3(ax + 26f, 0f, az + 10f), 90f, 4, true);
            RackRow(new Vector3(ax + 6f, 0f, az + 28.5f), 0f, 4, true);

            FallenDrywall(new Vector3(ax + 22f, 0f, az + 27f), 20f);
            FallenDrywall(new Vector3(ax + 9f, 0f, az + 16.5f), 200f);
            FallenDrywall(new Vector3(ax + 33f, 0f, az + 6f), 100f);
            CrateStack(new Vector3(ax + 18f, 0f, az + 9f), 3, 12f);
            CrateStack(new Vector3(ax + 19f, 0f, az + 10f), 2, 30f);
            CrateStack(new Vector3(ax + 30f, 0f, az + 14f), 2, 5f);
            Shelf(new Vector3(ax + 1.1f, 0f, az + 12f), 90f);
            Shelf(new Vector3(ax + 1.1f, 0f, az + 15f), 90f);
            Shelf(new Vector3(ax + 38.9f, 0f, az + 18f), 90f);
        }

        // ------------------------------------------------------------- CCTV (maze side)

        private struct CctvRig { public Camera cam; public Light light; public string title; }

        private static CctvRig MakeCctv(string title, Vector3 pos, Vector3 lookAt)
        {
            // The visible camera body lives in the (culled) world; the actual camera stays active.
            Vector3 dir = (lookAt - pos).normalized;
            var body = Part("CctvBody", Group("Decor"), pos, new Vector3(0.22f, 0.2f, 0.42f), M.Rack, 1f, false);
            body.transform.rotation = Quaternion.LookRotation(dir);
            var lens = Cylinder("CctvLens", pos + dir * 0.24f, new Vector3(0.1f, 0.03f, 0.1f), M.LedRed);
            lens.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);

            var go = new GameObject("CCTV " + title);
            go.transform.SetParent(Group("Zones"), false);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 72f;
            cam.nearClipPlane = 0.1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.34f, 0.29f, 0.13f);
            cam.enabled = false;
            var lightGo = new GameObject("AssistLight");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.4f, 0.6f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 13f;
            light.intensity = 1.6f;
            light.color = new Color(1f, 0.92f, 0.65f);
            light.shadows = LightShadows.None;
            light.enabled = false;
            return new CctvRig { cam = cam, light = light, title = title };
        }

        private static List<CctvRig> BuildMazeCctv()
        {
            var rigs = new List<CctvRig>();
            var a = MapLayout.AtriumRect;
            float ax = a.x * C, az = a.z * C;
            rigs.Add(MakeCctv("CAM 01  ATRIUM", new Vector3(ax + 20f, 10.2f, az + 1f), new Vector3(ax + 20f, 3f, az + 24f)));

            foreach (var r in MapLayout.Rooms)
            {
                if (r.kind == RoomKind.Pit && rigs.Count < 2)
                    rigs.Add(MakeCctv("CAM 02  THE PITFALLS", new Vector3(r.rect.x * C + 1f, 5.7f, r.rect.z * C + 1f), new Vector3(r.rect.x * C + 10f, 0f, r.rect.z * C + 10f)));
                else if (r.kind == RoomKind.Server && rigs.Count == 2)
                    rigs.Add(MakeCctv("CAM 03  RELAY STATION", new Vector3(r.rect.x * C + 1f, 2.75f, (r.rect.z + 1) * C + 1f), new Vector3(r.rect.x * C + 10f, 1.2f, (r.rect.z + 1) * C + 6f)));
            }

            // Three long corridors, far from the atrium.
            var candidates = new List<(Vector2Int cell, bool eastWest, float score)>();
            for (int z = 2; z < MapLayout.Height - 2; z++)
                for (int x = 2; x < MapLayout.Width - 2; x++)
                {
                    if (!StraightCell(x, z, true) && !StraightCell(x, z, false)) continue;
                    bool ew = StraightCell(x, z, true);
                    var centre = MapLayout.AtriumCentre;
                    float far = Mathf.Abs(x - centre.x) + Mathf.Abs(z - centre.y);
                    if (far < 12f) continue;
                    candidates.Add((new Vector2Int(x, z), ew, Rand(x, z, 1400)));
                }
            candidates.Sort((p, q) => q.score.CompareTo(p.score));
            string[] names = { "CAM 04  HALL A", "CAM 05  HALL B", "CAM 06  HALL C" };
            var taken = new List<Vector2Int>();
            foreach (var cd in candidates)
            {
                if (rigs.Count >= 6) break;
                bool close = false;
                foreach (var t in taken) if (Mathf.Abs(t.x - cd.cell.x) + Mathf.Abs(t.y - cd.cell.y) < 10) close = true;
                if (close) continue;
                taken.Add(cd.cell);
                Vector3 c = MapLayout.CellCenter(cd.cell.x, cd.cell.y);
                Vector3 dir = cd.eastWest ? Vector3.right : Vector3.forward;
                rigs.Add(MakeCctv(names[rigs.Count - 3], c - dir * 1.8f + Vector3.up * 2.9f, c + dir * 10f + Vector3.up * 1.2f));
            }
            return rigs;
        }

        // -------------------------------------------------- locked doors, fuse boxes

        private static void LockDoorsAndPlaceFuses()
        {
            // Lock a few doors that sit on loops, so locking them never cuts the map in two: they are shortcuts.
            var cells = new List<Vector2Int>();
            foreach (var md in _mazeDoors)
                if (MapLayout.RoomIdAt(md.cell.x, md.cell.y) < 0) cells.Add(md.cell);
            cells.Sort((p, q) => Rand(p.x, p.y, 1500).CompareTo(Rand(q.x, q.y, 1500)));
            var lockedCells = MapLayout.ChooseLockableDoors(cells, 6);
            var lockedDoors = new List<MazeDoor>();
            foreach (var md in _mazeDoors)
                if (lockedCells.Contains(md.cell))
                {
                    md.door.locked = true;
                    md.door.startOpen = false;
                    lockedDoors.Add(md);
                }

            // Fuse boxes in dead ends. The first ones are wired to locked doors; the rest restore the lights.
            var ends = new List<Vector2Int>(MapLayout.DeadEnds);
            ends.Sort((p, q) => Rand(p.x, p.y, 1510).CompareTo(Rand(q.x, q.y, 1510)));
            var unlockFuses = new List<(FuseBox fuse, Vector2Int cell, List<IndustrialDoor> doors)>();
            int made = 0;
            foreach (var cell in ends)
            {
                if (made >= 12) break;
                var kind = made < 4 ? FuseBox.Kind.UnlockDoors : FuseBox.Kind.RestoreLights;
                var fuse = MountFuseBox(cell, kind);
                if (fuse == null) continue;
                if (kind == FuseBox.Kind.UnlockDoors) unlockFuses.Add((fuse, cell, new List<IndustrialDoor>()));
                made++;
            }

            foreach (var md in lockedDoors)
            {
                int best = -1; float bestD = float.MaxValue;
                for (int i = 0; i < unlockFuses.Count; i++)
                {
                    float d = Mathf.Abs(unlockFuses[i].cell.x - md.cell.x) + Mathf.Abs(unlockFuses[i].cell.y - md.cell.y);
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best >= 0) unlockFuses[best].doors.Add(md.door);
            }
            foreach (var f in unlockFuses) f.fuse.doors = f.doors.ToArray();
        }

        private static FuseBox MountFuseBox(Vector2Int cell, FuseBox.Kind kind)
        {
            int x = cell.x, z = cell.y;
            int open = -1;
            for (int d = 0; d < 4; d++) if (!MapLayout.NeedsWall(x, z, d)) { open = d; break; }
            if (open < 0) return null;
            int back = (open + 2) % 4;
            Vector3 c = MapLayout.CellCenter(x, z);
            Vector3 n = new Vector3(MapLayout.DX[back], 0f, MapLayout.DZ[back]);       // towards the back wall
            Vector3 face = c + n * (C * 0.5f - T * 0.5f);
            Vector3 inward = -n;
            bool alongX = back == 0 || back == 2;
            float yaw = alongX ? 0f : 90f;
            Quaternion rot = Quaternion.LookRotation(inward);

            var root = new GameObject(kind == FuseBox.Kind.UnlockDoors ? "Fuse Box (unlock)" : "Fuse Box (lights)");
            root.transform.SetParent(Group("Decor"), false);
            root.transform.position = face + inward * 0.1f + Vector3.up * 1.4f;
            var fuse = root.AddComponent<FuseBox>();
            fuse.kind = kind;

            var body = Part("FuseBody", root.transform, root.transform.position, alongX ? new Vector3(0.6f, 0.8f, 0.18f) : new Vector3(0.18f, 0.8f, 0.6f), M.Rack, 1f);
            Part("FusePlate", root.transform, root.transform.position + inward * 0.095f, alongX ? new Vector3(0.52f, 0.7f, 0.02f) : new Vector3(0.02f, 0.7f, 0.52f), M.Metal, 1f, false);
            Part("FuseStripe", root.transform, root.transform.position + inward * 0.1f + Vector3.up * 0.3f, alongX ? new Vector3(0.52f, 0.08f, 0.012f) : new Vector3(0.012f, 0.08f, 0.52f), M.Hazard, 0.3f, false);
            var lampPart = Part("FuseLamp", root.transform, root.transform.position + inward * 0.1f + Vector3.up * 0.18f + (alongX ? Vector3.right : Vector3.forward) * 0.18f,
                new Vector3(0.07f, 0.07f, 0.03f), M.LedRed, 1f, false);
            fuse.lamp = lampPart.GetComponent<Renderer>();

            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.position = root.transform.position + inward * 0.1f - Vector3.up * 0.05f;
            Part("FuseLever", pivot.transform, pivot.transform.position + Vector3.up * 0.14f + inward * 0.03f,
                alongX ? new Vector3(0.07f, 0.3f, 0.07f) : new Vector3(0.07f, 0.3f, 0.07f), M.LedAmber, 1f);
            fuse.pivot = pivot.transform;
            // the lever swings towards/away from the viewer
            if (!alongX) pivot.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * pivot.transform.rotation;

            Label("EMERGENCY POWER", root.transform.position + inward * 0.115f + Vector3.up * 0.52f, Quaternion.LookRotation(n), 0.012f, new Color(1f, 0.8f, 0.2f), "Decor");

            _fuseCells.Add(cell);

            // Half of them are partly buried under fallen drywall.
            if (Rand(x, z, 1520) < 0.5f) FallenDrywall(c + inward * 0.0f + n * 1.0f, yaw);
            return fuse;
        }
    }
}
