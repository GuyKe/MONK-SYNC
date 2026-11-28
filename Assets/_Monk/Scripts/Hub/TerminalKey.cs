using System.Collections;
using Monk.Audio;
using Monk.Gameplay;
using Monk.Locomotion;
using Monk.World;
using UnityEngine;

namespace Monk.Hub
{
    /// <summary>One physical key on the terminal keyboard. Tap it with a hand.</summary>
    public class TerminalKey : MonoBehaviour, IHandPressable
    {
        public enum Special { None, Backspace, Enter, Space, Stats }

        public TerminalScreen screen;
        public char character;
        public Special special;
        public Transform cap;
        public float cooldown = 0.12f;

        private float _next;

        public void Press(HandContactEvent contact)
        {
            if (screen == null || Time.time < _next) return;
            _next = Time.time + cooldown;

            switch (special)
            {
                case Special.Backspace: screen.Backspace(); break;
                case Special.Enter: screen.Enter(); break;
                case Special.Space: screen.TypeChar(' '); break;
                case Special.Stats: screen.TogglePage(); break;
                default: screen.TypeChar(character); break;
            }
            AudioSource.PlayClipAtPoint(ProceduralAudio.Click(), transform.position, 0.5f);
            HapticFeedback.Pulse(0.2f, 0.03f);
            if (cap != null) StartCoroutine(Depress());
        }

        private IEnumerator Depress()
        {
            Vector3 rest = cap.localPosition;
            cap.localPosition = rest + Vector3.down * 0.012f;
            yield return new WaitForSeconds(0.1f);
            cap.localPosition = rest;
        }
    }
}
