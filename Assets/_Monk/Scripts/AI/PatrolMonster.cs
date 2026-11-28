using Monk.Audio;
using Monk.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace Monk.AI
{
    /// <summary>
    /// Large patrolling monster. Walks a loop of waypoints through the wide halls, investigates loud noises,
    /// and chases anything it can see. Because its NavMesh agent is wide, vents and narrow gaps are closed to it.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class PatrolMonster : MonoBehaviour
    {
        public enum State { Patrol, Investigate, Chase }

        public Transform[] waypoints;
        public float patrolSpeed = 2.2f;
        public float investigateSpeed = 3.2f;
        public float chaseSpeed = 4.6f;
        public float sightRange = 16f;
        [Range(10f, 180f)] public float sightHalfAngle = 70f;
        [Tooltip("A noise of loudness 1 is heard from this far away (m).")]
        public float hearingRange = 18f;
        public float catchDistance = 1.5f;
        public float loseSightSeconds = 4f;

        public State Current { get; private set; }

        private NavMeshAgent _agent;
        private AudioSource _steps;
        private int _wp;
        private float _wait, _lastSeen, _stuck, _stepTimer;
        private Vector3 _investigate;
        private Vector3 _lastPos;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _steps = gameObject.AddComponent<AudioSource>();
            _steps.spatialBlend = 1f;
            _steps.minDistance = 2f;
            _steps.maxDistance = 32f;
            _steps.rolloffMode = AudioRolloffMode.Linear;
            _steps.dopplerLevel = 0f;
            _steps.playOnAwake = false;
        }

        private void OnEnable()
        {
            NoiseBus.Emitted += OnNoise;
            _lastPos = transform.position;
            if (waypoints != null && waypoints.Length > 0) GoTo(waypoints[_wp].position, patrolSpeed);
        }

        private void OnDisable() => NoiseBus.Emitted -= OnNoise;

        private void OnNoise(Vector3 pos, float loudness)
        {
            if (Current == State.Chase) return;
            if (Vector3.Distance(pos, transform.position) > loudness * hearingRange) return;
            _investigate = pos;
            Current = State.Investigate;
            GoTo(pos, investigateSpeed);
        }

        private void GoTo(Vector3 p, float speed)
        {
            if (!_agent.isOnNavMesh) return;
            _agent.speed = speed * MonsterSlowZone.MultiplierAt(transform.position);
            if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) _agent.SetDestination(hit.position);
        }

        private void Update()
        {
            if (!_agent.isOnNavMesh) return;

            var player = PlayerHealth.Instance;
            bool sees = player != null && CanSee(player);
            if (sees)
            {
                Current = State.Chase;
                _lastSeen = Time.time;
            }
            else if (Current == State.Chase && Time.time - _lastSeen > loseSightSeconds)
            {
                Current = State.Investigate;
                _investigate = player != null ? player.BodyPosition : transform.position;
            }

            float baseSpeed = Current == State.Chase ? chaseSpeed : Current == State.Investigate ? investigateSpeed : patrolSpeed;
            _agent.speed = baseSpeed * MonsterSlowZone.MultiplierAt(transform.position);

            switch (Current)
            {
                case State.Chase:
                    if (player != null)
                    {
                        _agent.SetDestination(player.BodyPosition);
                        if (Vector3.Distance(transform.position, player.BodyPosition) < catchDistance)
                            player.Damage(100f, "Caught by a monster");
                    }
                    break;

                case State.Investigate:
                    if (Arrived()) { Current = State.Patrol; _wait = Random.Range(1f, 2.5f); }
                    else GoTo(_investigate, investigateSpeed);
                    break;

                default:
                    Patrol();
                    break;
            }

            Footsteps();
        }

        private void Patrol()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            if (_wait > 0f) { _wait -= Time.deltaTime; return; }

            // Give up on a waypoint that is unreachable (e.g. a closed door) or where progress has stalled.
            bool moving = (transform.position - _lastPos).sqrMagnitude > 0.0004f;
            _lastPos = transform.position;
            _stuck = moving ? 0f : _stuck + Time.deltaTime;

            if (Arrived() || _stuck > 3f)
            {
                _stuck = 0f;
                _wait = Random.Range(1f, 3f);
                _wp = (_wp + 1) % waypoints.Length;
                GoTo(waypoints[_wp].position, patrolSpeed);
            }
        }

        private bool Arrived() => !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.4f;

        private bool CanSee(PlayerHealth player)
        {
            Vector3 eye = transform.position + Vector3.up * 1.8f;
            Vector3 to = player.Head.position - eye;
            if (to.magnitude > sightRange) return false;
            if (Vector3.Angle(transform.forward, to) > sightHalfAngle) return false;
            return !Physics.Linecast(eye, player.Head.position, ~0, QueryTriggerInteraction.Ignore);
        }

        private void Footsteps()
        {
            float speed = _agent.velocity.magnitude;
            if (speed < 0.3f) return;
            _stepTimer -= Time.deltaTime;
            if (_stepTimer > 0f) return;
            _stepTimer = Mathf.Clamp(1.1f / speed, 0.22f, 0.9f);
            _steps.pitch = Random.Range(0.55f, 0.7f);
            _steps.PlayOneShot(Random.value > 0.5f ? ProceduralAudio.Squelch() : ProceduralAudio.Thud(), 1f);
        }
    }
}
