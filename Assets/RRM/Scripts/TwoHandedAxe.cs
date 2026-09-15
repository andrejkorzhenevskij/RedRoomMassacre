using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RRM
{
    public enum ChopIntent { Normal, RightArm, LeftArm, Leg }

    [DisallowMultipleComponent]
    public sealed class TwoHandedAxe : MonoBehaviour
    {
        [Min(0.1f)] public float pickupDistance = 1.6f;
        [Min(1f)] public float hitDamage = 45f;
        [Min(0.1f)] public float reach = 1.05f;
        [Header("Screen-space gesture")]
        [Min(1f)] public float gesturePixels = 55f;
        [Range(0f, 80f)] public float directionTolerance = 30f;
        [Min(0f)] public float chordWindow = 0.12f;
        [Header("One saved object, relocated on start")]
        [Tooltip("0 chooses a fresh random seed; any other value repeats the same choice.")]
        public int spawnSeed;
        public Vector3[] spawnPoints;
        public int SpawnIndex { get; private set; } = -1;
        public PlayerController Owner { get; private set; }
        public ChopIntent LastIntent { get; private set; }
        private MeleeAttack attack;
        private Transform[] arms;
        private Vector3[] armPositions, armScales;
        private Quaternion[] armRotations;
        private int buttons;
        private float pressedAt;
        private Vector2 motion;
        private bool waitForRelease = true;

        private void Start() => ChooseSpawn(spawnSeed);

        public bool ChooseSpawn(int seed)
        {
            if (Owner || spawnPoints == null || spawnPoints.Length == 0) return false;
            var random = seed == 0 ? new System.Random(System.Guid.NewGuid().GetHashCode()) : new System.Random(seed);
            int first = random.Next(spawnPoints.Length);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                int index = (first + i) % spawnPoints.Length;
                if (!SupportedPose(spawnPoints[index], out Vector3 position)) continue;
                SpawnIndex = index;
                transform.SetPositionAndRotation(position, Quaternion.identity);
                return true;
            }
            Debug.LogError("TwoHandedAxe has no clear supported spawn point.", this);
            return false;
        }

        public static bool SupportedPose(Vector3 point, out Vector3 position)
        {
            position = point;
            if (!Physics.Raycast(point + Vector3.up, Vector3.down, out RaycastHit surface, 3f, 1,
                    QueryTriggerInteraction.Ignore) || surface.normal.y < 0.95f) return false;
            position = surface.point + Vector3.up * 0.11f;
            Vector3 center = position + Vector3.forward * 0.55f;
            if (Physics.CheckBox(center, new Vector3(0.28f, 0.09f, 0.62f), Quaternion.identity, 1,
                    QueryTriggerInteraction.Ignore)) return false;
            foreach (float z in new[] { 0f, 1.05f })
                foreach (float x in new[] { -0.25f, 0.25f })
                    if (!Physics.Raycast(position + new Vector3(x, 0.04f, z), Vector3.down,
                            0.2f, 1, QueryTriggerInteraction.Ignore)) return false;
            return true;
        }

        public bool IsHeldBy(PlayerController holder) => holder && Owner == holder
            && holder.LeftHand.Item?.Source == this && holder.RightHand.Item?.Source == this;

        public bool TryPickup(PlayerController holder, HandSide side)
        {
            if (!isActiveAndEnabled || Owner || !holder || !holder.CanHandleItems
                || holder.LeftHand.State != HandState.Empty || holder.RightHand.State != HandState.Empty) return false;
            var melee = holder.GetComponent<MeleeAttack>();
            if (!melee || !melee.isActiveAndEnabled || melee.IsBusy) return false;
            var zones = holder.GetComponentsInChildren<Hurtbox>();
            Transform left = zones.FirstOrDefault(zone => zone.part == BodyPart.LeftArm)?.transform;
            Transform right = zones.FirstOrDefault(zone => zone.part == BodyPart.RightArm)?.transform;
            if (!left || !right) return false;
            Vector3 hand = holder.transform.TransformPoint(new Vector3(side == HandSide.Left ? -0.38f : 0.38f,
                1.05f * holder.HeightScale, 0.2f));
            if (!holder.CanReachItem(hand, transform.TransformPoint(Vector3.forward * 0.3f), pickupDistance, 1)) return false;
            HandItem.SetFalling(this, false);
            Owner = holder; attack = melee;
            arms = new[] { left, right };
            armPositions = arms.Select(arm => arm.localPosition).ToArray();
            armRotations = arms.Select(arm => arm.localRotation).ToArray();
            armScales = arms.Select(arm => arm.localScale).ToArray();
            transform.SetParent(holder.transform, false);
            var item = new HandItem(this, transform);
            holder.LeftHand.SetItem(item);
            holder.RightHand.SetItem(item);
            CancelInput();
            Pose();
            return true;
        }

        public bool TryPlace(PlayerController holder)
        {
            if (!IsHeldBy(holder) || !holder.CanHandleItems || attack.IsBusy) return false;
            DropFrom(holder);
            return true;
        }

        internal void DropFrom(PlayerController holder)
        {
            if (!holder || Owner != holder) return;
            if (attack.IsUsingAxe(this)) attack.InterruptAttack();
            Vector3 oldPosition = transform.position;
            transform.SetParent(null, true);
            // ponytail: supported local placement, otherwise retain the last visible pose; no falling-item physics.
            if (SupportedPose(oldPosition, out Vector3 position)
                && !Physics.Linecast(oldPosition, position, 1, QueryTriggerInteraction.Ignore))
                transform.SetPositionAndRotation(position, Quaternion.identity);
            for (int i = 0; i < arms.Length; i++)
                if (arms[i])
                {
                    arms[i].localPosition = armPositions[i];
                    arms[i].localRotation = armRotations[i];
                    arms[i].localScale = armScales[i];
                }
            if (holder.LeftHand.Item?.Source == this) holder.LeftHand.SetItem(null);
            if (holder.RightHand.Item?.Source == this) holder.RightHand.SetItem(null);
            Owner = null;
            CancelInput();
        }

        internal void CancelInput()
        {
            buttons = 0;
            motion = Vector2.zero;
            waitForRelease = true;
        }

        internal void ReadInput()
        {
            var mouse = Mouse.current;
            if (!Owner || !Owner.CanUseHands || Owner.InGrapple || Owner.BlockRecovering || mouse == null
                || !attack.isActiveAndEnabled || attack.IsBusy) { CancelInput(); return; }
            int down = (mouse.leftButton.isPressed ? 1 : 0) | (mouse.rightButton.isPressed ? 2 : 0);
            if (waitForRelease)
            {
                if (down == 0) waitForRelease = false;
                return;
            }
            int pressed = (mouse.leftButton.wasPressedThisFrame ? 1 : 0) | (mouse.rightButton.wasPressedThisFrame ? 2 : 0);
            int released = (mouse.leftButton.wasReleasedThisFrame ? 1 : 0) | (mouse.rightButton.wasReleasedThisFrame ? 2 : 0);
            if (buttons == 0)
            {
                if (pressed == 0) return;
                buttons = pressed;
                pressedAt = Time.time;
                motion = Vector2.zero;
            }
            else if (Time.time - pressedAt <= Mathf.Max(0f, chordWindow)) buttons |= pressed;
            motion += mouse.delta.ReadValue();
            if ((released & buttons) == 0) return;
            LastIntent = Recognize(buttons, motion);
            attack.TryHandAttack(Owner.RightHand, LastIntent);
            CancelInput();
        }

        private ChopIntent Recognize(int gestureButtons, Vector2 delta)
        {
            if (delta.magnitude < Mathf.Max(1f, gesturePixels)) return ChopIntent.Normal;
            Vector2 direction = gestureButtons == 3 ? Vector2.down : gestureButtons == 2 ? Vector2.left : Vector2.right;
            if (Vector2.Angle(delta, direction) > directionTolerance) return ChopIntent.Normal;
            return gestureButtons == 3 ? ChopIntent.Leg : gestureButtons == 2 ? ChopIntent.RightArm : ChopIntent.LeftArm;
        }

        internal void SetIntent(ChopIntent intent) => LastIntent = intent;

        private void LateUpdate()
        {
            if (!Owner) return;
            if (!IsHeldBy(Owner) || !Owner.HasHand(HandSide.Left) || !Owner.HasHand(HandSide.Right)
                || arms.Any(arm => !arm || !arm.gameObject.activeInHierarchy))
            { DropFrom(Owner); return; }
            Pose();
        }

        private void Pose()
        {
            if (Owner.TryGetComponent(out CharCrafterVisual rig))
            {
                rig.PoseAxe(this);
                return;
            }
            float height = Owner.HeightScale;
            transform.localPosition = new Vector3(0f, 1.08f * height, 0.08f);
            transform.rotation = attack.IsUsingAxe(this) ? attack.weaponPivot.rotation : Owner.transform.rotation;
            if (attack.IsUsingAxe(this)) transform.position = attack.weaponPivot.position;
            // ponytail: aim the existing blockout arms at two points on one handle; no IK or duplicate hands.
            for (int i = 0; i < arms.Length; i++)
            {
                Vector3 shoulder = Owner.transform.TransformPoint(new Vector3(i == 0 ? -0.35f : 0.35f, 1.32f * height, 0f));
                Vector3 grip = transform.TransformPoint(Vector3.forward * (i == 0 ? 0.55f : 0.22f));
                arms[i].position = (shoulder + grip) * 0.5f;
                arms[i].rotation = Quaternion.FromToRotation(Vector3.up, grip - shoulder);
                Vector3 scale = armScales[i];
                scale.y = Vector3.Distance(shoulder, grip) / Mathf.Max(0.01f, arms[i].parent.lossyScale.y);
                arms[i].localScale = scale;
            }
        }

        private void OnDisable()
        {
            if (Owner && gameObject.activeInHierarchy) DropFrom(Owner);
        }
    }
}
