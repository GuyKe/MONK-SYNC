using Monk.Audio;
using Monk.Gameplay;
using Monk.Locomotion;
using UnityEngine;

namespace Monk.World
{
    /// <summary>
    /// An emergency fuse box hidden in the maze. Flip its lever to either restore the lights around it for a while,
    /// or to unlock the doors it is wired to.
    /// </summary>
    public class FuseBox : MonoBehaviour, IHandPressable
    {
        public enum Kind { RestoreLights, UnlockDoors }

        public Kind kind;
        public float lightRadius = 36f;
        public float lightSeconds = 60f;
        public IndustrialDoor[] doors;
        public Transform pivot;
        public Renderer lamp;
        public float throwAngle = 40f;

        private bool _on;
        private float _offAt, _next;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private void Start() => Apply();

        public void Press(HandContactEvent contact)
        {
            if (Time.time < _next || _on) return;
            _next = Time.time + 0.6f;
            _on = true;

            if (kind == Kind.RestoreLights)
            {
                _offAt = Time.time + lightSeconds;
                PowerGrid.Restore(transform.position, lightRadius, lightSeconds);
            }
            else
            {
                if (doors != null) foreach (var d in doors) if (d != null) d.Unlock();
                PowerGrid.Restore(transform.position, 14f, 20f); // a short flicker of power to show it worked
            }

            Apply();
            AudioSource.PlayClipAtPoint(ProceduralAudio.Clank(), transform.position, 0.8f);
            AudioSource.PlayClipAtPoint(ProceduralAudio.Slam(), transform.position, 0.35f);
            HapticFeedback.Pulse(0.6f, 0.12f);
            NoiseBus.Emit(transform.position, 0.5f);
        }

        private void Update()
        {
            if (kind == Kind.RestoreLights && _on && Time.time >= _offAt)
            {
                _on = false;
                Apply();
            }
        }

        private void Apply()
        {
            if (pivot != null) pivot.localRotation = Quaternion.Euler(_on ? throwAngle : -throwAngle, 0f, 0f);
            if (lamp == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor(EmissionId, _on ? new Color(0.1f, 1f, 0.25f) * 2.5f : new Color(1f, 0.1f, 0.05f) * 2f);
            lamp.SetPropertyBlock(block);
        }
    }
}
