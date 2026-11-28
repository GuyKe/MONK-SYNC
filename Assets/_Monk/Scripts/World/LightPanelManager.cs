using UnityEngine;

namespace Monk.World
{
    /// <summary>
    /// The map has ~150 panels but only a few real lights: a small pool follows the nearest working panels.
    /// Dead panels get no light, which leaves the dark pockets. Raise lightCount if the headset has headroom.
    /// </summary>
    public class LightPanelManager : MonoBehaviour
    {
        public Transform target;
        public int lightCount = 4;
        public float range = 8f;
        public float intensity = 3.2f;
        public Color color = new Color(1f, 0.9f, 0.55f);
        public float maxPanelDistance = 14f;

        private Light[] _lights;
        private LightPanel[] _assigned;
        private float _scan;

        private void Awake()
        {
            _lights = new Light[lightCount];
            _assigned = new LightPanel[lightCount];
            for (int i = 0; i < lightCount; i++)
            {
                var go = new GameObject("PanelLight " + i);
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                l.range = range;
                l.color = color;
                l.intensity = 0f;
                _lights[i] = l;
            }
        }

        private void Update()
        {
            if (target == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                target = cam.transform;
            }

            _scan -= Time.deltaTime;
            if (_scan <= 0f)
            {
                _scan = 0.2f;
                Reassign();
            }

            for (int i = 0; i < _lights.Length; i++)
            {
                var panel = _assigned[i];
                float goal = panel != null ? panel.Intensity * intensity : 0f;
                _lights[i].intensity = Mathf.MoveTowards(_lights[i].intensity, goal, intensity * 12f * Time.deltaTime);
                if (panel != null) _lights[i].transform.position = panel.transform.position + Vector3.down * 0.5f;
            }
        }

        private void Reassign()
        {
            // Nearest working panels around the player.
            var best = new LightPanel[_lights.Length];
            var bestD = new float[_lights.Length];
            for (int i = 0; i < best.Length; i++) bestD[i] = float.MaxValue;

            Vector3 p = target.position;
            float maxSqr = maxPanelDistance * maxPanelDistance;
            foreach (var panel in LightPanel.All)
            {
                if (panel.mode == LightPanel.Mode.Dead) continue;
                float d = (panel.transform.position - p).sqrMagnitude;
                if (d > maxSqr) continue;
                for (int i = 0; i < best.Length; i++)
                {
                    if (d >= bestD[i]) continue;
                    for (int j = best.Length - 1; j > i; j--) { best[j] = best[j - 1]; bestD[j] = bestD[j - 1]; }
                    best[i] = panel; bestD[i] = d;
                    break;
                }
            }

            // Keep lights on the panels they already have, so nothing pops.
            var wanted = new System.Collections.Generic.List<LightPanel>(best);
            for (int i = 0; i < _assigned.Length; i++)
            {
                if (_assigned[i] != null && wanted.Contains(_assigned[i])) wanted.Remove(_assigned[i]);
                else _assigned[i] = null;
            }
            for (int i = 0; i < _assigned.Length; i++)
            {
                if (_assigned[i] != null) continue;
                while (wanted.Count > 0 && wanted[0] == null) wanted.RemoveAt(0);
                if (wanted.Count == 0) break;
                _assigned[i] = wanted[0];
                wanted.RemoveAt(0);
                _lights[i].transform.position = _assigned[i].transform.position + Vector3.down * 0.5f;
            }
        }
    }
}
