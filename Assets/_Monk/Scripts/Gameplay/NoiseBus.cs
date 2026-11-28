using System;
using UnityEngine;

namespace Monk.Gameplay
{
    /// <summary>Global noise channel. Hand slaps and door slams emit here; monsters listen.</summary>
    public static class NoiseBus
    {
        /// <summary>position, loudness 0..1</summary>
        public static event Action<Vector3, float> Emitted;

        public static void Emit(Vector3 position, float loudness) => Emitted?.Invoke(position, Mathf.Clamp01(loudness));
    }
}
