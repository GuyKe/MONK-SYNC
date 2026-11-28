using UnityEngine;

namespace Monk.Locomotion
{
    /// <summary>
    /// Legless avatar: a torso hanging under the head and two arms from shoulders to the (surface-pinned) hand visuals.
    /// Purely visual. There is no walking input anywhere in the project: movement comes only from hand contact.
    /// </summary>
    public class UpperBodyAvatar : MonoBehaviour
    {
        public ArmLocomotion locomotion;
        public Transform torso;            // any mesh, e.g. a scaled capsule. Optional.
        public LineRenderer leftArm;
        public LineRenderer rightArm;
        public float shoulderWidth = 0.38f;
        public float shoulderDrop = 0.22f;
        public float torsoDrop = 0.45f;

        private void LateUpdate()
        {
            if (locomotion == null || locomotion.head == null) return;
            Transform head = locomotion.head;

            // Follow head yaw only, so the torso stays upright.
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(head.up, Vector3.up);
            Quaternion yaw = Quaternion.LookRotation(fwd.normalized, Vector3.up);

            Vector3 neck = head.position + Vector3.down * shoulderDrop;
            if (torso != null)
            {
                torso.SetPositionAndRotation(head.position + Vector3.down * torsoDrop, yaw);
            }
            Draw(leftArm, neck + yaw * Vector3.left * shoulderWidth * 0.5f, locomotion.leftVisual);
            Draw(rightArm, neck + yaw * Vector3.right * shoulderWidth * 0.5f, locomotion.rightVisual);
        }

        private static void Draw(LineRenderer lr, Vector3 shoulder, Transform hand)
        {
            if (lr == null || hand == null) return;
            lr.positionCount = 2;
            lr.SetPosition(0, shoulder);
            lr.SetPosition(1, hand.position);
        }
    }
}
