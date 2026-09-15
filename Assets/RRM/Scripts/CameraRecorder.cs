using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace RRM
{
    [RequireComponent(typeof(CameraDevice))]
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
        public bool IsPlaced => device && device.State == CameraDeviceState.Placed;
        public bool IsRecording => isActiveAndEnabled && lens && Device && Device.isActiveAndEnabled
            && Device.State != CameraDeviceState.Broken;
        public bool ControlsVisible { get; private set; }
        private bool IsHeldByPlayer => device && device.Owner && !device.Owner.autonomousMovement;
        private PlayerHand HoldingHand => IsHeldByPlayer ? device.Owner.HandHolding(device) : null;
        public bool IsAiming => IsHeldByPlayer && isActiveAndEnabled && liveCamera && lens
            && HoldingHand?.IsHoldActionActive == true && !(playerAttack && playerAttack.IsUsingCamera(device));
        public RenderTexture LiveTexture => liveTexture;
        public int RecordedHits => Device.RecordedHits;
        // Round completion is not evidence: an unseen fatal event must still report NO.
        public bool ReportReady => subjects != null && Array.Exists(subjects, subject => subject && subject.IsDead);
        public string ReportText => "Camera ID: " + Device.Id
            + "\nCamera owner: " + (Device.Owner ? Device.Owner.name : "NONE")
            + "\nCamera state: " + Device.State.ToString().ToUpperInvariant()
            + (Device.State == CameraDeviceState.Broken ? " (diagnostic history only)" : "")
            + "\nHits recorded: " + Device.RecordedHits
            + "\nEvents recorded: " + Device.Events.Count
            + "\nDeath recorded: " + (Device.DeathRecorded ? "YES" : "NO")
            + "\nCameraBash recorded: " + Device.RecordedCameraBashes
            + "\nSeverings recorded: " + Device.RecordedSeverings
            + "\nLimbs recorded: " + (Device.RecordedSeverings > 0 ? Device.RecordedLimbNames : "NONE")
            + "\nReport weight: " + Device.ReportWeight.ToString("0.00")
            + "\nLast recorded distance: " + (Device.LastEventDistance.HasValue
                ? Device.LastEventDistance.Value.ToString("0.00") + " m" : "N/A")
            + "\n" + EvidenceText;
        public string EvidenceText => "Last event light: " + (Device.Events.Count > 0
            ? Device.Events[Device.Events.Count - 1].LightLevel.ToString("0.0") + "/100 "
                + Device.Events[Device.Events.Count - 1].Visibility.ToString().ToUpperInvariant() : "N/A")
            + "   Blood seen: " + BloodSeenCount
            + "\nLast event quality: " + (Device.Events.Count > 0
                ? (Device.Events[Device.Events.Count - 1].Clarity * 100f).ToString("0") + "%" : "N/A");
        public float? VisibleTargetLightLevel
        {
            get
            {
                if (!IsHeldByPlayer || !IsRecording || subjects == null) return null;
                foreach (Damageable target in subjects)
                {
                    if (!target || target.IsDead || target.gameObject == device.Owner.gameObject) continue;
                    Hurtbox torso = Array.Find(target.GetComponentsInChildren<Hurtbox>(), zone => zone.part == BodyPart.Torso);
                    if (torso && CanSee(torso.transform.position)) return GetLightLevel(torso.transform.position);
                }
                return null;
            }
        }
        public string LightReadout
        {
            get
            {
                if (!IsHeldByPlayer) return string.Empty;
                float here = GetLightLevel(Device.Owner.transform.position);
                float? target = VisibleTargetLightLevel;
                return "LIGHT HERE: " + here.ToString("0") + "/100 " + LightSource.Classify(here).ToString().ToUpperInvariant()
                    + "\nTARGET LIGHT: " + (target.HasValue
                        ? target.Value.ToString("0") + "/100   QUALITY: " + target.Value.ToString("0") + "%"
                        : "N/A   QUALITY: N/A (OFF CAMERA)");
            }
        }
        private int BloodSeenCount
        {
            get
            {
                int count = 0;
                foreach (var observation in Device.BloodEvents) if (observation.Visible) count++;
                return count;
            }
        }
        public string LastResult => Time.unscaledTime - resultTime < 1f ? "REC EVENT" : "STANDBY";

        private GUIStyle label;
        private CameraDevice device;
        private CameraDevice Device => device ? device : device = GetComponent<CameraDevice>();
        private GUIStyle heading;
        private Vector2 controlsScroll;
        private Vector2 reportScroll;
        private Vector2 hudScroll;
        private Camera gameCamera;
        private Rect originalViewport;
        private float HudHeight => Mathf.Min(144f, Screen.height * 0.32f);
        private float resultTime = float.NegativeInfinity;
        private RenderTexture liveTexture;
        private const float FeedAspect = 4f / 3f;
        private static readonly Color RecordingColor = new Color(0.35f, 0.85f, 0.65f);

        private void Awake() { device = GetComponent<CameraDevice>(); }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                ControlsVisible = !ControlsVisible;
            if (!IsAiming) return;
            Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity;
            Vector3 angles = transform.localEulerAngles;
            float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x) - delta.y, minPitch, maxPitch);
            transform.localRotation = Quaternion.Euler(pitch, angles.y + delta.x, 0f);
        }

        public void SetViewRotation(Quaternion rotation)
        {
            transform.rotation = rotation;
        }

        private void OnEnable()
        {
            if (subjects != null)
                foreach (Damageable subject in subjects)
                    if (subject)
                    {
                        subject.Damaged += Observe;
                        if (subject.TryGetComponent(out BloodEvidence evidence)) evidence.Created += ObserveBlood;
                    }
            RefreshLiveFeed();
        }

        internal void RefreshLiveFeed()
        {
            if (!IsRecording || !IsHeldByPlayer || !liveCamera || !lens)
            {
                StopLiveFeed();
                return;
            }
            if (liveTexture) return;
            playerAttack = device.Owner.GetComponent<MeleeAttack>();
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
                    if (subject)
                    {
                        subject.Damaged -= Observe;
                        if (subject.TryGetComponent(out BloodEvidence evidence)) evidence.Created -= ObserveBlood;
                    }
            StopLiveFeed();
            if (gameCamera)
            {
                gameCamera.rect = originalViewport;
                gameCamera.ResetAspect();
                gameCamera = null;
            }
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
            if ((hit.IsBleeding && !hit.IsFatal) || !IsRecording || !CanSee(hit.Point)) return;
            Device.Record(hit, Vector3.Distance(lens.position, hit.Point), GetLightLevel(hit.Point));
            resultTime = Time.unscaledTime;
        }

        public float GetLightLevel(Vector3 point) => LightSource.At(point);

        public CameraDevice.BloodObservation InspectBlood(BloodEvent evidence) =>
            new CameraDevice.BloodObservation(evidence, IsRecording && evidence.Mark && CanSee(evidence.Position),
                GetLightLevel(evidence.Position));

        private void ObserveBlood(BloodEvent evidence)
        {
            if (IsRecording) Device.RecordBlood(InspectBlood(evidence));
        }

        private void LateUpdate()
        {
            UpdateHudViewport();
            if (Device.State == CameraDeviceState.Broken) return;
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

        private void UpdateHudViewport()
        {
            if (!gameCamera && IsHeldByPlayer)
            {
                gameCamera = device.Owner.viewCamera;
                if (gameCamera) originalViewport = gameCamera.rect;
            }
            if (!gameCamera) return;
            Rect viewport = originalViewport;
            if (IsHeldByPlayer)
            {
                float inset = viewport.height * HudHeight / Screen.height;
                viewport.y += inset;
                viewport.height -= inset;
            }
            if (gameCamera.rect == viewport) return;
            gameCamera.rect = viewport;
            gameCamera.ResetAspect();
        }

        private void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                label.normal.textColor = new Color(0.88f, 0.9f, 0.88f);
                heading = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold };
            }
            float top = Screen.height - HudHeight;
            float feedWidth = Mathf.Min(320f, Screen.width * 0.4f, Screen.height * 0.4f * FeedAspect);
            if (IsHeldByPlayer)
            {
                GUI.color = new Color(0.035f, 0.045f, 0.04f, 0.88f);
                GUI.DrawTexture(new Rect(0, top, Screen.width, HudHeight), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            if (ControlsVisible)
            {
                float helpWidth = Mathf.Min(470f, Screen.width - 32f);
                float helpHeight = Mathf.Min(400f, Screen.height - 32f);
                var rect = new Rect((Screen.width - helpWidth) * 0.5f, (Screen.height - helpHeight) * 0.5f,
                    helpWidth, helpHeight);
                GUI.color = new Color(0.035f, 0.045f, 0.04f, 0.96f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUILayout.BeginArea(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, rect.height - 20f));
                GUILayout.Label("CONTROLS / F1", heading);
                controlsScroll = GUILayout.BeginScrollView(controlsScroll);
                GUILayout.Label("W/S: screen up / down; A/D: screen left / right (strafe)\n"
                    + "Mouse, no buttons held: turn body toward cursor\n"
                    + "Space: short release kicks a close reachable target in front; otherwise jumps\n"
                    + "Kick works with occupied hands; shares attack recovery; no kicks inside grapple\n"
                    + "Busy/rejected kick never turns into a jump; both actions need ground support\n"
                    + "Space (hold 0.2 s) or Left Ctrl (hold): crouch at half speed\n"
                    + "Release both crouch keys to stand when clear; long Space release never kicks/jumps\n"
                    + "Q: left hand - place / pick up nearby item\nE: right hand - place / pick up nearby item\n"
                    + "LMB: left hand action\nRMB: right hand action\n"
                    + "Two-handed axe: Q or E picks up only with BOTH hands empty; Q or E puts it down\n"
                    + "Axe: press, move mouse, release once; one ordinary damage hit, at most one severed limb\n"
                    + "RMB + left: RightArm; LMB + right: LeftArm; BOTH + down: Leg\n"
                    + "Directions are screen-space; arm names refer to the target's anatomical sides\n"
                    + "Other gestures/clicks: Normal. Pair buttons within the chord window (default 0.12 s)\n"
                    + "Release BOTH before retrying; movement threshold/tolerance are on the axe\n"
                    + "Axe gestures replace separate hand holds: no aim, grapple or block\n"
                    + "Severing needs real contact with the intended attached limb of a living target\n"
                    + "Leg gesture swings low; nearest contacted surviving leg is chosen\n"
                    + "Missing requested arm never redirects to the other arm; other contacts do ordinary damage\n"
                    + "Normal axe, fists, feet, camera and lamp cannot sever\n"
                    + "Each lost limb bleeds 5% max HP per game minute; sources add, bleeding can kill\n"
                    + "Moving while bleeding leaves surface trails; no healing or bandages\n"
                    + "Camera records the cut once and visible bleeding death, not repeated bleeding ticks\n"
                    + "Lost arm: no action/pickup with it; held item falls intact; two-handed axe drops\n"
                    + "One or two lost legs: 10% base speed, no jump/kick; gravity still works\n"
                    + "Severed parts stay in the room until R; cameras retain IDs and records when dropped\n"
                    + "CharCrafter test: detached proxies only; missing limbs still show on the whole skin\n"
                    + "Hand click: release before " + (playerAttack ? playerAttack.GetComponent<PlayerController>().handHoldThreshold : 0.22f).ToString("0.##") + " s; longer press = hold, never a release click\n"
                    + "Camera hand: hold, then move mouse to aim relative to body\n"
                    + "Release keeps that angle: looking backward stays behind you as you turn\n"
                    + "Empty hand: short release to punch\n"
                    + "Both hands empty: hold either button near a target in front to grapple\n"
                    + "Keep that button held; short click the free hand to punch when allowed\n"
                    + "Release gripping button to let go; fresh free-hand hold: one wall slam\n"
                    + "Wall slam needs the target against a wall behind them; no wall = no slam\n"
                    + "Outside grapple, hold BOTH empty hands: short frontal block window\n"
                    + "Both holds together choose block, not grab; release both before retrying\n"
                    + "Block expires while held; rear/late hits hurt; attack recovery cannot be cancelled\n"
                    + "Grappled: wait for BREAK FREE, then short LMB / RMB; one attempt per cycle\n"
                    + "Early input uses the attempt; missed openings repeat; no button mashing\n"
                    + "Lamp: lights in hand or on a surface; hold has no action\n"
                    + "Camera/lamp hand: short click strikes on release\n"
                    + "Both hands share windup / strike / recovery; spam never queues attacks\n"
                    + "Focus loss or item change cancels that hand's pending input\n"
                    + "Camera/lamp: first character hit breaks it; a miss keeps it; axe is not one-use\n"
                    + "Broken camera: no feed or recording, zero report weight; other witnesses keep CameraBash\n"
                    + "Occupied hand places its item, never swaps it; no pickup during a swing\n"
                    + "Enemy: evade the windup, counter during recovery; it may block or grab\n"
                    + "Enemy escape/block reactions can fail; your BREAK FREE window is real\n"
                    + "Death drops held items; survivors can collect the same camera and its records\n"
                    + "Camera report is a preview: owner updates on pickup; gameplay stays active\n"
                    + "R: restart room\nF1: close this guide", label);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (ReportReady)
            {
                DrawReport();
                return;
            }
            // Until round completion, placed recording has no remote video or event diagnostics.
            if (!IsHeldByPlayer) return;
            if (liveTexture)
            {
                float feedHeight = feedWidth / FeedAspect;
                var rect = new Rect(Screen.width - feedWidth - 16f, Screen.height - feedHeight - 16f,
                    feedWidth, feedHeight);
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.DrawTexture(rect, liveTexture, ScaleMode.ScaleToFit, false);
            }
            if (!showOverlay) return;
            float width = Screen.width - feedWidth - 36f;
            float columnWidth = Mathf.Max(80f, (width - 40f) / 3f);
            GUILayout.BeginArea(new Rect(12f, top + 8f, width, HudHeight - 16f));
            hudScroll = GUILayout.BeginScrollView(hudScroll);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(columnWidth));
            GUILayout.Label("RRM / COMBAT TEST 01", heading);
            GUILayout.Label("TAPE 01   REC " + RecordedHits, label);
            string phase = playerAttack ? playerAttack.Phase.ToString().ToUpperInvariant() : "READY";
            string healthText = subjects != null && subjects.Length > 0 && subjects[0]
                ? "   TARGET " + subjects[0].Health.ToString("0") + "/" + subjects[0].maxHealth.ToString("0") : "";
            GUILayout.Label(phase + healthText, label);
            GUILayout.EndVertical();
            GUILayout.Space(12f);
            GUILayout.BeginVertical(GUILayout.Width(columnWidth));
            GUI.color = Time.unscaledTime - resultTime < 0.25f ? RecordingColor : Color.white;
            GUILayout.Label(LastResult, label);
            GUI.color = Color.white;
            GUILayout.Label(LightReadout, label);
            GUILayout.EndVertical();
            GUILayout.Space(12f);
            GUILayout.BeginVertical(GUILayout.Width(columnWidth));
            GUILayout.Label(EvidenceText, label);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawReport()
        {
            var reports = Array.FindAll(FindObjectsByType<CameraRecorder>(FindObjectsSortMode.InstanceID),
                recorder => recorder.isActiveAndEnabled && recorder.ReportReady);
            // One report window; the data belongs to each device, not to a global recorder.
            if (reports.Length == 0 || reports[0] != this) return;
            float width = Mathf.Min(470f, Screen.width - 32f);
            float height = Mathf.Min(300f, Screen.height - 32f);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.color = new Color(0.035f, 0.045f, 0.04f, 0.96f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, rect.height - 20f));
            GUILayout.Label("CAMERA REPORT / PREVIEW", heading);
            reportScroll = GUILayout.BeginScrollView(reportScroll);
            foreach (CameraRecorder report in reports)
            {
                GUILayout.Label(report.ReportText, label);
                GUILayout.Space(14f);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
