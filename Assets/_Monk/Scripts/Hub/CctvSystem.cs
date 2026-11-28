using Monk.World;
using UnityEngine;

namespace Monk.Hub
{
    /// <summary>
    /// Renders the security cameras one at a time, only while you are near the monitor bank, and keeps the parts of
    /// the maze they look at switched on.
    /// </summary>
    public class CctvSystem : MonoBehaviour
    {
        public CctvCamera[] cameras;
        public Transform monitorCentre;
        public ChunkVisibility chunks;
        public float activeDistance = 28f;
        public int framesPerCamera = 2;

        private int _index, _frame;
        private bool _active;

        private void Start()
        {
            if (chunks == null) return;
            foreach (var c in cameras)
                if (c != null) chunks.foci.Add(c.cam.transform);
        }

        private void Update()
        {
            var main = Camera.main;
            bool near = main != null && Vector3.Distance(main.transform.position, monitorCentre.position) < activeDistance;
            if (near != _active)
            {
                _active = near;
                if (chunks != null) chunks.focusActive = near;
                if (!near) foreach (var c in cameras) if (c != null) c.SetRendering(false);
            }
            if (!_active || cameras.Length == 0) return;

            if (++_frame % framesPerCamera != 0) return;
            cameras[_index].SetRendering(false);
            _index = (_index + 1) % cameras.Length;
            cameras[_index].SetRendering(true);
        }
    }
}
