using System.Collections.Generic;
using UnityEngine;

namespace Monk.Gameplay
{
    /// <summary>Entering the box sets the respawn point (head position) for the player.</summary>
    public class CheckpointZone : MonoBehaviour
    {
        public static readonly List<CheckpointZone> All = new List<CheckpointZone>();

        public Bounds bounds;
        public Vector3 spawn;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.25f);
            Gizmos.DrawCube(bounds.center, bounds.size);
        }
    }
}
