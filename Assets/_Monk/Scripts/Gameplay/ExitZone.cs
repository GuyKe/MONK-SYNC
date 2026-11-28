using System;
using UnityEngine;

namespace Monk.Gameplay
{
    /// <summary>The noclip seam. Reaching it ends the level.</summary>
    public class ExitZone : MonoBehaviour
    {
        public Bounds bounds;
        public event Action Reached;
        private bool _done;

        private void Update()
        {
            if (_done || PlayerHealth.Instance == null) return;
            if (!bounds.Contains(PlayerHealth.Instance.BodyPosition)) return;
            _done = true;
            Debug.Log("[Exit] The player reached the exit seam.");
            Reached?.Invoke();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 1f, 0.4f, 0.3f);
            Gizmos.DrawCube(bounds.center, bounds.size);
        }
    }
}
