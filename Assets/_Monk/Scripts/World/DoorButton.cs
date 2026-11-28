using System.Collections;
using Monk.Audio;
using Monk.Gameplay;
using Monk.Locomotion;
using UnityEngine;

namespace Monk.World
{
    /// <summary>
    /// Push-button: slap it with a hand to toggle its door. The cap glows: green = open, amber = closed or moving,
    /// red = locked.
    /// </summary>
    public class DoorButton : MonoBehaviour, IHandPressable
    {
        public IndustrialDoor door;
        public Transform cap;
        public Renderer glow;
        public float cooldown = 0.7f;
        [Tooltip("World direction the cap moves when pressed (into the wall).")]
        public Vector3 pressDirection = Vector3.back;

        private float _next;
        private int _shown = -1;
        private MaterialPropertyBlock _mpb;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private void Start()
        {
            _mpb = new MaterialPropertyBlock();
            if (glow == null) glow = GetComponent<Renderer>();
        }

        public void Press(HandContactEvent contact)
        {
            if (door == null || Time.time < _next) return;
            _next = Time.time + cooldown;
            door.Toggle();
            AudioSource.PlayClipAtPoint(ProceduralAudio.Click(), transform.position, 0.9f);
            NoiseBus.Emit(transform.position, 0.2f);
            HapticFeedback.Pulse(0.35f, 0.05f);
            if (cap != null) StartCoroutine(Depress());
        }

        private void Update()
        {
            if (door == null || glow == null) return;
            int state = door.locked ? 0 : door.Current == IndustrialDoor.State.Open ? 1 : 2;
            bool moving = door.Current == IndustrialDoor.State.Warning || door.Current == IndustrialDoor.State.Closing || door.Current == IndustrialDoor.State.Opening;
            if (!moving && state == _shown) return;
            _shown = moving ? -2 : state;

            Color c = door.locked ? new Color(1f, 0.05f, 0.03f)
                : state == 1 ? new Color(0.1f, 1f, 0.25f)
                : new Color(1f, 0.6f, 0.05f);
            float k = moving ? (Mathf.Sin(Time.time * 18f) > 0f ? 3f : 0.4f) : 2.5f;
            _mpb.SetColor(EmissionId, c * k);
            glow.SetPropertyBlock(_mpb);
        }

        private IEnumerator Depress()
        {
            Vector3 rest = cap.position;
            cap.position = rest + pressDirection.normalized * 0.025f;
            yield return new WaitForSeconds(0.15f);
            cap.position = rest;
        }
    }
}
