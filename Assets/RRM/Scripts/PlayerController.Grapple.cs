using UnityEngine;

namespace RRM
{
    public sealed partial class PlayerController
    {
        [Header("Grapple")]
        [Min(0.1f)] public float grappleRange = 0.9f;
        [Range(0f, 90f)] public float grappleHalfAngle = 40f;
        [Min(0.1f)] public float escapeDelay = 0.65f;
        [Min(0.1f)] public float escapeWindow = 0.3f;
        [Min(0.1f)] public float escapeRepeatDelay = 0.45f;
        [Min(0f)] public float escapeProtection = 1.2f;
        [Header("Timed block")]
        [Min(0.02f)] public float blockWindow = 0.2f;
        [Min(0f)] public float blockRecovery = 0.35f;
        [Range(0f, 89f)] public float blockHalfAngle = 70f;
        private float blockUntil, blockReadyAt, blockedFeedbackUntil;
        public bool BlockRecovering => Time.time < blockReadyAt;
        public bool IsBlocking => Time.time < blockUntil && CanAct && !InGrapple
            && attack && attack.isActiveAndEnabled && !attack.IsBusy
            && LeftHand.State == HandState.Empty && RightHand.State == HandState.Empty;
        public PlayerController GrappleTarget { get; private set; }
        public PlayerController Captor { get; private set; }
        public HandSide GrappleHand { get; private set; }
        public bool IsCaptured => Captor;
        public bool InGrapple => grappleLocked;
        public bool EscapeWindowOpen => IsCaptured && CycleAge >= Delay && CycleAge < Delay + Window;
        public string EscapeHint => !IsCaptured ? string.Empty : attemptedCycle == EscapeCycle
            ? "GRABBED: attempt used; wait for next cycle"
            : EscapeWindowOpen ? "BREAK FREE: short LMB / RMB" : "GRABBED: wait for the opening";

        private bool grappleLocked;
        private RigidbodyConstraints beforeGrappleConstraints;
        private Vector3 grappleAnchor;
        private float escapeEpoch, protectedUntil, escapePressTime;
        private int attemptedCycle = -1, escapePressCycle;
        private HandSide escapePressHand;
        private bool escapePressEligible;
        private float Delay => Mathf.Max(0.1f, escapeDelay);
        private float Window => Mathf.Max(0.1f, escapeWindow);
        private float CycleLength => Delay + Window + Mathf.Max(0.1f, escapeRepeatDelay);
        private int EscapeCycle => Mathf.FloorToInt(Mathf.Max(0f, Time.time - escapeEpoch) / CycleLength);
        private float CycleAge => Mathf.Max(0f, Time.time - escapeEpoch) - EscapeCycle * CycleLength;

        // Commands are shared by local input and future enemy decisions, not by two combat implementations.
        public bool TryBeginGrapple(HandSide side, PlayerController target = null)
        {
            if (!CanAct || InGrapple || BlockRecovering || !attack || !attack.isActiveAndEnabled || attack.IsBusy || IsCrouching
                || LeftHand.State != HandState.Empty || RightHand.State != HandState.Empty) return false;
            if (!target)
            {
                float nearest = float.PositiveInfinity;
                foreach (Collider contact in Physics.OverlapSphere(body.position + Vector3.up,
                    grappleRange, attack.hurtboxMask, QueryTriggerInteraction.Collide))
                {
                    var candidate = contact.GetComponentInParent<PlayerController>();
                    if (!CanGrab(candidate)) continue;
                    float distance = (candidate.body.position - body.position).sqrMagnitude;
                    if (distance < nearest) { target = candidate; nearest = distance; }
                }
            }
            if (!CanGrab(target)) return false;
            target.attack?.CancelAttack();
            GrappleTarget = target;
            GrappleHand = side;
            target.Captor = this;
            LockGrapple(); target.LockGrapple();
            target.ResetEscapeCycle();
            // Keep the gripping press; discard the free hand and all pre-grab victim input.
            (side == HandSide.Left ? RightHand : LeftHand).CancelInput();
            target.LeftHand.CancelInput(); target.RightHand.CancelInput();
            return true;
        }

        private bool CanGrab(PlayerController target)
        {
            if (!target || target == this || !target.CanAct || target.InGrapple || target.IsCrouching
                || !target.capsule || !target.capsule.enabled || !capsule.enabled
                || Time.time < target.protectedUntil || Mathf.Abs(body.linearVelocity.y) > 0.5f
                || Mathf.Abs(target.body.linearVelocity.y) > 0.5f) return false;
            Vector3 offset = target.body.position - body.position;
            return Mathf.Abs(offset.y) < 0.3f && offset.sqrMagnitude <= grappleRange * grappleRange
                && Vector3.Dot(transform.forward, offset.normalized) >= Mathf.Cos(grappleHalfAngle * Mathf.Deg2Rad)
                && ClearGrapplePath(target);
        }

        private bool ClearGrapplePath(PlayerController target) =>
            !Physics.CheckCapsule(body.position + Vector3.up * 0.6f,
                target.body.position + Vector3.up * 0.6f, 0.12f, attack.obstructionMask, QueryTriggerInteraction.Ignore)
            && !Physics.Linecast(body.position + Vector3.up, target.body.position + Vector3.up,
                attack.obstructionMask, QueryTriggerInteraction.Ignore);

        private void LockGrapple()
        {
            blockUntil = 0f;
            grappleLocked = true;
            beforeGrappleConstraints = body.constraints;
            grappleAnchor = body.position;
            // ponytail: stationary pair at contact; no dragging, joints or pose snapping.
            body.constraints |= RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ
                | RigidbodyConstraints.FreezeRotation;
            StopGrappleMovement();
        }

        private void StopGrappleMovement()
        {
            movement = Vector2.zero;
            jumpRequested = spaceJumpEligible = crouchHeld = false;
            spacePressedAt = float.NegativeInfinity;
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
            body.angularVelocity = Vector3.zero;
        }

        private void ValidateGrapple()
        {
            if (!grappleLocked) return;
            var other = GrappleTarget ? GrappleTarget : Captor;
            if (!CanAct || !other || !other.CanAct || !other.grappleLocked || !attack || !attack.isActiveAndEnabled
                || (GrappleTarget && (LeftHand.Item != null || RightHand.Item != null))
                || Vector3.Distance(body.position, grappleAnchor) > 0.2f
                || Vector3.Distance(body.position, other.body.position) > (GrappleTarget ? grappleRange : other.grappleRange) + 0.15f
                || !ClearGrapplePath(other)) ReleaseGrapple();
        }

        public void ReleaseGrapple()
        {
            if (!grappleLocked) return;
            var other = GrappleTarget ? GrappleTarget : Captor;
            UnlockGrapple();
            if (other && (other.GrappleTarget == this || other.Captor == this)) other.UnlockGrapple();
        }

        private void UnlockGrapple()
        {
            grappleLocked = false;
            GrappleTarget = Captor = null;
            if (body && health && !health.IsDead) body.constraints = beforeGrappleConstraints;
            attack?.EndGrappleStrike();
            leftHand?.CancelInput(); rightHand?.CancelInput();
            escapePressEligible = false;
            movement = Vector2.zero;
        }

        private void ResetEscapeCycle()
        {
            escapeEpoch = Time.time;
            attemptedCycle = -1;
            escapePressEligible = false;
        }

        public bool BeginEscapeAttempt(HandSide side)
        {
            if (!CanAct || !HasHand(side) || !IsCaptured || attemptedCycle == EscapeCycle) return false;
            attemptedCycle = escapePressCycle = EscapeCycle;
            escapePressHand = side;
            escapePressTime = Time.time;
            escapePressEligible = EscapeWindowOpen;
            return escapePressEligible;
        }

        public bool CompleteEscapeAttempt(HandSide side)
        {
            if (!CanAct || !HasHand(side) || !IsCaptured || side != escapePressHand) return false;
            bool success = escapePressEligible && EscapeWindowOpen && escapePressCycle == EscapeCycle
                && Time.time - escapePressTime < Mathf.Max(0.01f, handHoldThreshold);
            escapePressEligible = false;
            if (!success) return false;
            protectedUntil = Time.time + Mathf.Max(0f, escapeProtection);
            ReleaseGrapple();
            return true;
        }

        internal bool CanStrikeWithHand(HandSide side) => CanAct && HasHand(side) && !IsCaptured && !BlockRecovering
            && (!GrappleTarget || (side != GrappleHand
                && Time.time >= GrappleTarget.escapeEpoch + GrappleTarget.Delay + GrappleTarget.Window));

        private void OnGrappleDamage(DamageEvent hit)
        {
            if (hit.IsFatal) { ReleaseGrapple(); return; }
            if (hit.IsBleeding) return;
            if (IsCaptured) ResetEscapeCycle();
            else if (GrappleTarget) ReleaseGrapple();
        }

        public bool TryBlock()
        {
            if (!CanAct || InGrapple || BlockRecovering || !attack || !attack.isActiveAndEnabled || attack.IsBusy
                || LeftHand.State != HandState.Empty || RightHand.State != HandState.Empty) return false;
            blockUntil = Time.time + Mathf.Max(0.02f, blockWindow);
            blockReadyAt = blockUntil + Mathf.Max(0f, blockRecovery);
            LeftHand.CancelInput(); RightHand.CancelInput();
            return true;
        }

        internal bool BlocksHit(GameObject source)
        {
            if (!IsBlocking || !source || source == gameObject) return false;
            Vector3 toward = Vector3.ProjectOnPlane(source.transform.position - transform.position, Vector3.up);
            if (toward.sqrMagnitude < 0.0001f || Vector3.Dot(transform.forward, toward.normalized)
                < Mathf.Cos(blockHalfAngle * Mathf.Deg2Rad)) return false;
            blockedFeedbackUntil = Time.time + 0.4f;
            return true;
        }

        internal bool FindSlamWall(out RaycastHit wall)
        {
            wall = default;
            if (!GrappleTarget || !ClearGrapplePath(GrappleTarget)) return false;
            Vector3 direction = Vector3.ProjectOnPlane(GrappleTarget.body.position - body.position, Vector3.up).normalized;
            var target = GrappleTarget;
            // ponytail: contact-only slam against a wall behind the victim; no dragging or root-motion system.
            float radius = target.capsule.radius * Mathf.Max(target.transform.lossyScale.x, target.transform.lossyScale.z);
            return Physics.SphereCast(target.body.position + Vector3.up * 1.08f, radius * 0.95f,
                direction, out wall, radius * 0.05f + attack.wallContactGap, attack.obstructionMask,
                QueryTriggerInteraction.Ignore) && Mathf.Abs(wall.normal.y) < 0.2f
                && Vector3.Dot(wall.normal, direction) < -0.8f;
        }

        private bool HandleGrappleInput()
        {
            if (IsCaptured)
            {
                if (LeftHand.WasPressedThisFrame) BeginEscapeAttempt(HandSide.Left);
                if (RightHand.WasPressedThisFrame) BeginEscapeAttempt(HandSide.Right);
                if (LeftHand.WasTappedThisFrame) CompleteEscapeAttempt(HandSide.Left);
                if (RightHand.WasTappedThisFrame) CompleteEscapeAttempt(HandSide.Right);
                return true;
            }
            if (GrappleTarget)
            {
                PlayerHand grip = GrappleHand == HandSide.Left ? LeftHand : RightHand;
                if (!grip.IsHoldActive) { ReleaseGrapple(); return true; }
                PlayerHand free = GrappleHand == HandSide.Left ? RightHand : LeftHand;
                if (free.HoldStartedThisFrame) attack.TryWallSlam(free);
                if (free.AttackRequested) attack.TryHandAttack(free);
                return true;
            }
            // Arbitrate the chord before either single-hand hold can acquire a target.
            if (LeftHand.State == HandState.Empty && RightHand.State == HandState.Empty
                && LeftHand.IsHoldActive && RightHand.IsHoldActive)
            {
                if (LeftHand.IsHoldActionActive && RightHand.IsHoldActionActive
                    && (LeftHand.HoldStartedThisFrame || RightHand.HoldStartedThisFrame))
                {
                    TryBlock();
                    LeftHand.CancelInput(); RightHand.CancelInput();
                }
                return false;
            }
            if (LeftHand.HoldStartedThisFrame && TryBeginGrapple(HandSide.Left)) return true;
            return RightHand.HoldStartedThisFrame && TryBeginGrapple(HandSide.Right);
        }
    }
}
