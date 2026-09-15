using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace RRM
{
    [RequireComponent(typeof(Rigidbody), typeof(Damageable))]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed partial class PlayerController : MonoBehaviour
    {
        public enum EnemyState { Idle, Approach, Windup, Attack, Recovery }
        public InputActionAsset inputActions;
        public Camera viewCamera;
        [FormerlySerializedAs("secondaryControls")] public bool autonomousMovement;
        public Damageable opponent;
        [Min(0.1f)] public float noticeDistance = 6f;
        [Min(0.1f)] public float attackDistance = 1.05f;
        public EnemyState State { get; private set; }
        public string DeathMessage => !autonomousMovement && health && health.IsDead ? "YOU DIED\nR: restart" : string.Empty;
        [Min(0.1f)] public float obstacleLookAhead = 0.75f;
        [Min(0f)] public float moveSpeed = 3.2f;
        [Min(0.1f)] public float acceleration = 12f;
        [Min(0.1f)] public float braking = 16f;
        [Min(1f)] public float turnSpeed = 360f;
        [Min(1f)] public float attackTurnSpeed = 35f;
        [Min(0.05f)] public float jumpHeight = 0.6f;
        [Range(0.4f, 0.9f)] public float crouchHeightScale = 0.65f;
        [Range(0.1f, 1f)] public float crouchSpeedScale = 0.5f;
        public LayerMask groundMask = 1;
        [Min(0.01f)] public float handHoldThreshold = 0.22f;
        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }
        internal float HeightScale => IsCrouching ? capsule.height / standingHeight : 1f;

        private Rigidbody body;
        private CapsuleCollider capsule;
        private PhysicsMaterial injuredSurface;
        private Transform bodyVisual;
        private Vector3 standingCenter, standingVisualScale;
        private float standingHeight;
        private bool jumpRequested, crouchHeld;
        private float spacePressedAt = float.NegativeInfinity;
        private bool spaceJumpEligible;
        private MeleeAttack attack;
        private PlayerHand leftHand;
        private PlayerHand rightHand;
        public PlayerHand LeftHand => leftHand ?? (leftHand = new PlayerHand(this, HandSide.Left));
        public PlayerHand RightHand => rightHand ?? (rightHand = new PlayerHand(this, HandSide.Right));
        public CameraDevice HeldCamera => LeftHand.Item?.Source as CameraDevice ?? RightHand.Item?.Source as CameraDevice;
        public TwoHandedAxe HeldAxe => LeftHand.Item?.Source is TwoHandedAxe axe
            && RightHand.Item?.Source == axe ? axe : null;
        private bool hasInputFocus = true;
        internal bool CanAct => isActiveAndEnabled && (autonomousMovement || hasInputFocus) && health && health.isActiveAndEnabled && !health.IsDead;
        internal bool CanUseHands => CanAct && !autonomousMovement;
        internal bool CanHandleItems => CanAct && !InGrapple && !BlockRecovering;
        public bool HasHand(HandSide side) => GetComponent<Damageable>().IsAttached(
            side == HandSide.Left ? BodyPart.LeftArm : BodyPart.RightArm);
        public bool HasBothLegs => GetComponent<Damageable>().HasBothLegs;
        public float LimbMovementScale => HasBothLegs ? 1f : 0.1f;

        public PlayerHand HandHolding(Component item)
        {
            if (!item) return null;
            if (LeftHand.Item?.Source == item) return LeftHand;
            return RightHand.Item?.Source == item ? RightHand : null;
        }

        internal bool CanReachItem(Vector3 hand, Vector3 target, float distance, LayerMask obstacles)
        {
            Vector3 chest = transform.position;
            chest.y = hand.y;
            return Vector3.Distance(hand, target) <= distance
                && !Physics.CheckSphere(hand, 0.04f, obstacles, QueryTriggerInteraction.Ignore)
                && !Physics.Linecast(chest, hand, obstacles, QueryTriggerInteraction.Ignore)
                && !Physics.Linecast(hand, target, obstacles, QueryTriggerInteraction.Ignore);
        }

        internal bool TryHoldCamera(CameraDevice device, HandSide side, Vector3 mountPosition)
        {
            PlayerHand hand = side == HandSide.Left ? LeftHand : RightHand;
            if (InGrapple || !device || device.State == CameraDeviceState.Broken || HeldCamera
                || hand.State != HandState.Empty || (attack && attack.IsBusy)) return false;
            HandItem.SetFalling(device, false);
            device.transform.SetParent(transform, true);
            device.transform.localPosition = mountPosition;
            hand.SetItem(new HandItem(device, device.transform));
            return true;
        }
        private Damageable health;
        private InputActionAsset actions;
        private InputAction move;
        private Vector2 movement;
        private Vector3 aimDirection = Vector3.forward;
        private float nextWanderChange;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            aimDirection = transform.forward;
            attack = GetComponent<MeleeAttack>();
            health = GetComponent<Damageable>();
            capsule = GetComponent<CapsuleCollider>();
            standingHeight = capsule.height;
            if (autonomousMovement) return;
            standingCenter = capsule.center;
            bodyVisual = GetComponent<HitFeedback>()?.visual;
            if (bodyVisual) standingVisualScale = bodyVisual.localScale;
            if (!viewCamera) viewCamera = Camera.main;
            if (!inputActions)
            {
                Debug.LogError("PlayerController needs an InputActionAsset.", this);
                enabled = false;
                return;
            }
            actions = Instantiate(inputActions);
            move = actions.FindAction("Player/Move", true);
        }

        private void OnEnable()
        {
            if (health) health.Damaged += OnGrappleDamage;
            actions?.FindActionMap("Player", true).Enable();
            nextWanderChange = 0f;
            ResetEnemyDecisions();
        }
        private void OnDisable()
        {
            blockUntil = 0f;
            ReleaseGrapple();
            if (attack) attack.CancelAttack();
            // Scene teardown deactivates the hierarchy; Unity forbids reparenting during that callback.
            if (gameObject.activeInHierarchy) DropHeldItems();
            if (health) health.Damaged -= OnGrappleDamage;
            actions?.Disable();
            movement = Vector2.zero;
            jumpRequested = crouchHeld = IsGrounded = false;
            spaceJumpEligible = false;
            spacePressedAt = float.NegativeInfinity;
            leftHand?.CancelInput();
            rightHand?.CancelInput();
        }
        private void OnDestroy()
        {
            if (actions) Destroy(actions);
            if (injuredSurface) Destroy(injuredSurface);
        }

        private void Update()
        {
            if (!autonomousMovement && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }
            if (health.IsDead)
            {
                ReleaseGrapple();
                if (attack && attack.IsBusy) attack.CancelAttack();
                DropHeldItems();
                State = EnemyState.Idle;
                movement = Vector2.zero; jumpRequested = spaceJumpEligible = false;
                leftHand?.CancelInput(); rightHand?.CancelInput();
                return;
            }
            ValidateGrapple();
            if (autonomousMovement)
            {
                if (opponent && attack) EngageOpponent();
                else if (InGrapple) movement = Vector2.zero;
                else Wander();
                return;
            }
            LeftHand.ReadInput();
            RightHand.ReadInput();
            if (InGrapple) HeldAxe?.CancelInput();
            if ((!HeldAxe || InGrapple) && HandleGrappleInput()) return;
            if (Keyboard.current != null)
            {
                var space = Keyboard.current.spaceKey;
                if (space.wasPressedThisFrame)
                {
                    spacePressedAt = Time.time;
                    spaceJumpEligible = IsGrounded && CanAct && HasBothLegs;
                }
                spaceJumpEligible &= IsGrounded && CanAct && HasBothLegs && !BlockRecovering && !(attack && attack.IsBusy);
                bool longPress = Time.time - spacePressedAt >= 0.2f;
                crouchHeld = CanAct && (Keyboard.current.leftCtrlKey.isPressed
                    || (space.isPressed && longPress && !float.IsNegativeInfinity(spacePressedAt)));
                // Resolve context only on a valid short release. A rejected kick never becomes a jump.
                if (space.wasReleasedThisFrame)
                {
                    if (spaceJumpEligible && !longPress)
                    {
                        Hurtbox target = attack ? attack.FindKickTarget() : null;
                        if (target) attack.TryKick(target);
                        else jumpRequested = true;
                    }
                    spaceJumpEligible = false;
                    spacePressedAt = float.NegativeInfinity;
                }
                if (Keyboard.current.qKey.wasPressedThisFrame) ToggleHand(LeftHand);
                else if (Keyboard.current.eKey.wasPressedThisFrame) ToggleHand(RightHand);
            }
            else
            {
                crouchHeld = spaceJumpEligible = false;
                spacePressedAt = float.NegativeInfinity;
            }
            movement = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            aimDirection = transform.forward;
            if (Mouse.current != null && !LeftHand.IsHoldActive && !RightHand.IsHoldActive && viewCamera)
            {
                Vector2 pointer = Mouse.current.position.ReadValue();
                if (viewCamera.pixelRect.Contains(pointer))
                {
                    Ray ray = viewCamera.ScreenPointToRay(pointer);
                    var plane = new Plane(Vector3.up, body.position);
                    if (plane.Raycast(ray, out float distance))
                    {
                        Vector3 direction = ray.GetPoint(distance) - body.position;
                        if (direction.sqrMagnitude > 0.04f) aimDirection = direction.normalized;
                    }
                }
            }
            if (HeldAxe) HeldAxe.ReadInput();
            else if (attack)
            {
                if (LeftHand.AttackRequested) attack.TryHandAttack(LeftHand);
                if (RightHand.AttackRequested) attack.TryHandAttack(RightHand);
            }
        }

        private void ToggleHand(PlayerHand hand)
        {
            if (!CanHandleItems || (attack && attack.IsBusy)) return;
            if (HeldAxe) { HeldAxe.TryPlace(this); return; }
            if (hand.Item?.Source is CameraDevice held)
            {
                held.TryPlace(this);
                return;
            }
            if (hand.Item?.Source is PortableLamp lamp) { lamp.TryPlace(this); return; }
            if (hand.State != HandState.Empty) return;
            // Only interaction presses scan the handful of world items, nearest accessible first.
            var nearby = FindObjectsByType<CameraDevice>().Cast<Component>()
                .Concat(FindObjectsByType<PortableLamp>())
                .Concat(FindObjectsByType<TwoHandedAxe>())
                .OrderBy(item => (item.transform.position - transform.position).sqrMagnitude);
            foreach (Component item in nearby)
                if (item is CameraDevice device ? device.TryPickup(this, hand.Side)
                    : item is PortableLamp nearbyLamp ? nearbyLamp.TryPickup(this, hand.Side)
                    : ((TwoHandedAxe)item).TryPickup(this, hand.Side)) return;
        }

        private void DropHeldItems()
        {
            // Run after damage observers: the fatal contact still sees the held lamp/camera.
            DropHandItem(leftHand); DropHandItem(rightHand);
        }

        private void DropHandItem(PlayerHand hand, bool falling = false)
        {
            Component item = hand?.Item?.Source;
            if (!item) return;
            Vector3 position = item.transform.position;
            Quaternion rotation = item.transform.rotation;
            if (item is CameraDevice camera) camera.DropFrom(this);
            else if (item is PortableLamp lamp) lamp.DropFrom(this);
            else if (item is TwoHandedAxe axe) axe.DropFrom(this);
            if (!falling) return;
            item.transform.SetPositionAndRotation(position, rotation);
            HandItem.SetFalling(item, true);
        }

        internal void OnLimbSevered(BodyPart part)
        {
            ReleaseGrapple();
            blockUntil = 0f;
            attack?.InterruptAttack();
            if (part == BodyPart.LeftArm || part == BodyPart.RightArm)
            {
                var hand = part == BodyPart.LeftArm ? LeftHand : RightHand;
                DropHandItem(hand, true);
                hand.CancelInput();
            }
            else
            {
                jumpRequested = spaceJumpEligible = false;
                // Floor friction otherwise cancels the 0.1-speed motor; its existing braking still stops us.
                if (!injuredSurface)
                {
                    injuredSurface = new PhysicsMaterial("Injured motor")
                    { staticFriction = 0f, dynamicFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
                    capsule.sharedMaterial = injuredSurface;
                }
            }
        }

        private void Wander()
        {
            if (movement.sqrMagnitude > 0.01f
                && (Time.time >= nextWanderChange || !ClearDirection(aimDirection)))
            {
                movement = Vector2.zero;
                nextWanderChange = Time.time + Random.Range(0.4f, 0.9f);
                return;
            }
            if (Time.time < nextWanderChange) return;
            // ponytail: local random steering only; no route planning around complex enclosures.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                if (direction.sqrMagnitude < 0.01f) continue;
                Vector3 heading = new Vector3(direction.x, 0, direction.y);
                if (!ClearDirection(heading)) continue;
                aimDirection = heading;
                movement = direction;
                nextWanderChange = Time.time + Random.Range(2f, 4f);
                return;
            }
            nextWanderChange = Time.time + 0.4f;
        }

        private bool ClearDirection(Vector3 direction) =>
            !ObstacleAhead(direction, out _);

        private bool ObstacleAhead(Vector3 direction, out RaycastHit hit) =>
            // Default-layer room geometry only; the low probe also catches the short south wall.
            Physics.SphereCast(body.position + Vector3.up * 0.45f, 0.3f, direction,
                out hit, obstacleLookAhead, 1, QueryTriggerInteraction.Ignore);

        private void FixedUpdate()
        {
            ValidateGrapple();
            if (InGrapple) { StopGrappleMovement(); return; }
            if (health.IsDead || (attack && attack.IsPaused)) return;
            if (!autonomousMovement)
            {
                UpdateCrouch();
                // Ground probe follows the existing upright capsule, not its visual body or camera.
                Vector3 foot = body.position + body.rotation * capsule.center
                    - Vector3.up * (capsule.height * 0.5f - capsule.radius);
                IsGrounded = Physics.SphereCast(foot + Vector3.up * 0.03f, capsule.radius * 0.95f,
                        Vector3.down, out RaycastHit ground, 0.1f, groundMask, QueryTriggerInteraction.Ignore)
                    && ground.normal.y >= 0.65f && Vector3.Dot(body.linearVelocity, ground.normal) <= 0.1f;
                if (jumpRequested && IsGrounded && HasBothLegs)
                {
                    Vector3 velocity = body.linearVelocity;
                    velocity.y = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);
                    body.linearVelocity = velocity;
                    IsGrounded = false;
                }
                jumpRequested = false;
            }
            Vector3 heading = new Vector3(movement.x, 0f, movement.y);
            if (!autonomousMovement && viewCamera)
            {
                Vector3 right = Vector3.ProjectOnPlane(viewCamera.transform.right, Vector3.up).normalized;
                Vector3 up = Vector3.ProjectOnPlane(viewCamera.transform.up, Vector3.up).normalized;
                heading = Vector3.ClampMagnitude(right * movement.x + up * movement.y, 1f);
            }
            Vector3 desired = heading
                * (moveSpeed * LimbMovementScale * (IsCrouching ? crouchSpeedScale : 1f) * (attack ? attack.MovementScale : 1f));
            Vector3 current = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            float rate = movement.sqrMagnitude > 0.01f ? acceleration : braking;
            Vector3 change = Vector3.ClampMagnitude(desired - current, rate * Time.fixedDeltaTime);
            body.AddForce(change, ForceMode.VelocityChange);
            if (autonomousMovement && opponent && attack && attack.IsBusy)
            {
                body.angularVelocity = Vector3.zero;
                return;
            }
            if (!autonomousMovement && (LeftHand.IsHoldActive || RightHand.IsHoldActive))
            {
                body.angularVelocity = Vector3.zero;
                return;
            }
            float speed = attack && attack.IsBusy ? attackTurnSpeed : turnSpeed;
            body.MoveRotation(Quaternion.RotateTowards(body.rotation,
                Quaternion.LookRotation(aimDirection), speed * Time.fixedDeltaTime));
        }

        private void UpdateCrouch()
        {
            if (crouchHeld == IsCrouching) return;
            if (!crouchHeld)
            {
                Vector3 top = body.position + body.rotation * capsule.center
                    + Vector3.up * (capsule.height * 0.5f - capsule.radius);
                if (Physics.CheckCapsule(top, top + Vector3.up * (standingHeight - capsule.height),
                    capsule.radius * 0.98f, groundMask, QueryTriggerInteraction.Ignore)) return;
            }
            float oldScale = HeightScale;
            capsule.height = crouchHeld ? Mathf.Max(capsule.radius * 2f, standingHeight * crouchHeightScale) : standingHeight;
            capsule.center = standingCenter - Vector3.up * ((standingHeight - capsule.height) * 0.5f);
            IsCrouching = crouchHeld;
            float scale = HeightScale;
            // ponytail: compress the blockout body/hurtboxes; replace with a pose when animation exists.
            if (bodyVisual) bodyVisual.localScale = Vector3.Scale(standingVisualScale, new Vector3(1, scale, 1));
            LowerHandItem(LeftHand, scale / oldScale);
            if (RightHand.Item?.Source != LeftHand.Item?.Source) LowerHandItem(RightHand, scale / oldScale);
        }

        private static void LowerHandItem(PlayerHand hand, float ratio)
        {
            Transform grip = hand.Item?.Grip;
            if (!grip) return;
            Vector3 position = grip.localPosition;
            position.y *= ratio;
            grip.localPosition = position;
        }

        private void OnApplicationFocus(bool focused)
        {
            hasInputFocus = focused;
            if (!focused && !autonomousMovement)
            {
                blockUntil = 0f;
                ReleaseGrapple();
                movement = Vector2.zero;
                jumpRequested = crouchHeld = false;
                spaceJumpEligible = false;
                spacePressedAt = float.NegativeInfinity;
                leftHand?.CancelInput();
                rightHand?.CancelInput();
                HeldAxe?.CancelInput();
            }
        }

        private void OnGUI()
        {
            if (!autonomousMovement && HeldAxe)
                GUI.Box(new Rect(12f, Mathf.Max(8f, Screen.height - 148f), Mathf.Min(240f, Screen.width - 24f), 44f),
                    "AXE / " + HeldAxe.LastIntent + "\n" + attack.Phase);
            if (!autonomousMovement && !IsCaptured && (IsBlocking || Time.time < blockedFeedbackUntil))
                GUI.Box(new Rect(Mathf.Max(8f, Screen.width * 0.5f - 100f), Mathf.Max(8f, Screen.height - 188f),
                    Mathf.Min(200f, Screen.width - 16f), 36f), Time.time < blockedFeedbackUntil ? "BLOCKED" : "BLOCK WINDOW");
            if (!autonomousMovement && IsCaptured)
                GUI.Box(new Rect(Mathf.Max(8f, Screen.width * 0.5f - 150f), Mathf.Max(8f, Screen.height - 188f),
                    Mathf.Min(300f, Screen.width - 16f), 36f), EscapeHint);
            if (DeathMessage.Length > 0)
                GUI.Box(new Rect(12f, 12f, Mathf.Min(220f, Screen.width - 24f), 58f), DeathMessage);
        }
    }
}
