using Monk.Audio;
using UnityEngine;

namespace Monk.Hub
{
    /// <summary>
    /// The hub's lighting and alarms follow the selected gamemode, so you can see the mode change before the airlock opens:
    /// cool white (escape), pulsing red with a siren (infection), near-dark amber with a klaxon (blackout),
    /// racing green with a ticking beep (speedrun).
    /// </summary>
    public class HubAtmosphere : MonoBehaviour
    {
        public Light[] lights;
        public Renderer[] strips;
        public Transform[] beacons;
        public Light[] beaconLights;
        public GameModeConsole console;

        private AudioSource _alarm, _tick;
        private MaterialPropertyBlock _mpb;
        private float _nextTick;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _alarm = gameObject.AddComponent<AudioSource>();
            _alarm.loop = true;
            _alarm.playOnAwake = false;
            _alarm.spatialBlend = 0.6f;
            _alarm.minDistance = 6f;
            _alarm.maxDistance = 40f;
            _alarm.rolloffMode = AudioRolloffMode.Linear;
            _tick = gameObject.AddComponent<AudioSource>();
            _tick.playOnAwake = false;
            _tick.spatialBlend = 0.6f;
            _tick.minDistance = 6f;
            _tick.maxDistance = 40f;
        }

        private void OnEnable() => GameModeState.Changed += OnMode;
        private void OnDisable() => GameModeState.Changed -= OnMode;
        private void Start() => OnMode(GameModeState.Current);

        private void OnMode(GameMode mode)
        {
            AudioClip clip = null;
            switch (mode)
            {
                case GameMode.Infection: clip = ProceduralAudio.Siren(); break;
                case GameMode.Blackout: clip = ProceduralAudio.Klaxon(); break;
            }
            if (clip == null) { _alarm.Stop(); return; }
            _alarm.clip = clip;
            _alarm.Play();
        }

        private void Update()
        {
            var mode = GameModeState.Current;
            bool muted = console != null && console.AlarmMuted;
            float t = Time.time;

            Color light; float level; Color strip; float beacon = 0f; float alarmVolume = 0f;
            switch (mode)
            {
                case GameMode.Infection:
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 6f);
                    light = new Color(1f, 0.1f, 0.06f); level = 0.3f + 2.2f * pulse;
                    strip = new Color(1f, 0.05f, 0.03f) * (0.5f + 3f * pulse);
                    beacon = 1f; alarmVolume = 0.55f;
                    break;
                }
                case GameMode.Blackout:
                {
                    float flicker = Mathf.PerlinNoise(t * 3f, 3f) > 0.82f ? 0.7f : 0f;
                    light = new Color(1f, 0.55f, 0.1f); level = 0.05f + flicker;
                    strip = new Color(1f, 0.5f, 0.05f) * (0.03f + flicker * 2f);
                    beacon = 0.6f; alarmVolume = 0.4f;
                    break;
                }
                case GameMode.Speedrun:
                {
                    light = Color.Lerp(new Color(0.2f, 1f, 0.3f), new Color(0.9f, 1f, 0.2f), 0.5f + 0.5f * Mathf.Sin(t * 5f));
                    level = 1.6f + 0.6f * Mathf.Sin(t * 10f);
                    strip = new Color(0.2f, 1f, 0.3f) * 2.5f;
                    if (!muted && t >= _nextTick)
                    {
                        _nextTick = t + 0.5f;
                        _tick.PlayOneShot(ProceduralAudio.Beep(), 0.35f);
                    }
                    break;
                }
                default:
                    light = new Color(0.82f, 0.94f, 1f); level = 1.8f;
                    strip = new Color(0.4f, 0.9f, 1f) * 2f;
                    break;
            }

            foreach (var l in lights)
            {
                if (l == null) continue;
                l.color = light;
                l.intensity = level;
            }
            for (int i = 0; i < strips.Length; i++)
            {
                if (strips[i] == null) continue;
                Color c = strip;
                if (mode == GameMode.Speedrun) c = strip * (Mathf.Sin(t * 9f - i * 0.9f) > 0f ? 1.3f : 0.15f); // chase
                _mpb.SetColor(EmissionId, c);
                strips[i].SetPropertyBlock(_mpb);
            }
            for (int i = 0; i < beacons.Length; i++)
                if (beacons[i] != null) beacons[i].Rotate(0f, (beacon > 0f ? 260f : 0f) * Time.deltaTime, 0f);
            foreach (var bl in beaconLights)
                if (bl != null) { bl.enabled = beacon > 0f; bl.intensity = 3f * beacon; }

            _alarm.volume = muted ? 0f : alarmVolume;
        }
    }
}
