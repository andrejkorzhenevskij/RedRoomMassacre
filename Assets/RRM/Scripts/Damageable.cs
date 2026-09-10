using System;
using UnityEngine;

namespace RRM
{
    public sealed class Damageable : MonoBehaviour
    {
        [Min(1f)] public float maxHealth = 90f;
        [SerializeField] private float currentHealth = 90f;
        public float Health => currentHealth;
        public bool IsDead => currentHealth <= 0f;
        public event Action<DamageEvent> Damaged;

        private void Awake() => ResetHealth();

        public void ResetHealth() => currentHealth = Mathf.Max(1f, maxHealth);

        public bool ApplyDamage(GameObject source, Hurtbox zone, Vector3 point,
            Vector3 direction, float baseDamage, float impulse)
        {
            if (IsDead || !isActiveAndEnabled || zone == null || zone.owner != this)
                return false;

            float amount = baseDamage * zone.damageMultiplier;
            if (!(baseDamage > 0f) || !(amount > 0f) || float.IsInfinity(amount)
                || float.IsNaN(impulse) || float.IsInfinity(impulse) || impulse < 0f
                || !Finite(point) || !Finite(direction))
                return false;

            amount = Mathf.Min(currentHealth, amount);
            currentHealth -= amount;
            Damaged?.Invoke(new DamageEvent(source, this, zone.part, point,
                direction.normalized, amount, impulse, IsDead));
            return true;
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude);
    }
}
