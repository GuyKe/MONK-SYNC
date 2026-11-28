using UnityEngine;

namespace Monk.World
{
    /// <summary>Marks a start room. The player appears at one of these, chosen at random, every time the game starts.</summary>
    public class SpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            Gizmos.DrawRay(transform.position, transform.forward);
        }
    }
}
