using Monk.Audio;
using Monk.Gameplay;
using Monk.Locomotion;
using Monk.World;
using UnityEngine;

namespace Monk.Hub
{
    /// <summary>A heavy lever on the gamemode console.</summary>
    public class ConsoleLever : MonoBehaviour, IHandPressable
    {
        public enum Action { MuteAlarm, PrepTime }

        public GameModeConsole console;
        public Action action;
        public Transform pivot;
        public float throwAngle = 38f;
        public float cooldown = 0.6f;

        private bool _flipped;
        private float _next;

        private void Start() => Apply();

        public void Press(HandContactEvent contact)
        {
            if (console == null || Time.time < _next) return;
            _next = Time.time + cooldown;
            _flipped = !_flipped;
            if (action == Action.MuteAlarm) console.SetAlarmMuted(_flipped);
            else console.CyclePrepTime();

            Apply();
            AudioSource.PlayClipAtPoint(ProceduralAudio.Clank(), transform.position, 0.4f);
            HapticFeedback.Pulse(0.45f, 0.1f);
        }

        private void Apply()
        {
            if (pivot != null) pivot.localRotation = Quaternion.Euler(_flipped ? throwAngle : -throwAngle, 0f, 0f);
        }
    }
}
