using UnityEngine;
using UnityEngine.XR;

namespace Monk.Locomotion
{
    /// <summary>
    /// Calibrates ArmLocomotion to the player's real arm span so reach and leverage are consistent
    /// regardless of body size. Hold both grips with arms stretched out to the sides for the hold time.
    /// The result is saved and re-applied on the next launch.
    /// </summary>
    public class ArmSpanCalibrator : MonoBehaviour
    {
        public ArmLocomotion locomotion;
        public float holdSeconds = 1.5f;
        [Tooltip("Controllers must be at least this far apart (m) to count as a T-pose.")]
        public float minSpan = 0.8f;
        public bool loadSavedOnStart = true;

        private const string PrefKey = "monk.armSpan";
        private float _held;

        public float MeasuredSpan { get; private set; }

        private void Start()
        {
            if (loadSavedOnStart && PlayerPrefs.HasKey(PrefKey)) Apply(PlayerPrefs.GetFloat(PrefKey));
        }

        private void Update()
        {
            if (locomotion == null || locomotion.leftController == null) return;

            bool gripping = Grip(XRNode.LeftHand) && Grip(XRNode.RightHand);
            float span = Vector3.Distance(locomotion.leftController.position, locomotion.rightController.position);

            if (gripping && span >= minSpan)
            {
                _held += Time.deltaTime;
                if (_held >= holdSeconds)
                {
                    _held = 0f;
                    Calibrate(span);
                }
            }
            else _held = 0f;
        }

        /// <summary>Apply a measured controller-to-controller span (m) and save it.</summary>
        public void Calibrate(float measuredSpan)
        {
            Apply(measuredSpan);
            PlayerPrefs.SetFloat(PrefKey, measuredSpan);
            PlayerPrefs.Save();
        }

        private void Apply(float measuredSpan)
        {
            MeasuredSpan = measuredSpan;
            // A short span means short arms: scale hand offsets up so the virtual reach matches the reference.
            locomotion.SetArmScale(locomotion.profile.referenceArmSpan / measuredSpan);
        }

        private static bool Grip(XRNode node)
        {
            var d = InputDevices.GetDeviceAtXRNode(node);
            return d.isValid && d.TryGetFeatureValue(CommonUsages.gripButton, out bool v) && v;
        }
    }
}
