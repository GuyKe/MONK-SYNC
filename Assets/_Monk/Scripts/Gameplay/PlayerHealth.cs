using System;
using Monk.Locomotion;
using UnityEngine;

namespace Monk.Gameplay
{
    /// <summary>
    /// Health, hazards and respawn for the player rig. Anything that hurts the player calls Damage().
    /// "Eliminated" means health reached 0: the player is returned to the last checkpoint.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        public ArmLocomotion locomotion;
        public float maxHealth = 100f;
        [Tooltip("Seconds of invulnerability after respawning.")]
        public float respawnGrace = 1.5f;
        [Tooltip("Head position to respawn at until a checkpoint is reached.")]
        public Vector3 startCheckpoint;

        public float Health { get; private set; }
        public event Action<string> Died;

        private Vector3 _checkpointHead;
        private bool _hasCheckpoint;
        private float _grace;
        private float _scanTimer;

        public Vector3 BodyPosition => locomotion.BodyPosition;
        public float BodyRadius => locomotion.profile.bodyRadius;
        public Transform Head => locomotion.head;

        private void Awake()
        {
            Instance = this;
            Health = maxHealth;
            if (startCheckpoint != Vector3.zero) SetCheckpoint(startCheckpoint);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetCheckpoint(Vector3 headPosition)
        {
            _checkpointHead = headPosition;
            _hasCheckpoint = true;
        }

        /// <summary>True if the body sphere touches the box (used for door crushing).</summary>
        public bool Overlaps(Bounds b) => b.SqrDistance(BodyPosition) <= BodyRadius * BodyRadius;

        public void Damage(float amount, string cause)
        {
            if (_grace > 0f || amount <= 0f) return;
            Health -= amount;
            if (Health <= 0f) Die(cause);
        }

        private void Die(string cause)
        {
            Debug.Log($"[Player] Eliminated: {cause}");
            Died?.Invoke(cause);
            if (_hasCheckpoint) locomotion.Teleport(_checkpointHead);
            Health = maxHealth;
            _grace = respawnGrace;
        }

        private void Update()
        {
            if (_grace > 0f) _grace -= Time.deltaTime;

            Vector3 p = BodyPosition;
            foreach (var z in HazardZone.All)
                if (z.bounds.Contains(p)) Damage(z.damage, z.cause);

            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = 0.2f;
                foreach (var c in CheckpointZone.All)
                    if (c.bounds.Contains(p)) SetCheckpoint(c.spawn);
            }
        }
    }
}
