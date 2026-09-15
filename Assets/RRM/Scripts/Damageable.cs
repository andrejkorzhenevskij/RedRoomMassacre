using System;
using UnityEngine;

namespace RRM
{
    public sealed partial class Damageable : MonoBehaviour
    {
        [Min(1f)] public float maxHealth = 90f;
        [SerializeField] private double currentHealth = 90d;
        public float Health => (float)currentHealth;
        public bool IsDead => currentHealth <= 0f;
        public event Action<DamageEvent> Damaged;

        private void Awake() => ResetHealth();

        public void ResetHealth() => currentHealth = Mathf.Max(1f, maxHealth);

        private void Update() => TickBleeding(Time.deltaTime);

        public double BleedingPerSecond => IsDead ? 0d : maxHealth * (0.05d / 60d) * SeveredLimbCount;

        public void TickBleeding(double seconds)
        {
            double rate = BleedingPerSecond;
            if (!isActiveAndEnabled || IsDead || !(seconds > 0d) || double.IsInfinity(seconds)
                || !(rate > 0d) || double.IsInfinity(rate)) return;
            Vector3 point = TryGetComponent(out CapsuleCollider capsule) ? capsule.bounds.center : transform.position;
            CommitDamage(null, BodyPart.Torso, point, Vector3.zero, rate * seconds, 0f, DamageKind.Bleeding, 0f);
        }

        public bool ApplyDamage(GameObject source, Hurtbox zone, Vector3 point,
            Vector3 direction, float baseDamage, float impulse,
            DamageKind kind = DamageKind.Melee, float baseWeight = 1f, bool severLimb = false)
        {
            if (IsDead || !isActiveAndEnabled || zone == null || zone.owner != this || !IsAttached(zone.part))
                return false;

            float amount = baseDamage * zone.damageMultiplier;
            if (!(baseDamage > 0f) || !(amount > 0f) || float.IsInfinity(amount)
                || float.IsNaN(impulse) || float.IsInfinity(impulse) || impulse < 0f
                || float.IsNaN(baseWeight) || float.IsInfinity(baseWeight) || baseWeight < 0f
                || !Finite(point) || !Finite(direction))
                return false;

            if (TryGetComponent(out PlayerController controller) && controller.BlocksHit(source)) return false;

            bool sever = severLimb && kind == DamageKind.Melee && source
                && source.TryGetComponent(out MeleeAttack weapon) && weapon.CanSever(zone);
            if (sever) severedLimbs |= 1 << (int)zone.part;
            CommitDamage(source, zone.part, point, direction, amount, impulse, kind, baseWeight,
                sever ? zone.part : (BodyPart?)null);
            // Observers snapshot the contact before a held light/camera is dropped.
            if (sever) DetachLimb(zone, direction);
            return true;
        }

        private void CommitDamage(GameObject source, BodyPart part, Vector3 point, Vector3 direction,
            double amount, float impulse, DamageKind kind, float weight, BodyPart? severedPart = null)
        {
            // Keep fractional loss in the same health value; float subtraction per frame loses small ticks.
            amount = Math.Min(currentHealth, amount);
            currentHealth -= amount;
            Damaged?.Invoke(new DamageEvent(source, this, part, point, direction.normalized,
                (float)amount, impulse, IsDead, kind, weight, severedPart));
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude);
    }
}
