using UnityEngine;

namespace Monk.Locomotion
{
    [CreateAssetMenu(menuName = "Monk/Locomotion Profile", fileName = "LocomotionProfile")]
    public class LocomotionProfile : ScriptableObject
    {
        [Header("Arms")]
        [Tooltip("Max distance a hand target may be from the head (m).")]
        public float maxArmLength = 1.5f;
        public float handRadius = 0.07f;
        [Tooltip("Gap kept between hand sphere and surface (m).")]
        public float handSkin = 0.01f;
        [Tooltip("Hand releases if its target is this far from the anchor (m).")]
        public float unstickDistance = 1.0f;
        [Tooltip("Hand releases once its target moves this far off the surface along the normal (m).")]
        public float releaseSlack = 0.02f;

        [Header("Surfaces")]
        [Range(0f, 1f), Tooltip("Slip used when a surface has no SurfaceProperties.")]
        public float defaultSlip = 0.03f;

        [Header("Arm-span scaling")]
        [Tooltip("Arm span (controller to controller, T-pose) the tuning is designed around (m).")]
        public float referenceArmSpan = 1.6f;
        public float minArmScale = 0.7f;
        public float maxArmScale = 1.5f;

        [Header("Body")]
        public float bodyRadius = 0.2f;
        [Tooltip("Body sphere centre relative to the head, in world space (m).")]
        public Vector3 bodyOffset = new Vector3(0f, -0.25f, 0f);
        public float bodySkin = 0.01f;

        [Header("Launch / flight")]
        public float jumpMultiplier = 1.2f;
        public float maxJumpSpeed = 6.5f;
        public int velocityHistorySize = 8;
        public float gravity = 9.81f;
        [Tooltip("Air drag, 1/s.")]
        public float drag = 0.1f;
        [Tooltip("Hard cap on body speed while hands are pushing (m/s).")]
        public float maxPushSpeed = 12f;
    }
}
