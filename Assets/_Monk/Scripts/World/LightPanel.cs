using System.Collections.Generic;
using UnityEngine;

namespace Monk.World
{
    /// <summary>A recessed ceiling fluorescent panel: steady, flickering or dead.</summary>
    public class LightPanel : MonoBehaviour
    {
        public enum Mode { Steady, Flicker, Dead }

        public static readonly List<LightPanel> All = new List<LightPanel>();

        public Mode mode = Mode.Steady;
        public Renderer rend;
        public Color emitColor = new Color(1f, 0.92f, 0.6f) * 2.2f;
        public float seed;

        /// <summary>0..1 current brightness.</summary>
        public float Intensity { get; private set; } = 1f;

        private MaterialPropertyBlock _mpb;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private void OnEnable()
        {
            All.Add(this);
            Intensity = mode == Mode.Dead ? 0f : 1f;
            if (mode == Mode.Flicker) _mpb = new MaterialPropertyBlock();
        }

        private void OnDisable() => All.Remove(this);

        private void Update()
        {
            if (mode != Mode.Flicker) return;

            float t = Time.time;
            float fast = Mathf.PerlinNoise(seed, t * 9f);
            float slow = Mathf.PerlinNoise(seed + 10f, t * 0.35f);

            float i = fast > 0.4f ? 1f : 0.04f;                       // stuttering
            if (slow > 0.78f)                                         // occasional violent burst
                i = Mathf.PerlinNoise(seed + 20f, t * 30f) > 0.5f ? 0.9f : 0f;

            Intensity = i;
            if (rend != null)
            {
                _mpb.SetColor(EmissionId, emitColor * i);
                rend.SetPropertyBlock(_mpb);
            }
        }
    }
}
