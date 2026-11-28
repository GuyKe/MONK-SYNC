using System.Collections.Generic;
using UnityEngine;

namespace Monk.Gameplay
{
    /// <summary>World-space box that damages the player's body while inside it (pit bottoms etc.).</summary>
    public class HazardZone : MonoBehaviour
    {
        public static readonly List<HazardZone> All = new List<HazardZone>();

        public Bounds bounds;
        public float damage = 100f;
        public string cause = "Fell";

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.35f);
            Gizmos.DrawCube(bounds.center, bounds.size);
        }
    }
}
