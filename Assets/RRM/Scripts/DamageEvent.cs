using UnityEngine;

namespace RRM
{
    public enum BodyPart { Torso, Head, LeftArm, RightArm, LeftLeg, RightLeg }
    public enum LimbState { Attached, Severed }
    public enum DamageKind { Melee, CameraBash, Bleeding }

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
        public readonly DamageKind Kind;
        public readonly float BaseWeight;
        public readonly BodyPart? SeveredPart;
        public readonly double GameTime;
        public readonly string TargetName;
        public bool IsBleeding => Kind == DamageKind.Bleeding;

        public DamageEvent(GameObject source, Damageable target, BodyPart part,
            Vector3 point, Vector3 direction, float amount, float impulse, bool isFatal,
            DamageKind kind = DamageKind.Melee, float baseWeight = 1f, BodyPart? severedPart = null)
        {
            Source = source;
            Target = target;
            Part = part;
            Point = point;
            Direction = direction;
            Amount = amount;
            Impulse = impulse;
            IsFatal = isFatal;
            Kind = kind;
            BaseWeight = baseWeight;
            SeveredPart = severedPart;
            GameTime = Time.timeAsDouble;
            TargetName = target ? target.name : string.Empty;
        }
    }
}
