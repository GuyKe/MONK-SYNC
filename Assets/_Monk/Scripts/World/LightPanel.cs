using System.Collections.Generic;
using UnityEngine;

namespace Monk.World
{
    /// <summary>A recessed ceiling fluorescent panel: steady, flickering or dead. Emergency power overrides all three.</summary>
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
        /// <summary>True while a fuse box has restored power here.</summary>
        public bool Surged { get; private set; }
        /// <summary>Gives off light right now (not dead, or restored).</summary>
        public bool Working => mode != Mode.Dead || Surged;

        private MaterialPropertyBlock _mpb;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private void OnEnable()
        {
            All.Add(this);
            _mpb = new MaterialPropertyBlock();
            PowerGrid.Changed += Refresh;
            Surged = false;
            Intensity = mode == Mode.Dead ? 0f : 1f;
            Refresh();
        }

        private void OnDisable()
        {
            All.Remove(this);
            PowerGrid.Changed -= Refresh;
        }

        private void Refresh()
        {
            bool restored = PowerGrid.IsRestored(transform.position);
            if (restored == Surged) return;
            Surged = restored;
            if (restored)
            {
                Intensity = 1f;
                Apply(1f);
            }
            else
            {
                Intensity = mode == Mode.Dead ? 0f : 1f;
                if (mode != Mode.Flicker && rend != null) rend.SetPropertyBlock(null);
            }
        }

        private void Apply(float i)
        {
            if (rend == null) return;
            _mpb.SetColor(EmissionId, emitColor * i);
            rend.SetPropertyBlock(_mpb);
        }

        private void Update()
        {
            if (mode != Mode.Flicker || Surged) return;

            float t = Time.time;
            float fast = Mathf.PerlinNoise(seed, t * 9f);
            float slow = Mathf.PerlinNoise(seed + 10f, t * 0.35f);

            float i = fast > 0.4f ? 1f : 0.04f;                       // stuttering
            if (slow > 0.78f)                                         // occasional violent burst
                i = Mathf.PerlinNoise(seed + 20f, t * 30f) > 0.5f ? 0.9f : 0f;

            Intensity = i;
            Apply(i);
        }
    }
}
