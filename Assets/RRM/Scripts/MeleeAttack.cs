using System.Collections.Generic;
using UnityEngine;

namespace RRM
{
    [RequireComponent(typeof(Damageable))]
    public sealed class MeleeAttack : MonoBehaviour
    {
        public enum AttackPhase { Ready, Windup, Strike, Recovery }

        public Transform weaponPivot;
        public Transform weaponTip;
        public LayerMask hurtboxMask;
        public LayerMask obstructionMask = 1;
        [Min(0.02f)] public float windup = 0.32f;
        [Min(0.02f)] public float strike = 0.18f;
        [Min(0.02f)] public float recovery = 0.5f;
        [Min(1f)] public float damage = 45f;
        [Min(0f)] public float impulse = 3.2f;
        [Min(0.01f)] public float hitRadius = 0.22f;
        [Min(0f)] public float contactPause = 0.065f;
        public AttackPhase Phase { get; private set; }
        public bool IsBusy => Phase != AttackPhase.Ready;
        public bool IsPaused => pauseRemaining > 0f;
        public float MovementScale => IsBusy ? 0.3f : 1f;

        private readonly HashSet<Damageable> hitTargets = new HashSet<Damageable>();
        private Damageable owner;
        private float elapsed;
        private float pauseRemaining;
        private Vector3 previousTip;

        private void Awake() => owner = GetComponent<Damageable>();

        public bool TryAttack()
        {
            if (owner == null) owner = GetComponent<Damageable>();
            if (!isActiveAndEnabled || owner.IsDead || IsBusy || !weaponPivot || !weaponTip)
                return false;
            hitTargets.Clear();
            elapsed = 0f;
            Phase = AttackPhase.Windup;
            return true;
        }

        private void FixedUpdate() => Tick(Time.fixedDeltaTime);

        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime) || !IsBusy) return;
            if (owner.IsDead) { CancelAttack(); return; }
            if (pauseRemaining > 0f)
            {
                pauseRemaining = Mathf.Max(0f, pauseRemaining - deltaTime);
                return;
            }

            elapsed += deltaTime;
            float duration = Phase == AttackPhase.Windup ? windup
                : Phase == AttackPhase.Strike ? strike : recovery;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.02f, duration));
            float angle = Phase == AttackPhase.Windup ? Mathf.Lerp(15f, -70f, progress)
                : Phase == AttackPhase.Strike ? Mathf.Lerp(-70f, 65f, progress)
                : Mathf.Lerp(65f, 15f, progress);
            weaponPivot.localRotation = Quaternion.Euler(0f, angle, 0f);
            if (Phase == AttackPhase.Strike) CheckContacts(previousTip, weaponTip.position);
            previousTip = weaponTip.position;

            if (progress < 1f) return;
            elapsed = 0f;
            Phase = Phase == AttackPhase.Windup ? AttackPhase.Strike
                : Phase == AttackPhase.Strike ? AttackPhase.Recovery : AttackPhase.Ready;
        }

        private void CheckContacts(Vector3 start, Vector3 end)
        {
            // ponytail: fixed-height tip sweep; use authored trajectories when combat animations arrive.
            // Sweeping the tip also catches contacts between physics steps and initial overlaps.
            foreach (Collider collider in Physics.OverlapCapsule(start, end, hitRadius,
                         hurtboxMask, QueryTriggerInteraction.Collide))
            {
                if (!collider.TryGetComponent(out Hurtbox zone) || zone.owner == null
                    || zone.owner == owner || hitTargets.Contains(zone.owner)) continue;
                Vector3 point = collider.ClosestPoint(end);
                Vector3 origin = transform.position + Vector3.up * 1.15f;
                if (Physics.Linecast(origin, point, obstructionMask, QueryTriggerInteraction.Ignore))
                    continue;
                Vector3 direction = Vector3.ProjectOnPlane(point - origin, Vector3.up).normalized;
                if (zone.owner.ApplyDamage(gameObject, zone, point, direction, damage, impulse))
                {
                    hitTargets.Add(zone.owner);
                    pauseRemaining = contactPause;
                }
            }
        }

        private void CancelAttack()
        {
            Phase = AttackPhase.Ready;
            elapsed = pauseRemaining = 0f;
            hitTargets.Clear();
            if (weaponPivot) weaponPivot.localRotation = Quaternion.Euler(0f, 15f, 0f);
        }

        private void OnDisable() => CancelAttack();

        private void OnDrawGizmosSelected()
        {
            if (!weaponTip) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(weaponTip.position, hitRadius);
        }
    }
}
