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
        public float speed;               // hand speed at impact (m/s)
        public SurfaceProperties surface; // may be null
    }

    /// <summary>
    /// Hand-contact propulsion.
    ///
    /// Each physics step a virtual hand sweeps from where it was to where the real hand now is. If geometry is
    /// in the way the hand is blocked at the surface and the body moves the opposite way by the blocked amount.
    /// There is no persistent "stuck" state: a hand only affects the body on steps where it is pressing into
    /// something, so a resting hand does nothing.
    ///
    /// The body's actual movement is recorded every step. While a hand touches, the body takes its recent
    /// average velocity (x jumpMultiplier); when hands let go, that velocity carries you.
    ///
    /// Put this on the rig root (the transform that parents the head and controllers).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ArmLocomotion : MonoBehaviour
    {
        [Header("References")]
        public LocomotionProfile profile;
        public Transform head;
        public Transform leftController;
        public Transform rightController;
        [Tooltip("Optional visuals. Placed at the virtual hand position (blocked at surfaces).")]
        public Transform leftVisual;
        public Transform rightVisual;
        [Tooltip("SphereCollider on a child object whose layer is excluded from surfaceMask.")]
        public SphereCollider bodyCollider;
        public LayerMask surfaceMask = ~(1 << 2); // everything except Ignore Raycast
        public bool debugLog;

        public event Action<HandContactEvent> HandContacted;

        public Vector3 Velocity { get; private set; }
        public bool IsHandTouching(HandSide s) => _hands[(int)s].touching;

        /// <summary>Virtual reach / real reach, set by ArmSpanCalibrator. 1 = no scaling.</summary>
        public float ArmScale { get; private set; } = 1f;
        public void SetArmScale(float scale) => ArmScale = Mathf.Clamp(scale, profile.minArmScale, profile.maxArmScale);

        private sealed class Hand
        {
            public HandSide side;
            public Transform controller;
            public Transform visual;
            public XRNode node;
            public Vector3 lastPos;   // where the virtual hand was at the end of the previous step
            public Vector3 target;    // this step's (scaled) real hand position
            public Vector3 resolved;  // this step's virtual hand position (blocked by geometry)
            public bool touching;
            public bool wasTouching;
        }

        private Hand[] _hands;
        private VelocityHistory _history;
        private SphereCollider _probe;
        private bool _initialised;
        private readonly Collider[] _overlaps = new Collider[16];

        private void Awake()
        {
            _hands = new[]
            {
                new Hand { side = HandSide.Left, controller = leftController, visual = leftVisual, node = XRNode.LeftHand },
                new Hand { side = HandSide.Right, controller = rightController, visual = rightVisual, node = XRNode.RightHand },
            };
            _history = new VelocityHistory(profile.velocityHistorySize);

            // Used only to push a hand out of geometry it ended up inside.
            var go = new GameObject("HandProbe") { layer = 2, hideFlags = HideFlags.HideInHierarchy };
            go.transform.SetParent(transform, false);
            _probe = go.AddComponent<SphereCollider>();
            _probe.isTrigger = true;
            _probe.radius = profile.handRadius;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            if (!_initialised)
            {
                foreach (var h in _hands)
                    if (h.controller != null) h.lastPos = ArmTarget(h);
                _initialised = true;
            }

            SyncBodyCollider();
            ResolveBodyPenetration();

            // 1. Hands: sweep each one, collect how far each wants to shove the body.
            Vector3 handMove = Vector3.zero;
            bool touching = false;
            foreach (var h in _hands)
            {
                h.wasTouching = h.touching;
                if (h.controller == null) { h.touching = false; continue; }

                h.touching = SolveHand(h, dt, out Vector3 delta);
                if (h.touching)
                {
                    handMove += delta;
                    touching = true;
                }
            }

            float cap = profile.maxPushSpeed * dt;
            if (handMove.sqrMagnitude > cap * cap) handMove = handMove.normalized * cap;

            // 2. Free flight: gravity and drag. While a hand touches, momentum is taken from the history below.
            if (touching)
            {
                Velocity = Vector3.zero;
            }
            else
            {
                Velocity += Vector3.down * (profile.gravity * dt);
                Velocity *= Mathf.Max(0f, 1f - profile.drag * dt);
            }

            // 3. Move the body.
            Vector3 before = BodyCenter();
            MoveBody(handMove + Velocity * dt, out Vector3 hitNormal);
            Vector3 actual = BodyCenter() - before;
            if (hitNormal != Vector3.zero && Vector3.Dot(Velocity, hitNormal) < 0f)
                Velocity = Vector3.ProjectOnPlane(Velocity, hitNormal);

            // 4. Remember where the hands are for next step's sweep.
            bool blocked = touching && (handMove - actual).magnitude > handMove.magnitude * 0.25f + 0.002f;
            foreach (var h in _hands)
            {
                if (h.controller == null) continue;
                if (h.touching && !blocked) h.lastPos = h.resolved;      // stays put at the surface
                else if (h.touching) h.lastPos = h.target + actual;      // body is wedged: let go, don't re-hit the same spot
                else h.lastPos = h.target;
                if (blocked) h.touching = false;
            }

            // 5. Momentum: record real body movement; a touching hand turns recent motion into velocity.
            _history.Push(actual / dt);
            if (touching)
            {
                Vector3 avg = _history.Average();
                if (avg.magnitude > profile.velocityLimit)
                    Velocity = Vector3.ClampMagnitude(avg * profile.jumpMultiplier, profile.maxJumpSpeed);
            }
        }

        private void LateUpdate()
        {
            foreach (var h in _hands)
            {
                if (h.visual == null || h.controller == null) continue;
                h.visual.position = h.touching ? h.resolved : ArmTarget(h);
                h.visual.rotation = h.controller.rotation;
            }
        }

        // ---------------------------------------------------------------- hands

        /// <summary>Real hand position, scaled about the head by arm span and clamped to max reach.</summary>
        private Vector3 ArmTarget(Hand h)
        {
            Vector3 fromHead = (h.controller.position - head.position) * ArmScale;
            return head.position + Vector3.ClampMagnitude(fromHead, profile.maxArmLength);
        }

        /// <summary>
        /// Sweeps the virtual hand toward the real one. Returns true if geometry blocked it; delta is then the
        /// body displacement (opposite to the blocked hand motion, less the share the surface lets slide).
        /// </summary>
        private bool SolveHand(Hand h, float dt, out Vector3 delta)
        {
            delta = Vector3.zero;
            Vector3 target = ArmTarget(h);
            h.target = target;
            h.resolved = target;

            float r = profile.handRadius;
            float skin = profile.handSkin;
            Vector3 from = h.lastPos;
            Vector3 travel = target - from;
            float dist = travel.magnitude;

            bool hit = false;
            Vector3 resolved = target;
            Vector3 normal = Vector3.up;
            Collider surfaceCollider = null;

            if (dist > 1e-5f &&
                Physics.SphereCast(from, r, travel / dist, out RaycastHit info, dist + skin, surfaceMask,
                    QueryTriggerInteraction.Ignore))
            {
                float stop = Mathf.Max(0f, info.distance - skin);
                if (stop < dist)
                {
                    hit = true;
                    resolved = from + travel / dist * stop;
                    normal = info.normal;
                    surfaceCollider = info.collider;
                }
            }

            // The sweep ignores colliders the hand already overlaps, so also push the hand out of anything it is inside.
            if (!hit && PushOut(ref resolved, out Vector3 pushNormal, out Collider inside))
            {
                hit = true;
                normal = pushNormal;
                surfaceCollider = inside;
            }

            if (!hit) return false;

            var surface = surfaceCollider != null ? surfaceCollider.GetComponentInParent<SurfaceProperties>() : null;
            float slip = surface != null ? surface.slipPercentage : profile.defaultSlip;

            // Surface slip: the hand slides along the surface by this share of its sideways motion.
            Vector3 blockedPart = target - resolved;
            Vector3 tangent = blockedPart - normal * Vector3.Dot(blockedPart, normal);
            if (slip > 0f && tangent.sqrMagnitude > 1e-10f)
            {
                Vector3 slide = tangent * slip;
                float slideLen = slide.magnitude;
                if (Physics.SphereCast(resolved, r, slide / slideLen, out RaycastHit slideHit, slideLen + skin,
                        surfaceMask, QueryTriggerInteraction.Ignore))
                    slide = slide / slideLen * Mathf.Max(0f, slideHit.distance - skin);
                resolved += slide;
            }

            h.resolved = resolved;
            delta = resolved - target; // body moves opposite to the blocked hand motion

            if (!h.wasTouching)
            {
                float speed = dist / dt;
                HandContacted?.Invoke(new HandContactEvent
                {
                    side = h.side, point = resolved - normal * r, normal = normal, speed = speed, surface = surface,
                });
                SendHaptic(h.node, Mathf.Clamp01(speed / 4f), 0.06f);
                if (debugLog) Debug.Log($"[Arm] {h.side} contact speed={speed:F2} slip={slip:F2} normal={normal}");
            }
            return true;
        }

        private bool PushOut(ref Vector3 p, out Vector3 normal, out Collider collider)
        {
            normal = Vector3.zero;
            collider = null;
            bool any = false;
            int n = Physics.OverlapSphereNonAlloc(p, profile.handRadius, _overlaps, surfaceMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var other = _overlaps[i];
                if (Physics.ComputePenetration(_probe, p, Quaternion.identity,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 dir, out float d))
                {
                    p += dir * (d + profile.handSkin);
                    normal = dir;
                    collider = other;
                    any = true;
                }
            }
            return any;
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

            for (int i = 0; i < 3 && remaining.sqrMagnitude > 1e-10f; i++)
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
                Gizmos.color = h.touching ? Color.green : Color.yellow;
                Gizmos.DrawWireSphere(h.touching ? h.resolved : h.lastPos, profile.handRadius);
            }
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(BodyCenter(), Velocity * 0.25f);
        }
    }
}
