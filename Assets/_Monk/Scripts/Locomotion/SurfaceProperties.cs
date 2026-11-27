using UnityEngine;

namespace Monk.Locomotion
{
    /// <summary>Optional per-surface tuning. Put on a collider (or a parent of it).</summary>
    public class SurfaceProperties : MonoBehaviour
    {
        [Range(0f, 1f), Tooltip("0 = hands cannot grip, 1 = full grip.")]
        public float grip = 1f;

        [Range(0f, 2f), Tooltip("Multiplier on the noise a hand slap makes (read by AI later).")]
        public float noise = 1f;
    }
}
