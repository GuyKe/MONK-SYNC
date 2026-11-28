using System.Collections;
using Monk.Audio;
using Monk.Gameplay;
using Monk.Locomotion;
using UnityEngine;

namespace Monk.World
{
    /// <summary>Push-button: slap it with a hand to toggle its door.</summary>
    public class DoorButton : MonoBehaviour, IHandPressable
    {
        public IndustrialDoor door;
        public Transform cap;
        public float cooldown = 0.7f;
        [Tooltip("World direction the cap moves when pressed (into the wall).")]
        public Vector3 pressDirection = Vector3.back;

        private float _next;

        public void Press(HandContactEvent contact)
        {
            if (door == null || Time.time < _next) return;
            _next = Time.time + cooldown;
            door.Toggle();
            AudioSource.PlayClipAtPoint(ProceduralAudio.Click(), transform.position, 0.9f);
            NoiseBus.Emit(transform.position, 0.2f);
            if (cap != null) StartCoroutine(Depress());
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
