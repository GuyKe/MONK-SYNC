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

        /// <summary>Virtual reach / real reach. Set by ArmSpanCalibrator. 1 = no scaling.</summary>
        public float ArmScale { get; private set; } = 1f;
        public void SetArmScale(float scale) => ArmScale = Mathf.Clamp(scale, profile.minArmScale, profile.maxArmScale);

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
            public Vector3 lastRel;     // hand offset from head last step (for per-step hand motion)
            public float slip;          // 0 = full traction, 1 = frictionless
        }

        private Hand[] _hands;
        private VelocityHistory _history;
        private bool _wasPushing;
        private float _blockedTime;
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
            foreach (var h in _hands)
            {
                h.lastFree = ArmTarget(h);
                h.lastRel = h.lastFree - head.position;
            }
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
                Vector3 push = pushSum / pushing;
                float cap = profile.maxPushSpeed * dt;
                if (push.sqrMagnitude > cap * cap) push = push.normalized * cap;

                // Gravity keeps acting while hands hold. Only hand motion (push) can resist it, so a relaxed
                // arm on a wall or floor does not hold you up.
                Velocity += Vector3.down * (profile.gravity * dt);
                if (push.sqrMagnitude > 1e-10f)
                {
                    Vector3 pushDir = push.normalized;
                    float against = Vector3.Dot(Velocity, pushDir);
                    if (against < 0f) Velocity -= pushDir * against;
                }
                Vector3 move = push + Velocity * dt;

                Vector3 before = BodyCenter();
                MoveBody(move, out Vector3 hitNormal);
                Vector3 actual = BodyCenter() - before;
                if (hitNormal != Vector3.zero && Vector3.Dot(Velocity, hitNormal) < 0f)
                    Velocity = Vector3.ProjectOnPlane(Velocity, hitNormal);

                // Blocked: hands ask for real movement but geometry stops the body. Let go so you can't get pinned.
                if (move.magnitude > 0.004f && actual.magnitude < move.magnitude * 0.25f) _blockedTime += dt;
                else _blockedTime = 0f;
                if (_blockedTime > profile.blockedReleaseTime)
                {
                    _blockedTime = 0f;
                    foreach (var h in _hands)
                    {
                        if (!h.stuck) continue;
                        h.stuck = false;
                        h.lastFree = ArmTarget(h);
                    }
                }

                _history.Push(actual / dt);
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
            // Scale the real hand offset so a short-armed and a long-armed player reach (and leverage) the same.
            Vector3 fromHead = (h.controller.position - head.position) * ArmScale;
            return head.position + Vector3.ClampMagnitude(fromHead, profile.maxArmLength);
        }

        /// <summary>Returns true if this hand is pushing the body; delta is the body displacement it asks for.</summary>
        private bool UpdateHand(Hand h, float dt, out Vector3 delta)
        {
            delta = Vector3.zero;
            if (h.controller == null) return false;

            Vector3 target = ArmTarget(h);
            Vector3 rel = target - head.position;
            Vector3 relDelta = rel - h.lastRel; // how the hand moved relative to the body this step
            h.lastRel = rel;

            if (h.stuck)
            {
                if (!TryHold(h, target, relDelta, out delta))
                {
                    h.stuck = false;
                    h.lastFree = target;
                    return false;
                }
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
                {
                    h.stuck = true;
                    h.slip = surface != null ? surface.slipPercentage : profile.defaultSlip;
                    h.normal = hit.normal;
                    h.anchor = from + (move / dist) * hit.distance + hit.normal * profile.handSkin;

                    float speed = dist / dt;
                    HandContacted?.Invoke(new HandContactEvent
                    {
                        side = h.side, point = hit.point, normal = hit.normal, speed = speed, surface = surface,
                    });
                    SendHaptic(h.node, Mathf.Clamp01(speed / 4f), 0.06f);

                    if (TryHold(h, target, relDelta, out delta)) return true;
                    h.stuck = false;
                }
            }

            h.lastFree = target;
            return false;
        }

        /// <summary>
        /// Resolves a stuck hand from how the hand moved relative to the body this step (relDelta).
        /// Pressing into the surface pushes the body the opposite way; the sideways part pushes the body
        /// except for the share the surface lets the hand slide (slip). Moving away does nothing, and
        /// drawing the hand off the surface releases it. No force comes from a hand that is merely resting.
        /// Returns false when the hand should let go.
        /// </summary>
        private bool TryHold(Hand h, Vector3 target, Vector3 relDelta, out Vector3 delta)
        {
            delta = Vector3.zero;
            float dn = Vector3.Dot(relDelta, h.normal);
            Vector3 tangent = relDelta - h.normal * dn;

            // A hand only counts as touching while it is actively moving into the surface. The moment it
            // stops pressing (or moves away) it lets go, and the body keeps the momentum from the push.
            if (dn > -profile.minPressDistance) return false;

            if (h.slip > 0f && tangent.sqrMagnitude > 1e-8f)
            {
                h.anchor += tangent * h.slip;
                if (!RevalidateAnchor(h)) return false; // slid off an edge
            }

            if ((target - h.anchor).magnitude > profile.unstickDistance) return false;

            Vector3 push = h.normal * Mathf.Min(dn, 0f) + tangent * (1f - h.slip);
            delta = -push;
            return true;
        }

        /// <summary>Re-seat a sliding anchor on the geometry under it (handles curves, edges, material changes).</summary>
        private bool RevalidateAnchor(Hand h)
        {
            const float lift = 0.05f;
            Vector3 origin = h.anchor + h.normal * lift;
            if (!Physics.SphereCast(origin, profile.handRadius, -h.normal, out RaycastHit hit,
                    lift + profile.handSkin + 0.03f, surfaceMask, QueryTriggerInteraction.Ignore))
                return false;

            h.normal = hit.normal;
            h.anchor = origin - h.normal * hit.distance + hit.normal * profile.handSkin;
            var surface = hit.collider.GetComponentInParent<SurfaceProperties>();
            h.slip = surface != null ? surface.slipPercentage : profile.defaultSlip;
            return true;
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
