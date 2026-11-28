using System.Collections.Generic;
using Monk.Audio;
using Monk.Cosmetics;
using Monk.Gameplay;
using Monk.Hub;
using Monk.Locomotion;
using Monk.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Monk.EditorTools
{
    /// <summary>
    /// The Async-style facility hub: an elevated concrete complex over the atrium with an observation window, an
    /// airlock onto a catwalk, a cosmetics wing (mirror, mannequins, holograms, terminal), the gamemode console and the
    /// CCTV wall. Local hub coordinates: x east 0..40, z north 0..24, y up from the hub floor.
    /// </summary>
    public static partial class MapBuilder
    {
        private const float HubW = 40f, HubD = 24f, HubH = 4.8f, HubY = MapLayout.HubFloorY;

        private static Vector3 _hubO;
        private static CctvSystem _cctvSystem;
        private static Font _font;
        private static readonly List<Transform> _spawnPads = new List<Transform>();
        private static readonly List<CosmeticButton> _cosButtons = new List<CosmeticButton>();
        private static WardrobePanel _wardrobe;

        /// <summary>Hub-local position (y measured from the hub floor).</summary>
        private static Vector3 HP(float x, float y, float z) => _hubO + new Vector3(x, y, z);
        /// <summary>Hub-local x/z with an absolute world y.</summary>
        private static Vector3 HW(float x, float worldY, float z) => new Vector3(_hubO.x + x, worldY, _hubO.z + z);

        private static readonly Quaternion ReadFromSouth = Quaternion.identity;                  // text on a north-facing wall
        private static readonly Quaternion ReadFromNorth = Quaternion.Euler(0f, 180f, 0f);        // text on a south wall
        private static readonly Quaternion ReadFromEast = Quaternion.LookRotation(Vector3.left);  // text on a west wall
        private static readonly Quaternion ReadFromWest = Quaternion.LookRotation(Vector3.right); // text on an east wall
        private static readonly Quaternion LyingFacingSouth = Quaternion.Euler(90f, 0f, 0f);     // flat, top of the text points north

        private static TextMesh Label(string text, Vector3 pos, Quaternion rot, float charSize, Color color, string group,
            TextAnchor anchor = TextAnchor.MiddleCenter, int fontSize = 64)
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Label");
            go.transform.SetParent(Group(group), false);
            go.transform.SetPositionAndRotation(pos, rot);
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.text = text;
            tm.characterSize = charSize;
            tm.fontSize = fontSize;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleCenter ? TextAlignment.Center : TextAlignment.Left;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
            return tm;
        }

        // ---------------------------------------------------------------- the hub

        private static void BuildHub(List<CctvRig> cams)
        {
            var hub = MapLayout.HubRect;
            _hubO = new Vector3(hub.x * C, HubY, hub.z * C);
            _spawnPads.Clear();
            _cosButtons.Clear();

            BuildHubShell();
            BuildHubFacade();
            BuildAirlockAndCatwalk();
            BuildScaffold();
            BuildWardrobeWing();
            BuildTerminal();
            var console = BuildGameModeConsole();
            BuildCctvWall(cams);
            BuildSpawnPads();
            BuildHubLighting(console);
        }

        private static void BuildHubShell()
        {
            Transform conc = Group("Hub:Concrete");
            Part("HubFloor", conc, HP(20f, -0.15f, 12f), new Vector3(HubW, 0.3f, HubD), M.HubFloor, 2f);
            Part("HubCeiling", conc, HP(20f, HubH + 0.15f, 12f), new Vector3(HubW + 0.6f, 0.3f, HubD + 0.6f), M.HubConcrete, 2f);
            Part("HubWallN", conc, HP(20f, HubH * 0.5f, HubD + 0.15f), new Vector3(HubW + 0.6f, HubH, 0.3f), M.HubConcrete, 2f);
            Part("HubWallW", conc, HP(-0.15f, HubH * 0.5f, 12f), new Vector3(0.3f, HubH, HubD + 0.6f), M.HubConcrete, 2f);
            Part("HubWallE", conc, HP(HubW + 0.15f, HubH * 0.5f, 12f), new Vector3(0.3f, HubH, HubD + 0.6f), M.HubConcrete, 2f);

            // Metal floor grates and plate, wing flooring.
            Part("GrateRunway", Group("Hub:Floor"), HP(25.5f, 0.02f, 4.5f), new Vector3(25f, 0.04f, 7f), M.HubGrate, 1.5f);
            Part("PlateFloor", Group("Hub:Floor"), HP(26f, 0.02f, 15f), new Vector3(28f, 0.04f, 14f), M.HubPlate, 1.5f);
            Part("WingFloor", Group("Hub:Floor"), HP(6f, 0.02f, 12f), new Vector3(12f, 0.04f, 24f), M.Linoleum, 2f);
            foreach (float z in new[] { 8.2f })
                Part("FloorHazard", Group("Hub:Decor"), HP(25.5f, 0.045f, z), new Vector3(25f, 0.01f, 0.3f), M.Hazard, 0.5f, false);

            // Hazard bands along the base of every wall.
            Part("BandN", Group("Hub:Decor"), HP(20f, 0.25f, HubD - 0.04f), new Vector3(HubW - 0.4f, 0.5f, 0.04f), M.Hazard, 0.5f, false);
            Part("BandS", Group("Hub:Decor"), HP(20f, 0.25f, 0.2f), new Vector3(HubW - 0.4f, 0.5f, 0.04f), M.Hazard, 0.5f, false);
            Part("BandW", Group("Hub:Decor"), HP(0.02f, 0.25f, 12f), new Vector3(0.04f, 0.5f, HubD - 0.4f), M.Hazard, 0.5f, false);
            Part("BandE", Group("Hub:Decor"), HP(HubW - 0.02f, 0.25f, 12f), new Vector3(0.04f, 0.5f, HubD - 0.4f), M.Hazard, 0.5f, false);

            // Exposed conduits, cable trays and sagging wire bundles.
            Transform metal = Group("Hub:Metal");
            Pipe(HP(1f, 3.0f, HubD - 0.4f), HP(HubW - 1f, 3.0f, HubD - 0.4f), 0.05f);
            Pipe(HP(1f, 3.25f, HubD - 0.4f), HP(HubW - 1f, 3.25f, HubD - 0.4f), 0.035f);
            Pipe(HP(HubW - 0.5f, 3.4f, 1f), HP(HubW - 0.5f, 3.4f, HubD - 1f), 0.08f);
            Pipe(HP(0.5f, 3.6f, 1f), HP(0.5f, 3.6f, HubD - 1f), 0.06f);
            Part("CableTray", metal, HP(26f, HubH - 0.35f, 12f), new Vector3(24f, 0.08f, 0.6f), M.Metal, 1f);
            Part("CableTray", metal, HP(26f, HubH - 0.35f, 5f), new Vector3(24f, 0.08f, 0.6f), M.Metal, 1f);
            for (int i = 0; i < 9; i++)
            {
                float x = 3f + i * 4.4f;
                Part("JunctionBox", Group("Hub:Decor"), HP(x, 2.5f, HubD - 0.3f), new Vector3(0.3f, 0.4f, 0.15f), M.Rack, 1f, false);
                Pipe(HP(x, 2.5f, HubD - 0.35f), HP(x, 3.0f, HubD - 0.35f), 0.03f);
                Wire(HP(x, HubH - 0.4f, 9f + (i % 3) * 3f), HP(x + 3.6f, HubH - 0.4f, 9f + (i % 3) * 3f), 0.45f);
            }
            Label("ASYNC RESEARCH FACILITY", HP(HubW - 0.1f, 3.2f, 12f), ReadFromWest, 0.09f, new Color(0.95f, 0.78f, 0.1f), "Hub:Decor");
            Label("SECTOR 0  -  LEVEL 0 ACCESS", HP(HubW - 0.1f, 2.7f, 12f), ReadFromWest, 0.045f, new Color(0.75f, 0.75f, 0.7f), "Hub:Decor");
        }

        /// <summary>A hanging cable: a few cylinders along a parabola.</summary>
        private static void Wire(Vector3 a, Vector3 b, float sag)
        {
            const int n = 6;
            Vector3 prev = a;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
                Pipe(prev, p, 0.02f);
                prev = p;
            }
        }

        // ------------------------------------------------------- observation window

        private static void BuildHubFacade()
        {
            var atrium = MapLayout.AtriumRect;
            float planeZ = MapLayout.HubRect.z * C;            // the facade plane
            float x0 = (atrium.x + 4) * C, x1 = (atrium.x + 8) * C;
            float yMid = HubY + 2.4f;
            Transform glass = Group("Hub:Glass"), metal = Group("Hub:Metal");

            // Wire-reinforced panes, one per cell, in a heavy steel frame.
            for (int i = 4; i <= 7; i++)
                Part("ObservationGlass", glass, new Vector3((atrium.x + i + 0.5f) * C, yMid, planeZ), new Vector3(3.8f, 3.0f, 0.06f), M.ReinforcedGlass, 1.5f);
            for (int i = 4; i <= 8; i++)
                Part("Mullion", metal, new Vector3((atrium.x + i) * C, yMid, planeZ), new Vector3(0.4f, 3.2f, 0.5f), M.Metal, 1f);
            Part("Transom", metal, new Vector3((x0 + x1) * 0.5f, yMid, planeZ), new Vector3(x1 - x0, 0.14f, 0.5f), M.Metal, 1f);
            Part("SillFrame", metal, new Vector3((x0 + x1) * 0.5f, HubY + 0.85f, planeZ), new Vector3(x1 - x0, 0.2f, 0.6f), M.Metal, 1f);
            Part("HeadFrame", metal, new Vector3((x0 + x1) * 0.5f, HubY + 3.95f, planeZ), new Vector3(x1 - x0, 0.2f, 0.6f), M.Metal, 1f);

            // A waist-high rail and warning labels on the inside.
            Part("WindowRail", metal, new Vector3((x0 + x1) * 0.5f, HubY + 1.05f, planeZ + 0.7f), new Vector3(x1 - x0 - 0.4f, 0.06f, 0.06f), M.Metal, 1f);
            for (int i = 0; i <= 4; i++)
                Part("RailPost", metal, new Vector3(x0 + 0.3f + i * ((x1 - x0 - 0.6f) / 4f), HubY + 0.5f, planeZ + 0.7f), new Vector3(0.06f, 1.0f, 0.06f), M.Metal, 1f);
            Label("OBSERVATION DECK", new Vector3((x0 + x1) * 0.5f, HubY + 4.3f, planeZ + 0.2f), ReadFromNorth, 0.07f, new Color(0.95f, 0.78f, 0.1f), "Hub:Decor");
        }

        // -------------------------------------------------------- airlock + catwalk

        private static void BuildAirlockAndCatwalk()
        {
            float planeZ = MapLayout.HubRect.z * C;
            Transform metal = Group("Hub:Metal");

            // Door: slides sideways into a housing in the hub's south-east corner.
            var root = new GameObject("Airlock Door");
            root.transform.SetParent(Group("Hub:Doors"), false);
            root.transform.position = HP(34f, 0f, 0.45f);
            var panel = Part("AirlockPanel", root.transform, HP(34f, 1.8f, 0.45f), new Vector3(3.6f, 3.6f, 0.5f), M.HubPlate, 1.5f);
            Part("AirlockStripe", panel.transform, HP(34f, 0.35f, 0.45f), new Vector3(3.6f, 0.6f, 0.52f), M.Hazard, 0.5f, false);
            Part("AirlockStripe", panel.transform, HP(34f, 3.25f, 0.45f), new Vector3(3.6f, 0.5f, 0.52f), M.Hazard, 0.5f, false);
            panel.AddComponent<Rigidbody>().isKinematic = true;
            var psp = panel.AddComponent<SurfaceProperties>();
            psp.kind = SurfaceKind.Metal;
            psp.slipPercentage = 0.1f;
            Part("AirlockHousing", metal, HP(37.7f, 1.9f, 0.45f), new Vector3(4.6f, 3.8f, 0.7f), M.Metal, 1f);

            var lamps = new List<Renderer>();
            foreach (float zz in new[] { 0.15f, -0.15f })
                foreach (float xx in new[] { 33.0f, 35.0f })
                    lamps.Add(Part("AirlockLamp", Group("Hub:Decor"), HP(xx, 3.75f, zz), new Vector3(0.3f, 0.15f, 0.08f), M.LampRed, 1f, false).GetComponent<Renderer>());

            var door = root.AddComponent<IndustrialDoor>();
            door.panel = panel.transform;
            door.panelCollider = panel.GetComponent<Collider>();
            door.warningLamps = lamps.ToArray();
            door.sparkMaterial = M.Spark;
            door.style = DoorStyle.SlideSideways;
            door.openOffset = new Vector3(4f, 0f, 0f);
            door.startOpen = false;
            door.hydraulic = true;
            door.crushDamage = 0f;          // the hub is safe
            door.warningSeconds = 1.2f;
            door.openSeconds = 2.4f;
            door.closeSeconds = 1.6f;

            // Glowing push-buttons on pedestals, inside and out.
            MakeAirlockButton(door, HP(31.0f, 0f, 2.6f), "AIRLOCK", true);
            MakeAirlockButton(door, HP(32.7f, 0f, -1.6f), "OPEN", false);
            Part("VestibuleStripe", Group("Hub:Decor"), HP(34f, 0.045f, 1.6f), new Vector3(3.6f, 0.01f, 2.0f), M.Hazard, 0.5f, false);
            foreach (float xx in new[] { 32.2f, 35.8f })
                Part("DoorFrameStripe", Group("Hub:Decor"), HP(xx, 1.7f, 0.17f), new Vector3(0.15f, 3.4f, 0.03f), M.Hazard, 0.5f, false);
            Label("AIRLOCK  -  CATWALK ACCESS", HP(34f, 4.15f, 0.18f), ReadFromNorth, 0.045f, new Color(0.95f, 0.78f, 0.1f), "Hub:Decor");

            // The catwalk: grated deck out over the atrium, railed except for a gate gap on the west side and the open end.
            float deckY = HubY - 0.1f;
            Part("Catwalk", Group("Hub:Floor"), new Vector3(_hubO.x + 34f, deckY, planeZ - 5.4f), new Vector3(4f, 0.2f, 10.5f), M.HubGrate, 1.5f);
            Part("CatwalkFrame", metal, new Vector3(_hubO.x + 34f, deckY - 0.2f, planeZ - 5.4f), new Vector3(4.1f, 0.2f, 10.6f), M.Metal, 1f, false);
            foreach (float side in new[] { 32.1f, 35.9f })
            {
                for (float z = -0.6f; z >= -10.2f; z -= 2f)
                    Part("RailPost", metal, new Vector3(_hubO.x + side, HubY + 0.55f, planeZ + z), new Vector3(0.07f, 1.1f, 0.07f), M.Metal, 1f);
                bool west = side < 34f;
                if (west)
                {
                    Part("RailTop", metal, new Vector3(_hubO.x + side, HubY + 1.1f, planeZ - 0.9f), new Vector3(0.06f, 0.06f, 1.5f), M.Metal, 1f);
                    Part("RailTop", metal, new Vector3(_hubO.x + side, HubY + 1.1f, planeZ - 7.0f), new Vector3(0.06f, 0.06f, 7.2f), M.Metal, 1f);
                    Part("RailMid", metal, new Vector3(_hubO.x + side, HubY + 0.55f, planeZ - 7.0f), new Vector3(0.05f, 0.05f, 7.2f), M.Metal, 1f);
                }
                else
                {
                    Part("RailTop", metal, new Vector3(_hubO.x + side, HubY + 1.1f, planeZ - 5.4f), new Vector3(0.06f, 0.06f, 10.4f), M.Metal, 1f);
                    Part("RailMid", metal, new Vector3(_hubO.x + side, HubY + 0.55f, planeZ - 5.4f), new Vector3(0.05f, 0.05f, 10.4f), M.Metal, 1f);
                }
            }
            // Hazard-striped drop edge with a gap in the middle to step through.
            foreach (float sx in new[] { 32.6f, 35.4f })
                Part("DropEdge", Group("Hub:Decor"), new Vector3(_hubO.x + sx, HubY + 0.03f, planeZ - 10.3f), new Vector3(1.0f, 0.02f, 0.4f), M.Hazard, 0.5f, false);
            Part("DropBar", metal, new Vector3(_hubO.x + 32.5f, HubY + 1.0f, planeZ - 10.4f), new Vector3(0.8f, 0.1f, 0.1f), M.Hazard, 0.3f, false);
            Part("DropBar", metal, new Vector3(_hubO.x + 35.5f, HubY + 1.0f, planeZ - 10.4f), new Vector3(0.8f, 0.1f, 0.1f), M.Hazard, 0.3f, false);
            Label("DROP ZONE", new Vector3(_hubO.x + 34f, HubY + 1.7f, planeZ - 10.4f), ReadFromWest * Quaternion.Euler(0f, 90f, 0f), 0.06f, new Color(1f, 0.3f, 0.1f), "Hub:Decor");

            // Supports down to the atrium floor.
            foreach (float z in new[] { -2f, -6f, -10f })
                foreach (float sx in new[] { 32.2f, 35.8f })
                    Part("CatwalkPillar", metal, new Vector3(_hubO.x + sx, HubY * 0.5f - 0.1f, planeZ + z), new Vector3(0.35f, HubY - 0.2f, 0.35f), M.Metal, 1f);
            for (int i = 0; i < 3; i++)
                Part("CatwalkBrace", metal, new Vector3(_hubO.x + 34f, HubY - 1.6f - i * 1.8f, planeZ - 6f), new Vector3(3.5f, 0.15f, 0.15f), M.Metal, 1f, false);
        }

        private static void MakeAirlockButton(IndustrialDoor door, Vector3 baseAt, string label, bool inside)
        {
            Transform metal = Group("Hub:Metal");
            Part("ButtonPost", metal, baseAt + Vector3.up * 0.55f, new Vector3(0.24f, 1.1f, 0.24f), M.HubPlate, 1f);
            Part("ButtonHead", metal, baseAt + Vector3.up * 1.12f, new Vector3(0.34f, 0.08f, 0.34f), M.Metal, 1f, false);
            var cap = Part("AirlockButton", Group("Hub:Decor"), baseAt + Vector3.up * 1.2f, new Vector3(0.24f, 0.1f, 0.24f), M.LedGreen, 1f);
            var b = cap.AddComponent<DoorButton>();
            b.door = door;
            b.cap = cap.transform;
            b.glow = cap.GetComponent<Renderer>();
            b.pressDirection = Vector3.down;
            Label(label, baseAt + Vector3.up * 1.5f, inside ? ReadFromNorth : ReadFromNorth, 0.03f, Color.white, "Hub:Decor");
        }

        // ------------------------------------------------------------ scaffold tower

        /// <summary>Platforms spiralling up from the atrium floor to the catwalk: the way back up after dropping in.</summary>
        private static void BuildScaffold()
        {
            float planeZ = MapLayout.HubRect.z * C;
            Transform metal = Group("Hub:Metal");
            (float x, float z, float top)[] steps = { (25.5f, -4.2f, 1.8f), (27.7f, -4.8f, 3.4f), (29.9f, -3.6f, 5.0f), (31.1f, -1.6f, 6.2f) };
            foreach (var st in steps)
            {
                Vector3 c = new Vector3(_hubO.x + st.x, st.top - 0.075f, planeZ + st.z);
                Part("ScaffoldDeck", Group("Hub:Floor"), c, new Vector3(2f, 0.15f, 2f), M.HubGrate, 1.5f);
                foreach (float dx in new[] { -0.9f, 0.9f })
                    foreach (float dz in new[] { -0.9f, 0.9f })
                        Part("ScaffoldPost", metal, new Vector3(c.x + dx, st.top * 0.5f, c.z + dz), new Vector3(0.1f, st.top, 0.1f), M.Metal, 1f);
                Part("ScaffoldHazard", Group("Hub:Decor"), new Vector3(c.x, st.top + 0.005f, c.z - 0.95f), new Vector3(2f, 0.01f, 0.1f), M.Hazard, 0.3f, false);
            }
            // Crates to get onto the first platform.
            CrateStack(new Vector3(_hubO.x + 23.6f, 0f, planeZ - 3.4f), 1, 8f);
            CrateStack(new Vector3(_hubO.x + 23.8f, 0f, planeZ - 5.6f), 2, 20f);
            Label("ROUTE TO HUB", new Vector3(_hubO.x + 24.5f, 2.9f, planeZ - 0.4f), ReadFromNorth * Quaternion.Euler(0f, 180f, 0f), 0.05f, new Color(0.95f, 0.78f, 0.1f), "Hub:Decor");
        }

        // ---------------------------------------------------------- wardrobe wing

        private static void BuildWardrobeWing()
        {
            Transform conc = Group("Hub:Concrete");
            // Partition between the wing and the main hall, with a wide doorway.
            Part("WingWall", conc, HP(12f, HubH * 0.5f, 4.5f), new Vector3(0.3f, HubH, 9f), M.HubConcrete, 2f);
            Part("WingWall", conc, HP(12f, HubH * 0.5f, 18.5f), new Vector3(0.3f, HubH, 11f), M.HubConcrete, 2f);
            Part("WingLintel", conc, HP(12f, (2.8f + HubH) * 0.5f, 11f), new Vector3(0.3f, HubH - 2.8f, 4f), M.HubConcrete, 2f);
            foreach (float z in new[] { 9f, 13f })
                Part("DoorwayFrame", Group("Hub:Decor"), HP(11.84f, 1.4f, z), new Vector3(0.04f, 2.8f, 0.2f), M.Hazard, 0.5f, false);
            Label("COSMETICS  -  WARDROBE", HP(11.8f, 3.4f, 11f), ReadFromEast, 0.05f, new Color(0.3f, 0.9f, 1f), "Hub:Decor");

            // Lockers along the west wall, a few ajar.
            for (int i = 0; i < 13; i++)
            {
                float z = 5.6f + i * 1.3f;
                var locker = Part("Locker", Group("Hub:Metal"), HP(0.5f, 1f, z), new Vector3(0.5f, 2f, 0.62f), M.Rack, 1f);
                Part("LockerVent", Group("Hub:Decor"), HP(0.76f, 1.7f, z), new Vector3(0.02f, 0.25f, 0.4f), M.Metal, 1f, false);
                if (i % 4 == 2)
                {
                    var ajar = Part("LockerDoor", Group("Hub:Metal"), HP(0.9f, 1f, z - 0.2f), new Vector3(0.04f, 1.9f, 0.55f), M.Metal, 1f);
                    ajar.transform.RotateAround(HP(0.78f, 1f, z - 0.3f), Vector3.up, 60f);
                }
            }
            Label("WARDROBE", HP(0.1f, 3.4f, 14f), ReadFromEast, 0.08f, new Color(0.95f, 0.78f, 0.1f), "Hub:Decor");

            BuildMirror();
            BuildWardrobeBoard();
            BuildMannequins();
            BuildHolograms();
        }

        private static void BuildMirror()
        {
            Vector3 c = HP(3.5f, 1.5f, HubD - 0.16f);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "Mirror";
            quad.transform.SetParent(Group("Hub:Decor"), false);
            quad.transform.SetPositionAndRotation(c, Quaternion.identity);   // a Quad faces -Z: into the room
            quad.transform.localScale = new Vector3(3.6f, 2.4f, 1f);
            var mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = M.Mirror;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mirror = quad.AddComponent<MirrorSurface>();
            mirror.surface = mr;
            mirror.mirrorMaterial = M.Mirror;

            Transform metal = Group("Hub:Metal");
            float z = HubD - 0.1f;
            Part("MirrorFrame", metal, new Vector3(c.x, c.y + 1.25f, z), new Vector3(3.8f, 0.16f, 0.12f), M.Metal, 1f);
            Part("MirrorFrame", metal, new Vector3(c.x, c.y - 1.25f, z), new Vector3(3.8f, 0.16f, 0.12f), M.Metal, 1f);
            Part("MirrorFrame", metal, new Vector3(c.x - 1.9f, c.y, z), new Vector3(0.16f, 2.6f, 0.12f), M.Metal, 1f);
            Part("MirrorFrame", metal, new Vector3(c.x + 1.9f, c.y, z), new Vector3(0.16f, 2.6f, 0.12f), M.Metal, 1f);
            Label("STEP UP TO THE MIRROR", new Vector3(c.x, c.y + 1.5f, z - 0.05f), ReadFromSouth, 0.03f, new Color(0.7f, 0.85f, 0.9f), "Hub:Decor");
        }

        /// <summary>The wardrobe board: prev/next buttons for hat, visor and badge, and suit colour swatches.</summary>
        private static void BuildWardrobeBoard()
        {
            float bx = 9.0f, z = HubD - 0.18f;
            Part("WardrobeBoard", Group("Hub:Metal"), HP(bx, 1.7f, z), new Vector3(5.2f, 2.2f, 0.12f), M.Rack, 1f, false);
            Part("WardrobeBoardTrim", Group("Hub:Decor"), HP(bx, 2.78f, z - 0.065f), new Vector3(5.2f, 0.06f, 0.02f), M.Hazard, 0.3f, false);

            string[] names = { "HAT", "VISOR", "BADGE", "SUIT" };
            float[] ys = { 2.35f, 1.9f, 1.45f, 1.0f };
            var panelObj = new GameObject("Wardrobe Panel");
            panelObj.transform.SetParent(Group("Hub:Decor"), false);
            _wardrobe = panelObj.AddComponent<WardrobePanel>();
            _wardrobe.valueTexts = new TextMesh[4];

            for (int row = 0; row < 4; row++)
            {
                float y = ys[row];
                Label(names[row], HP(bx - 2.3f, y, z - 0.08f), ReadFromSouth, 0.04f, new Color(0.95f, 0.78f, 0.1f), "Hub:Decor", TextAnchor.MiddleLeft);
                _wardrobe.valueTexts[row] = Label("-", HP(bx + 0.55f, y, z - 0.08f), ReadFromSouth, 0.035f, new Color(0.3f, 1f, 0.9f), "Hub:Decor");
                MakeCosmeticButton(HP(bx - 0.75f, y, z - 0.07f), (CosmeticSlot)row, -1, false, 0, M.LedAmber, "<");
                MakeCosmeticButton(HP(bx + 1.85f, y, z - 0.07f), (CosmeticSlot)row, 1, false, 0, M.LedAmber, ">");
            }
            // Suit colour swatches.
            for (int i = 0; i < CosmeticModels.SuitColors.Length; i++)
            {
                Material swatch = new Material(M.Plastic) { color = CosmeticModels.SuitColors[i] };
                MakeCosmeticButton(HP(bx - 1.95f + i * 0.65f, 0.55f, z - 0.07f), CosmeticSlot.Suit, 1, true, i, swatch, null);
            }
        }

        private static void MakeCosmeticButton(Vector3 pos, CosmeticSlot slot, int step, bool direct, int value, Material mat, string glyph)
        {
            var cap = Part("CosmeticButton", Group("Hub:Decor"), pos, new Vector3(0.26f, 0.26f, 0.08f), mat, 1f);
            var b = cap.AddComponent<CosmeticButton>();
            b.slot = slot;
            b.step = step;
            b.direct = direct;
            b.value = value;
            b.cap = cap.transform;
            b.pressDirection = Vector3.forward;
            _cosButtons.Add(b);
            if (glyph != null)
                Label(glyph, pos + Vector3.back * 0.05f, ReadFromSouth, 0.045f, Color.black, "Hub:Decor");
        }

        private static void BuildMannequins()
        {
            int[] hats = { 1, 2, 3 }, visors = { 2, 4, 3 }, badges = { 1, 2, 4 }, suits = { 1, 3, 5 };
            for (int i = 0; i < 3; i++)
            {
                float z = 15.6f + i * 2.4f;
                Part("MannequinPodium", Group("Hub:Concrete"), HP(10.2f, 0.07f, z), new Vector3(1.2f, 0.14f, 1.2f), M.HubConcrete, 1f);
                var mannequinRoot = new GameObject("Mannequin " + (i + 1));
                mannequinRoot.transform.SetParent(Group("Hub:Props"), false);
                mannequinRoot.transform.SetPositionAndRotation(HP(10.2f, 0.14f, z), Quaternion.LookRotation(Vector3.left));
                var head = CosmeticModels.Mannequin(mannequinRoot.transform, M.Accessory, CosmeticModels.SuitColors[suits[i]], 0);
                var hat = CosmeticModels.Hat(hats[i], head, M.Accessory, 0);
                if (hat != null) hat.transform.localPosition = new Vector3(0f, 0.11f, 0f);
                CosmeticModels.Visor(visors[i], head, M.Visors, 0);
                var badgeParent = new GameObject("BadgeMount").transform;
                badgeParent.SetParent(mannequinRoot.transform, false);
                badgeParent.localPosition = new Vector3(0.09f, 1.37f, 0.11f);
                CosmeticModels.Badge(badges[i], badgeParent, M.Accessory, 0);
            }
        }

        private static void BuildHolograms()
        {
            int[] hats = { 1, 2, 3, 4 };
            string[] names = { "HARD HAT", "TOP HAT", "TRAFFIC CONE", "ANTENNA" };
            for (int i = 0; i < 4; i++)
            {
                float x = 2.2f + i * 2.4f, z = 7.2f;
                Part("HoloPedestal", Group("Hub:Concrete"), HP(x, 0.3f, z), new Vector3(0.7f, 0.6f, 0.7f), M.HubConcrete, 1f);
                Part("HoloEmitter", Group("Hub:Decor"), HP(x, 0.62f, z), new Vector3(0.62f, 0.04f, 0.62f), M.LedCyan, 1f, false);

                var holo = new GameObject("Hologram " + names[i]);
                holo.transform.SetParent(Group("Hub:Decor"), false);
                holo.transform.position = HP(x, 1.0f, z);
                var model = new GameObject("Model").transform;
                model.SetParent(holo.transform, false);
                model.localScale = Vector3.one * 2.4f;
                var hat = CosmeticModels.Hat(hats[i], model, M.Hologram, 0);
                foreach (var r in hat.GetComponentsInChildren<Renderer>()) r.sharedMaterial = M.Hologram;
                holo.AddComponent<CosmeticHologram>().model = model;
                Label(names[i], HP(x, 1.75f, z), ReadFromNorth * Quaternion.Euler(0f, 180f, 0f), 0.025f, new Color(0.3f, 0.95f, 1f), "Hub:Decor");
            }
        }

        // ------------------------------------------------------------------ terminal

        private static void BuildTerminal()
        {
            // A retro CRT terminal against the south wall of the wing, with a big physical keyboard.
            Transform plastic = Group("Hub:Props");
            Part("TerminalDesk", plastic, HP(6.5f, 0.45f, 1.0f), new Vector3(4.2f, 0.9f, 1.4f), M.Plastic, 1f);
            Part("MonitorBody", plastic, HP(6.5f, 1.55f, 0.95f), new Vector3(1.6f, 1.2f, 0.95f), M.Plastic, 1f);
            Part("MonitorNeck", plastic, HP(6.5f, 1.0f, 0.95f), new Vector3(0.7f, 0.2f, 0.7f), M.Plastic, 1f, false);

            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(screen.GetComponent<Collider>());
            screen.name = "TerminalScreen";
            screen.transform.SetParent(Group("Hub:Decor"), false);
            screen.transform.SetPositionAndRotation(HP(6.5f, 1.58f, 1.435f), Quaternion.Euler(0f, 180f, 0f)); // faces +Z, towards the player
            screen.transform.localScale = new Vector3(1.35f, 0.95f, 1f);
            screen.GetComponent<MeshRenderer>().sharedMaterial = M.Crt;

            var text = Label("", HP(6.5f, 1.58f, 1.425f), ReadFromNorth, 0.0145f, new Color(0.35f, 1f, 0.45f), "Hub:Decor", TextAnchor.MiddleCenter, 48);
            text.alignment = TextAlignment.Left;
            text.anchor = TextAnchor.MiddleCenter;
            var term = text.gameObject.AddComponent<TerminalScreen>();
            term.text = text;
            term.mapSeed = MapLayout.Seed;

            // Keyboard: rows of real, tappable keys.
            Part("KeyboardBase", plastic, HP(6.5f, 0.93f, 2.2f), new Vector3(1.85f, 0.06f, 1.0f), M.Plastic, 1f);
            string[] rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
            const float pitch = 0.15f;
            for (int r = 0; r < rows.Length; r++)
            {
                float zRow = 1.82f + r * pitch;
                float xStart = 6.5f - (rows[r].Length - 1) * pitch * 0.5f;
                for (int k = 0; k < rows[r].Length; k++)
                    MakeKey(term, HP(xStart + k * pitch, 0.985f, zRow), rows[r][k].ToString(), rows[r][k], TerminalKey.Special.None, 0.12f);
            }
            float zBottom = 1.82f + rows.Length * pitch;
            MakeKey(term, HP(5.55f, 0.985f, zBottom), "BKSP", ' ', TerminalKey.Special.Backspace, 0.28f);
            MakeKey(term, HP(6.5f, 0.985f, zBottom), "SPACE", ' ', TerminalKey.Special.Space, 0.62f);
            MakeKey(term, HP(7.45f, 0.985f, zBottom), "ENTER", ' ', TerminalKey.Special.Enter, 0.28f);
            MakeKey(term, HP(8.25f, 0.985f, zBottom), "STATS", ' ', TerminalKey.Special.Stats, 0.28f);

            Label("OPERATOR TERMINAL", HP(6.5f, 2.55f, 0.3f), ReadFromNorth, 0.04f, new Color(0.95f, 0.78f, 0.1f), "Hub:Decor");
            Part("TerminalLamp", Group("Hub:Decor"), HP(5.55f, 2.2f, 1.43f), new Vector3(0.06f, 0.06f, 0.03f), M.LedGreen, 1f, false);
        }

        private static void MakeKey(TerminalScreen screen, Vector3 pos, string label, char ch, TerminalKey.Special special, float width)
        {
            var key = Part("Key " + label, Group("Hub:Decor"), pos, new Vector3(width, 0.05f, 0.12f), M.KeyCap, 1f);
            var k = key.AddComponent<TerminalKey>();
            k.screen = screen;
            k.character = ch;
            k.special = special;
            k.cap = key.transform;
            Label(label, pos + Vector3.up * 0.03f, Quaternion.Euler(90f, 180f, 0f), label.Length > 1 ? 0.016f : 0.03f, new Color(0.1f, 0.1f, 0.1f), "Hub:Decor");
        }

        // ------------------------------------------------------- gamemode console

        private static GameModeConsole BuildGameModeConsole()
        {
            const float cx = 26f, cz = 14f;
            var rootGo = new GameObject("Gamemode Console");
            rootGo.transform.SetParent(Group("Hub:Props"), false);
            rootGo.transform.position = HP(cx, 0f, cz);
            var console = rootGo.AddComponent<GameModeConsole>();

            Part("ConsoleBase", Group("Hub:Props"), HP(cx, 0.5f, cz), new Vector3(9.4f, 1.0f, 1.6f), M.HubPlate, 1.5f);
            Part("ConsoleTop", Group("Hub:Props"), HP(cx, 1.03f, cz - 0.1f), new Vector3(9.6f, 0.08f, 1.5f), M.Plastic, 1f, false);
            Part("ConsoleBack", Group("Hub:Props"), HP(cx, 2.1f, cz + 0.75f), new Vector3(9.4f, 2.2f, 0.2f), M.Rack, 1f);
            Part("ConsoleTrim", Group("Hub:Decor"), HP(cx, 1.05f, cz - 0.86f), new Vector3(9.6f, 0.06f, 0.04f), M.Hazard, 0.4f, false);

            // Three glowing status screens.
            Color cyan = new Color(0.3f, 1f, 0.95f);
            console.titleText = ScreenPanel(HP(cx, 2.55f, cz + 0.64f), 3.4f, 1.0f, "STANDARD ESCAPE", 0.06f, cyan);
            console.descriptionText = ScreenPanel(HP(cx, 1.75f, cz + 0.64f), 3.4f, 0.9f, "", 0.032f, new Color(0.8f, 0.95f, 0.9f));
            console.statusText = ScreenPanel(HP(cx - 3.25f, 2.2f, cz + 0.64f), 2.3f, 1.6f, "", 0.032f, new Color(0.4f, 1f, 0.5f));
            console.optionsText = ScreenPanel(HP(cx + 3.25f, 2.2f, cz + 0.64f), 2.3f, 1.6f, "", 0.032f, new Color(1f, 0.75f, 0.3f));
            Label("GAMEMODE CONTROL", HP(cx, 3.35f, cz + 0.64f), ReadFromSouth, 0.05f, new Color(0.95f, 0.78f, 0.1f), "Hub:Decor");

            // Four big mode buttons with coloured lamp rings.
            string[] labels = { "ESCAPE", "INFECTION", "BLACKOUT", "SPEEDRUN" };
            Color[] colors = { new Color(0.2f, 0.9f, 1f), new Color(1f, 0.1f, 0.08f), new Color(1f, 0.6f, 0.05f), new Color(0.2f, 1f, 0.3f) };
            var lamps = new Renderer[4];
            for (int i = 0; i < 4; i++)
            {
                float x = cx - 2.55f + i * 1.7f;
                var ring = Part("ModeRing", Group("Hub:Decor"), HP(x, 1.09f, cz - 0.3f), new Vector3(0.62f, 0.04f, 0.62f), M.LedWhite, 1f, false);
                lamps[i] = ring.GetComponent<Renderer>();
                Material capMat = new Material(M.Plastic) { color = colors[i] * 0.8f };
                var cap = Part("ModeButton " + labels[i], Group("Hub:Decor"), HP(x, 1.16f, cz - 0.3f), new Vector3(0.44f, 0.12f, 0.44f), capMat, 1f);
                var btn = cap.AddComponent<GameModeButton>();
                btn.mode = (GameMode)i;
                btn.cap = cap.transform;
                Label(labels[i], HP(x, 1.075f, cz - 0.78f), LyingFacingSouth, 0.028f, Color.white, "Hub:Decor");
            }
            console.modeLamps = lamps;

            // Two tactile levers at the ends.
            MakeConsoleLever(console, HP(cx - 4.1f, 1.07f, cz - 0.3f), ConsoleLever.Action.MuteAlarm, "ALARM");
            MakeConsoleLever(console, HP(cx + 4.1f, 1.07f, cz - 0.3f), ConsoleLever.Action.PrepTime, "PREP");
            return console;
        }

        private static TextMesh ScreenPanel(Vector3 pos, float w, float h, string text, float charSize, Color color)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "ScreenGlass";
            quad.transform.SetParent(Group("Hub:Decor"), false);
            quad.transform.SetPositionAndRotation(pos + Vector3.back * 0.02f, Quaternion.identity);   // faces -Z: towards the player
            quad.transform.localScale = new Vector3(w, h, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = M.Crt;
            var tm = Label(text, pos + Vector3.back * 0.04f, ReadFromSouth, charSize, color, "Hub:Decor");
            return tm;
        }

        private static void MakeConsoleLever(GameModeConsole console, Vector3 basePos, ConsoleLever.Action action, string label)
        {
            Part("LeverBase", Group("Hub:Props"), basePos + Vector3.up * 0.1f, new Vector3(0.5f, 0.2f, 0.5f), M.Metal, 1f, false);
            var root = new GameObject("Lever " + label);
            root.transform.SetParent(Group("Hub:Decor"), false);
            root.transform.position = basePos + Vector3.up * 0.2f;
            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);
            Part("LeverArm", pivot.transform, root.transform.position + Vector3.up * 0.32f, new Vector3(0.1f, 0.64f, 0.1f), M.Metal, 1f);
            Part("LeverKnob", pivot.transform, root.transform.position + Vector3.up * 0.7f, new Vector3(0.22f, 0.22f, 0.22f), M.LedRed, 1f);
            var lever = root.AddComponent<ConsoleLever>();
            lever.console = console;
            lever.action = action;
            lever.pivot = pivot.transform;
            Label(label, basePos + Vector3.back * 0.45f + Vector3.up * 0.01f, LyingFacingSouth, 0.03f, Color.white, "Hub:Decor");
        }

        // ------------------------------------------------------------------ CCTV wall

        private static void BuildCctvWall(List<CctvRig> cams)
        {
            var systemGo = new GameObject("CCTV System");
            systemGo.transform.SetParent(Group("Hub:Decor"), false);
            systemGo.transform.position = HP(27f, 2f, 22f);
            _cctvSystem = systemGo.AddComponent<CctvSystem>();
            _cctvSystem.monitorCentre = systemGo.transform;

            Part("MonitorDesk", Group("Hub:Props"), HP(27f, 0.4f, 22.9f), new Vector3(23f, 0.8f, 1.4f), M.HubPlate, 1.5f);
            float[] xs = { 18.5f, 27f, 35.5f };
            float[] ys = { 1.7f, 3.5f };
            var list = new List<CctvCamera>();
            for (int i = 0; i < cams.Count && i < 6; i++)
            {
                float x = xs[i % 3], y = ys[i / 3];
                float z = HubD - 0.3f;
                Part("MonitorFrame", Group("Hub:Props"), HP(x, y, z + 0.05f), new Vector3(2.95f, 1.75f, 0.25f), M.Plastic, 1f, false);

                var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(screen.GetComponent<Collider>());
                screen.name = "CctvScreen " + (i + 1);
                screen.transform.SetParent(Group("Hub:Decor"), false);
                screen.transform.SetPositionAndRotation(HP(x, y, z - 0.09f), Quaternion.identity);
                screen.transform.localScale = new Vector3(2.7f, 1.52f, 1f);
                var sr = screen.GetComponent<MeshRenderer>();
                sr.sharedMaterial = M.CctvScreen;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(overlay.GetComponent<Collider>());
                overlay.name = "Scanlines";
                overlay.transform.SetParent(Group("Hub:Decor"), false);
                overlay.transform.SetPositionAndRotation(HP(x, y, z - 0.1f), Quaternion.identity);
                overlay.transform.localScale = new Vector3(2.7f, 1.52f, 1f);
                var orr = overlay.GetComponent<MeshRenderer>();
                orr.sharedMaterial = M.Scanlines;
                orr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                orr.sharedMaterial.mainTextureScale = new Vector2(1f, 38f);

                var label = Label(cams[i].title, HP(x - 1.25f, y + 0.66f, z - 0.115f), ReadFromSouth, 0.022f, new Color(0.9f, 1f, 0.9f), "Hub:Decor", TextAnchor.MiddleLeft, 48);
                Part("RecDot", Group("Hub:Decor"), HP(x + 1.2f, y + 0.66f, z - 0.115f), new Vector3(0.08f, 0.08f, 0.02f), M.LedRed, 1f, false);

                var cc = screen.AddComponent<CctvCamera>();
                cc.cam = cams[i].cam;
                cc.assistLight = cams[i].light;
                cc.screen = sr;
                cc.screenMaterial = M.CctvScreen;
                cc.label = label;
                cc.title = cams[i].title;
                list.Add(cc);
            }
            _cctvSystem.cameras = list.ToArray();
            Label("SECURITY  -  LEVEL 0 CAMERAS", HP(27f, 4.45f, HubD - 0.2f), ReadFromSouth, 0.05f, new Color(0.95f, 0.78f, 0.1f), "Hub:Decor");
        }

        // ---------------------------------------------------------------- spawn pads

        private static void BuildSpawnPads()
        {
            float[] xs = { 17.5f, 21f, 24.5f, 28f };
            for (int i = 0; i < xs.Length; i++)
            {
                Vector3 c = HP(xs[i], 0.05f, 4.6f);
                Part("PadHazard", Group("Hub:Decor"), c, new Vector3(2.0f, 0.02f, 2.0f), M.Hazard, 0.4f, false);
                Part("PadInner", Group("Hub:Decor"), c + Vector3.up * 0.012f, new Vector3(1.5f, 0.02f, 1.5f), M.HubFloor, 1f, false);
                foreach (var corner in new[] { new Vector2(-0.6f, -0.6f), new Vector2(0.6f, -0.6f), new Vector2(-0.6f, 0.6f), new Vector2(0.6f, 0.6f) })
                    Part("PadLight", Group("Hub:Decor"), c + new Vector3(corner.x, 0.03f, corner.y), new Vector3(0.14f, 0.03f, 0.14f), M.LedCyan, 1f, false);

                var go = new GameObject("Spawn Pad " + (i + 1));
                go.transform.SetParent(Group("Zones"), false);
                go.transform.position = HP(xs[i], 0f, 4.6f);
                go.AddComponent<SpawnPoint>();
                _spawnPads.Add(go.transform);
            }
            Label("ARRIVALS", HP(23f, 0.06f, 2.0f), LyingFacingSouth, 0.07f, new Color(0.95f, 0.78f, 0.1f), "Hub:Decor");

            // Entering the hub (e.g. climbing back up) sets it as your respawn point.
            var cp = new GameObject("Hub Checkpoint");
            cp.transform.SetParent(Group("Zones"), false);
            var zone = cp.AddComponent<CheckpointZone>();
            zone.bounds = new Bounds(HP(20f, 2.4f, 12f), new Vector3(HubW, 5f, HubD));
            zone.spawn = HP(23f, 1.3f, 4.6f);
        }

        // ------------------------------------------------------- lighting + feedback

        private static void BuildHubLighting(GameModeConsole console)
        {
            var atmosGo = new GameObject("Hub Atmosphere");
            atmosGo.transform.SetParent(Group("Hub:Fixtures"), false);
            atmosGo.transform.position = HP(20f, 2f, 12f);
            var atmos = atmosGo.AddComponent<HubAtmosphere>();
            atmos.console = console;

            var lights = new List<Light>();
            foreach (var p in new[] { new Vector2(6f, 12f), new Vector2(20f, 6f), new Vector2(20f, 18f), new Vector2(32f, 6f), new Vector2(32f, 18f), new Vector2(26f, 12f) })
            {
                var go = new GameObject("HubLight");
                go.transform.SetParent(atmosGo.transform, false);
                go.transform.position = HP(p.x, HubH - 0.5f, p.y);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 15f;
                l.intensity = 1.8f;
                l.color = new Color(0.82f, 0.94f, 1f);
                l.shadows = LightShadows.None;
                lights.Add(l);
            }
            atmos.lights = lights.ToArray();

            var strips = new List<Renderer>();
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 9; i++)
                {
                    var strip = Part("LightStrip", Group("Hub:Decor"), HP(15f + i * 3f, HubH - 0.05f, row == 0 ? 6f : 18f), new Vector3(1.8f, 0.05f, 0.2f), M.LedCyan, 1f, false);
                    strips.Add(strip.GetComponent<Renderer>());
                }
            foreach (var wing in new[] { 3f, 6f, 9f })
                strips.Add(Part("LightStrip", Group("Hub:Decor"), HP(wing, HubH - 0.05f, 12f), new Vector3(1.8f, 0.05f, 0.2f), M.LedCyan, 1f, false).GetComponent<Renderer>());
            atmos.strips = strips.ToArray();

            // Rotating warning beacons.
            var beacons = new List<Transform>();
            var beaconLights = new List<Light>();
            foreach (var p in new[] { new Vector2(14f, 22f), new Vector2(38f, 2.5f) })
            {
                var b = new GameObject("Beacon");
                b.transform.SetParent(atmosGo.transform, false);
                b.transform.position = HP(p.x, HubH - 0.35f, p.y);
                Cylinder("BeaconBody", b.transform.position, new Vector3(0.25f, 0.12f, 0.25f), M.LampRed);
                var spot = new GameObject("BeaconLight");
                spot.transform.SetParent(b.transform, false);
                var l = spot.AddComponent<Light>();
                l.type = LightType.Spot;
                l.spotAngle = 55f;
                l.range = 20f;
                l.color = new Color(1f, 0.1f, 0.05f);
                l.shadows = LightShadows.None;
                l.enabled = false;
                spot.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
                beacons.Add(b.transform);
                beaconLights.Add(l);
            }
            atmos.beacons = beacons.ToArray();
            atmos.beaconLights = beaconLights.ToArray();

            // Metal reflections.
            var probeGo = new GameObject("HubReflection");
            probeGo.transform.SetParent(Group("Hub:Fixtures"), false);
            probeGo.transform.position = HP(20f, 2.2f, 12f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 128;
            probe.size = new Vector3(HubW, HubH, HubD);
            probe.boxProjection = true;
            probe.hdr = false;

            // Ventilation hum.
            foreach (var p in new[] { new Vector2(30f, 20f), new Vector2(6f, 4f) })
            {
                var v = new GameObject("HubVent");
                v.transform.SetParent(Group("Hub:Decor"), false);
                v.transform.position = HP(p.x, HubH - 0.6f, p.y);
                var ve = v.AddComponent<VentEmitter>();
                ve.volume = 0.22f;
            }
        }

        // ------------------------------------------------- player, spawn and gameplay

        private static ArmLocomotion BuildPlayerAndGameplay()
        {
            Vector3 start = _spawnPads[0].position;
            var loco = GymBuilder.BuildPlayer(_root, new Vector3(start.x, start.y + 0.1f, start.z), M);

            var health = loco.gameObject.AddComponent<PlayerHealth>();
            health.locomotion = loco;
            health.startCheckpoint = start + Vector3.up * 1.3f;
            loco.gameObject.AddComponent<HandInteractor>().locomotion = loco;
            loco.gameObject.AddComponent<HandImpactAudio>().locomotion = loco;
            var spawn = loco.gameObject.AddComponent<RandomSpawn>();
            spawn.locomotion = loco;
            spawn.health = health;

            // Wire the wardrobe to the player's avatar.
            var cosmetics = loco.GetComponent<AvatarCosmetics>();
            foreach (var b in _cosButtons) b.cosmetics = cosmetics;
            if (_wardrobe != null) _wardrobe.cosmetics = cosmetics;

            _root.gameObject.AddComponent<PowerGridTicker>();
            return loco;
        }
    }
}
