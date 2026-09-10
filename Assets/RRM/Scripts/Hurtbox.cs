using UnityEngine;

namespace RRM
{
    [RequireComponent(typeof(Collider))]
    public sealed class Hurtbox : MonoBehaviour
    {
        public Damageable owner;
        public BodyPart part;
        [Min(0.01f)] public float damageMultiplier = 1f;

        private void Reset()
        {
            owner = GetComponentInParent<Damageable>();
            GetComponent<Collider>().isTrigger = true;
        }
    }
}
