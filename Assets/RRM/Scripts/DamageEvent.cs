using UnityEngine;

namespace RRM
{
    public enum BodyPart { Torso, Head, LeftArm, RightArm }

    public readonly struct DamageEvent
    {
        public readonly GameObject Source;
        public readonly Damageable Target;
        public readonly BodyPart Part;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly float Amount;
        public readonly float Impulse;
        public readonly bool IsFatal;

        public DamageEvent(GameObject source, Damageable target, BodyPart part,
            Vector3 point, Vector3 direction, float amount, float impulse, bool isFatal)
        {
            Source = source;
            Target = target;
            Part = part;
            Point = point;
            Direction = direction;
            Amount = amount;
            Impulse = impulse;
            IsFatal = isFatal;
        }
    }
}
