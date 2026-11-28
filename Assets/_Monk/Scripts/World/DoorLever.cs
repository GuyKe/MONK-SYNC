using Monk.Audio;
using Monk.Gameplay;
using Monk.Locomotion;
using UnityEngine;

namespace Monk.World
{
    /// <summary>
    /// Lever switch: slap it to flip it.
    /// On an automatic door it switches between automatic cycling (up) and locked open (down).
    /// On a manual door it simply toggles the door.
    /// </summary>
    public class DoorLever : MonoBehaviour, IHandPressable
    {
        public IndustrialDoor door;
        public Transform pivot;
        public float throwAngle = 40f;
        public float cooldown = 0.6f;

        private bool _flipped;
        private bool _doorIsAutomatic;
        private float _next;

        private void Start()
        {
            if (door != null) _doorIsAutomatic = door.automatic;
            Apply();
        }

        public void Press(HandContactEvent contact)
        {
            if (door == null || Time.time < _next) return;
            _next = Time.time + cooldown;
            _flipped = !_flipped;

            if (_doorIsAutomatic)
            {
                door.automatic = !_flipped;
                if (_flipped) door.ForceOpen();
            }
            else
            {
                door.Toggle();
            }

            Apply();
            AudioSource.PlayClipAtPoint(ProceduralAudio.Clank(), transform.position, 0.35f);
            NoiseBus.Emit(transform.position, 0.3f);
        }

        private void Apply()
        {
            if (pivot != null) pivot.localRotation = Quaternion.Euler(_flipped ? throwAngle : -throwAngle, 0f, 0f);
        }
    }
}
