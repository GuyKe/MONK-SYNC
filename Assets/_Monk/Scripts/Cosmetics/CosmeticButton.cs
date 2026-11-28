using System.Collections;
using Monk.Audio;
using Monk.Gameplay;
using Monk.Locomotion;
using Monk.World;
using UnityEngine;

namespace Monk.Cosmetics
{
    /// <summary>A wardrobe button: step through a slot's options, or jump straight to one (suit colour swatches).</summary>
    public class CosmeticButton : MonoBehaviour, IHandPressable
    {
        public AvatarCosmetics cosmetics;
        public CosmeticSlot slot;
        public int step = 1;
        public bool direct;
        public int value;
        public Transform cap;
        public Vector3 pressDirection = Vector3.back;
        public float cooldown = 0.25f;

        private float _next;

        public void Press(HandContactEvent contact)
        {
            if (cosmetics == null || Time.time < _next) return;
            _next = Time.time + cooldown;
            if (direct) cosmetics.Set(slot, value);
            else cosmetics.Cycle(slot, step);
            AudioSource.PlayClipAtPoint(ProceduralAudio.Click(), transform.position, 0.8f);
            HapticFeedback.Pulse(0.3f, 0.05f);
            if (cap != null) StartCoroutine(Depress());
        }

        private IEnumerator Depress()
        {
            Vector3 rest = cap.position;
            cap.position = rest + pressDirection.normalized * 0.02f;
            yield return new WaitForSeconds(0.12f);
            cap.position = rest;
        }
    }
}
