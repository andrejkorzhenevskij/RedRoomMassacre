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
        [Header("Empty hand")]
        [Min(1f)] public float fistDamage = 18f;
        [Min(0.1f)] public float fistReach = 0.42f;
        public GameObject fistVisual;
        [Header("Wall slam")]
        [Min(1f)] public float wallSlamDamage = 48f;
        [Range(0.01f, 0.1f)] public float wallContactGap = 0.04f;
        [Header("Kick")]
        [Min(1f)] public float kickDamage = 24f;
        [Min(0.1f)] public float kickReach = 0.9f;
        [Range(0f, 80f)] public float kickHalfAngle = 35f;
        [Min(0.01f)] public float kickHeightTolerance = 0.45f;
        [Min(0.01f)] public float kickRadius = 0.14f;
        public AttackPhase Phase { get; private set; }
        public bool IsBusy => Phase != AttackPhase.Ready;
        public bool IsPaused => pauseRemaining > 0f;
        public float MovementScale => IsBusy ? 0.3f : 1f;

        private readonly HashSet<Damageable> hitTargets = new HashSet<Damageable>();
        private readonly Dictionary<Damageable, Contact> contacts = new Dictionary<Damageable, Contact>();
        private readonly List<Contact> orderedContacts = new List<Contact>();
        private struct Contact
        {
            public Hurtbox zone;
            public Vector3 point;
            public float distance;
            public bool sever;
        }
        private ChopIntent chopIntent;
        private Damageable owner;
        private float elapsed;
        private float pauseRemaining;
        private Vector3 previousTip;
        private float activeDamage, activeRadius, activeImpulse;
        private PortableLamp activeLamp;
        private CameraDevice activeCamera;
        private TwoHandedAxe activeAxe;
        private Quaternion cameraRestRotation;
        private DamageKind activeKind;
        private float activeWeight;
        private PlayerController handController;
        private bool consumedItem;
        private Damageable grappleStrikeTarget;
        private bool grappleStrike;
        private bool wallSlam;
        private Collider slamWall;
        private bool kicking;
        private Vector3 kickOffset, legRestPosition;
        private Quaternion legRestRotation;
        private Transform kickLeg;
        private Vector3 preKickPivot, preKickTip;
        private Quaternion preKickRotation;
        public bool IsKicking => kicking && IsBusy;
        private Vector3 KickOrigin => transform.TransformPoint(new Vector3(0.18f,
            0.45f * (GetComponent<PlayerController>()?.HeightScale ?? 1f), 0.08f));

        public bool IsUsingCamera(CameraDevice device) => IsBusy && device && activeCamera == device;
        public bool IsUsingAxe(TwoHandedAxe axe) => IsBusy && axe && activeAxe == axe;

        private void Awake()
        {
            owner = GetComponent<Damageable>();
            kickLeg = GetComponent<HitFeedback>()?.visual?.Find("Right Leg");
            if (kickLeg) { legRestPosition = kickLeg.localPosition; legRestRotation = kickLeg.localRotation; }
        }

        public bool TryAttack() => StartAttack(false);

        public float HandReach(PlayerHand hand) => hand?.Item?.Source is TwoHandedAxe axe ? axe.reach : hand?.Item?.Source is CameraDevice
            || hand?.Item?.Source is PortableLamp ? 0.65f : Mathf.Max(0.1f, fistReach);

        private bool StartAttack(bool handCommand)
        {
            if (owner == null) owner = GetComponent<Damageable>();
            var controller = GetComponent<PlayerController>();
            if (controller && (controller.BlockRecovering || controller.IsCaptured || (controller.InGrapple && !handCommand))) return false;
            if (!isActiveAndEnabled || owner.IsDead || IsBusy || !weaponPivot || !weaponTip)
                return false;
            hitTargets.Clear();
            activeDamage = damage;
            activeRadius = hitRadius;
            activeImpulse = impulse;
            activeKind = DamageKind.Melee;
            chopIntent = ChopIntent.Normal;
            activeWeight = 1f;
            consumedItem = false;
            handController = null;
            grappleStrikeTarget = null;
            grappleStrike = false;
            wallSlam = false;
            slamWall = null;
            elapsed = 0f;
            Phase = AttackPhase.Windup;
            return true;
        }

        public bool TryHandAttack(PlayerHand hand, ChopIntent intent = ChopIntent.Normal)
        {
            var controller = GetComponent<PlayerController>();
            if (!controller || hand == null || !controller.CanStrikeWithHand(hand.Side)
                || (hand != controller.LeftHand && hand != controller.RightHand)) return false;
            Component item = hand.Item?.Source;
            if (item && !(item is PortableLamp) && !(item is CameraDevice device && device.State == CameraDeviceState.Held)
                && !(item is TwoHandedAxe axe && axe.isActiveAndEnabled && axe.IsHeldBy(controller))) return false;
            if (!StartAttack(true)) return false;
            handController = controller;
            grappleStrikeTarget = controller.GrappleTarget ? controller.GrappleTarget.GetComponent<Damageable>() : null;
            grappleStrike = grappleStrikeTarget;
            activeLamp = hand.Item?.Source as PortableLamp;
            activeCamera = hand.Item?.Source as CameraDevice;
            activeAxe = hand.Item?.Source as TwoHandedAxe;
            chopIntent = activeAxe ? intent : ChopIntent.Normal;
            if (activeAxe) activeAxe.SetIntent(intent);
            bool itemStrike = activeLamp || activeCamera || activeAxe;
            if (activeCamera)
            {
                cameraRestRotation = activeCamera.transform.localRotation;
                activeKind = DamageKind.CameraBash;
                activeWeight = activeCamera.bashWeight;
            }
            weaponPivot.localPosition = new Vector3(activeAxe ? 0f : hand.Side == HandSide.Left ? -0.38f : 0.38f,
                (chopIntent == ChopIntent.Leg ? 0.4f : 1.08f) * handController.HeightScale, 0.08f);
            weaponTip.localPosition = Vector3.forward * HandReach(hand);
            activeDamage = Mathf.Max(1f, activeAxe ? activeAxe.hitDamage : activeCamera ? activeCamera.hitDamage : activeLamp ? activeLamp.hitDamage : fistDamage);
            activeRadius = itemStrike ? 0.2f : 0.14f;
            activeImpulse = itemStrike ? impulse : 1.5f;
            if (fistVisual) fistVisual.SetActive(!itemStrike);
            return true;
        }

        internal bool CanSever(Hurtbox zone) => activeAxe && activeAxe.isActiveAndEnabled
            && activeAxe.IsHeldBy(handController) && Phase == AttackPhase.Strike
            && zone && zone.isActiveAndEnabled && zone.owner && !zone.owner.IsDead
            && zone.owner.IsAttached(zone.part) && zone.GetComponent<MeshFilter>() && zone.GetComponent<MeshRenderer>()
            && (chopIntent == ChopIntent.LeftArm && zone.part == BodyPart.LeftArm
                || chopIntent == ChopIntent.RightArm && zone.part == BodyPart.RightArm
                || chopIntent == ChopIntent.Leg && (zone.part == BodyPart.LeftLeg || zone.part == BodyPart.RightLeg));

        public bool TryWallSlam(PlayerHand hand)
        {
            var controller = GetComponent<PlayerController>();
            if (!controller || !controller.FindSlamWall(out RaycastHit wall) || !TryHandAttack(hand)) return false;
            wallSlam = true;
            slamWall = wall.collider;
            activeDamage = Mathf.Max(1f, wallSlamDamage);
            activeImpulse = impulse;
            return true;
        }

        // Target eligibility deliberately ignores cooldown so context never falls back to a jump during recovery.
        public Hurtbox FindKickTarget()
        {
            Hurtbox nearest = null;
            float distance = float.PositiveInfinity;
            foreach (Collider contact in Physics.OverlapSphere(KickOrigin, kickReach,
                hurtboxMask, QueryTriggerInteraction.Collide))
            {
                if (!contact.TryGetComponent(out Hurtbox zone) || !CanKickTarget(zone, out Vector3 point)) continue;
                float candidate = (point - KickOrigin).sqrMagnitude;
                if (candidate < distance) { nearest = zone; distance = candidate; }
            }
            return nearest;
        }

        private bool CanKickTarget(Hurtbox target, out Vector3 point)
        {
            point = default;
            if (!target || !target.isActiveAndEnabled || !target.owner || target.owner == owner
                || target.owner.IsDead || !target.owner.isActiveAndEnabled
                || !target.TryGetComponent(out Collider shape) || !shape.enabled) return false;
            point = shape.ClosestPoint(KickOrigin);
            Vector3 offset = point - KickOrigin;
            Vector3 horizontal = Vector3.ProjectOnPlane(offset, Vector3.up);
            return horizontal.sqrMagnitude > 0.0001f && offset.sqrMagnitude <= kickReach * kickReach
                && Mathf.Abs(offset.y) <= kickHeightTolerance
                && Vector3.Dot(transform.forward, horizontal.normalized) >= Mathf.Cos(kickHalfAngle * Mathf.Deg2Rad)
                && ClearKickPath(point);
        }

        private bool ClearKickPath(Vector3 point)
        {
            Vector3 offset = point - KickOrigin;
            return !Physics.CheckSphere(KickOrigin, kickRadius, obstructionMask, QueryTriggerInteraction.Ignore)
                && !Physics.SphereCast(KickOrigin, kickRadius, offset.normalized, out _, offset.magnitude,
                    obstructionMask, QueryTriggerInteraction.Ignore);
        }

        public bool TryKick(Hurtbox target)
        {
            var controller = GetComponent<PlayerController>();
            if (!controller || !controller.CanAct || !controller.HasBothLegs || controller.InGrapple || !controller.IsGrounded
                || !CanKickTarget(target, out Vector3 point) || !StartAttack(false)) return false;
            kicking = true;
            handController = controller;
            preKickPivot = weaponPivot.localPosition;
            preKickRotation = weaponPivot.localRotation;
            preKickTip = weaponTip.localPosition;
            // Commit a body-relative trajectory at release, not a homing hit on the selected target.
            kickOffset = transform.InverseTransformVector(point - KickOrigin);
            activeDamage = Mathf.Max(1f, kickDamage);
            activeRadius = Mathf.Max(0.01f, kickRadius);
            activeImpulse = impulse;
            weaponPivot.position = KickOrigin;
            weaponPivot.localRotation = Quaternion.identity;
            weaponTip.localPosition = Vector3.zero;
            previousTip = weaponTip.position;
            if (fistVisual) fistVisual.SetActive(false);
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
            if (activeAxe && chopIntent == ChopIntent.LeftArm) angle = -angle;
            if (kicking)
            {
                float extension = Phase == AttackPhase.Windup ? Mathf.Lerp(0f, -0.12f, progress)
                    : Phase == AttackPhase.Strike ? Mathf.Lerp(-0.12f, 1f, progress) : 1f - progress;
                weaponPivot.position = KickOrigin;
                weaponPivot.localRotation = Quaternion.identity;
                weaponTip.localPosition = kickOffset * extension;
                // ponytail: pose the existing blockout leg; no rig or animation assets.
                if (kickLeg)
                {
                    kickLeg.localPosition = legRestPosition + kickOffset * (extension * 0.5f);
                    kickLeg.localRotation = legRestRotation * Quaternion.Euler(-55f * extension, 0f, 0f);
                }
            }
            else weaponPivot.localRotation = Quaternion.Euler(0f, angle, 0f);
            if (handController && !kicking)
            {
                Vector3 position = weaponPivot.localPosition;
                position.y = (chopIntent == ChopIntent.Leg ? 0.4f : 1.08f) * handController.HeightScale;
                weaponPivot.localPosition = position;
            }
            if (activeLamp)
            {
                activeLamp.transform.localPosition = weaponPivot.localPosition
                    + weaponPivot.localRotation * (Vector3.forward * 0.45f) - Vector3.up * 0.25f;
                activeLamp.transform.localRotation = weaponPivot.localRotation;
            }
            if (activeCamera)
            {
                activeCamera.transform.localPosition = weaponPivot.localPosition
                    + weaponPivot.localRotation * (Vector3.forward * 0.45f);
                activeCamera.transform.localRotation = weaponPivot.localRotation;
            }
            if (Phase == AttackPhase.Strike)
            {
                CheckContacts(previousTip, weaponTip.position);
                if (Phase != AttackPhase.Strike) return;
            }
            previousTip = weaponTip.position;

            if (progress < 1f) return;
            elapsed = 0f;
            Phase = Phase == AttackPhase.Windup ? AttackPhase.Strike
                : Phase == AttackPhase.Strike ? AttackPhase.Recovery : AttackPhase.Ready;
            if (!IsBusy) ResetHandPose();
        }

        private void CheckContacts(Vector3 start, Vector3 end)
        {
            if (consumedItem) return;
            RaycastHit wall = default;
            if (wallSlam && (!handController || !handController.FindSlamWall(out wall) || wall.collider != slamWall)) return;
            contacts.Clear();
            orderedContacts.Clear();
            // Gather real swept contacts before choosing a part; never rely on PhysX overlap array order.
            foreach (Collider collider in Physics.OverlapCapsule(start, end, activeRadius,
                         hurtboxMask, QueryTriggerInteraction.Collide))
            {
                if (!collider.TryGetComponent(out Hurtbox zone) || !zone.isActiveAndEnabled || zone.owner == null
                    || zone.owner == owner || zone.owner.IsDead || !zone.owner.IsAttached(zone.part)
                    || hitTargets.Contains(zone.owner)) continue;
                if (grappleStrikeTarget && zone.owner != grappleStrikeTarget) continue;
                Vector3 point = collider.ClosestPoint(end);
                Vector3 origin = kicking ? KickOrigin : activeAxe ? weaponPivot.position : transform.position + Vector3.up * 1.15f;
                if (kicking ? !ClearKickPath(point)
                    : activeAxe ? Physics.CheckSphere(origin, activeRadius, obstructionMask, QueryTriggerInteraction.Ignore)
                        || Physics.SphereCast(origin, activeRadius, (point - origin).normalized, out _,
                            Vector3.Distance(origin, point), obstructionMask, QueryTriggerInteraction.Ignore)
                    : Physics.Linecast(origin, point, obstructionMask, QueryTriggerInteraction.Ignore))
                    continue;
                var candidate = new Contact { zone = zone, point = point, distance = (point - end).sqrMagnitude, sever = CanSever(zone) };
                if (!contacts.TryGetValue(zone.owner, out Contact best) || candidate.sever && !best.sever
                    || candidate.sever == best.sever && (candidate.distance < best.distance
                        || candidate.distance == best.distance && (int)zone.part < (int)best.zone.part))
                    contacts[zone.owner] = candidate;
            }
            orderedContacts.AddRange(contacts.Values);
            orderedContacts.Sort((a, b) => a.distance != b.distance ? a.distance.CompareTo(b.distance)
                : string.CompareOrdinal(a.zone.owner.name, b.zone.owner.name));
            foreach (Contact contact in orderedContacts)
            {
                Hurtbox zone = contact.zone;
                Vector3 point = contact.point;
                Vector3 origin = kicking ? KickOrigin : transform.position + Vector3.up * 1.15f;
                Vector3 direction = Vector3.ProjectOnPlane(point - origin, Vector3.up).normalized;
                if (wallSlam) { point = wall.point + wall.normal * 0.01f; direction = -wall.normal; }
                // A blocked contact consumes this target for the whole swing, including after the window closes.
                hitTargets.Add(zone.owner);
                if (zone.owner.ApplyDamage(gameObject, zone, point, direction, activeDamage, activeImpulse, activeKind, activeWeight, contact.sever))
                {
                    pauseRemaining = contactPause;
                    if (Phase != AttackPhase.Strike) break;
                    if (activeAxe) { consumedItem = true; break; }
                    if (activeLamp || activeCamera)
                    {
                        // All synchronous damage/evidence observers run before light or recording is lost.
                        if (activeLamp) activeLamp.BreakAfterHit();
                        if (activeCamera) activeCamera.BreakAfterHit();
                        activeLamp = null;
                        activeCamera = null;
                        consumedItem = true;
                        break;
                    }
                }
            }
        }

        internal void EndGrappleStrike()
        {
            if (!grappleStrike || !IsBusy) return;
            Phase = AttackPhase.Recovery;
            elapsed = 0f;
            ResetHandPose();
        }

        internal void CancelAttack()
        {
            Phase = AttackPhase.Ready;
            elapsed = pauseRemaining = 0f;
            hitTargets.Clear();
            ResetHandPose();
            if (weaponPivot) weaponPivot.localRotation = Quaternion.Euler(0f, 15f, 0f);
        }

        internal void InterruptAttack()
        {
            if (!IsBusy) return;
            if (Phase != AttackPhase.Recovery) { Phase = AttackPhase.Recovery; elapsed = 0f; }
            ResetHandPose();
            consumedItem = true;
        }

        private void OnDisable()
        {
            GetComponent<PlayerController>()?.ReleaseGrapple();
            CancelAttack();
        }

        private void ResetHandPose()
        {
            if (kicking)
            {
                weaponPivot.localPosition = preKickPivot;
                weaponPivot.localRotation = preKickRotation;
                weaponTip.localPosition = preKickTip;
                if (kickLeg)
                {
                    kickLeg.localPosition = legRestPosition;
                    kickLeg.localRotation = legRestRotation;
                }
            }
            kicking = false;
            if (fistVisual) fistVisual.SetActive(false);
            if (activeLamp) activeLamp.RestoreHeldPose();
            if (activeCamera)
            {
                activeCamera.RestoreHeldPose();
                activeCamera.transform.localRotation = cameraRestRotation;
            }
            activeLamp = null;
            activeCamera = null;
            handController = null;
            activeAxe = null;
            grappleStrikeTarget = null;
            grappleStrike = false;
            wallSlam = false;
            slamWall = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (!weaponTip) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(weaponTip.position, hitRadius);
        }
    }
}
