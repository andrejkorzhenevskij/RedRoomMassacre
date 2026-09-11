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
                Vector3 origin = new Vector3(1000, 0, 1000);
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
                    Check(health.ApplyDamage(player, zone, visible, Vector3.forward, health.Health, 0), "Report receives real fatal DamageEvent");
                    CameraDevice witness = recorder.GetComponent<CameraDevice>();
                    CameraDevice blind = otherCamera.GetComponent<CameraDevice>();
                    Check(witness.Events.Count == 2 && witness.RecordedHits == 2 && witness.DeathRecorded,
                        "Witness retains separate hit events, including the fatal hit exactly once");
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
                Debug.Log("RRM CHECKS PASSED: damage validation, phases, deduplication, body parts, death, recording, occlusion.");
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

    public static class CombatPrototypePlayCheck
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
                if (EditorApplication.timeSinceStartup - started > 90)
                    throw new TimeoutException($"Play check timed out at step {step}, simulated age {Time.time - since:F2}, player {player.position}, return point {beforePlayer}.");
                float age = Time.time - since;
                EditorApplication.QueuePlayerLoopUpdate();
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
                    Require(!dummy.GetComponent<MeleeAttack>(), "Dummy movement does not add combat mechanics");
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
                    Require(Mathf.Abs(Mathf.DeltaAngle(angles.x, recorder.maxPitch)) < 0.1f
                        && Mathf.Abs(Mathf.DeltaAngle(angles.y, recorder.maxYaw)) < 0.1f,
                        "Large mouse deltas clamp downward pitch and right yaw");
                    ReadLiveFrame("handheld-down-limit.png");
                    AimCamera(new Vector2(-100000f, 100000f));
                    Next();
                }
                else if (step == 21 && age > 0.2f)
                {
                    Vector3 angles = recorder.transform.localEulerAngles;
                    Require(Mathf.Abs(Mathf.DeltaAngle(angles.x, recorder.minPitch)) < 0.1f
                        && Mathf.Abs(Mathf.DeltaAngle(angles.y, -recorder.maxYaw)) < 0.1f,
                        "Large mouse deltas clamp upward pitch and left yaw without wrapping");
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
                    recorder.transform.localRotation = Quaternion.Euler(8, 0, 0);
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
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                    Next();
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
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
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
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
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
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
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
                    recorder.transform.LookAt(point);
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
                    if (!seen) recorder.transform.Rotate(0, 180, 0, Space.World);
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
                else if (step == 80 && age > 0.6f)
                {
                    Require(SceneManager.GetActiveScene().path == CombatPrototypeBuilder.RangeScenePath,
                        "Camera range is the active playable scene");
                    Capture("range-overview.png", 1280, 800);
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/range-start-ui.png"));
                    beforeDummy = dummyBody.position;
                    Next();
                }
                else if (step == 81 && age > 1.3f)
                {
                    Require(Vector3.Distance(beforeDummy, dummyBody.position) > 0.2f,
                        "Existing Dummy wanders in the new room");
                    StageRange(new Vector3(-2, 0.02f, -2), new Vector3(2, 0.02f, -2));
                    Next();
                }
                else if (step == 82 && age > 0.3f)
                {
                    Require(recorder.CanSee(dummyBody.position + Vector3.up * 1.05f), "Doorway gives a clear view into the other room");
                    RedCenter(ReadLiveFrame("range-doorway-feed.png"));
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/range-doorway-ui.png"));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                    Next();
                }
                else if (step == 83 && age > 1.2f)
                {
                    Require(player.position.x > 0.8f, "D walks through the doorway into the second room");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
                    Next();
                }
                else if (step == 84 && age > 1.2f)
                {
                    Require(player.position.x < -1, "A returns through the same doorway");
                    StageRange(new Vector3(-1, 0.02f, 1), new Vector3(2, 0.02f, 1));
                    Next();
                }
                else if (step == 85 && age > 0.3f)
                {
                    Require(!recorder.CanSee(dummyBody.position + Vector3.up * 1.05f), "Solid divider hides the other room");
                    Require(Array.FindAll(ReadLiveFrame("range-divider-feed.png"), IsDummyRed).Length == 0,
                        "Dummy is absent from the real feed behind the divider");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
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
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                    Next();
                }
                else if (step == 90 && age > 0.3f)
                {
                    Require(recorder.IsPlaced && !recorder.LiveTexture, "F leaves the same passive camera watching the doorway");
                    Hurtbox torso = Array.Find(dummy.GetComponentsInChildren<Hurtbox>(), zone => zone.part == BodyPart.Torso);
                    Require(dummy.ApplyDamage(player.gameObject, torso, torso.transform.position, Vector3.forward, dummy.Health, 0)
                        && recorder.ReportReady && recorder.GetComponent<CameraDevice>().DeathRecorded,
                        "Placed camera records a fatal event through the doorway and produces its report");
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
                    Require(SceneManager.GetActiveScene().path == CombatPrototypeBuilder.RangeScenePath
                        && GameObject.Find("Room Divider North") && !recorder.IsPlaced && recorder.LiveTexture
                        && !recorder.ReportReady && recorder.RecordedHits == 0,
                        "R restarts the camera range, not the old scene, with one fresh handheld camera");
                    Require(Object.FindObjectsByType<CameraDevice>(FindObjectsSortMode.None).Length == 1, "Range contains only one camera device");
                    Finish(true, "RRM RANGE PLAY CHECK PASSED: Dummy motion, doorway traversal both ways, solid-wall collision, real feed occlusion, low/high cover, placed report and scene restart.");
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void AimAt(Vector3 target, bool attack = false)
        {
            Vector2 point = camera.WorldToScreenPoint(new Vector3(target.x, 0, target.z));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = (ushort)(attack ? 1 : 0) });
        }

        private static bool IsDummyRed(Color32 color) => color.r >= 70 && color.r >= color.g * 1.4f && color.r >= color.b * 1.4f;

        private static void StageRange(Vector3 playerPosition, Vector3 dummyPosition)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            dummy.GetComponent<PlayerController>().enabled = false;
            player.position = playerPosition;
            dummyBody.position = dummyPosition;
            player.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(dummyPosition - playerPosition, Vector3.up));
            dummyBody.rotation = Quaternion.identity;
            player.linearVelocity = dummyBody.linearVelocity = Vector3.zero;
            player.transform.SetPositionAndRotation(playerPosition, player.rotation);
            dummyBody.transform.SetPositionAndRotation(dummyPosition, Quaternion.identity);
            recorder.transform.LookAt(dummyBody.position + Vector3.up * 1.05f);
            Physics.SyncTransforms();
            AimCamera(Vector2.zero);
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

        private static void AimCamera(Vector2 delta, bool attack = false)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = camera.WorldToScreenPoint(player.position + Vector3.right * 5f),
                buttons = (ushort)(attack ? 3 : 2),
                delta = delta
            });
        }

        private static void ObserveHit(DamageEvent hit)
        {
            hitTime = Time.time;
            unscaledHitTime = Time.unscaledTime;
            damageEvents++;
            lastHitVisible = recorder.CanSee(hit.Point);
            Require(hit.Source == player.gameObject && hit.Target == dummy, "Melee event identifies the actual actors");
            Require(dummy.GetComponent<HitFeedback>().blood.particleCount > 0, "Blood emits immediately on DamageEvent");
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
                Require(varied > colors.Length / 10, "Rendered frame is nonblank");
                File.WriteAllBytes(Path.Combine(folder, fileName), pixels.EncodeToPNG());
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
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= WatchErrors;
            if (dummy) dummy.Damaged -= ObserveHit;
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
