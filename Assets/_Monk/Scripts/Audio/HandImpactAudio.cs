using Monk.Gameplay;
using Monk.Locomotion;
using UnityEngine;

namespace Monk.Audio
{
    /// <summary>Hand slap sounds by surface (wet carpet squelch, splash, thud, clank) and the noise they make.</summary>
    public class HandImpactAudio : MonoBehaviour
    {
        public ArmLocomotion locomotion;

        private AudioSource[] _pool;
        private int _next;

        private void Awake()
        {
            _pool = new AudioSource[6];
            for (int i = 0; i < _pool.Length; i++)
            {
                var go = new GameObject("ImpactSource " + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.minDistance = 1f;
                s.maxDistance = 20f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.dopplerLevel = 0f;
                _pool[i] = s;
            }
        }

        private void OnEnable()
        {
            if (locomotion != null) locomotion.HandContacted += OnContact;
        }

        private void OnDisable()
        {
            if (locomotion != null) locomotion.HandContacted -= OnContact;
        }

        private void OnContact(HandContactEvent e)
        {
            SurfaceKind kind = e.surface != null ? e.surface.kind : SurfaceKind.Wallpaper;
            float noiseScale = e.surface != null ? e.surface.noise : 1f;
            float hardness = Mathf.Clamp01(e.speed / 5f);

            AudioClip clip;
            float pitch = Random.Range(0.9f, 1.1f);
            switch (kind)
            {
                case SurfaceKind.Carpet: clip = ProceduralAudio.Squelch(); break;
                case SurfaceKind.Water: clip = ProceduralAudio.Splash(); break;
                case SurfaceKind.Metal: clip = ProceduralAudio.Clank(); pitch *= 1.2f; break;
                case SurfaceKind.Concrete: clip = ProceduralAudio.Thud(); pitch *= 1.3f; break;
                case SurfaceKind.Crate: clip = ProceduralAudio.Thud(); pitch *= 0.9f; break;
                default: clip = ProceduralAudio.Thud(); pitch *= 1.05f; break;
            }

            var src = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            src.transform.position = e.point;
            src.pitch = pitch;
            src.volume = Mathf.Lerp(0.15f, 1f, hardness);
            src.PlayOneShot(clip);

            NoiseBus.Emit(e.point, hardness * noiseScale);
        }
    }
}
