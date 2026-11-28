using Monk.World;
using UnityEngine;

namespace Monk.Audio
{
    /// <summary>
    /// Fluorescent buzz. One 3D source rides the nearest working panel (so there are not 150 audio sources),
    /// rising in pitch and volume as you approach it and stuttering with flickering panels.
    /// A quiet, non-positional low drone sits underneath.
    /// </summary>
    public class AmbienceController : MonoBehaviour
    {
        public Transform listener;
        public float humRange = 7f;

        private AudioSource _hum, _bed;
        private Vector3 _humPos;

        private void Start()
        {
            _hum = MakeSource("Buzz", true);
            _hum.clip = ProceduralAudio.Hum();
            _hum.minDistance = 1.5f;
            _hum.maxDistance = 16f;
            _hum.rolloffMode = AudioRolloffMode.Linear;
            _hum.volume = 0f;
            _hum.time = Random.value;
            _hum.Play();

            _bed = MakeSource("Drone", false);
            _bed.clip = ProceduralAudio.Hum();
            _bed.pitch = 0.5f;
            _bed.volume = 0.05f;
            _bed.Play();
        }

        private AudioSource MakeSource(string name, bool spatial)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = spatial ? 1f : 0f;
            s.dopplerLevel = 0f;
            return s;
        }

        private void Update()
        {
            if (listener == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                listener = cam.transform;
            }

            LightPanel nearest = null;
            float bestSqr = float.MaxValue;
            Vector3 p = listener.position;
            foreach (var panel in LightPanel.All)
            {
                if (panel.mode == LightPanel.Mode.Dead) continue;
                float d = (panel.transform.position - p).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; nearest = panel; }
            }

            if (nearest == null)
            {
                _hum.volume = Mathf.MoveTowards(_hum.volume, 0f, Time.deltaTime);
                return;
            }

            Vector3 target = nearest.transform.position;
            _humPos = (_humPos == Vector3.zero) ? target : Vector3.Lerp(_humPos, target, 8f * Time.deltaTime);
            _hum.transform.position = _humPos;

            float dist = Mathf.Sqrt(bestSqr);
            float prox = 1f - Mathf.Clamp01(dist / humRange);
            _hum.pitch = Mathf.Lerp(0.93f, 1.14f, prox * prox);                    // pitch climbs as you get close
            float goal = Mathf.Lerp(0.12f, 0.9f, prox) * (0.3f + 0.7f * nearest.Intensity); // dips when it flickers
            _hum.volume = Mathf.MoveTowards(_hum.volume, goal, 4f * Time.deltaTime);
        }
    }
}
