using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace RRM
{
    public sealed class CameraRecorder : MonoBehaviour
    {
        public Transform lens;
        public Camera liveCamera;
        public Damageable[] subjects;
        public MeleeAttack playerAttack;
        public LineRenderer cone;
        public LayerMask obstructionMask = 1;
        [Tooltip("Horizontal field of view, shared by the live feed and visibility checks.")]
        [Range(10f, 160f)] public float fieldOfView = 60f;
        [FormerlySerializedAs("range"), Min(0.1f)] public float conePreviewLength = 50f;
        public bool showOverlay = true;
        [Min(0.01f)] public float mouseSensitivity = 0.15f;
        [Range(-80f, 0f)] public float minPitch = -25f;
        [Range(0f, 80f)] public float maxPitch = 70f;
        [Range(0f, 90f)] public float maxYaw = 90f;
        public bool IsPlaced { get; private set; }
        public bool IsRecording => isActiveAndEnabled && lens;
        public bool ControlsVisible { get; private set; }
        public bool IsAiming => !IsPlaced && isActiveAndEnabled && liveCamera && lens
            && Mouse.current != null && Mouse.current.rightButton.isPressed;
        public RenderTexture LiveTexture => liveTexture;
        public int RecordedHits { get; private set; }
        public string LastResult => Time.unscaledTime - resultTime < 1f ? "REC EVENT" : "STANDBY";

        private GUIStyle label;
        private GUIStyle heading;
        private Vector2 controlsScroll;
        private float resultTime = float.NegativeInfinity;
        private RenderTexture liveTexture;
        private const float FeedAspect = 4f / 3f;
        private static readonly Color RecordingColor = new Color(0.35f, 0.85f, 0.65f);

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                ControlsVisible = !ControlsVisible;
            if (!IsPlaced && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                transform.SetParent(null, true);
                IsPlaced = true;
                StopLiveFeed();
            }
            if (!IsAiming || Mouse.current.rightButton.wasPressedThisFrame) return;
            Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity;
            Vector3 angles = transform.localEulerAngles;
            float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x) - delta.y, minPitch, maxPitch);
            float yaw = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.y) + delta.x, -maxYaw, maxYaw);
            transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void OnEnable()
        {
            if (subjects != null)
                foreach (Damageable subject in subjects)
                    if (subject) subject.Damaged += Observe;
            if (IsPlaced || !liveCamera || !lens) return;
            liveTexture = new RenderTexture(640, 480, 24, RenderTextureFormat.ARGB32)
            {
                name = "Handheld Live Feed",
                filterMode = FilterMode.Bilinear
            };
            liveTexture.Create();
            liveCamera.targetTexture = liveTexture;
            liveCamera.aspect = FeedAspect;
            liveCamera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(fieldOfView, FeedAspect);
            liveCamera.enabled = true;
        }

        private void OnDisable()
        {
            if (subjects != null)
                foreach (Damageable subject in subjects)
                    if (subject) subject.Damaged -= Observe;
            StopLiveFeed();
        }

        private void StopLiveFeed()
        {
            if (liveCamera)
            {
                liveCamera.enabled = false;
                liveCamera.targetTexture = null;
            }
            if (liveTexture)
            {
                liveTexture.Release();
                Destroy(liveTexture);
                liveTexture = null;
            }
        }

        public bool CanSee(Vector3 point)
        {
            if (!lens) return false;
            Vector3 offset = lens.InverseTransformDirection(point - lens.position);
            if (offset.z <= 0f) return false;
            float halfWidth = offset.z * Mathf.Tan(fieldOfView * Mathf.Deg2Rad * 0.5f);
            if (Mathf.Abs(offset.x) > halfWidth || Mathf.Abs(offset.y) > halfWidth / FeedAspect) return false;
            // Check only the actual point and intervening geometry, never the preview or render distance.
            return !Physics.Linecast(lens.position, point, obstructionMask, QueryTriggerInteraction.Ignore);
        }

        public void Observe(DamageEvent hit)
        {
            if (!IsRecording || !CanSee(hit.Point)) return;
            RecordedHits++;
            resultTime = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            if (liveCamera)
                liveCamera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(fieldOfView, FeedAspect);
            if (!lens || !cone) return;
            Vector3 origin = new Vector3(lens.position.x, 0.025f, lens.position.z);
            Vector3 forward = Vector3.ProjectOnPlane(lens.forward, Vector3.up).normalized;
            cone.positionCount = 19;
            cone.SetPosition(0, origin);
            for (int i = 0; i <= 16; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(Mathf.Lerp(-fieldOfView * 0.5f,
                    fieldOfView * 0.5f, i / 16f), Vector3.up) * forward;
                float distance = conePreviewLength;
                if (Physics.Raycast(lens.position, direction, out RaycastHit hit, conePreviewLength,
                        obstructionMask, QueryTriggerInteraction.Ignore)) distance = hit.distance;
                cone.SetPosition(i + 1, origin + direction * distance);
            }
            cone.SetPosition(18, origin);
        }

        private void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                label.normal.textColor = new Color(0.88f, 0.9f, 0.88f);
                heading = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold };
            }
            if (ControlsVisible)
            {
                float helpWidth = Mathf.Min(470f, Screen.width - 32f);
                float helpHeight = Mathf.Min(260f, Screen.height - 32f);
                var rect = new Rect((Screen.width - helpWidth) * 0.5f, (Screen.height - helpHeight) * 0.5f,
                    helpWidth, helpHeight);
                GUI.color = new Color(0.035f, 0.045f, 0.04f, 0.96f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUILayout.BeginArea(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, rect.height - 20f));
                GUILayout.Label("CONTROLS / F1", heading);
                controlsScroll = GUILayout.BeginScrollView(controlsScroll);
                GUILayout.Label("WASD / arrows: move\nMouse: turn character\nRMB + drag: aim handheld camera\n"
                    + "LMB: melee attack\nF: place camera (no live feed)\nR: restart room\nF1: close this guide", label);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            // Placed recording is private: neither video nor live event diagnostics reach the player.
            if (IsPlaced) return;
            if (liveTexture)
            {
                float feedWidth = Mathf.Min(320f, Screen.width * 0.4f, Screen.height * 0.4f * FeedAspect);
                float feedHeight = feedWidth / FeedAspect;
                var rect = new Rect(Screen.width - feedWidth - 16f, Screen.height - feedHeight - 16f,
                    feedWidth, feedHeight);
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.DrawTexture(rect, liveTexture, ScaleMode.ScaleToFit, false);
            }
            if (!showOverlay) return;
            float width = Mathf.Min(470f, Screen.width - 32f);
            GUI.color = new Color(0.035f, 0.045f, 0.04f, 0.88f);
            GUI.DrawTexture(new Rect(16, 16, width, 145), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(30, 25, width - 28, 30), "RRM / COMBAT TEST 01", heading);
            GUI.Label(new Rect(30, 57, width - 28, 24), "TAPE 01   REC " + RecordedHits, label);
            string phase = playerAttack ? playerAttack.Phase.ToString().ToUpperInvariant() : "READY";
            string healthText = subjects != null && subjects.Length > 0 && subjects[0]
                ? "   TARGET " + subjects[0].Health.ToString("0") + "/" + subjects[0].maxHealth.ToString("0") : "";
            GUI.Label(new Rect(30, 83, width - 28, 24), phase + healthText, label);
            GUI.color = Time.unscaledTime - resultTime < 0.25f ? RecordingColor : Color.white;
            GUI.Label(new Rect(30, 111, width - 28, 44), LastResult, label);
            GUI.color = Color.white;
        }
    }
}
