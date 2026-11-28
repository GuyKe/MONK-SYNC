using UnityEngine;

namespace Monk.Locomotion
{
    public enum SurfacePreset { Custom, Rough, Normal, Smooth, Slick, Ice }

    /// <summary>
    /// Per-surface tuning. Put on a collider (or a parent of it).
    /// slipPercentage is the share of a hand's sideways (tangential) motion that slides along the surface
    /// instead of moving the body: 0 = full traction (bark, carpet), 1 = no traction (ice).
    /// Pushing straight into any surface always works.
    /// </summary>
    public class SurfaceProperties : MonoBehaviour
    {
        [Range(0f, 1f)] public float slipPercentage = 0.03f;

        [Range(0f, 2f), Tooltip("Multiplier on the noise a hand slap makes (read by AI later).")]
        public float noise = 1f;

        public SurfacePreset preset = SurfacePreset.Custom;

        public static float SlipFor(SurfacePreset p)
        {
            switch (p)
            {
                case SurfacePreset.Rough: return 0f;
                case SurfacePreset.Normal: return 0.03f;
                case SurfacePreset.Smooth: return 0.3f;
                case SurfacePreset.Slick: return 0.7f;
                case SurfacePreset.Ice: return 1f;
                default: return -1f;
            }
        }

        public void ApplyPreset(SurfacePreset p)
        {
            preset = p;
            float s = SlipFor(p);
            if (s >= 0f) slipPercentage = s;
        }

        private void OnValidate()
        {
            float s = SlipFor(preset);
            if (s >= 0f) slipPercentage = s;
        }
    }
}
