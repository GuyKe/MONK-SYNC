using System.Collections.Generic;
using UnityEngine;

namespace Monk.AI
{
    /// <summary>Tight doorways and door frames: big monsters lose speed inside, agile players do not.</summary>
    public class MonsterSlowZone : MonoBehaviour
    {
        public static readonly List<MonsterSlowZone> All = new List<MonsterSlowZone>();

        public Bounds bounds;
        [Range(0.1f, 1f)] public float speedMultiplier = 0.5f;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public static float MultiplierAt(Vector3 p)
        {
            float m = 1f;
            foreach (var z in All)
                if (z.bounds.Contains(p)) m = Mathf.Min(m, z.speedMultiplier);
            return m;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.2f);
            Gizmos.DrawCube(bounds.center, bounds.size);
        }
    }
}
