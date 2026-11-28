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
            public Material Wallpaper, Carpet, CeilingTile, Concrete, Metal, Hazard, Crate, Fabric, Baseboard;
            public Material Water, Glass, Peel, Stain, Crack;
            public Material PanelOn, PanelFlicker, PanelDead, LampRed, LedGreen, LedRed, LedAmber;
            public Material Spark, Monster, MonsterEye, ExitGlow, Rack, Red, Paper;
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

            // ----- materials -----
            s.Wallpaper = Mat("Wallpaper", Color.white, wallpaper, 0.05f, 0f);
            s.Carpet = Mat("Carpet", Color.white, carpet, 0.05f, 0f);
            s.CeilingTile = Mat("CeilingTile", Color.white, ceiling, 0.05f, 0f);
            s.Concrete = Mat("Concrete", Color.white, concrete, 0.1f, 0f);
            s.Metal = Mat("Metal", Color.white, metal, 0.55f, 0.6f);
            s.Hazard = Mat("Hazard", Color.white, hazard, 0.2f, 0f);
            s.Crate = Mat("Crate", Color.white, crate, 0.05f, 0f);
            s.Fabric = Mat("Fabric", Color.white, fabric, 0.02f, 0f);
            s.Baseboard = Mat("Baseboard", Color.white, baseboard, 0.1f, 0f);
            s.Rack = Mat("Rack", new Color(0.1f, 0.11f, 0.12f), null, 0.4f, 0.5f);
            s.Red = Mat("Red", new Color(0.65f, 0.05f, 0.04f), null, 0.5f, 0.2f);
            s.Paper = Mat("Paper", new Color(0.88f, 0.86f, 0.78f), null, 0.1f, 0f);
            s.Monster = Mat("Monster", new Color(0.13f, 0.1f, 0.14f), null, 0.3f, 0f);

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
            s.MonsterEye = Mat("MonsterEye", Color.black, null, 0.5f, 0f);
            SetEmission(s.MonsterEye, new Color(1f, 0.1f, 0.05f) * 4f);
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
