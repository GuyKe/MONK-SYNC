using System.Collections;
using Monk.Audio;
using Monk.Gameplay;
using Monk.Locomotion;
using Monk.World;
using UnityEngine;

namespace Monk.Hub
{
    /// <summary>A big tactile button on the console that selects a gamemode.</summary>
    public class GameModeButton : MonoBehaviour, IHandPressable
    {
        public GameMode mode;
        public Transform cap;
        public Vector3 pressDirection = Vector3.down;
        public float cooldown = 0.5f;

        private float _next;

        public void Press(HandContactEvent contact)
        {
            if (Time.time < _next) return;
            _next = Time.time + cooldown;
            GameModeState.Set(mode);
            AudioSource.PlayClipAtPoint(ProceduralAudio.Click(), transform.position, 1f);
            HapticFeedback.Pulse(0.5f, 0.07f);
            if (cap != null) StartCoroutine(Depress());
        }

        private IEnumerator Depress()
        {
            Vector3 rest = cap.position;
            cap.position = rest + pressDirection.normalized * 0.03f;
            yield return new WaitForSeconds(0.15f);
            cap.position = rest;
        }
    }
}
