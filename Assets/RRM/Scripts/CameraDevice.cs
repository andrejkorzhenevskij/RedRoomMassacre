using System;
using System.Collections.Generic;
using UnityEngine;

namespace RRM
{
    public enum CameraDeviceState { Held, Placed, Broken }

    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class CameraDevice : MonoBehaviour
    {
        [Min(0.1f)] public float pickupDistance = 1.5f;
        public LayerMask pickupObstructionMask = 1;
        [Min(1f)] public float hitDamage = 68f;
        [Min(0f)] public float bashWeight = 100f;
        public string Id { get; private set; }
        public PlayerController Owner { get; private set; }
        public CameraDeviceState State => broken ? CameraDeviceState.Broken
            : Owner ? CameraDeviceState.Held : CameraDeviceState.Placed;
        public bool IsAiming => recorder && recorder.IsAiming;
        public IReadOnlyList<RecordedEvent> Events => events;
        public IReadOnlyList<BloodObservation> BloodEvents => bloodEvents;
        public int RecordedHits
        {
            get
            {
                int count = 0;
                foreach (var recorded in events) if (!recorded.Damage.IsBleeding) count++;
                return count;
            }
        }
        public string RecordedLimbNames => string.Join(", ", events
            .FindAll(recorded => recorded.Damage.SeveredPart.HasValue)
            .ConvertAll(recorded => recorded.Damage.SeveredPart.Value.ToString()));
        public int RecordedSeverings
        {
            get
            {
                int count = 0;
                foreach (var recorded in events) if (recorded.Damage.SeveredPart.HasValue) count++;
                return count;
            }
        }
        public bool DeathRecorded { get; private set; }
        public float? LastEventDistance => events.Count > 0 ? events[events.Count - 1].Distance : (float?)null;
        public int RecordedCameraBashes
        {
            get
            {
                int count = 0;
                foreach (var recorded in events) if (recorded.Damage.Kind == DamageKind.CameraBash) count++;
                return count;
            }
        }
        public float ReportWeight
        {
            get
            {
                if (broken) return 0f;
                float total = 0f;
                foreach (var recorded in events) total += recorded.Damage.BaseWeight * recorded.Clarity;
                return total;
            }
        }

        public readonly struct RecordedEvent
        {
            public readonly DamageEvent Damage;
            public bool Visible => true;
            public readonly float Distance;
            public readonly float LightLevel;
            public VisibilityLevel Visibility => LightSource.Classify(LightLevel);
            public float Clarity => LightLevel / 100f;

            public RecordedEvent(DamageEvent damage, float distance, float lightLevel)
            {
                Damage = damage;
                Distance = distance;
                LightLevel = Mathf.Clamp(lightLevel, 0f, 100f);
            }
        }

        public readonly struct BloodObservation
        {
            public readonly BloodEvent Blood;
            public readonly bool Visible;
            public readonly float LightLevel;
            public VisibilityLevel Visibility => LightSource.Classify(LightLevel);
            public float Clarity => Visible ? LightLevel / 100f : 0f;

            public BloodObservation(BloodEvent blood, bool visible, float lightLevel)
            {
                Blood = blood;
                Visible = visible;
                LightLevel = Mathf.Clamp(lightLevel, 0f, 100f);
            }
        }

        private readonly List<RecordedEvent> events = new List<RecordedEvent>();
        private readonly List<BloodObservation> bloodEvents = new List<BloodObservation>();

        private CameraRecorder recorder;
        private Vector3 handPosition;
        private bool broken;

        internal void Record(DamageEvent hit, float distance, float lightLevel)
        {
            if (broken) return;
            events.Add(new RecordedEvent(hit, distance, lightLevel));
            DeathRecorded |= hit.IsFatal;
        }

        internal void RecordBlood(BloodObservation observation)
        {
            if (!broken) bloodEvents.Add(observation);
        }

        internal void RestoreHeldPose()
        {
            if (!Owner || broken) return;
            Vector3 mount = handPosition;
            mount.x = Mathf.Abs(mount.x) * (Owner.HandHolding(this)?.Side == HandSide.Left ? -1f : 1f);
            mount.y *= Owner.HeightScale;
            transform.localPosition = mount;
        }

        internal void BreakAfterHit()
        {
            if (broken) return;
            broken = true;
            Owner?.HandHolding(this)?.SetItem(null);
            Owner = null;
            transform.SetParent(null, true);
            foreach (Renderer visual in GetComponentsInChildren<Renderer>()) visual.enabled = false;
            foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            // Keep this device's diagnostic history/report, but never its view or recording eligibility.
            if (recorder) recorder.RefreshLiveFeed();
        }

        private void Awake()
        {
            Id = Guid.NewGuid().ToString("N");
            recorder = GetComponent<CameraRecorder>();
            handPosition = transform.localPosition;
            var holder = GetComponentInParent<PlayerController>();
            if (holder && holder.TryHoldCamera(this, HandSide.Left, handPosition)) Owner = holder;
        }

        public bool TryPlace(PlayerController holder)
        {
            if (broken || !holder || !holder.CanHandleItems || Owner != holder || holder.GetComponent<MeleeAttack>()?.IsBusy == true) return false;
            DropFrom(holder);
            return true;
        }

        internal void DropFrom(PlayerController holder)
        {
            if (Owner != holder || !holder) return;
            transform.SetParent(null, true);
            holder.HandHolding(this)?.SetItem(null);
            Owner = null;
            if (recorder) recorder.RefreshLiveFeed();
        }

        public bool TryPickup(PlayerController holder, HandSide side = HandSide.Left)
        {
            if (broken || !isActiveAndEnabled || !holder || !holder.CanHandleItems || Owner || holder.HeldCamera)
                return false;
            Vector3 mount = handPosition;
            mount.x = Mathf.Abs(mount.x) * (side == HandSide.Left ? -1f : 1f);
            mount.y *= holder.HeightScale;
            Vector3 hand = holder.transform.TransformPoint(mount);
            if (!holder.CanReachItem(hand, transform.position, pickupDistance, pickupObstructionMask))
                return false;
            if (!holder.TryHoldCamera(this, side, mount)) return false;
            Owner = holder;
            if (recorder) recorder.RefreshLiveFeed();
            return true;
        }
    }
}
