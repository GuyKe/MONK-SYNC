using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;

namespace Monk.World
{
    /// <summary>Bakes the NavMesh when the map loads, then wakes the monsters (which need a NavMesh to stand on).</summary>
    public class MapRuntime : MonoBehaviour
    {
        public NavMeshSurface surface;
        public GameObject[] monsters;

        private IEnumerator Start()
        {
            yield return null; // let door panels and colliders settle
            if (surface != null) surface.BuildNavMesh();
            foreach (var m in monsters)
                if (m != null) m.SetActive(true);
        }
    }
}
