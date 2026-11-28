using System;
using System.Collections.Generic;
using UnityEngine;

namespace Monk.World
{
    /// <summary>
    /// Emergency power. A flipped fuse box restores every ceiling panel within a radius for a while,
    /// even the dead ones. Panels subscribe to Changed and re-check themselves.
    /// </summary>
    public static class PowerGrid
    {
        private struct Surge { public Vector3 centre; public float radius; public float until; }
        private static readonly List<Surge> Surges = new List<Surge>();

        public static event Action Changed;

        public static void Restore(Vector3 centre, float radius, float seconds)
        {
            Surges.Add(new Surge { centre = centre, radius = radius, until = Time.time + seconds });
            Changed?.Invoke();
        }

        public static bool IsRestored(Vector3 p)
        {
            float now = Time.time;
            foreach (var s in Surges)
                if (s.until > now && (p - s.centre).sqrMagnitude < s.radius * s.radius) return true;
            return false;
        }

        public static void Prune()
        {
            if (Surges.RemoveAll(s => s.until <= Time.time) > 0) Changed?.Invoke();
        }

        public static void Clear() => Surges.Clear();
    }

    /// <summary>Drives expiry of power surges. Lives on the map root.</summary>
    public class PowerGridTicker : MonoBehaviour
    {
        private float _t;
        private void Awake() => PowerGrid.Clear();
        private void OnDestroy() => PowerGrid.Clear();

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t < 0.5f) return;
            _t = 0f;
            PowerGrid.Prune();
        }
    }
}
