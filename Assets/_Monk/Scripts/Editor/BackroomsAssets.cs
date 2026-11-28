using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Monk.EditorTools
{
    /// <summary>
    /// Generates every texture and material the map uses, in code: no art files needed.
    /// Textures tile seamlessly. Output goes to Assets/_Monk/Art/Generated and is reused on later builds.
    /// </summary>
    public static class BackroomsAssets
    {
        public const string Dir = "Assets/_Monk/Art/Generated";

        public sealed class Set
        {
            public Material[] Wallpapers, Carpets, Ceilings;   // by theme: classic, pale / office, mold
            public Material Concrete, Metal, Hazard, Crate, Fabric, Baseboard;
            public Material PoolTile, Linoleum, Wood, Drywall;
            public Material MoldDecal, Scratches, Arrow, WallCrack;
            public Material Water, Glass, Peel, Stain, Crack;
            public Material HubConcrete, HubFloor, HubPlate, HubGrate, ReinforcedGlass, Plastic, KeyCap, Crt, Scanlines;
            public Material CctvScreen, Hologram, Mirror, Suit, Accessory, LedCyan, LedWhite;
            public Material[] Visors;   // index = visor kind; element 0 unused
            public Material PanelOn, PanelFlicker, PanelDead, LampRed, LedGreen, LedRed, LedAmber;
            public Material Spark, ExitGlow, Rack, Red, Paper;
            public Color PanelEmit;
        }

        [MenuItem("Monk/Delete Generated Map Textures")]
        public static void DeleteGenerated()
        {
            if (AssetDatabase.IsValidFolder(Dir)) AssetDatabase.DeleteAsset(Dir);
            AssetDatabase.Refresh();
            Debug.Log("Generated textures deleted. The next map build regenerates them.");
        }

        public static Set Build()
        {
            Directory.CreateDirectory(Dir);
            var s = new Set { PanelEmit = new Color(1f, 0.92f, 0.6f) * 2.2f };

            // ----- textures (all tile seamlessly) -----
            var wallpaper = Tex("Wallpaper", 512, (u, v) =>
            {
                float x = u * 512f, y = v * 512f;
                var baseC = new Color(0.80f, 0.70f, 0.30f);                    // mono-yellow
                float k = ((int)(x / 32f) % 2 == 0) ? 1f : 0.93f;               // vertical stripes
                float cx = (x % 64f) - 32f, cy = (y % 64f) - 32f;
                float diamond = Mathf.Abs(cx) + Mathf.Abs(cy);
                if (diamond < 14f && diamond > 9f) k *= 0.88f;                  // repeating diamond motif
                if (diamond < 4f) k *= 0.9f;
                k *= 1f + (H((int)x / 2, (int)y / 2, 3) - 0.5f) * 0.05f;       // paper grain
                float stain = Mathf.SmoothStep(0.55f, 0.82f, Fbm(u, v, 3, 4, 11));
                var c = baseC * k;
                c = Color.Lerp(c, new Color(0.46f, 0.33f, 0.12f), stain * 0.45f); // subtle water damage
                c.a = 1f;
                return c;
            });

            var carpet = Tex("Carpet", 512, (u, v) =>
            {
                var baseC = new Color(0.72f, 0.64f, 0.46f);                     // worn commercial beige
                float fiber = 1f + (H((int)(u * 512f), (int)(v * 512f), 5) - 0.5f) * 0.14f;
                float weave = 1f + (Mathf.Sin(u * 512f * 1.6f) * Mathf.Sin(v * 512f * 1.6f)) * 0.03f;
                float damp = Mathf.SmoothStep(0.5f, 0.75f, Fbm(u, v, 4, 4, 21));
                var c = baseC * fiber * weave;
                c = Color.Lerp(c, new Color(0.36f, 0.31f, 0.19f), damp * 0.6f);  // damp patches
                c.a = 1f;
                return c;
            });

            var ceiling = Tex("CeilingTile", 256, (u, v) =>
            {
                float x = u * 256f, y = v * 256f;
                var c = new Color(0.80f, 0.79f, 0.70f);
                float speck = H((int)x, (int)y, 9);
                if (speck > 0.985f) c *= 0.55f;                                 // acoustic pinholes
                c *= 1f + (Fbm(u, v, 8, 3, 31) - 0.5f) * 0.12f;
                float stain = Mathf.SmoothStep(0.6f, 0.85f, Fbm(u, v, 2, 3, 41));
                c = Color.Lerp(c, new Color(0.55f, 0.46f, 0.25f), stain * 0.35f);
                if (x < 4f || y < 4f) c = new Color(0.52f, 0.52f, 0.47f);        // grid runner
                c.a = 1f;
                return c;
            });

            var concrete = Tex("Concrete", 256, (u, v) =>
            {
                float n = Fbm(u, v, 4, 4, 51);
                float c0 = 0.42f + n * 0.2f + (H((int)(u * 256f), (int)(v * 256f), 6) - 0.5f) * 0.05f;
                var c = new Color(c0, c0 * 0.99f, c0 * 0.96f);
                float stain = Mathf.SmoothStep(0.62f, 0.85f, Fbm(u, v, 3, 3, 61));
                c = Color.Lerp(c, new Color(0.22f, 0.2f, 0.17f), stain * 0.5f);
                c.a = 1f;
                return c;
            });

            var hazard = Tex("HazardStripes", 128, (u, v) =>
            {
                int d = ((int)(u * 128f) + (int)(v * 128f)) / 16;
                return d % 2 == 0 ? new Color(0.95f, 0.75f, 0.08f) : new Color(0.07f, 0.07f, 0.07f);
            });

            var metal = Tex("Metal", 128, (u, v) =>
            {
                float brush = 0.28f + (H((int)(v * 128f), 0, 71) - 0.5f) * 0.05f + (Fbm(u, v, 4, 3, 72) - 0.5f) * 0.08f;
                var c = new Color(brush, brush * 1.08f, brush * 1.15f);
                c.a = 1f;
                return c;
            });

            var crate = Tex("Crate", 128, (u, v) =>
            {
                float plank = ((int)(v * 128f) % 32 < 2) ? 0.7f : 1f;
                float grain = 1f + (H((int)(u * 128f) / 2, (int)(v * 128f), 81) - 0.5f) * 0.12f;
                var c = new Color(0.62f, 0.46f, 0.28f) * plank * grain;
                c.a = 1f;
                return c;
            });

            var fabric = Tex("Fabric", 64, (u, v) =>
            {
                float k = 1f + (H((int)(u * 64f), (int)(v * 64f), 91) - 0.5f) * 0.18f;
                var c = new Color(0.34f, 0.42f, 0.5f) * k;
                c.a = 1f;
                return c;
            });

            var baseboard = Tex("Baseboard", 64, (u, v) =>
            {
                float stain = Fbm(u, v, 4, 3, 101);
                var c = Color.Lerp(new Color(0.27f, 0.18f, 0.1f), new Color(0.14f, 0.1f, 0.06f), stain);
                c.a = 1f;
                return c;
            });

            var peel = Tex("PeelingCorner", 128, (u, v) =>
            {
                float d = u + (1f - v);                                          // distance from the top-left corner
                float thr = 0.78f + (Fbm(u, v, 4, 3, 111) - 0.5f) * 0.14f;
                if (d >= thr) return new Color(0, 0, 0, 0);
                float edge = Mathf.SmoothStep(thr - 0.16f, thr, d);              // curled edge shadow
                var c = Color.Lerp(new Color(0.93f, 0.88f, 0.62f), new Color(0.42f, 0.36f, 0.18f), edge);
                c.a = 1f;
                return c;
            }, true);

            var stainDecal = Tex("WaterStain", 128, (u, v) =>
            {
                float dx = (u - 0.5f) * 2f, dy = (v - 0.55f) * 1.5f;
                float warp = (Fbm(u, v, 4, 3, 121) - 0.5f) * 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) + warp;
                float a = Mathf.SmoothStep(1f, 0.45f, r) * 0.55f;
                if (Mathf.Abs(dx + warp * 0.3f) < 0.07f && v < 0.5f) a = Mathf.Max(a, 0.42f * (v / 0.5f)); // drip
                var c = Color.Lerp(new Color(0.42f, 0.3f, 0.1f), new Color(0.25f, 0.17f, 0.05f), Mathf.SmoothStep(0.5f, 0.95f, r));
                c.a = a;
                return c;
            }, true);

            var crackDecal = Tex("GlassCrack", 256, (u, v) =>
            {
                float px = (u - 0.5f) * 2f, py = (v - 0.5f) * 2f;
                float r = Mathf.Sqrt(px * px + py * py), ang = Mathf.Atan2(py, px);
                float a = 0f;
                for (int k = 0; k < 14; k++)
                {
                    float ak = H(k, 7, 131) * Mathf.PI * 2f;
                    float along = Mathf.Cos(ang - ak) * r;
                    float dist = Mathf.Abs(Mathf.Sin(ang - ak)) * r;
                    float reach = 0.5f + H(k, 9, 132) * 0.5f;
                    if (along > 0f && r < reach && dist < 0.009f * (1.4f - r)) a = 0.9f;
                }
                foreach (float ring in new[] { 0.22f, 0.42f, 0.66f })
                    if (Mathf.Abs(r - ring) < 0.006f && Fbm(u, v, 6, 2, 133) > 0.45f) a = Mathf.Max(a, 0.6f);
                return new Color(0.9f, 0.96f, 1f, a);
            }, true);


            // ----- extra variety: more wallpapers, carpets, ceilings, tile, wood, decals -----
            var wallpaperPale = Tex("WallpaperPale", 512, (u, v) =>
            {
                float x = u * 512f, y = v * 512f;
                float k = (x % 16f) < 2f ? 0.84f : 1f;                                   // pinstripes
                float cx = (x % 64f) - 32f, cy = (y % 64f) - 32f;
                if ((Mathf.Abs(cx) < 2f && Mathf.Abs(cy) < 8f) || (Mathf.Abs(cy) < 2f && Mathf.Abs(cx) < 8f)) k *= 0.82f; // small crosses
                k *= 1f + (H((int)x / 2, (int)y / 2, 13) - 0.5f) * 0.04f;
                var c = new Color(0.86f, 0.80f, 0.52f) * k;
                float stain = Mathf.SmoothStep(0.58f, 0.85f, Fbm(u, v, 3, 4, 14));
                c = Color.Lerp(c, new Color(0.5f, 0.4f, 0.18f), stain * 0.35f);
                c.a = 1f;
                return c;
            });

            var wallpaperMold = Tex("WallpaperMold", 512, (u, v) =>
            {
                float x = u * 512f, y = v * 512f;
                float k = ((int)(x / 32f) % 2 == 0) ? 1f : 0.9f;
                float cx = (x % 64f) - 32f, cy = (y % 64f) - 32f;
                float diamond = Mathf.Abs(cx) + Mathf.Abs(cy);
                if (diamond < 14f && diamond > 9f) k *= 0.85f;
                var c = new Color(0.60f, 0.56f, 0.28f) * k;
                float stain = Mathf.SmoothStep(0.45f, 0.75f, Fbm(u, v, 3, 4, 16));
                c = Color.Lerp(c, new Color(0.34f, 0.24f, 0.08f), stain * 0.6f);
                float mold = Mathf.SmoothStep(0.52f, 0.78f, Fbm(u, v, 5, 4, 17));
                c = Color.Lerp(c, new Color(0.13f, 0.2f, 0.09f), mold * 0.7f);          // black-green mold
                c.a = 1f;
                return c;
            });

            var carpetMold = Tex("CarpetMoldy", 512, (u, v) =>
            {
                float fiber = 1f + (H((int)(u * 512f), (int)(v * 512f), 25) - 0.5f) * 0.16f;
                var c = new Color(0.5f, 0.45f, 0.29f) * fiber;
                float damp = Mathf.SmoothStep(0.4f, 0.7f, Fbm(u, v, 4, 4, 26));
                c = Color.Lerp(c, new Color(0.2f, 0.25f, 0.13f), damp * 0.8f);
                float dark = Mathf.SmoothStep(0.6f, 0.85f, Fbm(u, v, 6, 3, 27));
                c = Color.Lerp(c, new Color(0.08f, 0.09f, 0.05f), dark * 0.5f);
                c.a = 1f;
                return c;
            });

            var carpetOffice = Tex("CarpetOffice", 512, (u, v) =>
            {
                float x = u * 512f, y = v * 512f;
                float shade = 1f + (H((int)(u * 4f), (int)(v * 4f), 23) - 0.5f) * 0.14f;     // each carpet tile differs a little
                var c = new Color(0.38f, 0.40f, 0.43f) * shade * (1f + (H((int)x, (int)y, 24) - 0.5f) * 0.1f);
                if ((x % 128f) < 2f || (y % 128f) < 2f) c *= 0.6f;                           // tile seams
                float stain = Mathf.SmoothStep(0.6f, 0.85f, Fbm(u, v, 4, 3, 28));
                c = Color.Lerp(c, new Color(0.2f, 0.2f, 0.18f), stain * 0.45f);
                c.a = 1f;
                return c;
            });

            var ceilingStained = Tex("CeilingTileStained", 256, (u, v) =>
            {
                float x = u * 256f, y = v * 256f;
                var c = new Color(0.72f, 0.68f, 0.55f);
                if (H((int)x, (int)y, 19) > 0.98f) c *= 0.5f;
                float stain = Mathf.SmoothStep(0.45f, 0.75f, Fbm(u, v, 2, 3, 43));
                c = Color.Lerp(c, new Color(0.42f, 0.32f, 0.14f), stain * 0.7f);
                float ring = Mathf.Abs(Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) - 0.28f);
                if (ring < 0.012f) c = Color.Lerp(c, new Color(0.32f, 0.24f, 0.1f), 0.6f);    // dried water ring
                if (x < 4f || y < 4f) c = new Color(0.48f, 0.47f, 0.42f);
                c.a = 1f;
                return c;
            });

            var poolTile = Tex("PoolTile", 512, (u, v) =>
            {
                float x = u * 512f, y = v * 512f;
                bool grout = (x % 32f) < 2f || (y % 32f) < 2f;
                float shade = 1f + (H((int)(x / 32f), (int)(y / 32f), 31) - 0.5f) * 0.09f;
                var c = grout ? new Color(0.5f, 0.56f, 0.54f) : new Color(0.80f, 0.9f, 0.9f) * shade;
                float dirt = Mathf.SmoothStep(0.5f, 0.8f, Fbm(u, v, 4, 4, 32));
                c = Color.Lerp(c, new Color(0.25f, 0.34f, 0.24f), dirt * 0.5f);              // algae and grime
                c.a = 1f;
                return c;
            });

            var linoleum = Tex("Linoleum", 256, (u, v) =>
            {
                float sp = H((int)(u * 256f), (int)(v * 256f), 41);
                var c = new Color(0.42f, 0.46f, 0.44f) * (1f + (Fbm(u, v, 6, 3, 42) - 0.5f) * 0.25f);
                if (sp > 0.93f) c *= 1.25f;
                if (sp < 0.04f) c *= 0.7f;
                float seam = ((u * 256f) % 64f) < 1.5f || ((v * 256f) % 64f) < 1.5f ? 0.7f : 1f; // floor panels
                c *= seam;
                c.a = 1f;
                return c;
            });

            var wood = Tex("WoodPanel", 256, (u, v) =>
            {
                float x = u * 256f;
                int plank = (int)(x / 32f);
                float seam = (x % 32f) < 1.5f ? 0.55f : 1f;
                float grain = 1f + (Mathf.Sin(v * Mathf.PI * 2f * 8f + H(plank, 0, 51) * 10f) * 0.5f + (H((int)x, (int)(v * 64f), 52) - 0.5f)) * 0.08f;
                var c = new Color(0.36f, 0.22f, 0.12f) * (0.85f + H(plank, 1, 53) * 0.3f) * seam * grain;
                c.a = 1f;
                return c;
            });

            var drywall = Tex("Drywall", 128, (u, v) =>
            {
                float n = 1f + (H((int)(u * 128f), (int)(v * 128f), 55) - 0.5f) * 0.08f;
                var c = new Color(0.80f, 0.78f, 0.70f) * n;
                c = Color.Lerp(c, new Color(0.55f, 0.5f, 0.4f), Mathf.SmoothStep(0.6f, 0.85f, Fbm(u, v, 4, 3, 56)) * 0.5f);
                c.a = 1f;
                return c;
            });

            var moldDecal = Tex("MoldPatch", 128, (u, v) =>
            {
                float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float n = Fbm(u, v, 6, 3, 61);
                float a = Mathf.Clamp01((n - 0.42f) * 3f) * Mathf.SmoothStep(1f, 0.3f, r);
                return new Color(0.1f, 0.18f, 0.07f, a * 0.85f);
            }, true);

            var scratches = Tex("TallyScratches", 128, (u, v) =>
            {
                bool hit = false;
                for (int row = 0; row < 2; row++)
                {
                    float vv = v - row * 0.38f;
                    for (int g = 0; g < 3; g++)
                    {
                        float x0 = 0.08f + g * 0.3f;
                        for (int k = 0; k < 4; k++)
                            if (Mathf.Abs(u - (x0 + k * 0.05f)) < 0.007f && vv > 0.5f && vv < 0.88f) hit = true;
                        if (Mathf.Abs((vv - 0.58f) - (u - (x0 - 0.03f))) < 0.012f && u > x0 - 0.03f && u < x0 + 0.19f && vv > 0.5f && vv < 0.9f) hit = true;
                    }
                }
                return hit ? new Color(0.13f, 0.12f, 0.1f, 0.85f) : new Color(0, 0, 0, 0);
            }, true);

            var arrowDecal = Tex("SprayArrow", 128, (u, v) =>
            {
                bool shaft = Mathf.Abs(u - 0.5f) < 0.05f && v > 0.12f && v < 0.66f;
                bool head = v >= 0.66f && v < 0.9f && Mathf.Abs(u - 0.5f) < (0.9f - v) * 0.55f;
                if (!(shaft || head) || H((int)(u * 128f), (int)(v * 128f), 63) < 0.12f) return new Color(0, 0, 0, 0);
                return new Color(0.72f, 0.08f, 0.06f, 0.75f);
            }, true);

            var wallCrack = Tex("WallCrack", 256, (u, v) =>
            {
                float px = (u - 0.5f) * 2f, py = (v - 0.5f) * 2f;
                float r = Mathf.Sqrt(px * px + py * py), ang = Mathf.Atan2(py, px);
                float a = 0f;
                for (int k = 0; k < 6; k++)
                {
                    float ak = H(k, 3, 141) * Mathf.PI * 2f + Mathf.Sin(r * 9f + k) * 0.18f;      // wandering cracks
                    float along = Mathf.Cos(ang - ak) * r;
                    float dist = Mathf.Abs(Mathf.Sin(ang - ak)) * r;
                    if (along > 0f && r < 0.4f + H(k, 5, 142) * 0.6f && dist < 0.012f * (1.5f - r)) a = 0.9f;
                }
                return new Color(0.1f, 0.09f, 0.07f, a);
            }, true);


            // ----- normal maps and PBR masks -----
            var carpetNormal = NormalTex("CarpetNormal", 512, (u, v) =>
            {
                float x = u * 512f, y = v * 512f;
                float fiber = H((int)x, (int)y, 71) * 0.6f + H((int)x / 2, (int)y / 2, 72) * 0.4f;     // loops of yarn
                float weave = Mathf.Sin(x * 1.6f) * Mathf.Sin(y * 1.6f) * 0.25f;                      // fabric weave
                return fiber + weave + Fbm(u, v, 8, 3, 73) * 0.3f;
            }, 2.2f);

            var wallpaperNormal = NormalTex("WallpaperNormal", 512, (u, v) =>
            {
                float x = u * 512f, y = v * 512f;
                float stripe = ((int)(x / 32f) % 2 == 0) ? 0.3f : 0f;                                  // embossed stripes
                float cx = (x % 64f) - 32f, cy = (y % 64f) - 32f;
                float diamond = Mathf.Abs(cx) + Mathf.Abs(cy);
                float motif = (diamond < 14f && diamond > 9f) ? 0.5f : 0f;
                return stripe + motif + H((int)x, (int)y, 74) * 0.08f;
            }, 3f);

            var concreteNormal = NormalTex("ConcreteNormal", 256, (u, v) =>
                Fbm(u, v, 6, 4, 75) * 0.7f + (H((int)(u * 256f), (int)(v * 256f), 76) > 0.97f ? -0.5f : 0f), 2.5f);

            var ceilingNormal = NormalTex("CeilingNormal", 256, (u, v) =>
            {
                float pores = H((int)(u * 256f), (int)(v * 256f), 77) > 0.9f ? -0.6f : 0f;             // acoustic pinholes
                float x = u * 256f, y = v * 256f;
                float groove = (x < 4f || y < 4f) ? -0.4f : 0f;
                return pores + groove + Fbm(u, v, 8, 3, 78) * 0.2f;
            }, 2f);

            var hubConcrete = Tex("HubConcrete", 512, (u, v) =>
            {
                float n = Fbm(u, v, 4, 4, 81);
                float c0 = 0.36f + n * 0.16f + (H((int)(u * 512f), (int)(v * 512f), 82) - 0.5f) * 0.04f;
                var c = new Color(c0, c0 * 1.0f, c0 * 1.03f);
                // vertical grime streaks and scuffs near the bottom of every tile
                float streak = Mathf.SmoothStep(0.55f, 0.85f, Fbm(u * 0.25f, v, 8, 3, 83));
                c = Color.Lerp(c, new Color(0.12f, 0.12f, 0.11f), streak * 0.45f);
                float scuff = Mathf.Abs(Mathf.Sin((u * 40f + v * 13f))) > 0.985f ? 0.7f : 1f;
                c *= scuff;
                float rust = Mathf.SmoothStep(0.72f, 0.9f, Fbm(u, v, 3, 3, 84));
                c = Color.Lerp(c, new Color(0.33f, 0.2f, 0.12f), rust * 0.35f);
                c.a = 1f;
                return c;
            });

            var hubConcreteNormal = NormalTex("HubConcreteNormal", 512, (u, v) =>
                Fbm(u, v, 6, 4, 85) * 0.8f + (H((int)(u * 512f), (int)(v * 512f), 86) > 0.985f ? -0.7f : 0f), 2.2f);

            var hubFloor = Tex("HubFloor", 512, (u, v) =>
            {
                float x = u * 512f, y = v * 512f;
                float c0 = 0.2f + Fbm(u, v, 5, 4, 87) * 0.1f;
                var c = new Color(c0, c0, c0 * 1.05f);
                if ((x % 128f) < 2f || (y % 128f) < 2f) c *= 0.55f;                                    // slab joints
                float scuff = Mathf.SmoothStep(0.66f, 0.86f, Fbm(u * 2f, v * 2f, 6, 3, 88));
                c = Color.Lerp(c, new Color(0.45f, 0.44f, 0.42f), scuff * 0.3f);                       // boot scuffs
                c.a = 1f;
                return c;
            });

            var hubFloorNormal = NormalTex("HubFloorNormal", 512, (u, v) =>
            {
                float x = u * 512f, y = v * 512f;
                float joint = ((x % 128f) < 2f || (y % 128f) < 2f) ? -0.8f : 0f;
                return joint + Fbm(u, v, 8, 3, 89) * 0.4f;
            }, 2f);

            // Diamond plate: raised lozenges.
            float PlateHeight(float u, float v)
            {
                float x = (u * 8f) % 1f, y = (v * 8f) % 1f;
                bool alt = ((int)(u * 8f) + (int)(v * 8f)) % 2 == 0;
                float a = alt ? Mathf.Abs(x - 0.5f) * 1.2f + Mathf.Abs(y - 0.5f) * 0.4f : Mathf.Abs(x - 0.5f) * 0.4f + Mathf.Abs(y - 0.5f) * 1.2f;
                return Mathf.Clamp01(1f - a * 2.2f);
            }
            var plateNormal = NormalTex("PlateNormal", 256, PlateHeight, 3.5f);
            var plateAlbedo = Tex("PlateAlbedo", 256, (u, v) =>
            {
                float h = PlateHeight(u, v);
                float g = 0.38f + h * 0.18f + (H((int)(u * 256f), (int)(v * 256f), 91) - 0.5f) * 0.05f;
                g *= 1f - Mathf.SmoothStep(0.6f, 0.9f, Fbm(u, v, 4, 3, 92)) * 0.35f;                    // grime in the valleys
                return new Color(g, g * 1.02f, g * 1.06f, 1f);
            });
            var plateMask = MaskTex("PlateMask", 256, (u, v) =>
            {
                float scuff = Mathf.SmoothStep(0.55f, 0.85f, Fbm(u * 3f, v * 3f, 6, 3, 93));           // worn, scratched metal is duller
                float metallic = 0.9f - scuff * 0.3f;
                float smooth = Mathf.Lerp(0.62f, 0.2f, scuff) * (0.6f + 0.4f * PlateHeight(u, v));
                return new Color(metallic, 0f, 0f, smooth);
            });

            var grateAlbedo = Tex("GrateAlbedo", 256, (u, v) =>
            {
                float x = (u * 8f) % 1f, y = (v * 8f) % 1f;
                bool bar = x < 0.16f || y < 0.16f;
                if (!bar) return new Color(0.02f, 0.02f, 0.025f, 1f);                                  // the dark gap below
                float g = 0.32f + (H((int)(u * 256f), (int)(v * 256f), 94) - 0.5f) * 0.06f;
                return new Color(g, g * 1.04f, g * 1.1f, 1f);
            });
            var grateMask = MaskTex("GrateMask", 256, (u, v) =>
            {
                float x = (u * 8f) % 1f, y = (v * 8f) % 1f;
                bool bar = x < 0.16f || y < 0.16f;
                return bar ? new Color(0.85f, 0f, 0f, 0.5f - H((int)(u * 256f), (int)(v * 256f), 95) * 0.2f) : new Color(0f, 0f, 0f, 0.05f);
            });

            var glassWire = Tex("GlassWire", 256, (u, v) =>
            {
                // wire-reinforced safety glass: a thin diamond mesh in clear glass
                float d1 = Mathf.Abs(((u + v) * 8f) % 1f - 0.5f), d2 = Mathf.Abs(((u - v + 1f) * 8f) % 1f - 0.5f);
                bool wire = d1 > 0.485f || d2 > 0.485f;
                float smudge = Mathf.SmoothStep(0.7f, 0.9f, Fbm(u, v, 5, 3, 96)) * 0.12f;
                return wire ? new Color(0.75f, 0.85f, 0.85f, 0.55f) : new Color(0.8f, 0.95f, 1f, 0.05f + smudge);
            }, true);

            var scanlines = Tex("Scanlines", 64, (u, v) =>
                new Color(0f, 0f, 0f, ((int)(v * 64f) % 2 == 0) ? 0.28f : 0f), true);

            // ----- materials -----
            s.Wallpapers = new[]
            {
                Mat("Wallpaper", Color.white, wallpaper, 0.05f, 0f),
                Mat("WallpaperPale", Color.white, wallpaperPale, 0.05f, 0f),
                Mat("WallpaperMold", Color.white, wallpaperMold, 0.03f, 0f),
            };
            s.Carpets = new[]
            {
                Mat("Carpet", Color.white, carpet, 0.05f, 0f),
                Mat("CarpetMoldy", Color.white, carpetMold, 0.1f, 0f),
                Mat("CarpetOffice", Color.white, carpetOffice, 0.04f, 0f),
            };
            s.Ceilings = new[]
            {
                Mat("CeilingTile", Color.white, ceiling, 0.05f, 0f),
                Mat("CeilingTileStained", Color.white, ceilingStained, 0.05f, 0f),
            };
            s.PoolTile = Mat("PoolTile", Color.white, poolTile, 0.7f, 0f);
            s.Linoleum = Mat("Linoleum", Color.white, linoleum, 0.35f, 0f);
            s.Wood = Mat("WoodPanel", Color.white, wood, 0.25f, 0f);
            s.Drywall = Mat("Drywall", Color.white, drywall, 0.02f, 0f);
            s.MoldDecal = Mat("MoldDecal", Color.white, moldDecal, 0f, 0f);
            s.Scratches = Mat("ScratchDecal", Color.white, scratches, 0f, 0f);
            s.Arrow = Mat("ArrowDecal", Color.white, arrowDecal, 0f, 0f);
            s.WallCrack = Mat("WallCrackDecal", Color.white, wallCrack, 0f, 0f);
            MakeTransparent(s.MoldDecal);
            MakeTransparent(s.Scratches);
            MakeTransparent(s.Arrow);
            MakeTransparent(s.WallCrack);

            foreach (var m in s.Wallpapers) WithNormal(m, wallpaperNormal, 0.8f);
            foreach (var m in s.Carpets) WithNormal(m, carpetNormal, 1.2f);
            foreach (var m in s.Ceilings) WithNormal(m, ceilingNormal, 0.8f);
            WithNormal(s.Concrete, concreteNormal, 1f);

            // Facility hub: grimy concrete, scuffed floor, metal plate and grating with metallic reflections.
            s.HubConcrete = WithNormal(Mat("HubConcrete", Color.white, hubConcrete, 0.15f, 0f), hubConcreteNormal, 1f);
            s.HubFloor = WithNormal(Mat("HubFloor", Color.white, hubFloor, 0.35f, 0f), hubFloorNormal, 0.8f);
            s.HubPlate = WithMask(WithNormal(Mat("HubPlate", Color.white, plateAlbedo, 1f, 1f), plateNormal, 1f), plateMask);
            s.HubGrate = WithMask(Mat("HubGrate", Color.white, grateAlbedo, 1f, 1f), grateMask);
            s.ReinforcedGlass = Mat("ReinforcedGlass", Color.white, glassWire, 0.97f, 0f);
            MakeTransparent(s.ReinforcedGlass);
            s.Scanlines = Mat("Scanlines", Color.white, scanlines, 0f, 0f);
            MakeTransparent(s.Scanlines);
            s.Plastic = Mat("Plastic", new Color(0.72f, 0.7f, 0.62f), null, 0.35f, 0f);
            s.KeyCap = Mat("KeyCap", new Color(0.8f, 0.78f, 0.7f), null, 0.5f, 0f);
            s.Crt = Mat("Crt", new Color(0.02f, 0.05f, 0.03f), null, 0.85f, 0f);
            SetEmission(s.Crt, new Color(0.02f, 0.12f, 0.05f));
            s.CctvScreen = MatWithShader("CctvScreen", Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture"), Color.white, null);
            s.Mirror = MatWithShader("Mirror", Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture"), Color.white, null);
            s.Hologram = Mat("Hologram", new Color(0.2f, 0.9f, 1f, 0.45f), null, 0.9f, 0f);
            SetEmission(s.Hologram, new Color(0.1f, 0.8f, 1f) * 2f);
            MakeTransparent(s.Hologram);
            s.Suit = Mat("Suit", Color.white, null, 0.35f, 0f);
            s.Accessory = Mat("Accessory", Color.white, null, 0.4f, 0.1f);
            s.LedCyan = Mat("LedCyan", Color.black, null, 0.5f, 0f);
            SetEmission(s.LedCyan, new Color(0.2f, 0.9f, 1f) * 2f);
            s.LedWhite = Mat("LedWhite", Color.black, null, 0.5f, 0f);
            SetEmission(s.LedWhite, new Color(1f, 1f, 1f) * 2f);

            s.Visors = new Material[5];
            s.Visors[1] = Mat("VisorClear", new Color(0.7f, 0.95f, 1f, 0.22f), null, 0.95f, 0f);
            s.Visors[2] = Mat("VisorTinted", new Color(0.05f, 0.06f, 0.08f, 0.8f), null, 0.95f, 0f);
            s.Visors[3] = Mat("VisorRed", new Color(1f, 0.1f, 0.08f, 0.55f), null, 0.9f, 0f);
            s.Visors[4] = Mat("VisorGold", new Color(1f, 0.78f, 0.2f, 0.7f), null, 0.9f, 0.5f);
            for (int i = 1; i < s.Visors.Length; i++) MakeTransparent(s.Visors[i]);
            SetEmission(s.Visors[3], new Color(1f, 0.05f, 0.03f) * 1.5f);
            s.Concrete = Mat("Concrete", Color.white, concrete, 0.1f, 0f);
            s.Metal = Mat("Metal", Color.white, metal, 0.55f, 0.6f);
            s.Hazard = Mat("Hazard", Color.white, hazard, 0.2f, 0f);
            s.Crate = Mat("Crate", Color.white, crate, 0.05f, 0f);
            s.Fabric = Mat("Fabric", Color.white, fabric, 0.02f, 0f);
            s.Baseboard = Mat("Baseboard", Color.white, baseboard, 0.1f, 0f);
            s.Rack = Mat("Rack", new Color(0.1f, 0.11f, 0.12f), null, 0.4f, 0.5f);
            s.Red = Mat("Red", new Color(0.65f, 0.05f, 0.04f), null, 0.5f, 0.2f);
            s.Paper = Mat("Paper", new Color(0.88f, 0.86f, 0.78f), null, 0.1f, 0f);

            s.Water = Mat("Water", new Color(0.22f, 0.3f, 0.27f, 0.55f), null, 0.97f, 0.1f);
            MakeTransparent(s.Water);
            s.Glass = Mat("Glass", new Color(0.85f, 0.97f, 1f, 0.05f), null, 0.97f, 0f); // nearly clear
            MakeTransparent(s.Glass);
            s.Stain = Mat("WaterStainDecal", Color.white, stainDecal, 0f, 0f);
            MakeTransparent(s.Stain);
            s.Crack = Mat("GlassCrackDecal", Color.white, crackDecal, 0.8f, 0f);
            MakeTransparent(s.Crack);
            s.Glass.SetFloat("_Cull", 0f);
            s.Crack.SetFloat("_Cull", 0f);
            s.Peel = Mat("PeelDecal", Color.white, peel, 0f, 0f);
            MakeCutout(s.Peel);

            s.PanelOn = Mat("PanelOn", new Color(0.9f, 0.9f, 0.8f), null, 0.2f, 0f);
            SetEmission(s.PanelOn, s.PanelEmit);
            s.PanelFlicker = Mat("PanelFlicker", new Color(0.9f, 0.9f, 0.8f), null, 0.2f, 0f);
            SetEmission(s.PanelFlicker, s.PanelEmit);
            s.PanelDead = Mat("PanelDead", new Color(0.2f, 0.2f, 0.19f), null, 0.3f, 0f);
            s.LampRed = Mat("LampRed", new Color(0.4f, 0.05f, 0.03f), null, 0.5f, 0f);
            SetEmission(s.LampRed, Color.black);
            s.LedGreen = Mat("LedGreen", Color.black, null, 0.5f, 0f);
            SetEmission(s.LedGreen, new Color(0.1f, 1f, 0.25f) * 2.5f);
            s.LedRed = Mat("LedRed", Color.black, null, 0.5f, 0f);
            SetEmission(s.LedRed, new Color(1f, 0.08f, 0.05f) * 2.5f);
            s.LedAmber = Mat("LedAmber", Color.black, null, 0.5f, 0f);
            SetEmission(s.LedAmber, new Color(1f, 0.55f, 0.05f) * 2.5f);
            s.ExitGlow = Mat("ExitGlow", new Color(1f, 0.95f, 0.7f), null, 0.5f, 0f);
            SetEmission(s.ExitGlow, new Color(1f, 0.93f, 0.6f) * 4f);

            var sparkShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            s.Spark = MatWithShader("Spark", sparkShader, new Color(1f, 0.7f, 0.2f), null);

            AssetDatabase.SaveAssets();
            return s;
        }

        // ------------------------------------------------------------ texture helpers

        private static Texture2D Tex(string name, int size, Func<float, float, Color> pixel, bool alpha = false)
        {
            string path = $"{Dir}/{name}.png";
            if (!File.Exists(path))
            {
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        px[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
                t.SetPixels(px);
                File.WriteAllBytes(path, t.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Default;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.filterMode = FilterMode.Bilinear;
                imp.mipmapEnabled = true;
                imp.anisoLevel = 4;
                imp.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                imp.alphaIsTransparency = alpha;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float H(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static int Mod(int a, int p) => ((a % p) + p) % p;

        /// <summary>Value noise that wraps every `period` cells, so the texture tiles.</summary>
        private static float VNoise(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = Mod(x0, period), xb = Mod(x0 + 1, period), ya = Mod(y0, period), yb = Mod(y0 + 1, period);
            float a = H(xa, ya, seed), b = H(xb, ya, seed), c = H(xa, yb, seed), d = H(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, total = 0f;
            int p = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * VNoise(u, v, p, seed + o * 17);
                total += amp;
                amp *= 0.5f;
                p *= 2;
            }
            return sum / total;
        }


        /// <summary>Tangent-space normal map from a tileable height function.</summary>
        private static Texture2D NormalTex(string name, int size, Func<float, float, float> height, float strength)
        {
            string path = $"{Dir}/{name}.png";
            if (!File.Exists(path))
            {
                var h = new float[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++) h[y * size + x] = height((x + 0.5f) / size, (y + 0.5f) / size);
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float hl = h[y * size + (x - 1 + size) % size], hr = h[y * size + (x + 1) % size];
                        float hd = h[((y - 1 + size) % size) * size + x], hu = h[((y + 1) % size) * size + x];
                        var n = new Vector3((hl - hr) * strength, (hd - hu) * strength, 1f).normalized;
                        px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                    }
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                t.SetPixels(px);
                File.WriteAllBytes(path, t.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.NormalMap;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.mipmapEnabled = true;
                imp.anisoLevel = 4;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Linear mask: R = metallic, A = smoothness (URP Lit metallic-gloss map).</summary>
        private static Texture2D MaskTex(string name, int size, Func<float, float, Color> pixel)
        {
            string path = $"{Dir}/{name}.png";
            if (!File.Exists(path))
            {
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++) px[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
                t.SetPixels(px);
                File.WriteAllBytes(path, t.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.sRGBTexture = false;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.mipmapEnabled = true;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material WithNormal(Material m, Texture2D normal, float scale)
        {
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", scale);
            m.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material WithMask(Material m, Texture2D mask)
        {
            m.SetTexture("_MetallicGlossMap", mask);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Smoothness", 1f);       // the mask's alpha scales this
            m.SetFloat("_Metallic", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ----------------------------------------------------------- material helpers


        private static Material Mat(string name, Color color, Texture2D tex, float smoothness, float metallic)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return MatWithShader(name, shader, color, tex, smoothness, metallic);
        }

        private static Material MatWithShader(string name, Shader shader, Color color, Texture2D tex,
            float smoothness = 0f, float metallic = 0f)
        {
            string path = $"{Dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.color = color;
            if (tex != null) m.mainTexture = tex;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void SetEmission(Material m, Color c)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(m);
        }

        private static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", 5f);
            m.SetFloat("_DstBlend", 10f);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
        }

        private static void MakeCutout(Material m)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_Cull", 0f); // two-sided: the curled paper is visible from the back
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = 2450;
            EditorUtility.SetDirty(m);
        }
    }
}
