using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace RRM
{
    [RequireComponent(typeof(Rigidbody), typeof(Damageable))]
    public sealed class PlayerController : MonoBehaviour
    {
        public InputActionAsset inputActions;
        public Camera viewCamera;
        [FormerlySerializedAs("secondaryControls")] public bool autonomousMovement;
        [Min(0.1f)] public float obstacleLookAhead = 0.75f;
        [Min(0f)] public float moveSpeed = 3.2f;
        [Min(0.1f)] public float acceleration = 12f;
        [Min(0.1f)] public float braking = 16f;
        [Min(1f)] public float turnSpeed = 360f;
        [Min(1f)] public float attackTurnSpeed = 35f;

        private Rigidbody body;
        private MeleeAttack attack;
        public CameraDevice HeldCamera { get; internal set; }
        private Damageable health;
        private InputActionAsset actions;
        private InputAction move;
        private InputAction fire;
        private Vector2 movement;
        private Vector3 aimDirection = Vector3.forward;
        private float nextWanderChange;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            attack = GetComponent<MeleeAttack>();
            health = GetComponent<Damageable>();
            if (autonomousMovement) return;
            if (!viewCamera) viewCamera = Camera.main;
            if (!inputActions)
            {
                Debug.LogError("PlayerController needs an InputActionAsset.", this);
                enabled = false;
                return;
            }
            actions = Instantiate(inputActions);
            move = actions.FindAction("Player/Move", true);
            fire = actions.FindAction("Player/Attack", true);
        }

        private void OnEnable()
        {
            actions?.FindActionMap("Player", true).Enable();
            nextWanderChange = 0f;
        }
        private void OnDisable()
        {
            actions?.Disable();
            movement = Vector2.zero;
        }
        private void OnDestroy() { if (actions) Destroy(actions); }

        private void Update()
        {
            if (!autonomousMovement && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }
            if (health.IsDead) { movement = Vector2.zero; return; }
            if (autonomousMovement)
            {
                Wander();
                return;
            }
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                if (HeldCamera) HeldCamera.TryPlace(this);
                // ponytail: one device in this prototype; choose nearest only when multiple devices exist.
                else FindFirstObjectByType<CameraDevice>()?.TryPickup(this);
            }
            movement = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            if (Mouse.current != null && viewCamera && !(HeldCamera && HeldCamera.IsAiming))
            {
                Ray ray = viewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                Plane floor = new Plane(Vector3.up, Vector3.zero);
                if (floor.Raycast(ray, out float distance))
                {
                    Vector3 direction = ray.GetPoint(distance) - body.position;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 0.05f) aimDirection = direction.normalized;
                }
            }
            if (attack && fire.WasPressedThisFrame()) attack.TryAttack();
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
            // Default-layer room geometry only; the low probe also catches the short south wall.
            !Physics.SphereCast(body.position + Vector3.up * 0.45f, 0.3f, direction,
                out _, obstacleLookAhead, 1, QueryTriggerInteraction.Ignore);

        private void FixedUpdate()
        {
            if (health.IsDead || (attack && attack.IsPaused)) return;
            Vector3 desired = new Vector3(movement.x, 0f, movement.y)
                * (moveSpeed * (attack ? attack.MovementScale : 1f));
            Vector3 current = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            float rate = movement.sqrMagnitude > 0.01f ? acceleration : braking;
            Vector3 change = Vector3.ClampMagnitude(desired - current, rate * Time.fixedDeltaTime);
            body.AddForce(change, ForceMode.VelocityChange);
            if (HeldCamera && HeldCamera.IsAiming)
            {
                body.angularVelocity = Vector3.zero;
                return;
            }
            float speed = attack && attack.IsBusy ? attackTurnSpeed : turnSpeed;
            body.MoveRotation(Quaternion.RotateTowards(body.rotation,
                Quaternion.LookRotation(aimDirection), speed * Time.fixedDeltaTime));
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && !autonomousMovement) movement = Vector2.zero;
        }
    }
}
