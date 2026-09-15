using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace RRM
{
    public enum HandSide { Left, Right }
    public enum HandState { Empty, HoldingItem, Unavailable }

    public interface IHandItem
    {
        Component Source { get; }
        Transform Grip { get; }
    }

    internal sealed class HandItem : IHandItem
    {
        public Component Source { get; }
        public Transform Grip { get; }
        public HandItem(Component source, Transform grip) { Source = source; Grip = grip; }

        internal static void IgnoreActors(Collider shape)
        {
            foreach (var actor in Object.FindObjectsByType<Damageable>())
                foreach (var collider in actor.GetComponentsInChildren<Collider>())
                    if (collider != shape) Physics.IgnoreCollision(shape, collider);
        }

        internal static void SetFalling(Component item, bool falling)
        {
            var body = item.GetComponent<Rigidbody>();
            if (!falling)
            {
                if (!body) return;
                if (!body.isKinematic) body.linearVelocity = body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.detectCollisions = false;
                item.GetComponent<BoxCollider>().enabled = false;
                return;
            }
            if (!body) body = item.gameObject.AddComponent<Rigidbody>();
            var shape = item.GetComponent<BoxCollider>();
            if (!shape) shape = item.gameObject.AddComponent<BoxCollider>();
            // ponytail: one coarse box for each existing blockout item; no general equipment physics system.
            shape.center = item is PortableLamp ? Vector3.up * 0.35f
                : item is TwoHandedAxe ? Vector3.forward * 0.55f : Vector3.zero;
            shape.size = item is PortableLamp ? new Vector3(0.4f, 0.7f, 0.4f)
                : item is TwoHandedAxe ? new Vector3(0.6f, 0.22f, 1.2f) : new Vector3(0.55f, 0.35f, 0.65f);
            shape.enabled = true;
            item.gameObject.layer = LayerMask.NameToLayer("RRMActors");
            IgnoreActors(shape);
            body.mass = 1f;
            body.isKinematic = false;
            body.detectCollisions = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
        }
    }

    public sealed class PlayerHand
    {
        private readonly PlayerController owner;
        private readonly MeleeAttack attack;
        private IHandItem item;
        private int inputFrame = -1;
        private float pressedAt = float.PositiveInfinity;
        private bool tapPending, tapped, waitForRelease, pressed, holdStarted, holding;
        public HandSide Side { get; }
        public IHandItem Item => item != null && item.Source ? item : null;
        public bool IsAvailable => owner.HasHand(Side);
        public HandState State => !IsAvailable ? HandState.Unavailable : Item == null ? HandState.Empty : HandState.HoldingItem;

        public bool WasClickedThisFrame => WasTappedThisFrame;
        public bool WasTappedThisFrame { get { ReadInput(); return tapped; } }
        public bool IsHoldActive => Button?.isPressed == true;
        public bool IsHoldActionActive { get { ReadInput(); return IsHoldActive && Time.time - pressedAt >= HoldThreshold; } }
        public bool WasPressedThisFrame { get { ReadInput(); return pressed; } }
        public bool HoldStartedThisFrame { get { ReadInput(); return holdStarted; } }
        internal bool AttackRequested => WasTappedThisFrame;
        private float HoldThreshold => Mathf.Max(0.01f, owner.handHoldThreshold);

        private ButtonControl Button => owner && IsAvailable && owner.CanUseHands && Mouse.current != null
            ? (Side == HandSide.Left ? Mouse.current.leftButton : Mouse.current.rightButton) : null;

        internal PlayerHand(PlayerController owner, HandSide side)
        {
            this.owner = owner;
            Side = side;
            attack = owner.GetComponent<MeleeAttack>();
        }

        internal void ReadInput()
        {
            // Recorder and controller may update in either order; sample each gesture once.
            if (inputFrame == Time.frameCount) return;
            inputFrame = Time.frameCount;
            tapped = pressed = holdStarted = false;
            if (owner.HeldAxe && !owner.IsCaptured) { CancelInput(); return; }
            ButtonControl button = Button;
            if (button == null) { CancelInput(); return; }
            if (waitForRelease)
            {
                // Cancellation requires a fresh press, including after item/focus changes.
                if (!button.isPressed) waitForRelease = false;
                return;
            }
            if (button.wasPressedThisFrame)
            {
                pressedAt = Time.time;
                tapPending = pressed = true;
                holding = false;
            }
            if (button.isPressed && !holding && Time.time - pressedAt >= HoldThreshold)
                holdStarted = holding = true;
            tapPending &= Time.time - pressedAt < HoldThreshold && !(attack && attack.IsBusy);
            if (!button.wasReleasedThisFrame) return;
            tapped = tapPending;
            tapPending = holding = false;
            pressedAt = float.PositiveInfinity;
        }

        internal void CancelInput()
        {
            tapped = tapPending = pressed = holdStarted = holding = false;
            pressedAt = float.PositiveInfinity;
            waitForRelease = Button?.isPressed != false;
            inputFrame = Time.frameCount;
        }

        internal void SetItem(IHandItem value)
        {
            CancelInput();
            item = value;
            if (Item == null || !Item.Grip || Item.Source is TwoHandedAxe) return;
            Vector3 position = Item.Grip.localPosition;
            position.x = Mathf.Abs(position.x) * (Side == HandSide.Left ? -1f : 1f);
            Item.Grip.localPosition = position;
        }
    }
}
