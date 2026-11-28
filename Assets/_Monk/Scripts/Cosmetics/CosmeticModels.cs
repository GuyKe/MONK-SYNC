using System.Collections.Generic;
using UnityEngine;

namespace Monk.Cosmetics
{
    public enum CosmeticSlot { Hat, Visor, Badge, Suit }

    /// <summary>
    /// Builds the hats, visors, badges and mannequins out of primitives, so the same code dresses the player's
    /// avatar, the wardrobe mannequins and the hologram displays.
    /// </summary>
    public static class CosmeticModels
    {
        /// <summary>Visible in the mirror (and to other players later), hidden from your own eyes.</summary>
        public const int HiddenLayer = 30;

        public static readonly string[] HatNames = { "NONE", "HARD HAT", "TOP HAT", "TRAFFIC CONE", "ANTENNA" };
        public static readonly string[] VisorNames = { "NONE", "CLEAR", "TINTED", "RED", "GOLD" };
        public static readonly string[] BadgeNames = { "NONE", "STAR", "CIRCLE", "SQUARE", "BOLT" };
        public static readonly string[] SuitNames = { "YELLOW", "ORANGE", "WHITE", "BLUE", "GREEN", "RED", "BLACK" };
        public static readonly Color[] SuitColors =
        {
            new Color(0.95f, 0.78f, 0.1f), new Color(0.95f, 0.42f, 0.08f), new Color(0.9f, 0.9f, 0.88f),
            new Color(0.15f, 0.35f, 0.85f), new Color(0.15f, 0.65f, 0.25f), new Color(0.8f, 0.1f, 0.1f), new Color(0.1f, 0.1f, 0.12f),
        };

        public static int Count(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Hat: return HatNames.Length;
                case CosmeticSlot.Visor: return VisorNames.Length;
                case CosmeticSlot.Badge: return BadgeNames.Length;
                default: return SuitNames.Length;
            }
        }

        public static string NameOf(CosmeticSlot slot, int i)
        {
            switch (slot)
            {
                case CosmeticSlot.Hat: return HatNames[i];
                case CosmeticSlot.Visor: return VisorNames[i];
                case CosmeticSlot.Badge: return BadgeNames[i];
                default: return SuitNames[i];
            }
        }

        private static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        private static readonly Dictionary<(Material, Color), Material> Cache = new Dictionary<(Material, Color), Material>();

        private static Material Tinted(Material baseMat, Color c)
        {
            if (baseMat == null) return null;
            var key = (baseMat, c);
            if (!Cache.TryGetValue(key, out var m) || m == null)
            {
                m = new Material(baseMat) { color = c };
                Cache[key] = m;
            }
            return m;
        }

        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Color color,
            int layer, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Tinted(mat, color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.layer = layer;
            return go;
        }

        private static GameObject Root(string name, Transform parent, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>Hat sitting with its base at the parent's origin.</summary>
        public static GameObject Hat(int kind, Transform parent, Material mat, int layer)
        {
            if (kind <= 0) return null;
            var root = Root("Hat", parent, layer);
            var t = root.transform;
            switch (kind)
            {
                case 1: // hard hat
                    Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.09f, 0f), new Vector3(0.30f, 0.22f, 0.30f), mat, new Color(1f, 0.85f, 0.1f), layer);
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.04f, 0.02f), new Vector3(0.35f, 0.008f, 0.38f), mat, new Color(0.95f, 0.75f, 0.05f), layer);
                    Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.2f, 0f), new Vector3(0.04f, 0.03f, 0.26f), mat, new Color(0.9f, 0.7f, 0.05f), layer);
                    break;
                case 2: // top hat
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.04f, 0f), new Vector3(0.36f, 0.006f, 0.36f), mat, new Color(0.05f, 0.05f, 0.06f), layer);
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.14f, 0f), new Vector3(0.22f, 0.1f, 0.22f), mat, new Color(0.05f, 0.05f, 0.06f), layer);
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.07f, 0f), new Vector3(0.225f, 0.016f, 0.225f), mat, new Color(0.7f, 0.1f, 0.1f), layer);
                    break;
                case 3: // traffic cone
                    Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.015f, 0f), new Vector3(0.3f, 0.03f, 0.3f), mat, new Color(0.95f, 0.4f, 0.05f), layer);
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.08f, 0f), new Vector3(0.22f, 0.05f, 0.22f), mat, new Color(0.95f, 0.4f, 0.05f), layer);
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.16f, 0f), new Vector3(0.15f, 0.04f, 0.15f), mat, new Color(0.95f, 0.95f, 0.95f), layer);
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.23f, 0f), new Vector3(0.1f, 0.04f, 0.1f), mat, new Color(0.95f, 0.4f, 0.05f), layer);
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.04f, 0.05f), mat, new Color(0.95f, 0.4f, 0.05f), layer);
                    break;
                case 4: // antenna
                    Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.12f, 0f), new Vector3(0.012f, 0.12f, 0.012f), mat, new Color(0.6f, 0.6f, 0.65f), layer);
                    Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.25f, 0f), new Vector3(0.05f, 0.05f, 0.05f), mat, new Color(0.9f, 0.1f, 0.1f), layer);
                    break;
            }
            return root;
        }

        public static GameObject Visor(int kind, Transform parent, Material[] visorMaterials, int layer)
        {
            if (kind <= 0 || visorMaterials == null || kind >= visorMaterials.Length || visorMaterials[kind] == null) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Kill(go.GetComponent<Collider>());
            go.name = "Visor";
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.01f, 0.07f);
            go.transform.localScale = new Vector3(0.28f, 0.17f, 0.16f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = visorMaterials[kind];
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Badge pinned at the parent's origin, facing +Z.</summary>
        public static GameObject Badge(int kind, Transform parent, Material mat, int layer)
        {
            if (kind <= 0) return null;
            var root = Root("Badge", parent, layer);
            var t = root.transform;
            switch (kind)
            {
                case 1:
                    Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.07f, 0.07f, 0.012f), mat, new Color(1f, 0.8f, 0.1f), layer);
                    Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.07f, 0.07f, 0.012f), mat, new Color(1f, 0.8f, 0.1f), layer, new Vector3(0f, 0f, 45f));
                    break;
                case 2:
                    Prim(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(0.08f, 0.006f, 0.08f), mat, new Color(0.9f, 0.1f, 0.1f), layer, new Vector3(90f, 0f, 0f));
                    break;
                case 3:
                    Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.07f, 0.07f, 0.012f), mat, new Color(0.15f, 0.4f, 0.9f), layer);
                    break;
                case 4:
                    Prim(PrimitiveType.Cube, t, new Vector3(0.012f, 0.02f, 0f), new Vector3(0.025f, 0.06f, 0.012f), mat, new Color(1f, 0.9f, 0.1f), layer, new Vector3(0f, 0f, -20f));
                    Prim(PrimitiveType.Cube, t, new Vector3(-0.012f, -0.02f, 0f), new Vector3(0.025f, 0.06f, 0.012f), mat, new Color(1f, 0.9f, 0.1f), layer, new Vector3(0f, 0f, -20f));
                    break;
            }
            return root;
        }

        /// <summary>A display mannequin on a stand. Returns the head transform (hats and visors attach there).</summary>
        public static Transform Mannequin(Transform parent, Material mat, Color suit, int layer)
        {
            var root = Root("Mannequin", parent, layer).transform;
            Color grey = new Color(0.55f, 0.57f, 0.6f);
            Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 0.01f, 0f), new Vector3(0.6f, 0.01f, 0.6f), mat, grey, layer);
            Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 0.55f, 0f), new Vector3(0.05f, 0.55f, 0.05f), mat, grey, layer);
            Prim(PrimitiveType.Capsule, root, new Vector3(0f, 1.25f, 0f), new Vector3(0.36f, 0.3f, 0.22f), mat, suit, layer);
            Prim(PrimitiveType.Cylinder, root, new Vector3(-0.27f, 1.2f, 0f), new Vector3(0.09f, 0.28f, 0.09f), mat, suit, layer, new Vector3(0f, 0f, 12f));
            Prim(PrimitiveType.Cylinder, root, new Vector3(0.27f, 1.2f, 0f), new Vector3(0.09f, 0.28f, 0.09f), mat, suit, layer, new Vector3(0f, 0f, -12f));
            Prim(PrimitiveType.Sphere, root, new Vector3(0f, 1.72f, 0f), new Vector3(0.26f, 0.26f, 0.26f), mat, suit, layer);
            var anchor = Root("HeadAnchor", root, layer).transform;   // unscaled, so hats and visors keep their size
            anchor.localPosition = new Vector3(0f, 1.72f, 0f);
            return anchor;
        }
    }
}
