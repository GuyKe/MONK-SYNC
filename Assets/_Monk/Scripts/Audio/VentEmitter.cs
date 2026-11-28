using UnityEngine;

namespace Monk.Audio
{
    /// <summary>A distant ventilation shaft: a breathing rumble with the occasional echoing clank.</summary>
    public class VentEmitter : MonoBehaviour
    {
        public float volume = 0.45f;
        public Vector2 clankInterval = new Vector2(10f, 28f);

        private AudioSource _rumble, _clank;
        private float _nextClank;

        private void Start()
        {
            _rumble = gameObject.AddComponent<AudioSource>();
            Configure(_rumble, true);
            _rumble.clip = ProceduralAudio.VentRumble();
            _rumble.volume = volume;
            _rumble.time = Random.value * 3f;
            _rumble.Play();

            _clank = gameObject.AddComponent<AudioSource>();
            Configure(_clank, false);
            _nextClank = Time.time + Random.Range(clankInterval.x, clankInterval.y);
        }

        private static void Configure(AudioSource s, bool loop)
        {
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.minDistance = 2.5f;
            s.maxDistance = 30f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.dopplerLevel = 0f;
        }

        private void Update()
        {
            if (Time.time < _nextClank) return;
            _nextClank = Time.time + Random.Range(clankInterval.x, clankInterval.y);
            _clank.pitch = Random.Range(0.55f, 0.85f);
            _clank.PlayOneShot(ProceduralAudio.Clank(), 0.5f);
        }
    }
}
