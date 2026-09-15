using UnityEngine;

namespace RRM
{
    [DisallowMultipleComponent]
    public sealed class PortableLamp : MonoBehaviour
    {
        [Min(0.1f)] public float pickupDistance = 1.5f;
        [Min(1f)] public float hitDamage = 54f;
        public LightSource lightSource;
        public PlayerController Owner { get; private set; }
        private static readonly Vector3 Mount = new Vector3(0.55f, 0.85f, 0.35f);

        internal void RestoreHeldPose()
        {
            if (!Owner) return;
            Vector3 mount = Mount;
            mount.x *= Owner.HandHolding(this)?.Side == HandSide.Left ? -1f : 1f;
            mount.y *= Owner.HeightScale;
            transform.localPosition = mount;
            transform.localRotation = Quaternion.identity;
        }

        internal void BreakAfterHit()
        {
            Owner?.HandHolding(this)?.SetItem(null);
            Owner = null;
            lightSource.enabled = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        public bool TryPickup(PlayerController holder, HandSide side)
        {
            if (!isActiveAndEnabled || Owner || !holder || !holder.CanHandleItems
                || holder.GetComponent<MeleeAttack>().IsBusy) return false;
            PlayerHand hand = side == HandSide.Left ? holder.LeftHand : holder.RightHand;
            if (hand.State != HandState.Empty) return false;
            Vector3 mount = Mount;
            mount.x *= side == HandSide.Left ? -1f : 1f;
            mount.y *= holder.HeightScale;
            Vector3 origin = holder.transform.TransformPoint(mount);
            if (!holder.CanReachItem(origin, lightSource.transform.position, pickupDistance, 1)) return false;
            HandItem.SetFalling(this, false);
            transform.SetParent(holder.transform, false);
            transform.localPosition = mount;
            transform.localRotation = Quaternion.identity;
            hand.SetItem(new HandItem(this, transform));
            Owner = holder;
            return true;
        }

        public bool TryPlace(PlayerController holder)
        {
            if (!holder || !holder.CanHandleItems || Owner != holder || holder.GetComponent<MeleeAttack>().IsBusy) return false;
            return PlaceOnSupport(holder);
        }

        internal void DropFrom(PlayerController holder)
        {
            if (!holder || Owner != holder || PlaceOnSupport(holder)) return;
            // ponytail: no falling-item physics; retain the visible world pose if no support is reachable.
            transform.SetParent(null, true);
            holder.HandHolding(this)?.SetItem(null);
            Owner = null;
        }

        private bool PlaceOnSupport(PlayerController holder)
        {
            // ponytail: upright 0.36 x 0.7 m blockout; local support probes, not a placement system.
            Vector3 origin = transform.position + Vector3.up * 0.35f;
            if (!holder.CanReachItem(origin, origin, 0f, 1)) return false;
            foreach (float ahead in new[] { 0.35f, 0f, -0.2f })
            {
                Vector3 probe = origin + holder.transform.forward * ahead;
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit surface, 1.9f, 1, QueryTriggerInteraction.Ignore)
                    || surface.normal.y < 0.95f) continue;
                Vector3 bottom = surface.point + Vector3.up * 0.02f;
                Vector3 center = bottom + Vector3.up * 0.35f;
                if (Physics.Linecast(origin, center, 1, QueryTriggerInteraction.Ignore)
                    || Physics.CheckBox(center, new Vector3(0.2f, 0.34f, 0.2f), Quaternion.identity, 1,
                        QueryTriggerInteraction.Ignore)) continue;
                bool supported = true;
                foreach (float x in new[] { -0.16f, 0.16f })
                    foreach (float z in new[] { -0.16f, 0.16f })
                        supported &= Physics.Raycast(bottom + new Vector3(x, 0.04f, z), Vector3.down,
                            0.1f, 1, QueryTriggerInteraction.Ignore);
                if (!supported) continue;
                transform.SetParent(null, true);
                transform.SetPositionAndRotation(bottom, Quaternion.identity);
                holder.HandHolding(this)?.SetItem(null);
                Owner = null;
                return true;
            }
            return false;
        }
    }
}
