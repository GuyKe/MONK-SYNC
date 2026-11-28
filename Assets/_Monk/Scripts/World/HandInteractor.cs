using Monk.Locomotion;
using UnityEngine;

namespace Monk.World
{
    /// <summary>Forwards hand contacts to whatever IHandPressable the hand touched.</summary>
    public class HandInteractor : MonoBehaviour
    {
        public ArmLocomotion locomotion;

        private void OnEnable()
        {
            if (locomotion != null) locomotion.HandContacted += OnContact;
        }

        private void OnDisable()
        {
            if (locomotion != null) locomotion.HandContacted -= OnContact;
        }

        private void OnContact(HandContactEvent e)
        {
            if (e.collider == null) return;
            var pressable = e.collider.GetComponentInParent<IHandPressable>();
            pressable?.Press(e);
        }
    }
}
