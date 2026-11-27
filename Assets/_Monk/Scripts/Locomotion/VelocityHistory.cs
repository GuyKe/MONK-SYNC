using UnityEngine;

namespace Monk.Locomotion
{
    /// <summary>Fixed-size ring buffer of velocity samples. Pure data, no Unity lifecycle.</summary>
    public sealed class VelocityHistory
    {
        private readonly Vector3[] _samples;
        private int _next;
        private int _count;

        public VelocityHistory(int capacity)
        {
            _samples = new Vector3[Mathf.Max(1, capacity)];
        }

        public int Count => _count;

        public void Push(Vector3 v)
        {
            _samples[_next] = v;
            _next = (_next + 1) % _samples.Length;
            if (_count < _samples.Length) _count++;
        }

        public Vector3 Average()
        {
            if (_count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < _count; i++) sum += _samples[i];
            return sum / _count;
        }

        public void Clear()
        {
            _next = 0;
            _count = 0;
        }
    }
}
