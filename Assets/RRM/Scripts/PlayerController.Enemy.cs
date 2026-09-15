using UnityEngine;

namespace RRM
{
    public sealed partial class PlayerController
    {
        [Header("Enemy decisions")]
        public int decisionSeed = 173;
        [Min(0.02f)] public float decisionInterval = 0.25f;
        [Range(0f, 1f)] public float grappleChance = 0.2f;
        [Range(0f, 1f)] public float blockChance = 0.35f;
        [Min(0f)] public float blockReactionDelay = 0.25f;
        [Range(0f, 1f)] public float escapeChance = 0.55f;
        [Min(0f)] public float escapeReactionDelay = 0.12f;

        private System.Random decisions;
        private float nextDecision, blockAttemptAt, escapeAttemptAt, escapeReleaseAt;
        private float observedEscapeEpoch;
        private int observedEscapeCycle;
        private bool observedWindup;
        private Vector3 avoidDirection;

        private void ResetEnemyDecisions()
        {
            decisions = new System.Random(decisionSeed);
            nextDecision = 0f;
            blockAttemptAt = escapeAttemptAt = escapeReleaseAt = float.PositiveInfinity;
            observedEscapeEpoch = float.NegativeInfinity;
            observedEscapeCycle = -1;
            observedWindup = false;
            avoidDirection = Vector3.zero;
        }

        private void EngageOpponent()
        {
            movement = Vector2.zero;
            State = attack.Phase == MeleeAttack.AttackPhase.Windup ? EnemyState.Windup
                : attack.Phase == MeleeAttack.AttackPhase.Strike ? EnemyState.Attack
                : attack.IsBusy ? EnemyState.Recovery : EnemyState.Idle;
            if (!CanAct || !attack.isActiveAndEnabled) return;
            if (InGrapple) blockAttemptAt = float.PositiveInfinity;
            if (IsCaptured) { TryEnemyEscape(); return; }
            if (GrappleTarget)
            {
                if (Time.time >= nextDecision && !attack.IsBusy)
                {
                    nextDecision = Time.time + Mathf.Max(0.02f, decisionInterval);
                    attack.TryHandAttack(GrappleHand == HandSide.Left ? RightHand : LeftHand);
                }
                return;
            }

            Vector3 offset = opponent.transform.position - body.position;
            bool visible = !opponent.IsDead && opponent.isActiveAndEnabled
                && offset.sqrMagnitude <= noticeDistance * noticeDistance
                && opponent.TryGetComponent(out Collider targetBody)
                && !Physics.Linecast(body.position + Vector3.up * 1.08f, targetBody.bounds.center,
                    attack.obstructionMask, QueryTriggerInteraction.Ignore);
            var opposingAttack = opponent.GetComponent<MeleeAttack>();
            bool windup = opposingAttack && opposingAttack.Phase == MeleeAttack.AttackPhase.Windup;
            if (windup && !observedWindup && visible && !attack.IsBusy && !BlockRecovering
                && offset.sqrMagnitude < 4f && decisions.NextDouble() < blockChance)
                blockAttemptAt = Time.time + Mathf.Max(0f, blockReactionDelay);
            observedWindup = windup;
            if (Time.time >= blockAttemptAt)
            {
                blockAttemptAt = float.PositiveInfinity;
                if (visible && opposingAttack && (windup || opposingAttack.Phase == MeleeAttack.AttackPhase.Strike)) TryBlock();
            }
            if (attack.IsBusy || BlockRecovering || !visible) return;

            Vector3 direction = Vector3.ProjectOnPlane(offset, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.01f) return;
            aimDirection = direction;
            State = EnemyState.Approach;
            // Preserve a held camera when the other hand can fight; every hit still uses the hand command.
            PlayerHand hand = RightHand.Item?.Source is CameraDevice ? LeftHand : RightHand;
            if (!hand.IsAvailable) hand = hand == LeftHand ? RightHand : LeftHand;
            float reach = Mathf.Min(attackDistance, attack.HandReach(hand) + 0.3f);
            if (offset.magnitude <= reach)
            {
                if (Vector3.Dot(transform.forward, direction) < 0.97f || Time.time < nextDecision
                    || !float.IsPositiveInfinity(blockAttemptAt)) return;
                nextDecision = Time.time + Mathf.Max(0.02f, decisionInterval);
                if (LeftHand.State == HandState.Empty && RightHand.State == HandState.Empty
                    && decisions.NextDouble() < grappleChance
                    && TryBeginGrapple(decisions.Next(2) == 0 ? HandSide.Left : HandSide.Right,
                        opponent.GetComponent<PlayerController>())) return;
                if (attack.TryHandAttack(hand)) State = EnemyState.Windup;
                return;
            }
            // ponytail: follow a local obstacle tangent, not a route through unseen rooms or a maze.
            if (ObstacleAhead(direction, out RaycastHit obstacle))
            {
                Vector3 tangent = Vector3.Cross(Vector3.up, obstacle.normal).normalized;
                if (Vector3.Dot(tangent, avoidDirection.sqrMagnitude > 0.01f ? avoidDirection : direction) < 0f)
                    tangent = -tangent;
                if (!ClearDirection(tangent)) tangent = -tangent;
                if (tangent.sqrMagnitude < 0.01f || !ClearDirection(tangent)) return;
                direction = avoidDirection = tangent;
            }
            movement = new Vector2(direction.x, direction.z);
        }

        private void TryEnemyEscape()
        {
            if (observedEscapeEpoch != escapeEpoch || observedEscapeCycle != EscapeCycle)
            {
                observedEscapeEpoch = escapeEpoch;
                observedEscapeCycle = EscapeCycle;
                escapeAttemptAt = escapeReleaseAt = float.PositiveInfinity;
                if (decisions.NextDouble() < escapeChance)
                    escapeAttemptAt = escapeEpoch + EscapeCycle * CycleLength + Delay + Mathf.Max(0f, escapeReactionDelay);
            }
            if (Time.time >= escapeAttemptAt)
            {
                escapeAttemptAt = float.PositiveInfinity;
                if (BeginEscapeAttempt(HasHand(HandSide.Left) ? HandSide.Left : HandSide.Right)) escapeReleaseAt = Time.time + 0.05f;
            }
            if (Time.time >= escapeReleaseAt)
            {
                escapeReleaseAt = float.PositiveInfinity;
                CompleteEscapeAttempt(escapePressHand);
            }
        }
    }
}
