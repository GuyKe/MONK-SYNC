using UnityEngine;

namespace Monk.Cosmetics
{
    /// <summary>A rotating, bobbing hologram of an item over a pedestal.</summary>
    public class CosmeticHologram : MonoBehaviour
    {
        public Transform model;
        public float spinSpeed = 40f;
        public float bobHeight = 0.04f;

        private Vector3 _rest;

        private void Start()
        {
            if (model != null) _rest = model.localPosition;
        }

        private void Update()
        {
            if (model == null) return;
            model.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            model.localPosition = _rest + Vector3.up * (Mathf.Sin(Time.time * 1.6f + transform.position.x) * bobHeight);
        }
    }
}
