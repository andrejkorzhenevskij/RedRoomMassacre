using System;
using System.Collections.Generic;
using UnityEngine;

namespace RRM
{
    public enum CameraDeviceState { Held, Placed }

    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class CameraDevice : MonoBehaviour
    {
        [Min(0.1f)] public float pickupDistance = 1.5f;
        public LayerMask pickupObstructionMask = 1;
        public string Id { get; private set; }
        public PlayerController Owner { get; private set; }
        public CameraDeviceState State => Owner ? CameraDeviceState.Held : CameraDeviceState.Placed;
        public bool IsAiming => recorder && recorder.IsAiming;
        public IReadOnlyList<RecordedEvent> Events => events;
        public int RecordedHits => events.Count;
        public bool DeathRecorded { get; private set; }
        public float? LastEventDistance => events.Count > 0 ? events[events.Count - 1].Distance : (float?)null;

        public readonly struct RecordedEvent
        {
            public readonly DamageEvent Damage;
            public readonly float Distance;

            public RecordedEvent(DamageEvent damage, float distance)
            {
                Damage = damage;
                Distance = distance;
            }
        }

        private readonly List<RecordedEvent> events = new List<RecordedEvent>();

        private CameraRecorder recorder;
        private Vector3 handPosition;
        private Quaternion handRotation;

        internal void Record(DamageEvent hit, float distance)
        {
            events.Add(new RecordedEvent(hit, distance));
            DeathRecorded |= hit.IsFatal;
        }

        private void Awake()
        {
            Id = Guid.NewGuid().ToString("N");
            recorder = GetComponent<CameraRecorder>();
            handPosition = transform.localPosition;
            handRotation = transform.localRotation;
            Owner = GetComponentInParent<PlayerController>();
            if (Owner) Owner.HeldCamera = this;
        }

        public bool TryPlace(PlayerController holder)
        {
            if (!holder || Owner != holder) return false;
            handRotation = transform.localRotation;
            transform.SetParent(null, true);
            holder.HeldCamera = null;
            Owner = null;
            if (recorder) recorder.RefreshLiveFeed();
            return true;
        }

        public bool TryPickup(PlayerController holder)
        {
            if (!isActiveAndEnabled || !holder || !holder.isActiveAndEnabled || Owner || holder.HeldCamera)
                return false;
            Vector3 hand = holder.transform.TransformPoint(handPosition);
            if (Vector3.Distance(hand, transform.position) > pickupDistance
                || Physics.Linecast(hand, transform.position, pickupObstructionMask, QueryTriggerInteraction.Ignore))
                return false;
            Owner = holder;
            holder.HeldCamera = this;
            transform.SetParent(holder.transform, false);
            transform.localPosition = handPosition;
            transform.localRotation = handRotation;
            if (recorder) recorder.RefreshLiveFeed();
            return true;
        }
    }
}
