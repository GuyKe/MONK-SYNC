using UnityEngine;
using UnityEngine.XR;

namespace Monk.Gameplay
{
    /// <summary>Controller vibration helpers.</summary>
    public static class HapticFeedback
    {
        public static void Pulse(float amplitude, float seconds)
        {
            amplitude = Mathf.Clamp01(amplitude);
            foreach (var node in new[] { XRNode.LeftHand, XRNode.RightHand })
            {
                var d = InputDevices.GetDeviceAtXRNode(node);
                if (d.isValid) d.SendHapticImpulse(0, amplitude, seconds);
            }
        }

        /// <summary>A heavy door slammed at pos: both controllers rumble, softer the farther away you are.</summary>
        public static void Slam(Vector3 pos, float range = 25f)
        {
            var player = PlayerHealth.Instance;
            if (player == null) return;
            float d = Vector3.Distance(player.BodyPosition, pos);
            if (d > range) return;
            float k = 1f - d / range;
            Pulse(0.08f + k * 0.55f, Mathf.Lerp(0.08f, 0.4f, k));
        }
    }
}
