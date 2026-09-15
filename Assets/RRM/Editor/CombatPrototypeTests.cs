using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RRM.Editor
{
    public static class CombatPrototypeTests
    {
        [MenuItem("RRM/Run Combat Checks")]
        public static void Run()
        {
            Scene previous = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(previous.path))
                throw new InvalidOperationException("Open a saved scene before running combat checks.");
            Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(testScene);
            try
            {
                int layer = LayerMask.NameToLayer("RRMHurtboxes");
                Check(layer >= 0, "Prototype layers exist");
                var movement = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions")
                    .FindAction("Player/Move", true);
                foreach (var binding in movement.bindings)
                    Check(!binding.path.EndsWith("Arrow", StringComparison.OrdinalIgnoreCase),
                        "Player movement has no arrow-key bindings");
                Vector3 origin = new Vector3(1000, 0, 1000);
                var source = new GameObject("Check Light").AddComponent<LightSource>();
                source.transform.position = origin;
                source.radius = 4f;
                source.intensity = 100f;
                Check(LightSource.At(origin) == 100f && LightSource.At(origin + Vector3.right * 2) == 50f
                    && LightSource.At(origin + Vector3.right * 4) == 0f
                    && LightSource.At(origin + Vector3.right * 5) == 0f, "Light fades from 100 to zero with distance");
                Check(Mathf.Abs(LightSource.At(origin + Vector3.right * 1.99f)
                    - LightSource.At(origin + Vector3.right * 2.01f)) < 1f, "Light changes smoothly between nearby points");
                var secondSource = new GameObject("Check Second Light").AddComponent<LightSource>();
                secondSource.transform.position = origin;
                secondSource.radius = 4f;
                secondSource.intensity = 35f;
                Check(LightSource.At(origin) == 100f && LightSource.At(origin + Vector3.right * 2) == 67.5f,
                    "Overlapping sources add their contributions, capped at 100");
                source.enabled = false;
                Check(LightSource.At(origin) == 35f, "Disabled source stops contributing immediately");
                secondSource.gameObject.SetActive(false);
                Check(LightSource.At(origin) == 0f, "Inactive sources do not illuminate a point");
                source.enabled = true;
                source.transform.position += Vector3.right * 10;
                Check(LightSource.At(origin) == 0f && LightSource.At(source.transform.position) == 100f,
                    "Moving a source moves its light field without rebuilding anything");
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(secondSource.gameObject);
                var player = new GameObject("Check Attacker");
                player.transform.position = origin;
                player.AddComponent<Damageable>().ResetHealth();
                var attack = player.AddComponent<MeleeAttack>();
                attack.hurtboxMask = 1 << layer;
                attack.contactPause = 0f;
                attack.weaponPivot = new GameObject("Pivot").transform;
                attack.weaponPivot.SetParent(player.transform, false);
                attack.weaponPivot.localPosition = Vector3.up * 1.15f;
                attack.weaponTip = new GameObject("Tip").transform;
                attack.weaponTip.SetParent(attack.weaponPivot, false);
                attack.weaponTip.localPosition = Vector3.forward * 1.25f;
                var target = new GameObject("Check Target");
                target.transform.position = origin + Vector3.forward * 1.25f;
                var health = target.AddComponent<Damageable>();
                health.ResetHealth();
                Hurtbox zone = Zone(target, layer, -0.03f);
                Zone(target, layer, 0.03f);
                int events = 0, deaths = 0;
                DamageEvent last = default;
                health.Damaged += hit => { events++; if (hit.IsFatal) deaths++; last = hit; };
                Physics.SyncTransforms();

                Check(!health.ApplyDamage(player, zone, origin, Vector3.forward, -1, 0), "Negative damage rejected");
                Check(!health.ApplyDamage(player, zone, origin, Vector3.forward, float.NaN, 0), "NaN damage rejected");
                Check(!health.ApplyDamage(player, zone, origin, Vector3.forward, float.PositiveInfinity, 0), "Infinite damage rejected");
                Check(events == 0 && health.Health == 90, "Invalid damage has no side effects");
                Check(attack.TryAttack(), "Attack starts");
                Check(!attack.TryAttack(), "Attack spam cannot restart windup");
                for (int i = 0; i < 10; i++) attack.Tick(0.02f);
                Check(events == 0, "Windup causes no damage");
                Finish(attack);
                Check(events == 1 && health.Health == 45, "One swing hits overlapping body parts only once");
                Check(last.Target == health && last.Source == player && !last.IsFatal, "Damage event identifies source and target");
                Check(attack.TryAttack(), "Attack starts again after recovery");
                Finish(attack);
                Check(events == 2 && deaths == 1 && health.Health == 0, "Second swing kills exactly once");
                Check(!health.ApplyDamage(player, zone, origin, Vector3.forward, 45, 3), "Dead target rejects more damage");

                health.ResetHealth();
                zone.part = BodyPart.Head;
                zone.damageMultiplier = 2.2f;
                Check(health.ApplyDamage(player, zone, origin, Vector3.forward, 45, 3), "Head multiplier applies");
                Check(health.IsDead && last.Amount == 90 && last.Part == BodyPart.Head, "Fatal event reports actual damage and body part");
                zone.part = BodyPart.Torso;
                zone.damageMultiplier = 1f;
                health.ResetHealth();

                var cameraObject = new GameObject("Check Recorder");
                cameraObject.transform.position = origin + Vector3.up * 1.15f;
                var recorder = cameraObject.AddComponent<CameraRecorder>();
                recorder.lens = cameraObject.transform;
                recorder.showOverlay = false;
                Vector3 visible = origin + new Vector3(0, 1.15f, 2);
                Check(recorder.CanSee(visible), "Camera sees a point in front");
                Check(!recorder.CanSee(origin + new Vector3(2, 1.15f, 0)), "Camera excludes side angles");
                Check(recorder.CanSee(origin + new Vector3(0, 1.15f, 6)), "Camera sees beyond the old five-metre limit");
                Check(recorder.CanSee(origin + new Vector3(0, 1.15f, 100000)), "Detection has no gameplay distance cap");
                recorder.conePreviewLength = 1f;
                Check(recorder.CanSee(visible), "Preview length does not restrict detection");
                Check(recorder.CanSee(origin + new Vector3(1f, 1.9f, 2f)), "A visible feed corner is inside the rectangular FOV");
                Check(!recorder.CanSee(origin + new Vector3(0, 3, 2)), "Vertical FOV matches the live feed");
                Check(!recorder.CanSee(origin + new Vector3(0, 1.15f, -2)), "Camera excludes points behind lens");
                recorder.Observe(new DamageEvent(player, health, BodyPart.Torso,
                    origin + new Vector3(0, 1.15f, -2), Vector3.forward, 45, 3, false));
                Check(recorder.RecordedHits == 0 && recorder.LastResult == "STANDBY", "Off-camera event has no recording feedback");
                var recordingLight = new GameObject("Recording Light").AddComponent<LightSource>();
                recordingLight.transform.position = visible;
                recordingLight.intensity = 90f;
                recorder.Observe(new DamageEvent(player, health, BodyPart.Torso, visible, Vector3.forward, 45, 3, false));
                Check(recorder.RecordedHits == 1 && recorder.LastResult == "REC EVENT", "Visible event is recorded");

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = origin + new Vector3(0, 1.15f, 0.6f);
                wall.transform.localScale = new Vector3(2, 3, 0.2f);
                Physics.SyncTransforms();
                Check(!recorder.CanSee(visible), "Wall blocks recording");
                Check(!recorder.CanSee(origin + new Vector3(0, 1.15f, 100000)), "Wall also blocks very distant points");
                recorder.Observe(new DamageEvent(player, health, BodyPart.Torso, visible, Vector3.forward, 45, 3, false));
                Check(recorder.RecordedHits == 1, "Occluded event does not add a recording");
                Check(attack.TryAttack(), "Attack at wall starts");
                Finish(attack);
                Check(health.Health == 90, "Wall blocks melee damage");
                Object.DestroyImmediate(wall);
                Physics.SyncTransforms();
                Check(attack.TryAttack(), "Attack starts after removing wall");
                Finish(attack);
                Check(health.Health == 45, "Same target is hittable without wall");
                cameraObject.transform.rotation = Quaternion.Euler(15f, 75f, 0f);
                Check(recorder.CanSee(cameraObject.transform.TransformPoint(Vector3.forward * 100000f)),
                    "Distant visibility follows handheld yaw and pitch");
                Check(!recorder.CanSee(visible), "Turning the lens changes the detected field of view");
                var otherCamera = new GameObject("Independent Recorder").AddComponent<CameraRecorder>();
                otherCamera.lens = otherCamera.transform;
                otherCamera.transform.SetPositionAndRotation(cameraObject.transform.position, Quaternion.Euler(0, 180, 0));
                cameraObject.transform.rotation = Quaternion.identity;
                recorder.subjects = otherCamera.subjects = new[] { health };
                Check(!recorder.ReportReady && !otherCamera.ReportReady, "Reports stay hidden while the target lives");
                health.Damaged += recorder.Observe;
                health.Damaged += otherCamera.Observe;
                try
                {
                    recordingLight.intensity = 15f;
                    Check(health.ApplyDamage(player, zone, visible, Vector3.forward, health.Health, 0), "Report receives real fatal DamageEvent");
                    CameraDevice witness = recorder.GetComponent<CameraDevice>();
                    CameraDevice blind = otherCamera.GetComponent<CameraDevice>();
                    Check(witness.Events.Count == 2 && witness.RecordedHits == 2 && witness.DeathRecorded,
                        "Witness retains separate hit events, including the fatal hit exactly once");
                    Check(Mathf.Abs(witness.Events[0].Clarity - 0.9f) < 0.001f
                        && Mathf.Abs(witness.Events[1].Clarity - 0.15f) < 0.001f
                        && recorder.ReportText.Contains("Last event quality: 15%"),
                        "The same visible event point drops from 90% to 15% quality; a dark death is still witnessed");
                    Check(blind.Events.Count == 0 && !blind.DeathRecorded && !blind.LastEventDistance.HasValue,
                        "The same damage is not copied to a camera facing away");
                    Check(recorder.ReportReady && otherCamera.ReportReady
                        && otherCamera.ReportText.Contains("Death recorded: NO")
                        && otherCamera.ReportText.Contains("Last recorded distance: N/A"),
                        "Death opens reports without pretending an unseen death was recorded");
                    Check(witness.Events[1].Damage.Target == health && witness.Events[1].Damage.IsFatal
                        && Mathf.Abs(witness.Events[1].Distance - 2f) < 0.001f, "Event retains its target and lens-to-hit distance");
                    cameraObject.transform.position += Vector3.right * 50f;
                    Check(Mathf.Abs(witness.LastEventDistance.Value - 2f) < 0.001f, "Distance is captured at the event, not recomputed later");
                }
                finally { health.Damaged -= recorder.Observe; health.Damaged -= otherCamera.Observe; }
                Debug.Log("RRM CHECKS PASSED: smooth/additive/bounded light, moving/disabled sources, damage validation, phases, deduplication, body parts, death, recording, occlusion.");
            }
            finally
            {
                EditorSceneManager.CloseScene(testScene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        private static Hurtbox Zone(GameObject target, int layer, float x)
        {
            var obj = new GameObject("Overlapping Hurtbox");
            obj.layer = layer;
            obj.transform.SetParent(target.transform, false);
            obj.transform.localPosition = new Vector3(x, 1.15f, 0);
            var collider = obj.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.5f, 0.6f, 0.5f);
            collider.isTrigger = true;
            var zone = obj.AddComponent<Hurtbox>();
            zone.owner = target.GetComponent<Damageable>();
            return zone;
        }

        private static void Finish(MeleeAttack attack)
        {
            for (int i = 0; i < 100; i++) attack.Tick(0.02f);
            Check(!attack.IsBusy, "Attack recovers");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("RRM CHECK FAILED: " + message);
        }
    }

    public static partial class CombatPrototypePlayCheck
    {
        private const string Active = "RRM.PlayCheck.Active";
        private static Keyboard keyboard;
        private static Mouse mouse;
        private static List<InputDevice> otherInputs;
        private static Rigidbody player;
        private static Rigidbody dummyBody;
        private static Damageable dummy;
        private static CameraRecorder recorder;
        private static Camera camera;
        private static int step;
        private static float since;
        private static double started;
        private static float beforeMove;
        private static Vector3 beforePlayer, beforeDummy, beforeLens;
        private static Quaternion beforeLensRotation;
        private static float beforeRedCenter;
        private static float beforeRedHeight;
        private static Vector3 mountLocalPosition, beforeMount;
        private static Quaternion beforePlayerRotation, initialHandheldRotation;
        private static float hitTime;
        private static float unscaledHitTime;
        private static int damageEvents;
        private static bool lastHitVisible;
        private static float beforeHealth;
        private static bool bloodCaptured;
        private static Vector3 wanderStart, wanderPrevious, wanderHeading;
        private static float wanderDistance, wanderSampleTime, wanderStopped, wanderLongestStop, wanderHeadingSince;
        private static int wanderPauses, wanderTurns;
        private static bool wanderMoving;
        private static Collider[] roomColliders;
        private static CapsuleCollider dummyCollider;
        private static Color32[] groundFeed;
        private static RenderTexture placedTexture;
        private static CameraDevice placedDevice;
        private static string deviceId;
        private static int placedRecordings;
        private static Color32[] beforeLiveFrame;
        private static float wanderMinX, wanderMaxX, wanderMinZ, wanderMaxZ;
        private static UnityEngine.Random.State oldRandomState;
        private static bool failed;
        private static bool oldRunInBackground;
        private static float oldCaptureDeltaTime;
        private static InputSettings.BackgroundBehavior oldBackgroundBehavior;
        private static InputSettings.EditorInputBehaviorInPlayMode oldEditorInputBehavior;

        public static void UpdateAndRun()
        {
            CombatPrototypeBuilder.UpdateInteractivity();
            Run();
        }

        public static void RunAndExit()
        {
            SessionState.SetBool("RRM.PlayCheck.ExitEditor", true);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            Run();
        }

        public static void RunCameraAndExit()
        {
            SessionState.SetBool("RRM.PlayCheck.CameraOnly", true);
            RunAndExit();
        }

        public static void RunReportsAndExit()
        {
            SessionState.SetBool("RRM.PlayCheck.ReportsOnly", true);
            RunCameraAndExit();
        }

        public static void RunRangeAndExit()
        {
            SessionState.SetBool("RRM.PlayCheck.RangeOnly", true);
            RunAndExit();
        }

        public static void RunHeightAndExit()
        {
            SessionState.SetBool("RRM.PlayCheck.HeightOnly", true);
            RunAndExit();
        }

        public static void RunControlsAndExit()
        {
            SessionState.SetBool("RRM.PlayCheck.ControlsOnly", true);
            RunAndExit();
        }

        public static void RunLocomotionAndExit()
        {
            SessionState.SetBool("RRM.PlayCheck.LocomotionOnly", true);
            RunAndExit();
        }

        public static void RunStarterCoverAndExit()
        {
            CombatPrototypeBuilder.SetStarterCoverHeight();
            SessionState.SetBool("RRM.PlayCheck.StarterCover", true);
            RunAndExit();
        }

        public static void RunEnemyAndExit()
        {
            SessionState.SetBool("RRM.PlayCheck.EnemyOnly", true);
            RunAndExit();
        }

        public static void RestoreCombinedAndRun()
        {
            CombatPrototypeBuilder.RestoreTwoRoomPrototype();
            RunCombinedAndExit();
        }

        public static void RunCombinedAndExit()
        {
            SessionState.SetBool("RRM.PlayCheck.Combined", true);
            RunRangeAndExit();
        }

        private static bool combined;
        private static bool heightOnly;
        private static bool controlsOnly;
        private static Damageable playerHealth;
        private static int enemyHits;
        private static float enemyHitTime;
        private static Quaternion enemyFacing;
        private static int leftHandClicks, rightHandClicks, handInputFrame, handClickBaseline;
        private static bool leftHandHolding, rightHandHolding;
        private static int movementCase;
        private static Vector3 expectedMovement;
        private static float standingCapsuleHeight, jumpVelocity;
        private static bool spaceTapReleased;
        private static GameObject stanceCeiling;
        private static readonly (Vector2 input, Key[] keys)[] BodyMoves =
        {
            (Vector2.down, new[] { Key.S }),
            (Vector2.left, new[] { Key.A }),
            (Vector2.right, new[] { Key.D }),
            (new Vector2(1, 1).normalized, new[] { Key.W, Key.D })
        };
        private static Vector3 lightSourcePosition;

        public static void RetuneLightsAndRun()
        {
            CombatPrototypeBuilder.RetuneLightTestLayout();
            PrepareEvidenceAndRun();
        }

        public static void PrepareEvidenceAndRun()
        {
            CombatPrototypeBuilder.AddEvidenceTestAreas();
            CombatPrototypeBuilder.AddEvidenceTestAreas(); // Verify repeat setup does not duplicate sources.
            RunCombinedAndExit();
        }

        public static void RunEvidenceAndExit()
        {
            SessionState.SetBool("RRM.PlayCheck.EvidenceOnly", true);
            RunAndExit();
        }

        private static BloodEvent evidenceMark;
        private static Color32[] evidenceFrame;

        public static void Run()
        {
            foreach (string name in new[] { "live-ui.png", "combat-blood-ui.png", "handheld-aim-ui.png",
                "rec-visible-ui.png", "rec-hidden-ui.png", "height-ground-ui.png", "height-platform-ui.png",
                "passive-handheld-ui.png", "passive-recording-ui.png", "controls-handheld-ui.png", "controls-placed-ui.png",
                "device-pickup-ui.png", "report-held-yes.png", "report-held-no.png", "report-placed-no.png", "report-placed-yes.png" })
            {
                string screenshot = Path.GetFullPath("Verification/" + name);
                if (File.Exists(screenshot)) File.Delete(screenshot);
            }
            EditorSceneManager.OpenScene(SessionState.GetBool("RRM.PlayCheck.RangeOnly", false)
                && !SessionState.GetBool("RRM.PlayCheck.Combined", false)
                ? CombatPrototypeBuilder.RangeScenePath : CombatPrototypeBuilder.ScenePath);
            CombatPrototypeTests.Run();
            SessionState.SetBool(Active, true);
            Resume();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Active, false)) return;
            EditorApplication.playModeStateChanged -= StateChanged;
            EditorApplication.playModeStateChanged += StateChanged;
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                oldRunInBackground = Application.runInBackground;
                oldCaptureDeltaTime = Time.captureDeltaTime;
                oldBackgroundBehavior = InputSystem.settings.backgroundBehavior;
                oldEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                Application.runInBackground = true;
                oldRandomState = UnityEngine.Random.state;
                UnityEngine.Random.InitState(int.TryParse(Environment.GetEnvironmentVariable("RRM_PLAY_CHECK_SEED"), out int seed) ? seed : 12345);
                // Screenshot encoding must not consume the simulated input-hold interval.
                Time.captureDeltaTime = 1f / 60f;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                // Batch mode has no focused Game View to receive keyboard or mouse events.
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                // Isolate queued game input from real desktop mouse/keyboard events, restoring them in Finish.
                otherInputs = new List<InputDevice>();
                foreach (InputDevice device in InputSystem.devices)
                    if (device.enabled && (device is Mouse || device is Keyboard))
                    {
                        otherInputs.Add(device);
                        InputSystem.DisableDevice(device);
                    }
                keyboard = InputSystem.AddDevice<Keyboard>();
                mouse = InputSystem.AddDevice<Mouse>();
                player = GameObject.Find("Player").GetComponent<Rigidbody>();
                dummy = GameObject.Find("Dummy").GetComponent<Damageable>();
                dummyBody = dummy.GetComponent<Rigidbody>();
                recorder = Object.FindFirstObjectByType<CameraRecorder>();
                camera = Camera.main;
                started = EditorApplication.timeSinceStartup;
                since = Time.time;
                step = SessionState.GetBool("RRM.PlayCheck.CameraOnly", false) ? 40 : 0;
                SessionState.SetBool("RRM.PlayCheck.CameraOnly", false);
                if (SessionState.GetBool("RRM.PlayCheck.ReportsOnly", false)) step = 55;
                SessionState.SetBool("RRM.PlayCheck.ReportsOnly", false);
                if (SessionState.GetBool("RRM.PlayCheck.RangeOnly", false)) step = 80;
                SessionState.SetBool("RRM.PlayCheck.RangeOnly", false);
                if (SessionState.GetBool("RRM.PlayCheck.EvidenceOnly", false)) step = 100;
                SessionState.SetBool("RRM.PlayCheck.EvidenceOnly", false);
                combined = SessionState.GetBool("RRM.PlayCheck.Combined", false);
                SessionState.SetBool("RRM.PlayCheck.Combined", false);
                heightOnly = SessionState.GetBool("RRM.PlayCheck.HeightOnly", false);
                SessionState.SetBool("RRM.PlayCheck.HeightOnly", false);
                if (heightOnly) step = 33;
                controlsOnly = SessionState.GetBool("RRM.PlayCheck.ControlsOnly", false);
                SessionState.SetBool("RRM.PlayCheck.ControlsOnly", false);
                if (controlsOnly || combined) step = 120;
                if (SessionState.GetBool("RRM.PlayCheck.LocomotionOnly", false)) step = 200;
                SessionState.SetBool("RRM.PlayCheck.LocomotionOnly", false);
                if (SessionState.GetBool("RRM.PlayCheck.EnemyOnly", false)) step = 300;
                SessionState.SetBool("RRM.PlayCheck.EnemyOnly", false);
                if (SessionState.GetBool("RRM.PlayCheck.StarterCover", false)) step = 500;
                SessionState.SetBool("RRM.PlayCheck.StarterCover", false);
                failed = false;
                hitTime = -1f;
                damageEvents = 0;
                bloodCaptured = false;
                dummy.Damaged += ObserveHit;
                Application.logMessageReceived += WatchErrors;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Active, false))
            {
                SessionState.SetBool(Active, false);
                if (Application.isBatchMode || SessionState.GetBool("RRM.PlayCheck.ExitEditor", false))
                {
                    SessionState.SetBool("RRM.PlayCheck.ExitEditor", false);
                    EditorApplication.Exit(SessionState.GetInt("RRM.PlayCheck.ExitCode", 1));
                }
            }
        }

        private static void WatchErrors(string message, string stack, LogType type)
        {
            if (type == LogType.Exception && message.Contains("Index was out of range")
                && stack.Contains("UnityEditor.Search.SearchDatabase"))
            {
                Debug.LogWarning("Unity editor search index failed during startup; gameplay checks continue independently.");
                return;
            }
            if (type == LogType.Exception || type == LogType.Error) failed = true;
        }

        private static void Tick()
        {
            try
            {
                if (!Application.isPlaying) return;
                if (failed) throw new InvalidOperationException("Runtime logged an error; see the preceding entry.");
                // Native captures can make simulated frames much slower than wall time in the Editor.
                if (EditorApplication.timeSinceStartup - started > (combined ? 240 : step >= 200 ? 180 : 90))
                    throw new TimeoutException($"Play check timed out at step {step}, simulated age {Time.time - since:F2}, "
                        + $"player {(player ? player.position.ToString() : "RELOADING")}, return point {beforePlayer}.");
                float age = Time.time - since;
                EditorApplication.QueuePlayerLoopUpdate();
                if (SessionState.GetInt("RRM.PlayCheck.Lamps", 0) != 0) { TickLamps(age); return; }
                if (step >= 500) { TickStarterCover(age); return; }
                if (step == 10)
                    Require(dummyBody.position.z >= -6.55f, "Dummy never crosses the south wall during the impulse");
                if (step == 4 && hitTime >= 0f && !bloodCaptured && Time.time - hitTime > 0.12f)
                {
                    CaptureBlood();
                    bloodCaptured = true;
                }
                if (step == 0 && age > 0.4f)
                {
                    Require(dummy.Health == 90, "Dummy starts alive");
                    Capture("prototype-room.png", 1280, 800);
                    beforeMove = player.position.z;
                    beforeDummy = dummyBody.position;
                    dummy.SendMessage("OnApplicationFocus", false);
                    AimAt(dummy.transform.position);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    Next();
                }
                else if (step == 1 && age > 0.45f)
                {
                    Require(player.position.z > beforeMove + 0.4f,
                        $"W input moves the player (distance: {player.position.z - beforeMove:F3}, velocity: {player.linearVelocity})");
                    Require(Vector3.ProjectOnPlane(dummyBody.position - beforeDummy, Vector3.up).magnitude > 0.25f,
                        $"Dummy moves autonomously while the player approaches: from {beforeDummy} to {dummyBody.position}, velocity {dummyBody.linearVelocity}, t={Time.time}");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Vector3 approach = Vector3.ProjectOnPlane(dummyBody.linearVelocity, Vector3.up).normalized;
                    player.position = dummy.transform.position + approach * 1.8f;
                    player.rotation = Quaternion.LookRotation(-approach);
                    player.linearVelocity = Vector3.zero;
                    AimAt(dummy.transform.position);
                    Next();
                }
                else if (step == 2 && age > 0.3f)
                {
                    AimAt(dummy.transform.position, true);
                    Next();
                }
                else if (step == 3 && age > 0.08f)
                {
                    Require(player.GetComponent<MeleeAttack>().IsBusy, "Mouse input starts an attack");
                    AimAt(dummy.transform.position);
                    Next();
                }
                else if (step == 4 && age > 1.3f)
                {
                    Require(dummy.Health < 90, "Runtime swing damages the dummy");
                    Require(bloodCaptured, "A real mouse attack produced visible blood");
                    Require(damageEvents == 1 && recorder.RecordedHits == (lastHitVisible ? 1 : 0),
                        "Exactly one damage event is recorded only when visible");
                    Require(!player.GetComponent<MeleeAttack>().IsBusy, "Runtime recovery completes");
                    // Test the fatal fall in clear space, not leaning against the approaching player.
                    player.position = new Vector3(-6f, 0.02f, -4f);
                    dummyBody.position = new Vector3(0, 0.02f, 0);
                    dummyBody.rotation = Quaternion.identity;
                    player.linearVelocity = dummyBody.linearVelocity = Vector3.zero;
                    dummyBody.angularVelocity = Vector3.zero;
                    Hurtbox zone = dummy.GetComponentInChildren<Hurtbox>();
                    dummy.ApplyDamage(player.gameObject, zone, dummyBody.position + Vector3.up * 1.15f, Vector3.forward, 500, 3);
                    Require(dummy.IsDead, "Dummy can die in Play Mode");
                    Next();
                }
                else if (step == 5 && age > 0.8f)
                {
                    Capture("prototype-fatal.png", 960, 720);
                    Require(Vector3.Dot(dummy.transform.up, Vector3.up) < 0.9f, "Fatal hit releases the body to fall");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                    Next();
                }
                else if (step == 6 && age > 0.6f)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Require(GameObject.Find("Dummy").GetComponent<Damageable>().Health == 90, "R resets the dummy");
                    Require(Object.FindFirstObjectByType<CameraRecorder>().RecordedHits == 0, "R resets the tape counter");
                    player = GameObject.Find("Player").GetComponent<Rigidbody>();
                    dummy = GameObject.Find("Dummy").GetComponent<Damageable>();
                    dummyBody = dummy.GetComponent<Rigidbody>();
                    recorder = Object.FindFirstObjectByType<CameraRecorder>();
                    camera = Camera.main;
                    Require(dummy.GetComponent<PlayerController>().autonomousMovement, "Dummy uses autonomous movement");
                    Require(dummy.GetComponent<MeleeAttack>(), "Dummy reuses the existing melee component");
                    Require(recorder.liveCamera && recorder.liveCamera != camera
                        && recorder.liveCamera.transform == recorder.lens
                        && recorder.lens.IsChildOf(player.transform), "Live camera is mounted in the player's hand");
                    FrameDummy();
                    Next();
                }
                else if (step == 7 && age > 0.4f)
                {
                    beforeLiveFrame = ReadLiveFrame("live-before.png");
                    RedCenter(beforeLiveFrame);
                    beforePlayer = player.position;
                    beforeDummy = dummyBody.position;
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/live-ui.png"));
                    Next();
                }
                else if (step == 8 && age > 0.55f
                    && (Vector3.Distance(dummyBody.position, beforeDummy) > 0.3f || age > 3f))
                {
                    Vector3 travel = Vector3.ProjectOnPlane(dummyBody.position - beforeDummy, Vector3.up);
                    Require(travel.magnitude > 0.3f, "Dummy moves without keyboard input");
                    Require(Vector3.Distance(player.position, beforePlayer) < 0.05f, "Player stays still without input");
                    Color32[] afterLiveFrame = ReadLiveFrame("live-after.png");
                    RedCenter(afterLiveFrame);
                    int changed = ChangedPixels(beforeLiveFrame, afterLiveFrame);
                    Require(changed > 300, "The randomly moving dummy changes the live texture");
                    Debug.Log($"RRM LIVE CHECK: dummy moved {travel.magnitude:F2} m, {changed} live pixels changed.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Next();
                }
                else if (step == 9 && age > 0.2f)
                {
                    player.position = new Vector3(-1f, 0.02f, -6f);
                    dummyBody.position = new Vector3(1f, 0.02f, -6f);
                    player.linearVelocity = dummyBody.linearVelocity = Vector3.zero;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                    dummyBody.AddForce(Vector3.back * 8f, ForceMode.VelocityChange);
                    Next();
                }
                else if (step == 10 && age > 1f)
                {
                    Require(player.position.z >= -6.55f && player.position.z < -6.2f && player.position.y > -0.05f,
                        "The south wall physically stops the player");
                    Require(dummyBody.position.z >= -6.55f && dummyBody.position.y > -0.05f,
                        "The dummy stays inside the room after an impulse at the wall");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    FrameDummy();
                    Next();
                }
                else if (step == 11 && age > 0.4f)
                {
                    beforeLens = recorder.liveCamera.transform.position;
                    beforeLensRotation = recorder.liveCamera.transform.rotation;
                    AimAt(player.position + Vector3.right * 5f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                    Next();
                }
                else if (step == 12 && age > 0.5f)
                {
                    Require(Vector3.Distance(recorder.liveCamera.transform.position, beforeLens) > 0.3f,
                        "Handheld viewpoint moves with the player");
                    Require(Quaternion.Angle(recorder.liveCamera.transform.rotation, beforeLensRotation) > 60f,
                        "Handheld viewpoint turns with the player");
                    ReadLiveFrame("live-turned.png");
                    if (!Application.isBatchMode)
                    {
                        Require(File.Exists(Path.GetFullPath("Verification/live-ui.png")), "Game View screenshot was captured");
                        Require(File.Exists(Path.GetFullPath("Verification/combat-blood-ui.png")), "Hit screenshot including UI was captured");
                    }
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    dummy.GetComponent<PlayerController>().enabled = false;
                    dummyBody.position = new Vector3(8.3f, 0.02f, 6.3f);
                    dummyBody.linearVelocity = Vector3.zero;
                    dummy.GetComponent<PlayerController>().enabled = true;
                    wanderStart = wanderPrevious = dummyBody.position;
                    wanderMinX = wanderMaxX = dummyBody.position.x;
                    wanderMinZ = wanderMaxZ = dummyBody.position.z;
                    wanderDistance = wanderStopped = wanderLongestStop = 0;
                    wanderPauses = wanderTurns = 0;
                    wanderMoving = false;
                    wanderHeading = Vector3.zero;
                    wanderSampleTime = Time.time;
                    roomColliders = GameObject.Find("Test Room").GetComponentsInChildren<Collider>();
                    dummyCollider = dummy.GetComponent<CapsuleCollider>();
                    player.position = new Vector3(-6f, 0.02f, -4f);
                    player.linearVelocity = Vector3.zero;
                    Next();
                }
                else if (step == 13)
                {
                    Vector3 position = dummyBody.position;
                    Require(Mathf.Abs(position.x) < 8.55f && Mathf.Abs(position.z) < 6.55f && position.y > -0.05f,
                        $"Wandering Dummy stays inside the room: {position}");
                    foreach (Collider wall in roomColliders)
                        Require(!Physics.ComputePenetration(dummyCollider, position, dummyBody.rotation,
                            wall, wall.transform.position, wall.transform.rotation, out _, out float depth) || depth < 0.06f,
                            $"Dummy does not pass through {wall.name}: penetration {depth:F3}");
                    float dt = Time.time - wanderSampleTime;
                    wanderSampleTime = Time.time;
                    wanderDistance += Vector3.ProjectOnPlane(position - wanderPrevious, Vector3.up).magnitude;
                    wanderPrevious = position;
                    wanderMinX = Mathf.Min(wanderMinX, position.x); wanderMaxX = Mathf.Max(wanderMaxX, position.x);
                    wanderMinZ = Mathf.Min(wanderMinZ, position.z); wanderMaxZ = Mathf.Max(wanderMaxZ, position.z);
                    Vector3 velocity = Vector3.ProjectOnPlane(dummyBody.linearVelocity, Vector3.up);
                    Require(velocity.magnitude < 1.4f, "Dummy keeps its slow walking speed");
                    if (velocity.magnitude < 0.05f)
                    {
                        if (wanderMoving) wanderPauses++;
                        wanderMoving = false;
                        wanderStopped += dt;
                        wanderLongestStop = Mathf.Max(wanderLongestStop, wanderStopped);
                        Require(wanderStopped < 2.5f, "Dummy does not remain stuck at a wall or corner");
                    }
                    else if (velocity.magnitude > 0.5f)
                    {
                        wanderMoving = true;
                        wanderStopped = 0;
                        if (wanderHeading == Vector3.zero || Vector3.Dot(wanderHeading, velocity.normalized) < 0.7f)
                        {
                            wanderTurns++;
                            wanderHeading = velocity.normalized;
                            wanderHeadingSince = Time.time;
                        }
                        if (Time.time - wanderHeadingSince > 0.55f)
                            Require(Vector3.Dot(dummy.transform.forward, velocity.normalized) > 0.85f,
                                "Dummy faces its walking direction after turning");
                    }
                    if (age > 4f && age < 5f)
                        Require(Vector3.Distance(position, wanderStart) > 1f, "Dummy finds a free direction out of the room corner");
                    if (age > 40f)
                    {
                        Require(wanderDistance > 15f && wanderMaxX - wanderMinX > 3f && wanderMaxZ - wanderMinZ > 3f,
                            $"Dummy explores the room: {wanderDistance:F1} m, X span {wanderMaxX - wanderMinX:F1}, Z span {wanderMaxZ - wanderMinZ:F1}");
                        Require(wanderPauses >= 3 && wanderTurns >= 4 && wanderLongestStop > 0.15f,
                            $"Dummy walks, pauses and changes direction: {wanderPauses} pauses, {wanderTurns} turns");
                        Capture("dummy-wander-room.png", 1280, 800);
                        Debug.Log($"RRM WANDER CHECK: 40 s, {wanderDistance:F1} m, {wanderPauses} pauses, {wanderTurns} turns, longest stop {wanderLongestStop:F2} s; room collisions and corner escape passed.");
                        Require(dummy.GetComponent<HitFeedback>().blood.particleCount == 0, "Blood is a short burst, not continuous emission");
                        FrameDummy();
                        // Keep the existing target still for before/after camera image comparisons.
                        dummy.GetComponent<PlayerController>().enabled = false;
                        dummyBody.linearVelocity = Vector3.zero;
                        Next();
                    }
                }
                else if (step == 14 && age > 0.4f)
                {
                    Color32[] frame = ReadLiveFrame("handheld-forward.png");
                    beforeRedCenter = RedCenter(frame);
                    beforeRedHeight = RedCenter(frame, true);
                    beforePlayerRotation = player.rotation;
                    initialHandheldRotation = recorder.transform.localRotation;
                    mountLocalPosition = recorder.transform.localPosition;
                    AimCamera(Vector2.zero);
                    Next();
                }
                else if (step == 15 && age > 0.1f)
                {
                    // Editor callbacks read editor input buffers; verify the resulting runtime image below.
                    AimCamera(new Vector2(80f, 0f));
                    Next();
                }
                else if (step == 16 && age > 0.2f)
                {
                    Require(RedCenter(ReadLiveFrame("handheld-right.png")) < beforeRedCenter - 40f,
                        "Yaw right moves the real target left in the live feed");
                    AimCamera(new Vector2(-160f, 0f));
                    Next();
                }
                else if (step == 17 && age > 0.2f)
                {
                    Require(RedCenter(ReadLiveFrame("handheld-left.png")) > beforeRedCenter + 40f,
                        "Yaw left moves the real target right in the live feed");
                    AimCamera(new Vector2(80f, -50f));
                    Next();
                }
                else if (step == 18 && age > 0.2f)
                {
                    Require(RedCenter(ReadLiveFrame("handheld-down.png"), true) > beforeRedHeight + 25f,
                        "Looking down moves the target upward in the live feed");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/handheld-aim-ui.png"));
                    AimCamera(new Vector2(0f, 100f));
                    Next();
                }
                else if (step == 19 && age > 0.2f)
                {
                    Require(RedCenter(ReadLiveFrame("handheld-up.png"), true) < beforeRedHeight - 25f,
                        "Looking up moves the target downward in the live feed");
                    Require(Quaternion.Angle(player.rotation, beforePlayerRotation) < 0.5f,
                        "Camera drag never rotates the character");
                    Require(Vector3.Distance(recorder.transform.localPosition, mountLocalPosition) < 0.001f,
                        "Camera drag leaves its mount in the player's hand");
                    AimCamera(new Vector2(50000f, -50000f));
                    Next();
                }
                else if (step == 20 && age > 0.2f)
                {
                    Vector3 angles = recorder.transform.localEulerAngles;
                    Require(Mathf.Abs(Mathf.DeltaAngle(angles.x, recorder.maxPitch)) < 0.1f,
                        "Large mouse deltas clamp downward pitch while yaw remains unrestricted");
                    ReadLiveFrame("handheld-down-limit.png");
                    AimCamera(new Vector2(-100000f, 100000f));
                    Next();
                }
                else if (step == 21 && age > 0.2f)
                {
                    Vector3 angles = recorder.transform.localEulerAngles;
                    Require(Mathf.Abs(Mathf.DeltaAngle(angles.x, recorder.minPitch)) < 0.1f,
                        "Large mouse deltas clamp upward pitch while yaw remains unrestricted");
                    ReadLiveFrame("handheld-up-limit.png");
                    // Compare rendered transforms: Rigidbody.position is ahead of interpolation.
                    beforePlayer = player.transform.position;
                    beforeMount = recorder.transform.position;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    Next();
                }
                else if (step == 22 && age > 0.5f)
                {
                    Vector3 travel = player.transform.position - beforePlayer;
                    Require(travel.z > 0.4f, "WASD works while aiming the handheld camera");
                    Require(Vector3.Distance(recorder.transform.position - beforeMount, travel) < 0.02f,
                        "The handheld camera moves exactly with the character");
                    Require(Quaternion.Angle(player.rotation, beforePlayerRotation) < 0.5f,
                        "Moving while filming does not turn the character");
                    beforeLensRotation = recorder.transform.localRotation;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.QueueStateEvent(mouse, new MouseState
                    {
                        position = camera.WorldToScreenPoint(player.position + Vector3.forward * 5f),
                        delta = new Vector2(10000f, 10000f)
                    });
                    Next();
                }
                else if (step == 23 && age > 0.3f)
                {
                    Require(Quaternion.Angle(recorder.transform.localRotation, beforeLensRotation) < 0.1f,
                        "Releasing RMB retains the camera angle and ignores free mouse movement");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                    Next();
                }
                else if (step == 24 && age > 0.6f)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    recorder = Object.FindFirstObjectByType<CameraRecorder>();
                    Require(Quaternion.Angle(recorder.transform.localRotation, initialHandheldRotation) < 0.1f,
                        "Restart restores the authored camera angle");
                    ReadLiveFrame("handheld-restarted.png");
                    if (!Application.isBatchMode)
                        Require(File.Exists(Path.GetFullPath("Verification/handheld-aim-ui.png")), "Handheld UI screenshot was captured");
                    player = GameObject.Find("Player").GetComponent<Rigidbody>();
                    dummy = GameObject.Find("Dummy").GetComponent<Damageable>();
                    dummyBody = dummy.GetComponent<Rigidbody>();
                    camera = Camera.main;
                    dummy.GetComponent<PlayerController>().enabled = false;
                    dummy.Damaged += ObserveHit;
                    damageEvents = 0;
                    PositionForRecordingHit();
                    AimAt(dummyBody.position);
                    Next();
                }
                else if (step == 25 && age > 0.3f)
                {
                    AimCamera(Vector2.zero);
                    Next();
                }
                else if (step == 26 && age > 0.1f)
                {
                    AimCamera(new Vector2(45f / recorder.mouseSensitivity, 0f));
                    Next();
                }
                else if (step == 27 && age > 0.2f)
                {
                    RedCenter(ReadLiveFrame("rec-target.png"));
                    Require(recorder.RecordedHits == 0 && recorder.LastResult == "STANDBY", "Recording starts idle");
                    AimCamera(Vector2.zero, true);
                    Next();
                }
                else if (step == 28 && damageEvents > 0 && Time.time - hitTime > 0.08f)
                {
                    Require(damageEvents == 1 && dummy.Health < 90 && lastHitVisible,
                        "First real melee hit occurs inside the handheld FOV");
                    Require(recorder.RecordedHits == 1 && recorder.LastResult == "REC EVENT",
                        "Visible melee hit increments REC and shows feedback");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/rec-visible-ui.png"));
                    Debug.Log("RRM RECORDING CHECK: visible mouse-driven hit -> REC 1, REC EVENT.");
                    AimCamera(Vector2.zero);
                    Next();
                }
                else if (step == 29 && age > 1.2f && Time.unscaledTime - unscaledHitTime > 1.1f)
                {
                    Require(recorder.LastResult == "STANDBY" && !player.GetComponent<MeleeAttack>().IsBusy,
                        "Recording feedback expires and the swing recovers");
                    PositionForRecordingHit();
                    AimCamera(new Vector2(55f / recorder.mouseSensitivity, 0f));
                    Next();
                }
                else if (step == 30 && age > 0.2f)
                {
                    Require(!recorder.CanSee(dummyBody.position + Vector3.up * 1.15f), "Camera is turned away from the hit area");
                    beforeHealth = dummy.Health;
                    AimCamera(Vector2.zero, true);
                    Next();
                }
                else if (step == 31 && damageEvents > 1 && Time.time - hitTime > 0.08f)
                {
                    Require(damageEvents == 2 && dummy.Health < beforeHealth && !lastHitVisible,
                        "Second real melee hit still deals damage outside the handheld FOV");
                    Require(recorder.RecordedHits == 1 && recorder.LastResult == "STANDBY",
                        "Off-camera hit produces no recording feedback or counter change");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/rec-hidden-ui.png"));
                    Debug.Log("RRM RECORDING CHECK: off-camera mouse-driven hit -> REC remains 1, STANDBY.");
                    AimCamera(Vector2.zero);
                    Next();
                }
                else if (step == 32 && age > 0.2f)
                {
                    if (!Application.isBatchMode)
                        Require(File.Exists(Path.GetFullPath("Verification/rec-visible-ui.png"))
                            && File.Exists(Path.GetFullPath("Verification/rec-hidden-ui.png")), "Both recording feedback screenshots exist");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                    Next();
                }
                else if (step == 33 && age > 0.6f)
                {
                    Require(SceneManager.GetActiveScene().path == CombatPrototypeBuilder.ScenePath
                        && GameObject.Find("Camera Platform") && GameObject.Find("Platform Ramp"),
                        "Height test uses the existing platform and ramp in CombatPrototype");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    player = GameObject.Find("Player").GetComponent<Rigidbody>();
                    dummy = GameObject.Find("Dummy").GetComponent<Damageable>();
                    dummyBody = dummy.GetComponent<Rigidbody>();
                    recorder = Object.FindFirstObjectByType<CameraRecorder>();
                    camera = Camera.main;
                    dummy.GetComponent<PlayerController>().enabled = false;
                    // Stage a stationary target behind the existing block; ascent/descent use only WASD.
                    player.position = new Vector3(5.5f, 0.02f, -5.5f);
                    dummyBody.position = new Vector3(4.4f, 0.02f, 4.8f);
                    player.rotation = dummyBody.rotation = Quaternion.identity;
                    player.linearVelocity = dummyBody.linearVelocity = Vector3.zero;
                    recorder.SetViewRotation(Quaternion.Euler(8, 0, 0));
                    AimCamera(Vector2.zero);
                    Next();
                }
                else if (step == 34 && age > 0.5f)
                {
                    Require(!recorder.CanSee(dummyBody.position + Vector3.up * 1.1f),
                        "The concrete block hides the target's torso from floor height");
                    beforePlayer = player.transform.position;
                    beforeLens = recorder.lens.position;
                    beforeLensRotation = recorder.lens.rotation;
                    groundFeed = ReadLiveFrame("height-ground-feed.png");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/height-ground-ui.png"));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    Next();
                }
                else if (step == 35 && (player.position.z > -0.75f || age > 5f))
                {
                    Require(player.position.z > -0.75f, $"W walks up the ramp onto the platform: {player.position}");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Next();
                }
                else if (step == 36 && age > 0.6f)
                {
                    Vector3 travel = player.transform.position - beforePlayer;
                    Require(Mathf.Abs(travel.y - 0.9f) < 0.04f, $"Player stands on the 0.9 m platform: rise {travel.y:F3}");
                    Require(Vector3.Distance(recorder.lens.position - beforeLens, travel) < 0.02f,
                        "The handheld lens gains exactly the player's height and displacement");
                    Require(Quaternion.Angle(recorder.lens.rotation, beforeLensRotation) < 0.1f,
                        "Walking uphill does not change the handheld viewing angle");
                    Require(recorder.CanSee(dummyBody.position + Vector3.up * 1.1f),
                        "The elevated camera now sees the target's torso over the same block");
                    Vector3 raisedPosition = recorder.transform.position;
                    try
                    {
                        recorder.transform.position -= Vector3.up * travel.y;
                        Require(!recorder.CanSee(dummyBody.position + Vector3.up * 1.1f),
                            "At the same horizontal position and angle, lowering the lens hides the torso");
                    }
                    finally { recorder.transform.position = raisedPosition; }
                    Color32[] raised = ReadLiveFrame("height-platform-feed.png");
                    RedCenter(raised);
                    int changed = ChangedPixels(groundFeed, raised);
                    Require(changed > 3000, $"Live feed changes after ascent: {changed} pixels");
                    Capture("height-platform-room.png", 1280, 800);
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/height-platform-ui.png"));
                    Debug.Log($"RRM HEIGHT CHECK: W ascent {travel.y:F3} m, lens follows, target visible over block, feed changes {changed} pixels.");
                    beforePlayer = player.transform.position;
                    Next();
                }
                else if (step == 37 && age > 0.75f)
                {
                    Require(Vector3.Distance(player.transform.position, beforePlayer) < 0.08f,
                        "Player stays on the platform without input");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                    Next();
                }
                else if (step == 38 && (player.position.z < -5.35f || age > 5f))
                {
                    Require(player.position.z < -5.35f, $"S walks back down the ramp: {player.position}");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Next();
                }
                else if (step == 39 && age > 0.5f)
                {
                    Require(Mathf.Abs(player.position.y) < 0.04f, "Player returns to floor height without teleporting or jumping");
                    Require(Mathf.Abs(recorder.lens.position.y - beforeLens.y) < 0.02f, "The lens returns to its original height");
                    Require(!recorder.CanSee(dummyBody.position + Vector3.up * 1.1f), "The block hides the torso again after descent");
                    ReadLiveFrame("height-return-feed.png");
                    if (!Application.isBatchMode)
                        Require(File.Exists(Path.GetFullPath("Verification/height-ground-ui.png"))
                            && File.Exists(Path.GetFullPath("Verification/height-platform-ui.png")), "Both height UI screenshots exist");
                    Debug.Log("RRM HEIGHT CHECK: S descent to floor, camera returns with player, live feed still active.");
                    if (combined)
                    {
                        step = 200;
                        since = Time.time;
                        return;
                    }
                    if (combined || heightOnly)
                    {
                        Finish(true, heightOnly ? "RRM HEIGHT PLAY CHECK PASSED: WASD ascent/descent, stable platform stance, "
                            + "lens follows player, unchanged aim, real live feed and height-dependent visibility."
                            : "RRM COMBINED PLAY CHECK PASSED: two-room layout, doorway traversal, cover, report, "
                            + "real melee, persistent floor/wall marks, native feed pixels, placed inspection, LightLevel, "
                            + "occlusion, restart, WASD ascent/descent, stable platform stance and height-dependent camera visibility.");
                        return;
                    }
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                    Next();
                }
                else if (step == 200 && age > 0.2f)
                {
                    StageRange(new Vector3(-6, 0.02f, -4), new Vector3(2.1f, 0.02f, 1.1f));
                    player.rotation = Quaternion.Euler(0, 90, 0);
                    player.transform.rotation = player.rotation;
                    standingCapsuleHeight = player.GetComponent<CapsuleCollider>().height;
                    mountLocalPosition = recorder.transform.localPosition;
                    placedTexture = recorder.LiveTexture;
                    Next();
                }
                else if (step == 201 && age > 0.4f)
                {
                    Require(player.GetComponent<PlayerController>().IsGrounded, "Standing on floor permits a jump");
                    beforePlayer = player.position;
                    beforeLens = recorder.lens.position;
                    beforeLensRotation = recorder.lens.rotation;
                    beforeLiveFrame = ReadLiveFrame("movement-standing-feed.png");
                    Capture("movement-standing.png", 1280, 800);
                    spaceTapReleased = false;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    Next();
                }
                else if (step == 202 && age > 0.06f && !spaceTapReleased)
                {
                    Require(player.GetComponent<PlayerController>().IsGrounded,
                        "Space press waits for tap/hold instead of jumping immediately");
                    spaceTapReleased = true;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                }
                else if (step == 202 && age > 0.22f)
                {
                    Require(!player.GetComponent<PlayerController>().IsGrounded && player.position.y > beforePlayer.y + 0.25f
                        && player.linearVelocity.y > 0 && Vector3.ProjectOnPlane(player.position - beforePlayer, Vector3.up).magnitude < 0.03f,
                        "Short Space release makes a modest standing jump without horizontal drift");
                    Require(Vector3.Distance(recorder.lens.position - beforeLens, player.transform.position - beforePlayer) < 0.04f
                        && Quaternion.Angle(recorder.lens.rotation, beforeLensRotation) < 0.1f
                        && ChangedPixels(beforeLiveFrame, ReadLiveFrame("movement-jump-feed.png")) > 1000,
                        "Jump raises the actual lens and changes live pixels without steering it");
                    Capture("movement-jump.png", 1280, 800);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Next();
                }
                else if (step == 203 && age > 0.06f)
                {
                    jumpVelocity = player.linearVelocity.y;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    Next();
                }
                else if (step == 204 && age > 0.06f)
                {
                    Require(player.linearVelocity.y < jumpVelocity - 0.2f && player.position.y < beforePlayer.y + 0.7f,
                        "A second Space press in the air cannot add another jump");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Next();
                }
                else if (step == 205 && age > 0.85f)
                {
                    Require(player.GetComponent<PlayerController>().IsGrounded && Mathf.Abs(player.position.y - beforePlayer.y) < 0.03f,
                        "An air tap cannot queue a jump; landing returns to the same surface");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    Next();
                }
                else if (step == 206 && age > 0.25f)
                {
                    CheckStance(true);
                    Require(Vector3.Distance(player.position, beforePlayer) < 0.03f
                        && ChangedPixels(beforeLiveFrame, ReadLiveFrame("movement-crouch-feed.png")) > 1000,
                        "Stationary crouch keeps feet planted and lowers the live viewpoint");
                    Capture("movement-crouch.png", 1280, 800);
                    stanceCeiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stanceCeiling.transform.position = player.position + Vector3.up * 1.4f;
                    stanceCeiling.transform.localScale = new Vector3(1.2f, 0.2f, 1.2f);
                    Physics.SyncTransforms();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Next();
                }
                else if (step == 207 && age > 0.2f)
                {
                    CheckStance(true);
                    Require(Vector3.Distance(player.position, beforePlayer) < 0.03f,
                        "Long Space release neither jumps nor expands the capsule into a low ceiling");
                    Object.DestroyImmediate(stanceCeiling);
                    Physics.SyncTransforms();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
                    Next();
                }
                else if (step == 208 && age > 0.4f)
                {
                    CheckStance(true);
                    beforePlayer = player.position;
                    Next();
                }
                else if (step == 209 && age > 0.4f)
                {
                    float speed = Vector3.ProjectOnPlane(player.position - beforePlayer, Vector3.up).magnitude / age;
                    Require(Mathf.Abs(speed - player.GetComponent<PlayerController>().moveSpeed * 0.5f) < 0.25f,
                        $"Held Space + W moves at half speed: {speed:F2} m/s");
                    Debug.Log($"RRM LOCOMOTION SPEED: crouched {speed:F2} m/s at {player.position}.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    Next();
                }
                else if (step == 210 && age > 0.35f)
                {
                    CheckStance(false);
                    float speed = Vector3.ProjectOnPlane(player.linearVelocity, Vector3.up).magnitude;
                    Require(speed > 2.9f && player.GetComponent<PlayerController>().IsGrounded,
                        $"Releasing held Space restores normal speed without jumping: {speed:F2} m/s at {player.position}");
                    Debug.Log($"RRM LOCOMOTION SPEED: standing {speed:F2} m/s at {player.position}.");
                    StageRange(new Vector3(-7, 0.02f, -4), new Vector3(2.1f, 0.02f, 1.1f), false);
                    player.rotation = Quaternion.Euler(0, 90, 0);
                    player.transform.rotation = player.rotation;
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    Next();
                }
                else if (step == 211 && age > 0.2f)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    AimCamera(Vector2.zero);
                    Next();
                }
                else if (step == 212 && age > 0.35f)
                {
                    beforePlayer = player.position;
                    beforePlayerRotation = player.rotation;
                    beforeLensRotation = recorder.lens.rotation;
                    AimCamera(new Vector2(90f / recorder.mouseSensitivity, 0));
                    spaceTapReleased = false;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
                    Next();
                }
                else if (step == 213 && age > 0.06f && !spaceTapReleased)
                {
                    spaceTapReleased = true;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                }
                else if (step == 213 && age > 0.25f)
                {
                    Require(player.position.x > beforePlayer.x + 0.4f && player.position.y > beforePlayer.y + 0.3f
                        && player.linearVelocity.x > 2.9f && !player.GetComponent<PlayerController>().IsGrounded,
                        "W + Space keeps forward momentum throughout the moving jump");
                    Require(Quaternion.Angle(player.rotation, beforePlayerRotation) < 0.1f
                        && Mathf.Abs(Quaternion.Angle(recorder.lens.rotation, beforeLensRotation) - 90f) < 0.1f,
                        "Hand input turns the camera independently during a moving jump");
                    beforeLensRotation = recorder.lens.rotation;
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    Next();
                }
                else if (step == 214 && age > 1.1f)
                {
                    // Allow the motor to recover from the landing contact before measuring full speed.
                    Require(player.GetComponent<PlayerController>().IsGrounded && Mathf.Abs(player.position.y) < 0.03f
                        && player.linearVelocity.x > 2.9f && Quaternion.Angle(recorder.lens.rotation, beforeLensRotation) < 0.1f,
                        $"After landing: grounded={player.GetComponent<PlayerController>().IsGrounded}, "
                        + $"position={player.position}, velocity={player.linearVelocity}, "
                        + $"camera drift={Quaternion.Angle(recorder.lens.rotation, beforeLensRotation):F3}, age={age:F3}");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
                    Next();
                }
                else if (step == 215 && age > 0.3f)
                {
                    CheckStance(true);
                    placedDevice = recorder.GetComponent<CameraDevice>();
                    deviceId = placedDevice.Id;
                    beforeMount = recorder.transform.position;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.Q));
                    Next();
                }
                else if (step == 216 && age > 0.2f)
                {
                    Require(recorder.IsPlaced && !recorder.LiveTexture && Vector3.Distance(recorder.transform.position, beforeMount) < 0.01f,
                        "Q places the same camera at crouched hand height");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Next();
                }
                else if (step == 217 && age > 0.3f)
                {
                    Require(!player.GetComponent<PlayerController>().IsCrouching && !placedDevice.Owner
                        && Vector3.Distance(recorder.transform.position, beforeMount) < 0.01f,
                        "Standing up cannot move a placed camera");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
                    Next();
                }
                else if (step == 218 && age > 0.2f)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.E));
                    Next();
                }
                else if (step == 219 && age > 0.2f)
                {
                    CheckStance(true);
                    Require(player.GetComponent<PlayerController>().RightHand.Item?.Source == placedDevice && placedDevice.Id == deviceId,
                        "Crouched pickup uses the requested lowered hand and preserves camera ID");
                    Capture("movement-crouch-right-camera.png", 1280, 800);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
                    Next();
                }
                else if (step == 220 && age > 0.3f)
                {
                    CheckStance(false);
                    Require(recorder.ControlsVisible, "F1 still opens the guide with jump/crouch controls");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                    Next();
                }
                else if (step == 221 && age > 0.6f)
                {
                    player = GameObject.Find("Player").GetComponent<Rigidbody>();
                    dummy = GameObject.Find("Dummy").GetComponent<Damageable>();
                    dummyBody = dummy.GetComponent<Rigidbody>();
                    recorder = Object.FindFirstObjectByType<CameraRecorder>();
                    camera = Camera.main;
                    Require(!player.GetComponent<PlayerController>().IsCrouching && !recorder.IsPlaced
                        && Mathf.Abs(player.GetComponent<CapsuleCollider>().height - standingCapsuleHeight) < 0.001f,
                        "Scene restart restores standing height and normal held camera");
                    StageRange(new Vector3(5.5f, 0.92f, -0.5f), new Vector3(2.1f, 0.02f, -3), false);
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    Next();
                }
                else if (step == 222 && age > 0.4f)
                {
                    Require(player.GetComponent<PlayerController>().IsGrounded && Mathf.Abs(player.position.y - 0.9f) < 0.03f,
                        "Ground detection works on the existing raised platform, not only y=0");
                    spaceTapReleased = false;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    Next();
                }
                else if (step == 223 && age > 0.06f && !spaceTapReleased)
                {
                    spaceTapReleased = true;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                }
                else if (step == 223 && age > 0.25f)
                {
                    Require(player.position.y > 1.2f && !player.GetComponent<PlayerController>().IsGrounded,
                        "Space also launches from an elevated surface");
                    Next();
                }
                else if (step == 224 && age > 0.9f)
                {
                    Require(player.GetComponent<PlayerController>().IsGrounded && Mathf.Abs(player.position.y - 0.9f) < 0.03f
                        && recorder.LiveTexture && Quaternion.Angle(player.rotation, player.transform.rotation) < 0.1f,
                        "Elevated jump lands back on the platform with a working feed");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    Next();
                }
                else if (step == 225 && age > 0.06f)
                {
                    player.GetComponent<PlayerController>().SendMessage("OnApplicationFocus", false);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Next();
                }
                else if (step == 226 && age > 0.3f)
                {
                    Require(player.GetComponent<PlayerController>().IsGrounded && Mathf.Abs(player.position.y - 0.9f) < 0.03f,
                        "Losing focus cancels a pending Space tap; release cannot jump");
                    Debug.Log("RRM LOCOMOTION CHECK PASSED: standing/moving/elevated Space taps, no air jump, "
                        + "Space hold crouch without a release jump, Ctrl crouch, half speed and recovery, headroom, "
                        + "independent live camera, lowered pickup, restart and focus cancellation.");
                    if (combined)
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                        step = 300;
                        since = Time.time;
                    }
                    else
                    {
                        Debug.Log("RRM LOCOMOTION PLAY CHECK PASSED; checking saved starter cover next.");
                        step = 500;
                        since = Time.time;
                    }
                }
                else if (step == 300 && age > 0.7f)
                {
                    player = GameObject.Find("Player").GetComponent<Rigidbody>();
                    playerHealth = player.GetComponent<Damageable>();
                    dummy = GameObject.Find("Dummy").GetComponent<Damageable>();
                    dummyBody = dummy.GetComponent<Rigidbody>();
                    recorder = Object.FindFirstObjectByType<CameraRecorder>();
                    camera = Camera.main;
                    enemyHits = damageEvents = 0;
                    dummy.Damaged -= ObserveHit;
                    dummy.Damaged += ObserveHit;
                    playerHealth.Damaged += ObserveEnemyHit;
                    Require(playerHealth.Health == 90 && dummy.Health == 90
                        && dummy.GetComponent<PlayerController>().opponent == playerHealth
                        && dummy.GetComponent<PlayerController>().State == PlayerController.EnemyState.Idle,
                        "Existing scene starts with a distant idle enemy and two healthy actors");
                    Require(Array.IndexOf(recorder.subjects, playerHealth) >= 0 && Array.IndexOf(recorder.subjects, dummy) >= 0
                        && dummy.GetComponent<MeleeAttack>().weaponPivot.GetComponentInChildren<Hurtbox>().part == BodyPart.RightArm,
                        "Recorder observes both actors; enemy strikes with its existing arm");
                    StageRange(new Vector3(-1, 0.02f, 1), new Vector3(1, 0.02f, 1));
                    dummy.GetComponent<PlayerController>().enabled = true;
                    beforeDummy = dummyBody.position;
                    Next();
                }
                else if (step == 301 && age > 1f)
                {
                    Require(dummy.GetComponent<PlayerController>().State == PlayerController.EnemyState.Idle
                        && Vector3.Distance(beforeDummy, dummyBody.position) < 0.05f && enemyHits == 0,
                        "Solid divider prevents detection, approach and attacks");
                    StageRange(new Vector3(2.1f, 0.02f, -4), new Vector3(2.1f, 0.02f, 0));
                    dummy.GetComponent<PlayerController>().enabled = true;
                    beforeDummy = dummyBody.position;
                    Next();
                }
                else if (step == 302 && age > 1f)
                {
                    Require(dummy.GetComponent<PlayerController>().State == PlayerController.EnemyState.Approach
                        && Vector3.Distance(beforeDummy, dummyBody.position) > 0.7f
                        && dummyBody.linearVelocity.magnitude < 1.5f && enemyHits == 0,
                        "Visible nearby enemy approaches slowly with the existing Rigidbody");
                    ReadLiveFrame("enemy-approach-feed.png");
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q, Key.W));
                    Next();
                }
                else if (step == 303 && (dummy.GetComponent<MeleeAttack>().Phase == MeleeAttack.AttackPhase.Windup || age > 4f))
                {
                    Require(dummy.GetComponent<MeleeAttack>().Phase == MeleeAttack.AttackPhase.Windup && enemyHits == 0,
                        "Enemy commits to a windup before it can hurt the player");
                    placedDevice = recorder.GetComponent<CameraDevice>();
                    deviceId = placedDevice.Id;
                    Require(placedDevice.State == CameraDeviceState.Placed && recorder.IsRecording && !recorder.LiveTexture,
                        "Q places one camera and hides live feed while logical recording continues");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    enemyFacing = dummyBody.rotation;
                    beforePlayer = player.position;
                    Next();
                }
                else if (step == 304 && age > 0.12f)
                {
                    var feedback = dummy.GetComponent<HitFeedback>();
                    var properties = new MaterialPropertyBlock();
                    feedback.bodyRenderers[0].GetPropertyBlock(properties);
                    Require(properties.GetColor("_BaseColor").g > 0.6f && enemyHits == 0,
                        "Windup is visibly amber, not an invisible timer");
                    Capture("enemy-windup.png", 1280, 800);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                    Next();
                }
                else if (step == 305 && (dummy.GetComponent<MeleeAttack>().Phase == MeleeAttack.AttackPhase.Recovery || age > 2f))
                {
                    Require(dummy.GetComponent<MeleeAttack>().Phase == MeleeAttack.AttackPhase.Recovery
                        && enemyHits == 0 && playerHealth.Health == 90
                        && Vector3.Distance(beforePlayer, player.position) > 0.7f
                        && Quaternion.Angle(enemyFacing, dummyBody.rotation) < 1f,
                        "S escapes the committed swing; enemy cannot track or damage the retreating player");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    // Arrange counter range without interrupting the enemy's actual recovery timer.
                    player.position = dummyBody.position + Vector3.back * 1.15f;
                    player.rotation = Quaternion.identity;
                    player.transform.SetPositionAndRotation(player.position, player.rotation);
                    player.linearVelocity = Vector3.zero;
                    Physics.SyncTransforms();
                    AimCamera(Vector2.zero, true);
                    Next();
                }
                else if (step == 306 && (damageEvents > 0 || age > 1f))
                {
                    Require(damageEvents == 1 && dummy.Health < 90 && enemyHits == 0
                        && dummy.GetComponent<MeleeAttack>().Phase == MeleeAttack.AttackPhase.Recovery,
                        "A real mouse counterstrike damages the enemy during its recovery");
                    Require(placedDevice.RecordedHits == 1 && placedDevice.Events[0].Damage.Target == dummy,
                        "Placed camera witnesses the player's counterstrike without restoring its feed");
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    Next();
                }
                else if (step == 307 && age > 1f)
                {
                    Require(enemyHits == 0, "Recovery cannot deliver another hit");
                    StageRange(new Vector3(2.1f, 0.02f, -2), new Vector3(2.1f, 0.02f, -1), false);
                    dummy.GetComponent<PlayerController>().enabled = true;
                    beforeLens = recorder.transform.position;
                    beforeLensRotation = recorder.transform.rotation;
                    Next();
                }
                else if (step == 308 && ((enemyHits > 0 && Time.time - enemyHitTime > 0.12f) || age > 3f))
                {
                    Require(enemyHits == 1 && playerHealth.Health > 0 && playerHealth.Health <= 48,
                        $"One real enemy hit is costly but survivable: HP={playerHealth.Health}, hits={enemyHits}");
                    Require(placedDevice.Events[placedDevice.Events.Count - 1].Damage.Target == playerHealth,
                        "The same placed device records damage to the player as well as the Dummy");
                    Capture("enemy-player-hit.png", 1280, 800);
                    Next();
                }
                else if (step == 309 && (playerHealth.IsDead || age > 8f))
                {
                    Require(playerHealth.IsDead && enemyHits <= 3 && recorder.ReportReady && placedDevice.DeathRecorded,
                        $"Standing in range is lethal and the placed camera witnesses death: HP={playerHealth.Health}, hits={enemyHits}");
                    Require(placedDevice.Events[placedDevice.Events.Count - 1].Damage.IsFatal
                        && placedDevice.Events[placedDevice.Events.Count - 1].Damage.Target == playerHealth
                        && placedDevice.RecordedHits == enemyHits + 1 && placedDevice.Id == deviceId
                        && Vector3.Distance(beforeLens, recorder.transform.position) < 0.001f
                        && Quaternion.Angle(beforeLensRotation, recorder.transform.rotation) < 0.1f && !recorder.LiveTexture,
                        "Fatal event retains correct actor/ID, fixed camera pose and private recording");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
                    AimCamera(Vector2.zero, true);
                    Next();
                }
                else if (step == 310 && age > 1.6f)
                {
                    Require(player.GetComponent<PlayerController>().DeathMessage.Contains("YOU DIED")
                        && !player.GetComponent<MeleeAttack>().IsBusy && !player.GetComponent<MeleeAttack>().TryAttack()
                        && !dummy.GetComponent<MeleeAttack>().IsBusy
                        && Vector3.Dot(player.transform.up, Vector3.up) < 0.9f,
                        $"Dead player falls, cannot attack, shows restart text and is no longer targeted: "
                        + $"text={player.GetComponent<PlayerController>().DeathMessage}, enemy={dummy.GetComponent<MeleeAttack>().Phase}, up={player.transform.up}");
                    foreach (Hurtbox zone in player.GetComponentsInChildren<Hurtbox>())
                        Require(!zone.GetComponent<Collider>().enabled, "Dead player's hurtboxes no longer accept contact");
                    Capture("enemy-player-death.png", 1280, 800);
                    if (!Application.isBatchMode)
                        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/enemy-death-ui.png"));
                    Next();
                }
                else if (step == 311 && age > 0.2f)
                {
                    playerHealth.Damaged -= ObserveEnemyHit;
                    recorder.enabled = false;
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                    Next();
                }
                else if (step == 312 && age > 0.8f)
                {
                    var freshPlayer = GameObject.Find("Player").GetComponent<PlayerController>();
                    var freshCamera = Object.FindFirstObjectByType<CameraRecorder>();
                    Require(freshPlayer.GetComponent<Damageable>().Health == 90 && freshPlayer.DeathMessage.Length == 0
                        && GameObject.Find("Dummy").GetComponent<Damageable>().Health == 90
                        && freshCamera.LiveTexture && freshCamera.RecordedHits == 0
                        && Object.FindObjectsByType<CameraDevice>().Length == 1
                        && GameObject.Find("Low Cover") && GameObject.Find("High Cover") && GameObject.Find("Camera Platform"),
                        "R restarts both actors and one held camera, even with its old recorder disabled; both rooms survive");
                    Finish(true, combined ? "RRM COMBINED PLAY CHECK PASSED: hands, melee, rooms, cover, recording, evidence, light, "
                        + "height, jump/crouch, actual feed pixels AND dangerous enemy, dodge, counter, player death and restart."
                        : "RRM ENEMY PLAY CHECK PASSED: idle/occlusion, slow approach, readable windup, real WASD dodge, "
                        + "mouse counter in recovery, real enemy hits/blood/death, placed recording of both actors and restart.");
                }
                else if (step == 40 && age > 0.6f)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    player = GameObject.Find("Player").GetComponent<Rigidbody>();
                    dummy = GameObject.Find("Dummy").GetComponent<Damageable>();
                    dummyBody = dummy.GetComponent<Rigidbody>();
                    recorder = Object.FindFirstObjectByType<CameraRecorder>();
                    camera = Camera.main;
                    dummy.GetComponent<PlayerController>().enabled = false;
                    FrameDummy();
                    dummy.GetComponent<PlayerController>().enabled = true;
                    AimCamera(Vector2.zero);
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/passive-handheld-ui.png"));
                    Next();
                }
                else if (step == 41 && age > 0.4f)
                {
                    beforeLiveFrame = ReadLiveFrame("placed-before-feed.png");
                    RedCenter(beforeLiveFrame);
                    Require(!recorder.IsPlaced && recorder.IsRecording && recorder.transform.IsChildOf(player.transform),
                        "Camera starts active in the player's hand with a live feed");
                    placedDevice = recorder.GetComponent<CameraDevice>();
                    deviceId = placedDevice.Id;
                    Require(Guid.TryParseExact(deviceId, "N", out _) && placedDevice.State == CameraDeviceState.Held
                        && placedDevice.Owner == player.GetComponent<PlayerController>()
                        && placedDevice.Owner.HeldCamera == placedDevice,
                        "Existing handheld is a CameraDevice with an ID and matching holder reference");
                    mountLocalPosition = placedDevice.transform.localPosition;
                    beforeLens = recorder.lens.position;
                    beforeLensRotation = recorder.lens.rotation;
                    placedTexture = recorder.LiveTexture;
                    // F unlocks facing immediately; measure the original angle before pressing it.
                    beforePlayerRotation = player.rotation;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                    Next();
                }
                else if (step == 42 && age > 0.1f)
                {
                    Require(recorder.IsPlaced && recorder.transform.parent == null && !recorder.IsAiming,
                        "F detaches the existing camera and disables handheld aiming");
                    Require(placedDevice.State == CameraDeviceState.Placed && !placedDevice.Owner
                        && !player.GetComponent<PlayerController>().HeldCamera && placedDevice.Id == deviceId,
                        "Placement clears ownership, keeps the same device ID and empties the player's hand");
                    Require(recorder.IsRecording && !recorder.liveCamera.enabled
                        && !recorder.liveCamera.targetTexture && !recorder.LiveTexture && !placedTexture,
                        "Placed recording stays active without a rendering camera or live texture");
                    Require(Vector3.Distance(recorder.lens.position, beforeLens) < 0.001f
                        && Quaternion.Angle(recorder.lens.rotation, beforeLensRotation) < 0.1f,
                        "Placement preserves the exact world viewpoint");
                    beforePlayer = player.transform.position;
                    beforeDummy = dummyBody.position;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.D));
                    AimCamera(new Vector2(1000, 1000));
                    Next();
                }
                else if (step == 43 && age > 0.65f
                    && (Vector3.Distance(dummyBody.position, beforeDummy) > 0.25f || age > 3f))
                {
                    Require(Vector3.Distance(player.transform.position, beforePlayer) > 1f,
                        "Player walks away independently after placing the camera");
                    Require(Quaternion.Angle(player.rotation, beforePlayerRotation) > 45f,
                        $"Holding RMB no longer locks the player's facing after placement: turn {Quaternion.Angle(player.rotation, beforePlayerRotation):F1}");
                    Require(Vector3.Distance(recorder.lens.position, beforeLens) < 0.001f
                        && Quaternion.Angle(recorder.lens.rotation, beforeLensRotation) < 0.1f,
                        "Player movement, turning and mouse deltas cannot move the placed camera");
                    Require(recorder.IsRecording && !recorder.liveCamera.enabled
                        && !recorder.LiveTexture && !recorder.liveCamera.targetTexture,
                        "Placed camera remains logically active without giving the player a live view");
                    Require(dummy.GetComponent<PlayerController>().enabled
                        && Vector3.Distance(dummyBody.position, beforeDummy) > 0.25f, "Dummy keeps wandering while the player leaves");
                    Hurtbox torso = Array.Find(dummy.GetComponentsInChildren<Hurtbox>(), zone => zone.part == BodyPart.Torso);
                    Require(torso && recorder.CanSee(torso.transform.position), "Placed visibility still uses the fixed lens with rendering disabled");
                    int recorded = recorder.RecordedHits;
                    // Test-only damage exercises the real event subscription, not a new recording/scoring system.
                    Require(dummy.ApplyDamage(player.gameObject, torso, torso.transform.position, Vector3.forward, 1f, 0f)
                        && recorder.RecordedHits == recorded + 1, "Placed camera still receives visible DamageEvents privately");
                    recorder.enabled = false;
                    Require(!recorder.IsRecording, "Disabled recorder is not logically recording");
                    dummy.ApplyDamage(player.gameObject, torso, torso.transform.position, Vector3.forward, 1f, 0f);
                    Require(recorder.RecordedHits == recorded + 1, "Disabled recorder unsubscribes from damage");
                    recorder.enabled = true;
                    Require(recorder.IsRecording && !recorder.liveCamera.enabled && !recorder.LiveTexture,
                        "Re-enabling placed recording does not restore remote video");
                    dummy.ApplyDamage(player.gameObject, torso, torso.transform.position, Vector3.forward, 1f, 0f);
                    Require(recorder.RecordedHits == recorded + 2, "Re-enabled recorder subscribes exactly once");
                    placedRecordings = recorder.RecordedHits;
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/passive-recording-ui.png"));
                    Debug.Log("RRM PASSIVE CHECK: F hides feed, releases texture and stops rendering; fixed recorder receives damage privately while player and Dummy move.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                    Next();
                }
                else if (step == 44 && age > 0.2f)
                {
                    Require(recorder.IsPlaced && recorder.transform.parent == null
                        && Object.FindObjectsByType<CameraRecorder>(FindObjectsSortMode.None).Length == 1
                        && recorder.IsRecording && !recorder.LiveTexture && !recorder.liveCamera.enabled,
                        "F from too far away cannot pick up the device or restore remote video");
                    if (!Application.isBatchMode)
                        Require(File.Exists(Path.GetFullPath("Verification/passive-handheld-ui.png"))
                            && File.Exists(Path.GetFullPath("Verification/passive-recording-ui.png")), "Handheld and passive screenshots exist");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
                    Next();
                }
                else if (step == 45 && age > 0.2f)
                {
                    Require(recorder.ControlsVisible && recorder.IsPlaced && recorder.IsRecording
                        && !recorder.LiveTexture && !recorder.liveCamera.enabled, "F1 opens controls without restoring remote video");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/controls-placed-ui.png"));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Next();
                }
                else if (step == 46 && age > 0.2f)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
                    Next();
                }
                else if (step == 47 && age > 0.2f)
                {
                    Require(!recorder.ControlsVisible && recorder.IsRecording && !recorder.LiveTexture,
                        "F1 closes controls and leaves placed recording passive");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    step = 52;
                    since = Time.time;
                }
                else if (step == 48 && age > 0.6f)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    recorder = Object.FindFirstObjectByType<CameraRecorder>();
                    Require(!recorder.IsPlaced && recorder.IsRecording && recorder.RecordedHits == 0
                        && recorder.transform.IsChildOf(GameObject.Find("Player").transform)
                        && Object.FindObjectsByType<CameraRecorder>(FindObjectsSortMode.None).Length == 1 && !placedTexture,
                        "Restart removes the placed camera, releases its texture and restores one handheld camera");
                    Require(!placedDevice && recorder.GetComponent<CameraDevice>().Id != deviceId
                        && Object.FindObjectsByType<CameraDevice>(FindObjectsSortMode.None).Length == 1,
                        "Restart creates one new device instance instead of keeping an orphan or reusing its ID");
                    ReadLiveFrame("placed-restart-feed.png");
                    Require(!recorder.ControlsVisible, "Restart begins with controls closed");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
                    Next();
                }
                else if (step == 49 && age > 0.2f)
                {
                    Require(recorder.ControlsVisible && !recorder.IsPlaced && recorder.IsRecording
                        && recorder.LiveTexture && recorder.liveCamera.enabled, "F1 also opens controls while handheld");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/controls-handheld-ui.png"));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Next();
                }
                else if (step == 50 && age > 0.2f)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
                    Next();
                }
                else if (step == 51 && age > 0.2f)
                {
                    Require(!recorder.ControlsVisible && recorder.LiveTexture && recorder.liveCamera.enabled,
                        "Closing controls restores the normal handheld display");
                    if (!Application.isBatchMode)
                        Require(File.Exists(Path.GetFullPath("Verification/controls-handheld-ui.png"))
                            && File.Exists(Path.GetFullPath("Verification/controls-placed-ui.png")), "Both controls screenshots exist");
                    step = 55;
                    since = Time.time;
                }
                else if (step == 52)
                {
                    Vector3 offset = beforePlayer - player.transform.position;
                    offset.y = 0f;
                    if (offset.magnitude > 0.2f)
                    {
                        var keys = new List<Key>();
                        if (offset.x > 0.1f) keys.Add(Key.D);
                        if (offset.x < -0.1f) keys.Add(Key.A);
                        if (offset.z > 0.1f) keys.Add(Key.W);
                        if (offset.z < -0.1f) keys.Add(Key.S);
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
                    }
                    else
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                        Next();
                    }
                }
                else if (step == 53 && age > 0.3f)
                {
                    Require(placedDevice == recorder.GetComponent<CameraDevice>() && placedDevice.Id == deviceId
                        && placedDevice.Owner == player.GetComponent<PlayerController>()
                        && placedDevice.State == CameraDeviceState.Held && placedDevice.Owner.HeldCamera == placedDevice,
                        "Walking back and pressing F picks up the same device and assigns its owner again");
                    Require(recorder.RecordedHits == placedRecordings && recorder.IsRecording
                        && recorder.LiveTexture && recorder.liveCamera.enabled,
                        "Pickup preserves recorded events and restores the same recorder's live feed");
                    Require(Vector3.Distance(placedDevice.transform.localPosition, mountLocalPosition) < 0.001f,
                        "Pickup restores the existing hand mount");
                    ReadLiveFrame("device-pickup-feed.png");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/device-pickup-ui.png"));
                    beforePlayer = player.transform.position;
                    beforeLens = recorder.lens.position;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                    Next();
                }
                else if (step == 54 && age > 0.5f)
                {
                    Require(Vector3.Distance(player.transform.position, beforePlayer) > 0.5f
                        && Vector3.Distance(recorder.lens.position - beforeLens, player.transform.position - beforePlayer) < 0.05f,
                        "After pickup the camera follows the moving holder again");
                    CheckCameraOwnershipTransfer();
                    Debug.Log("RRM DEVICE CHECK: same ID through F place/pickup, owner cleared/reassigned, transfer to another holder, wall/distance guards and live feed restored.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                    step = 48;
                    since = Time.time;
                }
                else if ((step == 55 || step == 59 || step == 63 || step == 67) && age > 0.6f)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    player = GameObject.Find("Player").GetComponent<Rigidbody>();
                    dummy = GameObject.Find("Dummy").GetComponent<Damageable>();
                    dummyBody = dummy.GetComponent<Rigidbody>();
                    recorder = Object.FindFirstObjectByType<CameraRecorder>();
                    camera = Camera.main;
                    dummy.GetComponent<PlayerController>().enabled = false;
                    FrameDummy();
                    AimCamera(Vector2.zero);
                    Require(!recorder.ReportReady && recorder.RecordedHits == 0
                        && recorder.GetComponent<CameraDevice>().Events.Count == 0,
                        "New round starts without a report or retained evidence");
                    Next();
                }
                else if ((step == 56 || step == 60 || step == 64 || step == 68) && age > 0.3f)
                {
                    var device = recorder.GetComponent<CameraDevice>();
                    Hurtbox torso = Array.Find(dummy.GetComponentsInChildren<Hurtbox>(), zone => zone.part == BodyPart.Torso);
                    Vector3 point = torso.transform.position;
                    recorder.SetViewRotation(Quaternion.LookRotation(point - recorder.transform.position));
                    Physics.SyncTransforms();
                    Require(recorder.CanSee(point), "Report setup frames the damage point");
                    bool seen = step == 56 || step == 68;
                    bool placed = step >= 64;
                    float distance = Vector3.Distance(recorder.lens.position, point);
                    if (!placed)
                    {
                        Require(dummy.ApplyDamage(player.gameObject, torso, point, Vector3.forward, 1, 0)
                            && device.Events.Count == 1 && !recorder.ReportReady, "Visible nonfatal hit is retained without an early report");
                        device.enabled = false;
                        dummy.ApplyDamage(player.gameObject, torso, point, Vector3.forward, 1, 0);
                        Require(device.Events.Count == 1, "An inactive CameraDevice cannot witness damage");
                        device.enabled = true;
                    }
                    if (!seen) recorder.SetViewRotation(Quaternion.Euler(0, 180, 0) * recorder.transform.rotation);
                    if (placed) Require(device.TryPlace(player.GetComponent<PlayerController>()), "Report camera is placed before the fatal event");
                    Require(recorder.CanSee(point) == seen, "Fatal visibility matches the report case");
                    Require(dummy.ApplyDamage(player.gameObject, torso, point, Vector3.forward, dummy.Health, 0),
                        "Existing Damageable delivers the fatal event to subscribed recorders");
                    int hits = (placed ? 0 : 1) + (seen ? 1 : 0);
                    Require(recorder.ReportReady && device.RecordedHits == hits && device.Events.Count == hits
                        && device.DeathRecorded == seen && recorder.ReportText.Contains("Death recorded: " + (seen ? "YES" : "NO")),
                        "Report counts only witnessed DamageEvents and does not infer a recorded death from target health");
                    Require(recorder.ReportText.Contains(device.Id)
                        && recorder.ReportText.Contains("Camera owner: " + (placed ? "NONE" : "Player")), "Report identifies this device and its current owner");
                    if (hits > 0)
                        Require(device.LastEventDistance.HasValue && Mathf.Abs(device.LastEventDistance.Value - distance) < 0.001f,
                            "Report distance refers to the last witnessed event, even if the death was missed");
                    else Require(!device.LastEventDistance.HasValue && recorder.ReportText.Contains("N/A"), "No evidence has no invented distance");
                    if (placed) Require(!recorder.LiveTexture && !recorder.liveCamera.enabled, "End-of-round report does not restore placed live video");
                    Next();
                }
                else if ((step == 57 || step == 61 || step == 65 || step == 69) && age > 0.2f)
                {
                    string name = step == 57 ? "report-held-yes" : step == 61 ? "report-held-no"
                        : step == 65 ? "report-placed-no" : "report-placed-yes";
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/" + name + ".png"));
                    Debug.Log("RRM REPORT CHECK: " + name + "\n" + recorder.ReportText);
                    Next();
                }
                else if ((step == 58 || step == 62 || step == 66 || step == 70) && age > 0.3f)
                {
                    if (step == 70)
                        Finish(true, "RRM CAMERA REPORT PLAY CHECK PASSED: held/placed YES/NO, separate device evidence, stored distances, inactive rejection and restart.");
                    else
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                        Next();
                    }
                }
                else if (step == 120 && age > 0.4f)
                {
                    leftHandClicks = rightHandClicks = 0;
                    leftHandHolding = rightHandHolding = false;
                    handInputFrame = -1;
                    InputSystem.onAfterUpdate += CountHandClicks;
                    initialHandheldRotation = recorder.transform.localRotation;
                    StageRange(new Vector3(-6, 0.02f, -4), new Vector3(-7.2f, 0.02f, -4));
                    player.rotation = Quaternion.Euler(0, 90, 0);
                    player.transform.rotation = player.rotation;
                    recorder.SetViewRotation(Quaternion.Euler(8, 90, 0));
                    mountLocalPosition = recorder.transform.localPosition;
                    placedTexture = recorder.LiveTexture;
                    Next();
                }
                else if (step == 121 && age > 0.3f)
                {
                    var controller = player.GetComponent<PlayerController>();
                    Require(controller.RightHand.Item?.Source == player.GetComponent<MeleeAttack>()
                        && controller.LeftHand.State == HandState.HoldingItem
                        && controller.LeftHand.Item.Source == recorder.GetComponent<CameraDevice>()
                        && controller.HandHolding(controller.HeldCamera) == controller.LeftHand,
                        "The camera starts in the left hand; the existing weapon occupies the right hand");
                    CaptureHandLayout(HandSide.Left);
                    Require(leftHandClicks == 1 && rightHandClicks == 0
                        && leftHandHolding && !rightHandHolding,
                        $"LMB click/hold: right {rightHandClicks}/{rightHandHolding}, left {leftHandClicks}/{leftHandHolding}");
                    controller.enabled = false;
                    Require(!controller.LeftHand.IsHoldActive && !controller.LeftHand.WasClickedThisFrame
                        && !recorder.IsAiming, "Disabled holder cannot issue hand actions or aim the camera");
                    controller.enabled = true;
                    beforePlayerRotation = player.rotation;
                    beforeLiveFrame = ReadLiveFrame("controls-forward-feed.png");
                    AimCamera(new Vector2(180f / recorder.mouseSensitivity, 0));
                    Next();
                }
                else if (step == 122 && age > 0.2f)
                {
                    Require(Quaternion.Angle(player.rotation, beforePlayerRotation) < 0.1f,
                        "LMB turns only the left-hand camera, never the idle character");
                    Require(Vector3.Dot(player.transform.forward,
                        Vector3.ProjectOnPlane(recorder.lens.forward, Vector3.up).normalized) < -0.99f,
                        "Mouse drag can aim the handheld fully behind the body");
                    var backward = ReadLiveFrame("controls-backward-feed.png");
                    RedCenter(backward);
                    Require(ChangedPixels(beforeLiveFrame, backward) > 3000
                        && recorder.CanSee(dummyBody.position + Vector3.up * 1.05f),
                        "Turning backward changes the real feed and reveals the target behind Player");
                    beforeLiveFrame = backward;
                    beforePlayer = player.transform.position;
                    beforeMount = recorder.transform.position;
                    beforeLensRotation = recorder.transform.rotation;
                    AimCamera(Vector2.zero);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    Next();
                }
                else if (step == 123 && age > 0.6f)
                {
                    Require(leftHandClicks == 1 && rightHandClicks == 0 && !player.GetComponent<MeleeAttack>().IsBusy,
                        "A long camera hold never repeats its click action");
                    Vector3 travel = player.transform.position - beforePlayer;
                    Require(travel.x > 0.8f && Mathf.Abs(travel.z) < 0.05f,
                        $"W moves along the east-facing body, not world north or the backward camera: {travel}");
                    Require(Quaternion.Angle(player.rotation, beforePlayerRotation) < 0.1f
                        && Quaternion.Angle(recorder.transform.rotation, beforeLensRotation) < 0.1f
                        && Vector3.Distance(recorder.transform.position - beforeMount, travel) < 0.02f,
                        "Body advances while the backward lens follows position without changing direction");
                    Require(ChangedPixels(beforeLiveFrame, ReadLiveFrame("controls-walking-backward-feed.png")) > 1000,
                        "The same live feed continues updating during body-relative movement");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/controls-backward-walk-ui.png"));
                    beforePlayer = player.transform.position;
                    InputSystem.QueueStateEvent(mouse, new MouseState
                        { position = camera.WorldToScreenPoint(player.position + Vector3.right * 5f) });
                    Next();
                }
                else if (step == 124 && age > 0.3f)
                {
                    Require(!leftHandHolding
                        && leftHandClicks == 1, "Releasing LMB ends hold without issuing another click");
                    Require(!recorder.IsAiming && player.transform.position.x - beforePlayer.x > 0.4f
                        && Quaternion.Angle(recorder.transform.rotation, beforeLensRotation) < 0.1f
                        && Quaternion.Angle(player.rotation, beforePlayerRotation) < 0.1f,
                        "Releasing the camera hand changes neither camera direction nor W movement");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    AimCamera(Vector2.zero);
                    Next();
                }
                else if (step == 125 && age > 0.2f)
                {
                    AimCamera(new Vector2(0, -100000));
                    Next();
                }
                else if (step == 126 && age > 0.2f)
                {
                    Require(Mathf.Abs(Mathf.DeltaAngle(recorder.transform.eulerAngles.x, recorder.maxPitch)) < 0.1f,
                        "Body-relative camera still limits downward pitch");
                    AimCamera(new Vector2(0, 100000));
                    Next();
                }
                else if (step == 127 && age > 0.2f)
                {
                    Require(Mathf.Abs(Mathf.DeltaAngle(recorder.transform.eulerAngles.x, recorder.minPitch)) < 0.1f,
                        "Body-relative camera still limits upward pitch");
                    step = 128;
                    since = Time.time;
                }
                else if (step == 128 && age > 0.2f)
                {
                    StageRange(new Vector3(-4.8f, 0.02f, -3.9f), new Vector3(-7.2f, 0.02f, -4));
                    player.rotation = Quaternion.identity;
                    player.transform.rotation = player.rotation;
                    recorder.SetViewRotation(Quaternion.Euler(8, 180, 0));
                    beforeLiveFrame = ReadLiveFrame("controls-rear-north-feed.png");
                    AimAt(player.position + Vector3.right * 4f);
                    Next();
                }
                else if (step == 129 && age > 0.6f)
                {
                    Require(Vector3.Dot(player.transform.forward, Vector3.right) > 0.99f
                        && !recorder.IsAiming, "Free mouse turns the idle body from north to east");
                    Require(Vector3.Dot(Vector3.ProjectOnPlane(recorder.lens.forward, Vector3.up).normalized,
                        Vector3.left) > 0.99f && Mathf.Abs(Mathf.DeltaAngle(recorder.transform.localEulerAngles.y, 180f)) < 0.1f,
                        "A backward camera turns with the body and now looks west, still behind Player");
                    Require(ChangedPixels(beforeLiveFrame, ReadLiveFrame("controls-rear-east-feed.png")) > 1000,
                        "Body turning changes the real rear-facing live image");
                    beforePlayer = player.position;
                    beforeLensRotation = recorder.transform.rotation;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    step = 1291;
                    since = Time.time;
                }
                else if (step == 1291 && age > 0.6f)
                {
                    Vector3 travel = player.position - beforePlayer;
                    Require(travel.x > 0.8f && Mathf.Abs(travel.z) < 0.06f
                        && Quaternion.Angle(recorder.transform.rotation, beforeLensRotation) < 1f,
                        "After a mouse turn, W moves east while the released camera keeps looking west");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    movementCase = 0;
                    step = 130;
                    since = Time.time;
                }
                else if (step == 130 && age > 0.2f)
                {
                    StageRange(new Vector3(-4.8f, 0.02f, -3.9f), new Vector3(-7.2f, 0.02f, 4));
                    player.rotation = Quaternion.Euler(0, 90, 0);
                    player.transform.rotation = player.rotation;
                    recorder.SetViewRotation(Quaternion.Euler(8, 270, 0));
                    Vector2 input = BodyMoves[movementCase].input;
                    expectedMovement = player.rotation * new Vector3(input.x, 0, input.y);
                    Next();
                }
                else if (step == 131 && age > 0.25f)
                {
                    beforePlayer = player.position;
                    beforePlayerRotation = player.rotation;
                    beforeLensRotation = recorder.transform.rotation;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(BodyMoves[movementCase].keys));
                    Next();
                }
                else if (step == 132 && age > 0.65f)
                {
                    Vector3 travel = Vector3.ProjectOnPlane(player.position - beforePlayer, Vector3.up);
                    float forward = Vector3.Dot(travel, expectedMovement);
                    Require(forward > 0.5f && (travel - expectedMovement * forward).magnitude < 0.08f,
                        $"Body-relative {BodyMoves[movementCase].keys[0]} movement: {travel}, expected {expectedMovement}");
                    Require(Quaternion.Angle(player.rotation, beforePlayerRotation) < 0.1f
                        && Quaternion.Angle(recorder.transform.rotation, beforeLensRotation) < 0.1f
                        && Vector3.Distance(recorder.transform.localPosition, mountLocalPosition) < 0.001f,
                        "Backpedal, strafe and diagonal input do not rotate the body or the held camera");
                    beforePlayerRotation = player.rotation;
                    Next();
                }
                else if (step == 133 && age > 0.3f)
                {
                    Require(Quaternion.Angle(player.rotation, beforePlayerRotation) < 0.5f
                        && Quaternion.Angle(recorder.transform.rotation, beforeLensRotation) < 0.1f,
                        $"Holding {BodyMoves[movementCase].keys[0]} does not auto-turn: body {Quaternion.Angle(player.rotation, beforePlayerRotation):F3}, camera {Quaternion.Angle(recorder.transform.rotation, beforeLensRotation):F3}");
                    Debug.Log("RRM BODY CONTROL: " + BodyMoves[movementCase].keys[0] + ", direction " + expectedMovement
                        + ", no keyboard auto-turn, camera angle retained.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    movementCase++;
                    step = movementCase < BodyMoves.Length ? 130 : 134;
                    since = Time.time;
                }
                else if (step == 134 && age > 0.3f)
                {
                    Require(recorder.LiveTexture == placedTexture, "Body and camera turning reuse the same live texture");
                    placedDevice = recorder.GetComponent<CameraDevice>();
                    deviceId = placedDevice.Id;
                    beforeMount = recorder.transform.position;
                    beforeLensRotation = recorder.transform.rotation;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                    Next();
                }
                else if (step == 135 && age > 0.2f)
                {
                    var controller = player.GetComponent<PlayerController>();
                    Require(controller.RightHand.Item?.Source == player.GetComponent<MeleeAttack>()
                        && controller.LeftHand.State == HandState.Empty && !controller.HeldCamera
                        && controller.HandHolding(placedDevice) == null,
                        "Q clears the left hand without changing the device ID or creating an item");
                    Require(recorder.IsPlaced && recorder.IsRecording && !recorder.LiveTexture
                        && Vector3.Distance(recorder.transform.position, beforeMount) < 0.001f
                        && Quaternion.Angle(recorder.transform.rotation, beforeLensRotation) < 0.1f,
                        "Placing the independently aimed camera keeps its exact pose and passive recording");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    AimCamera(new Vector2(1000, 1000));
                    Next();
                }
                else if (step == 136 && age > 0.2f)
                {
                    Require(leftHandHolding && !recorder.IsAiming
                        && Quaternion.Angle(recorder.transform.rotation, beforeLensRotation) < 0.1f,
                        "Empty left-hand hold cannot steer the placed camera");
                    CheckRejectedHandPickup();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    Next();
                }
                else if (step == 137 && age > 0.3f)
                {
                    Require(!recorder.IsPlaced && recorder.LiveTexture && placedDevice.Id == deviceId
                        && Quaternion.Angle(recorder.transform.rotation, beforeLensRotation) < 0.1f,
                        "Pickup restores the same camera and retains its independently chosen angle");
                    Require(player.GetComponent<PlayerController>().RightHand.Item.Source == placedDevice,
                        "Pickup restores the camera's right-hand item context");
                    CaptureHandLayout(HandSide.Right);
                    StageRange(new Vector3(2.1f, 0.02f, -2), new Vector3(2.1f, 0.02f, -0.65f), false);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    step = 140;
                    since = Time.time;
                }
                else if (step == 140 && age > 0.2f)
                {
                    handClickBaseline = leftHandClicks;
                    beforeLensRotation = recorder.transform.rotation;
                    InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1, delta = new Vector2(1000, 1000) });
                    Next();
                }
                else if (step == 141 && age > 0.3f)
                {
                    var controller = player.GetComponent<PlayerController>();
                    Require(leftHandClicks == handClickBaseline + 1 && leftHandHolding
                        && !rightHandHolding && controller.LeftHand.Item?.Source == player.GetComponent<MeleeAttack>()
                        && !recorder.IsAiming && player.GetComponent<MeleeAttack>().IsBusy
                        && Quaternion.Angle(recorder.transform.rotation, beforeLensRotation) < 0.1f,
                        "LMB strikes with the transferred weapon without aiming the right-hand camera");
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    Next();
                }
                else if (step == 142 && age > 0.8f)
                {
                    Require(!leftHandHolding && leftHandClicks == handClickBaseline + 1,
                        "Left-hand release ends its hold without repeating click");
                    Require(damageEvents == 1 && dummy.Health < 90 && dummy.GetComponent<BloodEvidence>().Marks.Count == 1
                        && recorder.RecordedHits == 1, "Transferred left-hand weapon hits, emits blood and records one real event");
                    Debug.Log("RRM HAND TRANSFER: left-hand mouse hit produced damage, blood and a right-camera recording.");
                    beforeLiveFrame = ReadLiveFrame("hands-right-before-feed.png");
                    rightHandClicks = 0;
                    AimCamera(Vector2.zero);
                    Next();
                }
                else if (step == 143 && age > 0.2f)
                {
                    AimCamera(new Vector2(60f / recorder.mouseSensitivity, 0));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                    Next();
                }
                else if (step == 144 && age > 0.3f)
                {
                    var controller = player.GetComponent<PlayerController>();
                    Require(rightHandHolding && rightHandClicks == 1 && !player.GetComponent<MeleeAttack>().IsBusy
                        && Mathf.Abs(Quaternion.Angle(recorder.transform.rotation, beforeLensRotation) - 60f) < 0.1f
                        && ChangedPixels(beforeLiveFrame, ReadLiveFrame("hands-right-aim-feed.png")) > 1000,
                        "RMB aims the same camera in the right hand, changes real feed, and never attacks");
                    Require(controller.RightHand.Item.Source == placedDevice
                        && controller.LeftHand.Item?.Source == player.GetComponent<MeleeAttack>(),
                        "Q cannot steal or duplicate the camera already held by the other hand");
                    beforePlayer = player.position;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow, Key.F));
                    Next();
                }
                else if (step == 145 && age > 0.4f)
                {
                    Require(Vector3.ProjectOnPlane(player.position - beforePlayer, Vector3.up).magnitude < 0.02f
                        && !recorder.IsPlaced, "Arrow input does not move Player, and legacy F does not place the camera");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    Next();
                }
                else if (step == 146 && age > 0.2f)
                {
                    Require(recorder.IsPlaced && recorder.IsRecording && !recorder.LiveTexture
                        && !placedDevice.Owner && player.GetComponent<PlayerController>().RightHand.State == HandState.Empty
                        && placedDevice.Id == deviceId && !player.GetComponent<MeleeAttack>().IsBusy,
                        "E releases the right-hand camera; held RMB does not become a fresh attack click");
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Next();
                }
                else if (step == 147 && age > 0.2f)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q, Key.E));
                    Next();
                }
                else if (step == 148 && age > 0.2f)
                {
                    var controller = player.GetComponent<PlayerController>();
                    Require(controller.LeftHand.Item?.Source == placedDevice
                        && controller.RightHand.Item?.Source == player.GetComponent<MeleeAttack>()
                        && placedDevice.Owner == controller && placedDevice.Id == deviceId && recorder.LiveTexture
                        && Object.FindObjectsByType<CameraDevice>(FindObjectsSortMode.None).Length == 1,
                        "Simultaneous Q/E deterministically picks up once into the left hand, preserving ID/owner");
                    CaptureHandLayout(HandSide.Left);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
                    Next();
                }
                else if (step == 149 && age > 0.2f)
                {
                    Require(recorder.ControlsVisible, "F1 opens the updated hand controls guide");
                    if (!Application.isBatchMode)
                        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/hands-controls-f1.png"));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Next();
                }
                else if (step == 150 && age > 0.2f)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
                    Next();
                }
                else if (step == 151 && age > 0.2f)
                {
                    Require(!recorder.ControlsVisible, "F1 closes the guide without affecting the camera");
                    Debug.Log("RRM HANDS CHECK PASSED: Q/E placement and pickup, same ID, both camera hands, click/hold, "
                        + "visible weapon transfer both ways, left-hand melee, failed-pickup rollback, no camera-hand attack, "
                        + "no arrows/F, simultaneous keys, F1 and passive recording.");
                    InputSystem.onAfterUpdate -= CountHandClicks;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                    step = 138;
                    since = Time.time;
                }
                else if (step == 138 && age > 0.6f)
                {
                    player = GameObject.Find("Player").GetComponent<Rigidbody>();
                    dummy = GameObject.Find("Dummy").GetComponent<Damageable>();
                    dummyBody = dummy.GetComponent<Rigidbody>();
                    recorder = Object.FindFirstObjectByType<CameraRecorder>();
                    camera = Camera.main;
                    Require(recorder.LiveTexture && !recorder.IsPlaced
                        && Quaternion.Angle(recorder.transform.localRotation, initialHandheldRotation) < 0.1f,
                        "Restart restores the authored camera angle relative to the body");
                    Require(player.GetComponent<PlayerController>().LeftHand.Item.Source == recorder.GetComponent<CameraDevice>()
                        && player.GetComponent<PlayerController>().RightHand.Item?.Source == player.GetComponent<MeleeAttack>(),
                        "Restart restores fresh hands around the new camera device");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    if (controlsOnly)
                        Finish(true, "RRM CONTROLS PLAY CHECK PASSED: rear-facing real feed, body-relative WASD/diagonal, "
                            + "mouse-only body turn, rear view follows body, hand controls, pitch limits, place/pickup and restart.");
                    else
                    {
                        damageEvents = 0;
                        dummy.Damaged += ObserveHit;
                        step = 80;
                        since = Time.time;
                    }
                }
                else if (step == 100 && age > 0.4f)
                {
                    Require(dummy.GetComponent<BloodEvidence>(), "CombatPrototype has its evidence emitter");
                    Require(Object.FindObjectsByType<LightSource>().Length == 8 && !GameObject.Find("Area Floor Preview"),
                        "Eight logical sources form the test layout without duplication");
                    CheckLiveLightHud();
                    StageRange(new Vector3(2.1f, 0.02f, -2), new Vector3(2.1f, 0.02f, -0.65f));
                    Next();
                }
                else if (step == 101 && age > 0.3f)
                {
                    CheckHudPlacement();
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/light-quality-hud.png"));
                    AimCamera(Vector2.zero, true);
                    Next();
                }
                else if (step == 102 && age > 1.3f)
                {
                    BloodEvidence evidence = dummy.GetComponent<BloodEvidence>();
                    Require(damageEvents == 1 && dummy.Health < 90 && evidence.Marks.Count == 1,
                        "Real mouse melee produces one DamageEvent and one persistent mark");
                    Require(player.GetComponent<PlayerController>().LeftHand.Item.Source == recorder.GetComponent<CameraDevice>()
                        && recorder.RecordedHits == 1, "Left-hand filming and one right-weapon-hand click record the real hit together");
                    evidenceMark = evidence.Marks[0];
                    Require(evidenceMark.Mark && !evidenceMark.Mark.GetComponent<Collider>()
                        && Mathf.Abs(evidenceMark.Position.y - 0.015f) < 0.03f, "Floor evidence is offset above the surface without collision");
                    Require(recorder.GetComponent<CameraDevice>().BloodEvents.Count == 1, "Each damage-created mark reaches this device once");
                    Vector3 floor = evidenceMark.Position;
                    StageRange(new Vector3(floor.x, 0.02f, floor.z - 2), new Vector3(floor.x, 0.02f, floor.z + 3));
                    recorder.SetViewRotation(Quaternion.LookRotation(evidenceMark.Position - recorder.transform.position));
                    Next();
                }
                else if (step == 103 && age > 0.3f)
                {
                    Require(recorder.InspectBlood(evidenceMark).Visible, "Handheld can inspect the surviving floor mark");
                    evidenceFrame = ReadLiveFrame("evidence-floor-feed.png");
                    evidenceMark.Mark.GetComponent<Renderer>().enabled = false;
                    Next();
                }
                else if (step == 104 && age > 0.2f)
                {
                    Require(ChangedPixels(evidenceFrame, ReadLiveFrame("evidence-floor-without-mark.png")) > 20,
                        "Persistent blood contributes pixels to the real live feed after the burst has ended");
                    evidenceMark.Mark.GetComponent<Renderer>().enabled = true;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                    Next();
                }
                else if (step == 105 && age > 0.3f)
                {
                    Require(recorder.IsPlaced && recorder.IsRecording && !recorder.LiveTexture
                        && recorder.InspectBlood(evidenceMark).Visible, "Placed camera still sees persistent evidence without remote video");
                    recorder.transform.Rotate(0, 180, 0, Space.World);
                    Require(!recorder.InspectBlood(evidenceMark).Visible, "Turning away changes current blood visibility to NO");
                    CheckEvidenceLight(new Vector3(2.8f, 1.05f, 0.4f), VisibilityLevel.Normal);
                    CheckEvidenceLight(new Vector3(2.8f, 1.05f, -1.8f), VisibilityLevel.Low);
                    CheckEvidenceLight(new Vector3(1f, 1.05f, -4.5f), VisibilityLevel.Dark);
                    var events = recorder.GetComponent<CameraDevice>().Events;
                    Require(events[events.Count - 3].Clarity > events[events.Count - 2].Clarity
                        && events[events.Count - 2].Clarity > events[events.Count - 1].Clarity,
                        "Normal, low and dark retain progressively lower clarity on the device");
                    float wallX = GameObject.Find("East Wall").GetComponent<Collider>().bounds.min.x;
                    CheckEvidenceLight(new Vector3(wallX - 0.525f, 1.05f, 0));
                    var marks = dummy.GetComponent<BloodEvidence>().Marks;
                    BloodEvent wall = marks[marks.Count - 1];
                    Require(Mathf.Abs(wall.Position.x - (wallX - 0.015f)) < 0.03f && wall.Position.y > 0.9f,
                        "Nearby east wall receives evidence at impact height instead of on the floor");
                    recorder.transform.position = wall.Position + Vector3.right * 2;
                    recorder.transform.LookAt(wall.Position);
                    Require(!recorder.InspectBlood(wall).Visible, "The wall blocks evidence when the camera is on its opposite side");
                    dummyBody.position += Vector3.back * 3;
                    dummyBody.transform.position = dummyBody.position;
                    Vector3 oldPosition = camera.transform.position;
                    Quaternion oldRotation = camera.transform.rotation;
                    float oldSize = camera.orthographicSize;
                    try
                    {
                        camera.transform.position = wall.Position + Vector3.left * 2;
                        camera.transform.LookAt(wall.Position);
                        camera.orthographicSize = 0.4f;
                        Require(Array.FindAll(Capture("evidence-wall.png", 640, 480), IsDummyRed).Length > 100,
                            "Wall evidence is rendered on the exposed face");
                    }
                    finally
                    {
                        camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
                        camera.orthographicSize = oldSize;
                    }
                    Require(evidenceMark.Mark, "First melee mark survives later hits and camera movement");
                    Next();
                }
                else if (step == 106 && age > 0.3f)
                {
                    Capture("evidence-room.png", 1280, 800);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                    Next();
                }
                else if (step == 107 && age > 0.6f)
                {
                    var fresh = Object.FindFirstObjectByType<CameraDevice>();
                    Require(!evidenceMark.Mark && !GameObject.Find("Blood Evidence")
                        && fresh.BloodEvents.Count == 0 && fresh.Events.Count == 0
                        && Object.FindFirstObjectByType<BloodEvidence>().Marks.Count == 0,
                        "Scene restart clears world marks and per-device observations");
                    if (combined)
                    {
                        step = 33;
                        since = Time.time;
                    }
                    else Finish(true, "RRM EVIDENCE PLAY CHECK PASSED: real melee, persistent floor/wall marks, native feed pixels, placed inspection, same-target LightLevel gradient, event snapshots, occlusion and restart.");
                }
                else if (step == 80 && age > 0.6f)
                {
                    Require(SceneManager.GetActiveScene().path == (combined ? CombatPrototypeBuilder.ScenePath : CombatPrototypeBuilder.RangeScenePath),
                        "Camera range is the active playable scene");
                    if (combined)
                        Require(GameObject.Find("Low Cover") && GameObject.Find("High Cover") && GameObject.Find("Blind Corner")
                            && dummy.GetComponent<BloodEvidence>() && Object.FindObjectsByType<LightSource>().Length == 8,
                            "One scene contains the previous layout AND the new evidence/light features");
                    CheckHudPlacement();
                    Capture(combined ? "combined-prototype.png" : "range-overview.png", 1280, 800);
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/" + (combined ? "combined-prototype-ui.png" : "range-start-ui.png")));
                    if (combined)
                    {
                        float dark = CheckLightPreview(new Vector3(1f, 0.005f, -4.5f));
                        foreach (Vector3 bright in new[] { new Vector3(-5.1f, 0.005f, -3.1f),
                            new Vector3(-5.6f, 0.005f, 3.7f), new Vector3(2.8f, 0.005f, 0.4f),
                            new Vector3(3f, 0.005f, -4.5f), new Vector3(5.8f, 0.005f, 4.2f) })
                            Require(dark - CheckLightPreview(bright) > 0.15f,
                                "Both rooms contain multiple clearly distinct bright pockets and dark ground");
                        var source = GameObject.Find("Light Source East").GetComponent<LightSource>();
                        lightSourcePosition = source.transform.position;
                        source.enabled = false;
                    }
                    beforeDummy = dummyBody.position;
                    Next();
                }
                else if (step == 81 && age > 1.3f)
                {
                    if (combined)
                    {
                        Require(CheckLightPreview(new Vector3(2.8f, 0.005f, 0.4f)) > 0.17f,
                            "Disabling a source updates its floor tint");
                        var source = GameObject.Find("Light Source East").GetComponent<LightSource>();
                        source.transform.position = lightSourcePosition + Vector3.back * 4;
                        source.enabled = true;
                    }
                    Require(combined ? dummy.GetComponent<PlayerController>().State == PlayerController.EnemyState.Idle
                        && Vector3.Distance(beforeDummy, dummyBody.position) < 0.05f
                        : Vector3.Distance(beforeDummy, dummyBody.position) > 0.2f,
                        "Current enemy waits for detection; archived noncombat Dummy still wanders");
                    StageRange(new Vector3(-2, 0.02f, -2), new Vector3(2, 0.02f, -2));
                    Next();
                }
                else if (step == 82 && age > 0.3f)
                {
                    if (combined)
                    {
                        Require(CheckLightPreview(new Vector3(2.8f, 0.005f, -3.6f))
                            < CheckLightPreview(new Vector3(2.8f, 0.005f, 0.4f)), "Moving a source moves the visual gradient");
                        GameObject.Find("Light Source East").transform.position = lightSourcePosition;
                    }
                    Require(recorder.CanSee(dummyBody.position + Vector3.up * 1.05f), "Doorway gives a clear view into the other room");
                    RedCenter(ReadLiveFrame("range-doorway-feed.png"));
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/range-doorway-ui.png"));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    Next();
                }
                else if (step == 83 && (player.position.x > 0.8f || age > 3f))
                {
                    Require(player.position.x > 0.8f, $"Body-forward W crosses the doorway: {player.position}");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                    Next();
                }
                else if (step == 84 && (player.position.x < -1 || age > 3f))
                {
                    Require(player.position.x < -1, $"Body-backward S returns through the doorway: {player.position}");
                    StageRange(new Vector3(-1, 0.02f, 1), new Vector3(2, 0.02f, 1));
                    Next();
                }
                else if (step == 85 && age > 0.3f)
                {
                    Require(!recorder.CanSee(dummyBody.position + Vector3.up * 1.05f), "Solid divider hides the other room");
                    Require(!recorder.VisibleTargetLightLevel.HasValue && recorder.LightReadout.Contains("OFF CAMERA"),
                        "Light HUD does not reveal a hidden target through the divider");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/hud-off-camera.png"));
                    Require(Array.FindAll(ReadLiveFrame("range-divider-feed.png"), IsDummyRed).Length == 0,
                        "Dummy is absent from the real feed behind the divider");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    Next();
                }
                else if (step == 86 && age > 1f)
                {
                    Require(player.position.x < -0.43f && player.position.x > -1.1f,
                        "Rigidbody cannot walk through the solid divider");
                    StageRange(new Vector3(-4.92f, 0.02f, -2), new Vector3(-5.5f, 0.02f, 1));
                    Next();
                }
                else if (step == 87 && age > 0.3f)
                {
                    Require(recorder.CanSee(dummyBody.position + Vector3.up * 1.05f)
                        && !recorder.CanSee(dummyBody.position + Vector3.up * 0.25f),
                        "Low cover hides the lower body but leaves the torso visible to the camera");
                    RedCenter(ReadLiveFrame("range-low-cover-feed.png"));
                    StageRange(new Vector3(2.78f, 0.02f, 2.7f), new Vector3(2.2f, 0.02f, 5.3f));
                    Next();
                }
                else if (step == 88 && age > 0.3f)
                {
                    Require(!recorder.CanSee(dummyBody.position + Vector3.up * 1.05f), "High cover hides the torso");
                    Require(Array.FindAll(ReadLiveFrame("range-high-cover-feed.png"), IsDummyRed).Length == 0,
                        "High cover hides Dummy in the actual live image");
                    StageRange(new Vector3(-2, 0.02f, -2), new Vector3(2, 0.02f, -2));
                    Next();
                }
                else if (step == 89 && age > 0.3f)
                {
                    Require(recorder.CanSee(dummyBody.position + Vector3.up * 1.05f), "Doorway is visible again after repositioning");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                    Next();
                }
                else if (step == 90 && age > 0.3f)
                {
                    Require(recorder.IsPlaced && !recorder.LiveTexture, "F leaves the same passive camera watching the doorway");
                    Require(camera.rect == new Rect(0, 0, 1, 1), "Placing the camera restores the full room viewport");
                    Require(recorder.LightReadout == string.Empty && !recorder.VisibleTargetLightLevel.HasValue,
                        "Placed recording does not expose live light or quality diagnostics");
                    Hurtbox torso = Array.Find(dummy.GetComponentsInChildren<Hurtbox>(), zone => zone.part == BodyPart.Torso);
                    Require(dummy.ApplyDamage(player.gameObject, torso, torso.transform.position, Vector3.forward, dummy.Health, 0)
                        && recorder.ReportReady && recorder.GetComponent<CameraDevice>().DeathRecorded,
                        "Placed camera records a fatal event through the doorway and produces its report");
                    var events = recorder.GetComponent<CameraDevice>().Events;
                    Require(events[events.Count - 1].Damage.IsFatal && events[events.Count - 1].Clarity < 0.25f
                        && recorder.ReportText.Contains("Last event quality:"), "A witnessed low-light death has poor recorded quality");
                    Next();
                }
                else if (step == 91 && age > 0.2f)
                {
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/range-report-ui.png"));
                    Next();
                }
                else if (step == 92 && age > 0.3f)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                    Next();
                }
                else if (step == 93 && age > 0.6f)
                {
                    recorder = Object.FindFirstObjectByType<CameraRecorder>();
                    Require(SceneManager.GetActiveScene().path == (combined ? CombatPrototypeBuilder.ScenePath : CombatPrototypeBuilder.RangeScenePath)
                        && GameObject.Find("Room Divider North") && !recorder.IsPlaced && recorder.LiveTexture
                        && !recorder.ReportReady && recorder.RecordedHits == 0,
                        "R restarts the camera range, not the old scene, with one fresh handheld camera");
                    Require(Object.FindObjectsByType<CameraDevice>(FindObjectsSortMode.None).Length == 1, "Range contains only one camera device");
                    if (combined)
                    {
                        player = GameObject.Find("Player").GetComponent<Rigidbody>();
                        dummy = GameObject.Find("Dummy").GetComponent<Damageable>();
                        dummyBody = dummy.GetComponent<Rigidbody>();
                        camera = Camera.main;
                        damageEvents = 0;
                        dummy.Damaged += ObserveHit;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        step = 100;
                        since = Time.time;
                    }
                    else Finish(true, "RRM RANGE PLAY CHECK PASSED: Dummy motion, doorway traversal both ways, solid-wall collision, real feed occlusion, low/high cover, placed report and scene restart.");
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void AimAt(Vector3 target, bool attack = false)
        {
            Vector2 point = camera.WorldToScreenPoint(new Vector3(target.x, 0, target.z));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = (ushort)(attack ? 2 : 0) });
        }

        private static bool IsDummyRed(Color32 color) => color.r >= 70 && color.r >= color.g * 1.4f && color.r >= color.b * 1.4f;

        private static float CheckLightPreview(Vector3 point)
        {
            GameObject overlay = GameObject.Find("Light Level Preview");
            Require(overlay && !overlay.GetComponent<Collider>()
                && Object.FindObjectsByType<LightLevelPreview>().Length == 1, "One nonphysical floor preview exists");
            Material material = overlay.GetComponent<Renderer>().sharedMaterial;
            var texture = material.GetTexture("_BaseMap") as Texture2D;
            Vector3 uv = overlay.transform.InverseTransformPoint(point) + new Vector3(0.5f, 0.5f, 0);
            float alpha = texture.GetPixelBilinear(uv.x, uv.y).a;
            float expected = GameObject.Find("Floor").GetComponent<LightLevelPreview>().opacity
                * (1f - Mathf.SmoothStep(0f, 1f, recorder.GetLightLevel(point) / 70f));
            Require(Mathf.Abs(alpha - expected) < 0.006f && alpha <= 0.185f
                && material.GetFloat("_ZWrite") == 0f, "Preview pixels match LightLevel with a subtle, non-depth-writing tint");
            return alpha;
        }

        private static void CheckHudPlacement()
        {
            // Batch mode has no visible Game View; validate its layout in the graphical run.
            if (Application.isBatchMode) return;
            float height = Mathf.Min(144f, Screen.height * 0.32f);
            Require(Mathf.Abs(camera.pixelRect.yMin - height) < 1f
                && Mathf.Abs(camera.pixelRect.yMax - Screen.height) < 1f,
                "Room viewport is above the compact HUD strip, including after restart");
            foreach (Vector3 point in new[] { new Vector3(-8.2f, 2.6f, 6.2f), new Vector3(8.2f, 2.6f, 6.2f),
                new Vector3(-8.2f, 0, -6.2f), new Vector3(8.2f, 0, -6.2f) })
            {
                Vector3 projected = camera.WorldToViewportPoint(point);
                Require(projected.x > 0 && projected.x < 1 && projected.y > 0 && projected.y < 1,
                    "Both rooms fit inside the unobstructed game viewport");
            }
            Require(recorder.LiveTexture.width == 640 && recorder.LiveTexture.height == 480,
                "Repositioning the feed does not change the recording camera or texture resolution");
            Debug.Log("RRM HUD PLACEMENT: " + Screen.width + "x" + Screen.height + ", room=" + camera.pixelRect
                + ", HUD height=" + height);
        }

        private static void CheckLiveLightHud()
        {
            float previous = 101f;
            foreach (Vector3 position in new[] { new Vector3(2.8f, 0.02f, 0.4f),
                new Vector3(2.8f, 0.02f, -1.8f), new Vector3(1f, 0.02f, -4.5f) })
            {
                StageRange(position + Vector3.forward * 1.6f, position);
                Hurtbox torso = Array.Find(dummy.GetComponentsInChildren<Hurtbox>(), zone => zone.part == BodyPart.Torso);
                float light = recorder.GetLightLevel(torso.transform.position);
                Require(recorder.VisibleTargetLightLevel.HasValue
                    && Mathf.Abs(recorder.VisibleTargetLightLevel.Value - light) < 0.001f
                    && recorder.LightReadout.Contains("TARGET LIGHT: " + light.ToString("0") + "/100")
                    && recorder.LightReadout.Contains("QUALITY: " + light.ToString("0") + "%")
                    && recorder.LightReadout.Contains("LIGHT HERE: " + recorder.GetLightLevel(player.position).ToString("0"))
                    && light < previous, "HUD follows the same target and player across bright, low and dark positions");
                previous = light;
                Debug.Log("RRM LIVE LIGHT HUD: " + recorder.LightReadout.Replace('\n', ' '));
            }
        }

        private static void CheckEvidenceLight(Vector3 point, VisibilityLevel? expected = null)
        {
            Hurtbox torso = Array.Find(dummy.GetComponentsInChildren<Hurtbox>(), zone => zone.part == BodyPart.Torso);
            dummyBody.position += point - torso.transform.position;
            dummyBody.transform.position = dummyBody.position;
            dummyBody.linearVelocity = Vector3.zero;
            recorder.transform.position = point + Vector3.forward * 3;
            recorder.transform.LookAt(point - Vector3.up * 0.5f);
            Physics.SyncTransforms();
            float lightLevel = recorder.GetLightLevel(torso.transform.position);
            Require(lightLevel >= 0 && lightLevel <= 100
                && (!expected.HasValue || LightSource.Classify(lightLevel) == expected.Value),
                "The same Dummy samples the expected light at its current position");
            var device = recorder.GetComponent<CameraDevice>();
            int hits = device.RecordedHits;
            Require(dummy.ApplyDamage(player.gameObject, torso, point, Vector3.forward, 1, 0), "Lighting fixture sends real DamageEvent");
            Require(device.RecordedHits == hits + 1 && Mathf.Abs(device.Events[hits].LightLevel - lightLevel) < 0.001f,
                "Placed recorder stores the lighting at the impact point, not at the camera");
            var observation = device.BloodEvents[device.BloodEvents.Count - 1];
            Physics.Linecast(recorder.lens.position, observation.Blood.Position, out RaycastHit obstruction,
                recorder.obstructionMask, QueryTriggerInteraction.Ignore);
            Require(observation.Visible, "Blood is visible from the fixture: mark=" + observation.Blood.Position
                + ", lens=" + recorder.lens.position + ", obstruction=" + obstruction.collider
                + ", local=" + recorder.lens.InverseTransformPoint(observation.Blood.Position));
            float surfaceLevel = recorder.GetLightLevel(observation.Blood.Position);
            Require(Mathf.Abs(observation.LightLevel - surfaceLevel) < 0.001f
                && Mathf.Abs(observation.Clarity - observation.LightLevel / 100f) < 0.00001f,
                $"Blood retains surface light: recorded={observation.LightLevel:R}, current={surfaceLevel:R}, clarity={observation.Clarity:R}");
            var sources = Array.FindAll(Object.FindObjectsByType<LightSource>(), source => source.isActiveAndEnabled);
            foreach (LightSource source in sources) source.enabled = false;
            try
            {
                Require(recorder.GetLightLevel(point) == 0f && Mathf.Abs(device.Events[hits].LightLevel - lightLevel) < 0.001f
                    && device.BloodEvents[device.BloodEvents.Count - 1].LightLevel == observation.LightLevel,
                    "Switching off current illumination does not rewrite recorded hit or blood history");
            }
            finally { foreach (LightSource source in sources) source.enabled = true; }
            Quaternion facing = recorder.transform.rotation;
            recorder.transform.Rotate(0, 180, 0, Space.World);
            Require(!recorder.InspectBlood(observation.Blood).Visible
                && recorder.InspectBlood(observation.Blood).Clarity == 0f && observation.Visible,
                "Current visibility can change without rewriting the original observation");
            recorder.transform.rotation = facing;
            Debug.Log("RRM EVIDENCE LIGHT: same Dummy at " + point + " LightLevel=" + lightLevel.ToString("0.00")
                + " " + device.Events[hits].Visibility + ", surface=" + observation.LightLevel.ToString("0.00"));
        }

        private static void StageRange(Vector3 playerPosition, Vector3 dummyPosition, bool aim = true)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            dummy.GetComponent<PlayerController>().enabled = false;
            if (dummy.TryGetComponent(out MeleeAttack enemyAttack))
            {
                enemyAttack.enabled = false;
                enemyAttack.enabled = true;
            }
            player.position = playerPosition;
            dummyBody.position = dummyPosition;
            player.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(dummyPosition - playerPosition, Vector3.up));
            dummyBody.rotation = Quaternion.identity;
            player.linearVelocity = dummyBody.linearVelocity = Vector3.zero;
            player.transform.SetPositionAndRotation(playerPosition, player.rotation);
            dummyBody.transform.SetPositionAndRotation(dummyPosition, Quaternion.identity);
            recorder.SetViewRotation(Quaternion.LookRotation(dummyBody.position + Vector3.up * 1.05f - recorder.transform.position));
            Physics.SyncTransforms();
            if (aim) AimCamera(Vector2.zero);
        }

        private static void CheckRejectedHandPickup()
        {
            var controller = player.GetComponent<PlayerController>();
            var attack = player.GetComponent<MeleeAttack>();
            Vector3 position = placedDevice.transform.position;
            Vector3 weaponPosition = attack.weaponPivot.localPosition;
            Vector3 mount = mountLocalPosition;
            mount.x = Mathf.Abs(mount.x);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position = (controller.transform.TransformPoint(mount) + position) * 0.5f;
                wall.transform.localScale = Vector3.one * 0.2f;
                Physics.SyncTransforms();
                Require(!placedDevice.TryPickup(controller, HandSide.Right), "Requested right hand cannot pick up through a wall");
                wall.SetActive(false);
                placedDevice.transform.position += Vector3.up * 10;
                Require(!placedDevice.TryPickup(controller, HandSide.Right), "Out-of-reach pickup fails");
                placedDevice.transform.position = position;
                Require(attack.TryAttack() && !placedDevice.TryPickup(controller, HandSide.Right),
                    "An active weapon sweep cannot teleport to the other hand during pickup");
                Require(controller.RightHand.Item?.Source == attack && controller.LeftHand.State == HandState.Empty
                    && attack.weaponPivot.localPosition == weaponPosition && !placedDevice.Owner
                    && placedDevice.transform.position == position && placedDevice.Id == deviceId,
                    "Rejected pickups leave weapon, device, ownership and hand states untouched");
            }
            finally
            {
                placedDevice.transform.position = position;
                attack.enabled = false;
                attack.enabled = true;
                Object.DestroyImmediate(wall);
                Physics.SyncTransforms();
            }
        }

        private static void CheckStance(bool crouched)
        {
            var controller = player.GetComponent<PlayerController>();
            var capsule = player.GetComponent<CapsuleCollider>();
            float scale = crouched ? controller.crouchHeightScale : 1f;
            Require(controller.IsCrouching == crouched && Mathf.Abs(capsule.height - standingCapsuleHeight * scale) < 0.001f
                && Mathf.Abs(capsule.center.y - capsule.height * 0.5f) < 0.001f
                && Mathf.Abs(player.GetComponent<HitFeedback>().visual.localScale.y - scale) < 0.001f,
                "Stance changes the actual collider/body/hurtboxes without moving the capsule's feet");
            Require(Mathf.Abs(recorder.transform.localPosition.y - mountLocalPosition.y * scale) < 0.001f
                && recorder.transform.lossyScale == Vector3.one && recorder.LiveTexture
                && Quaternion.Angle(recorder.lens.rotation, beforeLensRotation) < 0.1f,
                "Held camera follows stance height without scaling or changing its independent direction");
        }

        private static void CaptureHandLayout(HandSide cameraSide, string fileName = null)
        {
            var controller = player.GetComponent<PlayerController>();
            var attack = player.GetComponent<MeleeAttack>();
            var cameraHand = controller.HandHolding(controller.HeldCamera);
            var weaponHand = controller.HandHolding(attack);
            float sign = cameraSide == HandSide.Left ? -1 : 1;
            Require(cameraHand != null && cameraHand.Side == cameraSide && weaponHand != null && weaponHand != cameraHand
                && recorder.transform.parent == player.transform && attack.weaponPivot.parent == player.transform
                && recorder.transform.localPosition.x * sign > 0.4f && attack.weaponPivot.localPosition.x * sign < -0.4f,
                "Actual camera housing/lens and weapon pivot occupy their controlling hands, not the same hand");
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            float size = camera.orthographicSize;
            Rect rect = camera.rect;
            try
            {
                camera.transform.position = player.position + player.rotation * new Vector3(0, 5, -4);
                camera.transform.LookAt(player.position + Vector3.up);
                camera.orthographicSize = 1.7f;
                camera.rect = new Rect(0, 0, 1, 1);
                Capture(fileName ?? "hands-camera-" + cameraSide.ToString().ToLowerInvariant() + ".png", 800, 800);
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.orthographicSize = size;
                camera.rect = rect;
            }
        }

        private static void CheckCameraOwnershipTransfer()
        {
            var holder = player.GetComponent<PlayerController>();
            var nextHolder = dummy.GetComponent<PlayerController>();
            nextHolder.enabled = false;
            dummyBody.position = player.transform.position + player.transform.right * 0.8f;
            dummyBody.rotation = player.transform.rotation;
            dummyBody.linearVelocity = Vector3.zero;
            // Editor-tick teleport: synchronize the interpolated hand pose before querying reach.
            nextHolder.transform.SetPositionAndRotation(dummyBody.position, dummyBody.rotation);
            Physics.SyncTransforms();
            nextHolder.enabled = true;
            Require(!placedDevice.TryPickup(nextHolder) && !placedDevice.TryPlace(nextHolder)
                && placedDevice.Owner == holder, "Another holder cannot take or place an occupied camera");
            Require(placedDevice.TryPlace(holder), "Current holder can release the device");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Vector3 hand = nextHolder.transform.TransformPoint(mountLocalPosition);
                wall.transform.position = (hand + placedDevice.transform.position) * 0.5f;
                wall.transform.localScale = Vector3.one * 0.25f;
                Physics.SyncTransforms();
                Require(!placedDevice.TryPickup(nextHolder) && !placedDevice.Owner,
                    "A nearby device cannot be picked up through solid geometry");
            }
            finally { Object.DestroyImmediate(wall); Physics.SyncTransforms(); }
            Vector3 nextHand = nextHolder.transform.TransformPoint(mountLocalPosition);
            Require(Vector3.Distance(nextHand, placedDevice.transform.position) <= placedDevice.pickupDistance,
                "Transfer setup puts the new holder's hand within pickup reach");
            Require(!Physics.Linecast(nextHand, placedDevice.transform.position, out RaycastHit obstruction, 1,
                QueryTriggerInteraction.Ignore), $"Transfer setup is clear after removing test wall: {obstruction.collider}");
            Require(placedDevice.TryPickup(nextHolder), "Unobstructed new holder can pick up the free device");
            Require(placedDevice.Owner == nextHolder
                && nextHolder.HeldCamera == placedDevice && !holder.HeldCamera
                && placedDevice.Id == deviceId && !recorder.LiveTexture,
                "An unobstructed new holder receives the same device, not a copy or a remote feed");
            Require(placedDevice.TryPlace(nextHolder) && placedDevice.TryPickup(holder)
                && placedDevice.Owner == holder && !nextHolder.HeldCamera
                && placedDevice.Id == deviceId && recorder.RecordedHits == placedRecordings
                && recorder.LiveTexture && Object.FindObjectsByType<CameraDevice>(FindObjectsSortMode.None).Length == 1
                && Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 2,
                "Returning the same device preserves events and creates no extra device or rendering camera");
        }

        private static void CountHandClicks()
        {
            // EditorApplication.update sees editor input buffers, not the gameplay mouse state.
            if (!player || InputState.currentUpdateType != InputUpdateType.Dynamic || handInputFrame == Time.frameCount) return;
            handInputFrame = Time.frameCount;
            var controller = player.GetComponent<PlayerController>();
            leftHandHolding = controller.LeftHand.IsHoldActive;
            rightHandHolding = controller.RightHand.IsHoldActive;
            if (controller.LeftHand.WasClickedThisFrame) leftHandClicks++;
            if (controller.RightHand.WasClickedThisFrame) rightHandClicks++;
        }

        private static void AimCamera(Vector2 delta, bool attack = false)
        {
            var controller = player.GetComponent<PlayerController>();
            var hand = controller.HandHolding(controller.HeldCamera);
            ushort cameraButton = (ushort)(hand == null || hand.Side == HandSide.Left ? 1 : 2);
            var weaponHand = controller.HandHolding(player.GetComponent<MeleeAttack>());
            int weaponButton = weaponHand == null ? 0 : weaponHand.Side == HandSide.Left ? 1 : 2;
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = camera.WorldToScreenPoint(player.position + Vector3.right * 5f),
                buttons = (ushort)(cameraButton | (attack ? weaponButton : 0)),
                delta = delta
            });
        }

        private static void ObserveHit(DamageEvent hit)
        {
            if (hit.IsBleeding) return;
            hitTime = Time.time;
            unscaledHitTime = Time.unscaledTime;
            damageEvents++;
            lastHitVisible = recorder.CanSee(hit.Point);
            Require(hit.Source == player.gameObject && hit.Target == dummy, "Melee event identifies the actual actors");
            Require(dummy.GetComponent<HitFeedback>().blood.particleCount > 0, "Blood emits immediately on DamageEvent");
        }

        private static void ObserveEnemyHit(DamageEvent hit)
        {
            if (hit.IsBleeding) return;
            enemyHits++;
            enemyHitTime = Time.time;
            Require(hit.Source == dummy.gameObject && hit.Target == playerHealth
                && dummy.GetComponent<MeleeAttack>().Phase == MeleeAttack.AttackPhase.Strike
                && player.GetComponent<HitFeedback>().blood.particleCount > 0,
                "Enemy's existing melee produces the real player DamageEvent and immediate blood");
            Debug.Log($"RRM ENEMY HIT: #{enemyHits}, part={hit.Part}, damage={hit.Amount}, HP={playerHealth.Health}, fatal={hit.IsFatal}");
        }

        private static void PositionForRecordingHit()
        {
            player.position = new Vector3(0, 0.02f, 0);
            dummyBody.position = new Vector3(0, 0.02f, 1.35f);
            player.rotation = dummyBody.rotation = Quaternion.identity;
            player.linearVelocity = dummyBody.linearVelocity = Vector3.zero;
        }

        private static void CaptureBlood()
        {
            ParticleSystem particles = dummy.GetComponent<HitFeedback>().blood;
            Require(particles.particleCount > 0, "Blood particles survive long enough to be visible");
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            Color32[] withBlood = Capture("prototype-contact.png", 1280, 800);
            Color32[] withoutBlood;
            // Same scene and instant: isolate the blood renderer's actual contribution to the image.
            renderer.enabled = false;
            try { withoutBlood = Capture("contact-without-blood.png", 1280, 800); }
            finally { renderer.enabled = true; }
            int visible = 0;
            for (int i = 0; i < withBlood.Length; i++)
            {
                Color32 a = withBlood[i], b = withoutBlood[i];
                if (a.r > a.g * 1.8f && a.r > a.b * 1.8f
                    && Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) > 40) visible++;
            }
            Require(visible > 80, $"Blood is visible in the top-down rendered frame ({visible} pixels)");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/combat-blood-ui.png"));
            Debug.Log($"RRM BLOOD CHECK: {particles.particleCount} particles, {visible} visible red pixels after mouse melee hit.");
        }

        private static void FrameDummy()
        {
            player.position = new Vector3(0, 0.02f, -2);
            dummyBody.position = new Vector3(-1.2f, 0.02f, 1.8f);
            player.rotation = dummyBody.rotation = Quaternion.identity;
            player.linearVelocity = dummyBody.linearVelocity = Vector3.zero;
            AimAt(player.position + Vector3.forward * 5f);
        }

        private static Color32[] ReadLiveFrame(string fileName)
        {
            RenderTexture target = recorder.LiveTexture;
            Require(target && target.IsCreated() && recorder.liveCamera.enabled
                && recorder.liveCamera.targetTexture == target, "A running camera renders to the live texture");
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                // Read the continuously rendered texture, without manually rendering a replacement frame.
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.GetFullPath("Verification/" + fileName), pixels.EncodeToPNG());
                return pixels.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
            }
        }

        private static int ChangedPixels(Color32[] before, Color32[] after)
        {
            int changed = 0;
            for (int i = 0; i < after.Length; i++)
                if (Math.Abs(after[i].r - before[i].r) + Math.Abs(after[i].g - before[i].g)
                    + Math.Abs(after[i].b - before[i].b) > 40) changed++;
            return changed;
        }

        private static float RedCenter(Color32[] colors, bool vertical = false)
        {
            int count = 0;
            double x = 0;
            for (int i = 0; i < colors.Length; i++)
            {
                Color32 color = colors[i];
                if (color.r < 70 || color.r < color.g * 1.4f || color.r < color.b * 1.4f) continue;
                count++;
                x += vertical ? i / recorder.LiveTexture.width : i % recorder.LiveTexture.width;
            }
            Require(count > 100, "The actual red dummy is visible in the handheld image");
            return (float)(x / count);
        }

        private static void TickStarterCover(float age)
        {
            var controller = player.GetComponent<PlayerController>();
            Bounds cover = GameObject.Find("Low Cover").GetComponent<Collider>().bounds;
            if (step == 500 && age > 0.5f)
            {
                Require(Mathf.Abs(cover.size.y - 0.45f) < 0.001f && controller.jumpHeight == 0.6f,
                    "Saved cover is 45 cm; existing jump is unchanged");
                StageRange(new Vector3(cover.center.x, 0.02f, cover.min.z - 0.85f), new Vector3(6, 0.02f, 5), false);
                player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                Next();
            }
            else if (step == 501 && age > 0.3f)
            {
                Require(controller.IsGrounded, "Start on the floor, not already on cover");
                beforeLens = recorder.lens.position;
                spaceTapReleased = false;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
                Next();
            }
            else if (step == 502 && age > 0.06f && !spaceTapReleased)
            {
                spaceTapReleased = true;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            }
            else if (step == 502 && age > 0.2f && (player.position.z >= cover.center.z - 0.2f || age > 1.2f))
            {
                Require(player.position.y > cover.max.y - 0.05f && player.position.z > cover.min.z + 0.2f,
                    $"W + Space clears the front edge: {player.position}");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Next();
            }
            else if (step == 503 && age > 0.7f)
            {
                Require(controller.IsGrounded && Mathf.Abs(player.position.y - cover.max.y) < 0.04f
                    && player.position.z > cover.min.z && player.position.z < cover.max.z,
                    $"Stable landing on cover: {player.position}");
                Require(recorder.LiveTexture && Mathf.Abs(recorder.lens.position.y - beforeLens.y - cover.max.y) < 0.05f,
                    "Handheld lens rises with the player and feed stays live");
                Capture("starter-cover-jump.png", 1280, 800);
                ReadLiveFrame("starter-cover-feed.png");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                Next();
            }
            else if (step == 504 && age > 0.85f)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Next();
            }
            else if (step == 505 && age > 0.4f)
            {
                Require(controller.IsGrounded && Mathf.Abs(player.position.y) < 0.04f && player.position.z < cover.min.z,
                    "S walks back off cover and lands on floor");
                Finish(true, "RRM STARTER COVER PLAY CHECK PASSED: saved 45 cm cover, real W+Space landing, live camera and S descent; movement unchanged.");
            }
        }

        private static void Next() { step++; since = Time.time; }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("RRM PLAY CHECK FAILED: " + message);
        }

        private static Color32[] Capture(string fileName, int width, int height)
        {
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Rendering checks need a graphics device");
            string folder = Path.GetFullPath("Verification");
            Directory.CreateDirectory(folder);
            var texture = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            float oldAspect = camera.aspect;
            try
            {
                camera.aspect = width / (float)height;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Color32[] colors = pixels.GetPixels32();
                int varied = 0;
                Color32 first = colors[0];
                foreach (Color32 color in colors)
                    if (Math.Abs(color.r - first.r) + Math.Abs(color.g - first.g) + Math.Abs(color.b - first.b) > 30) varied++;
                File.WriteAllBytes(Path.Combine(folder, fileName), pixels.EncodeToPNG());
                Require(varied > colors.Length / 10, $"Rendered frame is nonblank: {fileName}, {varied}/{colors.Length} varied pixels");
                return colors;
            }
            finally
            {
                camera.aspect = oldAspect;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        private static void Finish(bool success, string message)
        {
            InputSystem.onAfterUpdate -= CountHandClicks;
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= WatchErrors;
            if (dummy) dummy.Damaged -= ObserveHit;
            if (playerHealth) playerHealth.Damaged -= ObserveEnemyHit;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (otherInputs != null)
                foreach (InputDevice device in otherInputs)
                    if (device.added) InputSystem.EnableDevice(device);
            Application.runInBackground = oldRunInBackground;
            UnityEngine.Random.state = oldRandomState;
            Time.captureDeltaTime = oldCaptureDeltaTime;
            InputSystem.settings.backgroundBehavior = oldBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInputBehavior;
            SessionState.SetInt("RRM.PlayCheck.ExitCode", success ? 0 : 1);
            if (success) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.isPlaying = false;
        }
    }
}
