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

        private AudioSource _hum, _bed, _steps;
        private float _nextSteps;
        private int _stepsLeft;
        private float _stepTimer;
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

            // Echoing footsteps somewhere out in the maze.
            var go = new GameObject("DistantSteps");
            go.transform.SetParent(transform, false);
            _steps = go.AddComponent<AudioSource>();
            _steps.spatialBlend = 1f;
            _steps.minDistance = 4f;
            _steps.maxDistance = 45f;
            _steps.rolloffMode = AudioRolloffMode.Linear;
            _steps.dopplerLevel = 0f;
            _steps.playOnAwake = false;
            var echo = go.AddComponent<AudioEchoFilter>();
            echo.delay = 280f;
            echo.decayRatio = 0.55f;
            echo.wetMix = 0.6f;
            echo.dryMix = 0.7f;
            _nextSteps = Time.time + Random.Range(12f, 30f);
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

            UpdateFootsteps();

            LightPanel nearest = null;
            float bestSqr = float.MaxValue;
            Vector3 p = listener.position;
            foreach (var panel in LightPanel.All)
            {
                if (!panel.Working) continue;
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

        private void UpdateFootsteps()
        {
            if (_stepsLeft > 0)
            {
                _stepTimer -= Time.deltaTime;
                if (_stepTimer <= 0f)
                {
                    _stepTimer = Random.Range(0.5f, 0.62f);
                    _stepsLeft--;
                    _steps.pitch = Random.Range(0.55f, 0.75f);
                    _steps.transform.position += _stepDir * 0.6f;
                    _steps.PlayOneShot(Random.value > 0.5f ? ProceduralAudio.Squelch() : ProceduralAudio.Thud(), 0.8f);
                }
                return;
            }
            if (Time.time < _nextSteps) return;

            _nextSteps = Time.time + Random.Range(20f, 50f);
            _stepsLeft = Random.Range(6, 12);
            Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(16f, 32f);
            _steps.transform.position = new Vector3(listener.position.x + ring.x, 1f, listener.position.z + ring.y);
            Vector2 toward = (-ring).normalized;
            _stepDir = new Vector3(toward.x, 0f, toward.y);
        }

        private Vector3 _stepDir;
    }
}
