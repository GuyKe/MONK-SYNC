using UnityEngine;

namespace Monk.Locomotion
{
    [CreateAssetMenu(menuName = "Monk/Locomotion Profile", fileName = "LocomotionProfile")]
    public class LocomotionProfile : ScriptableObject
    {
        [Header("Hands")]
        [Tooltip("Max distance a virtual hand may be from the head (m).")]
        public float maxArmLength = 1.5f;
        public float handRadius = 0.06f;
        [Tooltip("Gap the hand keeps from a surface it is blocked by (m).")]
        public float handSkin = 0.005f;

        [Header("Surfaces")]
        [Range(0f, 1f), Tooltip("Slip used when a surface has no SurfaceProperties. 0 = full traction, 1 = ice.")]
        public float defaultSlip = 0.03f;

        [Header("Arm-span scaling")]
        [Tooltip("Arm span (controller to controller, T-pose) the tuning is designed around (m).")]
        public float referenceArmSpan = 1.6f;
        public float minArmScale = 0.7f;
        public float maxArmScale = 1.5f;

        [Header("Body")]
        public float bodyRadius = 0.2f;
        [Tooltip("Body sphere centre relative to the head (m, world axes).")]
        public Vector3 bodyOffset = new Vector3(0f, -0.1f, 0f);
        public float bodySkin = 0.01f;

        [Header("Momentum")]
        [Tooltip("Multiplier on the arm velocity that becomes body velocity.")]
        public float jumpMultiplier = 1.1f;
        public float maxJumpSpeed = 6.5f;
        [Tooltip("Body speed (m/s) below which a touch does not launch you. Keeps resting contact calm.")]
        public float velocityLimit = 0.3f;
        public int velocityHistorySize = 8;
        public float gravity = 9.81f;
        [Tooltip("Air drag, 1/s.")]
        public float drag = 0.05f;
        [Tooltip("Hard cap on how fast hands can shove the body (m/s).")]
        public float maxPushSpeed = 12f;
    }
}
