using UnityEngine;

namespace Monk.World
{
    /// <summary>
    /// The map is big, so it is built in blocks. Only blocks near the player stay active (renderers, colliders,
    /// lights, doors); the fog hides the edge. Blocks far away cost nothing.
    /// </summary>
    public class ChunkVisibility : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Blocks whose centre is closer than this (plus half a block) are active (m).")]
        public float radius = 56f;
        public float hysteresis = 8f;
        public float blockSize = 24f;
        public GameObject[] blocks;
        public Vector3[] centers;

        private bool[] _on;
        private float _timer;

        private void Start()
        {
            _on = new bool[blocks.Length];
            for (int i = 0; i < blocks.Length; i++) _on[i] = blocks[i] != null && blocks[i].activeSelf;
            Refresh(true);
        }

        private void Update()
        {
            if (target == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                target = cam.transform;
            }
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 0.25f;
            Refresh(false);
        }

        private void Refresh(bool force)
        {
            Transform t = target != null ? target : (Camera.main != null ? Camera.main.transform : null);
            if (t == null) return;
            Vector2 p = new Vector2(t.position.x, t.position.z);
            float reach = blockSize * 0.71f;
            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i] == null) continue;
                float d = Vector2.Distance(p, new Vector2(centers[i].x, centers[i].z));
                bool want = _on[i] ? d < radius + hysteresis + reach : d < radius + reach;
                if (force || want != _on[i])
                {
                    _on[i] = want;
                    blocks[i].SetActive(want);
                }
            }
        }
    }
}
