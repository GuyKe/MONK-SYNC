using System;
using UnityEngine;
using UnityEngine.XR;

namespace Monk.Locomotion
{
    public enum HandSide { Left, Right }

    public struct HandContactEvent
    {
        public HandSide side;
        public Vector3 point;
        public Vector3 normal;
        public float speed;          // hand speed at impact (m/s)
        public SurfaceProperties surface; // may be null
    }

    /// <summary>
    /// Arm locomotion. The body moves opposite to hands that are pressed against surfaces,
    /// and is launched with the averaged body velocity when the last hand lets go.
    /// Put this on the rig root (the transform that parents head and controllers).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ArmLocomotion : MonoBehaviour
    {
        [Header("References")]
        public LocomotionProfile profile;
        public Transform head;
        public Transform leftController;
        public Transform rightController;
        [Tooltip("Optional visuals; they are pinned to the surface while a hand is stuck.")]
        public Transform leftVisual;
        public Transform rightVisual;
        [Tooltip("SphereCollider on a child object (layer excluded from surfaceMask). Moved to the body centre each step.")]
        public SphereCollider bodyCollider;
        public LayerMask surfaceMask = ~(1 << 2); // everything except Ignore Raycast

        public event Action<HandContactEvent> HandContacted;

        public Vector3 Velocity { get; private set; }
        public bool IsHandStuck(HandSide s) => _hands[(int)s].stuck;
        public Vector3 HandAnchor(HandSide s) => _hands[(int)s].anchor;

        private sealed class Hand
        {
            public HandSide side;
            public Transform controller;
            public Transform visual;
            public XRNode node;
            public bool stuck;
            public Vector3 anchor;      // sphere centre while stuck
            public Vector3 normal;
            public Vector3 lastFree;    // last un-stuck hand position
            public float grip = 1f;
        }

        private Hand[] _hands;
        private VelocityHistory _history;
        private bool _wasPushing;
        private readonly Collider[] _overlaps = new Collider[16];

        private void Awake()
        {
            _hands = new[]
            {
                new Hand { side = HandSide.Left, controller = leftController, visual = leftVisual, node = XRNode.LeftHand },
                new Hand { side = HandSide.Right, controller = rightController, visual = rightVisual, node = XRNode.RightHand },
            };
            _history = new VelocityHistory(profile.velocityHistorySize);
        }

        private void Start()
        {
            foreach (var h in _hands) h.lastFree = ArmTarget(h);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            SyncBodyCollider();
            ResolveBodyPenetration();

            Vector3 pushSum = Vector3.zero;
            int pushing = 0;
            foreach (var h in _hands)
            {
                if (UpdateHand(h, dt, out Vector3 delta))
                {
                    pushSum += delta;
                    pushing++;
                }
            }

            if (pushing > 0)
            {
                Vector3 move = pushSum / pushing;
                float cap = profile.maxPushSpeed * dt;
                if (move.sqrMagnitude > cap * cap) move = move.normalized * cap;

                Vector3 before = BodyCenter();
                MoveBody(move, out _);
                Vector3 actual = BodyCenter() - before;

                _history.Push(actual / dt);
                Velocity = Vector3.zero;
                _wasPushing = true;
            }
            else
            {
                if (_wasPushing)
                {
                    Velocity = Vector3.ClampMagnitude(_history.Average() * profile.jumpMultiplier, profile.maxJumpSpeed);
                    _history.Clear();
                    _wasPushing = false;
                }

                Velocity += Vector3.down * (profile.gravity * dt);
                Velocity *= Mathf.Max(0f, 1f - profile.drag * dt);

                MoveBody(Velocity * dt, out Vector3 hitNormal);
                if (hitNormal != Vector3.zero && Vector3.Dot(Velocity, hitNormal) < 0f)
                    Velocity = Vector3.ProjectOnPlane(Velocity, hitNormal);
            }
        }

        private void LateUpdate()
        {
            foreach (var h in _hands)
            {
                if (h.visual == null || h.controller == null) continue;
                h.visual.position = h.stuck ? h.anchor : h.controller.position;
                h.visual.rotation = h.controller.rotation;
            }
        }

        // ---------------------------------------------------------------- hands

        private Vector3 ArmTarget(Hand h)
        {
            Vector3 p = h.controller.position;
            Vector3 fromHead = p - head.position;
            return head.position + Vector3.ClampMagnitude(fromHead, profile.maxArmLength);
        }

        /// <summary>Returns true if this hand is pushing the body; delta is the body displacement it asks for.</summary>
        private bool UpdateHand(Hand h, float dt, out Vector3 delta)
        {
            delta = Vector3.zero;
            if (h.controller == null) return false;

            Vector3 target = ArmTarget(h);

            if (h.stuck)
            {
                Vector3 toTarget = target - h.anchor;
                bool offSurface = Vector3.Dot(toTarget, h.normal) > profile.releaseSlack;
                bool tooFar = toTarget.magnitude > profile.unstickDistance;
                if (offSurface || tooFar)
                {
                    h.stuck = false;
                    h.lastFree = target;
                    SendHaptic(h.node, 0.1f, 0.03f);
                    return false;
                }

                delta = -toTarget * h.grip;
                return true;
            }

            // Free hand: look for a surface between last free position and target.
            Vector3 from = h.lastFree;
            Vector3 move = target - from;
            float dist = move.magnitude;
            float r = profile.handRadius;

            if (dist > 1e-4f &&
                Physics.SphereCast(from, r, move / dist, out RaycastHit hit, dist + profile.handSkin,
                    surfaceMask, QueryTriggerInteraction.Ignore))
            {
                var surface = hit.collider.GetComponentInParent<SurfaceProperties>();
                float grip = surface != null ? surface.grip : 1f;
                if (grip > 0f)
                {
                    h.stuck = true;
                    h.grip = grip;
                    h.normal = hit.normal;
                    h.anchor = from + (move / dist) * hit.distance + hit.normal * profile.handSkin;

                    float speed = dist / dt;
                    HandContacted?.Invoke(new HandContactEvent
                    {
                        side = h.side, point = hit.point, normal = hit.normal, speed = speed, surface = surface,
                    });
                    SendHaptic(h.node, Mathf.Clamp01(speed / 4f), 0.06f);

                    delta = -(target - h.anchor) * grip;
                    return true;
                }
            }

            h.lastFree = target;
            return false;
        }

        private static void SendHaptic(XRNode node, float amplitude, float duration)
        {
            var d = InputDevices.GetDeviceAtXRNode(node);
            if (d.isValid) d.SendHapticImpulse(0, amplitude, duration);
        }

        // ----------------------------------------------------------------- body

        private Vector3 BodyCenter() => head.position + profile.bodyOffset;

        private void SyncBodyCollider()
        {
            if (bodyCollider == null) return;
            bodyCollider.radius = profile.bodyRadius;
            bodyCollider.transform.position = BodyCenter();
            bodyCollider.center = Vector3.zero;
        }

        /// <summary>Move the rig by delta, sliding along surfaces. Reports the last hit normal.</summary>
        private void MoveBody(Vector3 delta, out Vector3 lastNormal)
        {
            lastNormal = Vector3.zero;
            Vector3 remaining = delta;
            float r = profile.bodyRadius;

            for (int i = 0; i < 3 && remaining.sqrMagnitude > 1e-8f; i++)
            {
                float dist = remaining.magnitude;
                Vector3 dir = remaining / dist;
                if (Physics.SphereCast(BodyCenter(), r, dir, out RaycastHit hit, dist + profile.bodySkin,
                        surfaceMask, QueryTriggerInteraction.Ignore))
                {
                    float travel = Mathf.Max(0f, hit.distance - profile.bodySkin);
                    transform.position += dir * Mathf.Min(travel, dist);
                    remaining = Vector3.ProjectOnPlane(dir * (dist - travel), hit.normal);
                    lastNormal = hit.normal;
                }
                else
                {
                    transform.position += remaining;
                    return;
                }
            }
        }

        /// <summary>Pushes the rig out of geometry the head walked into (room-scale leaning).</summary>
        private void ResolveBodyPenetration()
        {
            if (bodyCollider == null) return;
            Vector3 c = BodyCenter();
            int n = Physics.OverlapSphereNonAlloc(c, profile.bodyRadius, _overlaps, surfaceMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var other = _overlaps[i];
                if (other == bodyCollider) continue;
                if (Physics.ComputePenetration(bodyCollider, c, Quaternion.identity,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 dir, out float d))
                {
                    transform.position += dir * (d + 0.001f);
                    c = BodyCenter();
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (profile == null || head == null || _hands == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(BodyCenter(), profile.bodyRadius);
            foreach (var h in _hands)
            {
                Gizmos.color = h.stuck ? Color.green : Color.yellow;
                Gizmos.DrawWireSphere(h.stuck ? h.anchor : h.lastFree, profile.handRadius);
            }
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(BodyCenter(), Velocity * 0.25f);
        }
    }
}
