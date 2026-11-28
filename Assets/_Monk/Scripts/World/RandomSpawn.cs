using System.Collections;
using Monk.Gameplay;
using Monk.Locomotion;
using UnityEngine;

namespace Monk.World
{
    /// <summary>On start, moves the player to a random SpawnPoint and makes it the respawn point.</summary>
    public class RandomSpawn : MonoBehaviour
    {
        public ArmLocomotion locomotion;
        public PlayerHealth health;
        [Tooltip("-1 = random every time. 0.. = always use that start room (for testing).")]
        public int forcedIndex = -1;
        public float headHeight = 1.3f;

        private IEnumerator Start()
        {
            // Give the headset a couple of frames to report its pose before teleporting.
            yield return null;
            yield return null;

            var points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.InstanceID);
            if (points.Length == 0) yield break;

            var point = points[forcedIndex >= 0 ? Mathf.Min(forcedIndex, points.Length - 1) : Random.Range(0, points.Length)];
            Vector3 head = point.transform.position + Vector3.up * headHeight;
            locomotion.Teleport(head);
            if (health != null) health.SetCheckpoint(head);
            Debug.Log($"[Spawn] Starting in {point.name} ({points.Length} start rooms in the map).");
        }
    }
}
