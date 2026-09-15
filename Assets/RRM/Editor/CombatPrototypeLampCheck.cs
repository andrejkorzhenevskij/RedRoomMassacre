using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace RRM.Editor
{
    public static partial class CombatPrototypePlayCheck
    {
        private static PortableLamp testLamp;
        private static Vector3 lampPlacedPosition;
        private static readonly Vector3 LightProbe = new Vector3(1.5f, 1.05f, -2f);
        private static float lightBefore, lightNear, savedQuality;
        private static int lampSources;
        private static float lampHitLight, lampHitContribution, lampHitDamage;
        private static Vector3 lampHitPoint;
        private static int lampHitCount;
        private static GameObject otherLamp;
        private static bool lampLitDuringDamage;
        private static bool itemTapReleased;
        private static IEnumerator<float> handCheck;
        private static float handWait;

        public static void RunContextualHandsAndExit()
        {
            SessionState.SetInt("RRM.PlayCheck.Lamps", 6);
            RunAndExit();
        }

        public static void PrepareLampsAndExit()
        {
            CombatPrototypeBuilder.UpdatePortableLamps();
            CombatPrototypeBuilder.UpdatePortableLamps();
            RunLampsAndExit();
        }

        public static void RunLampsAndExit()
        {
            SessionState.SetInt("RRM.PlayCheck.Lamps", 3);
            RunAndExit();
        }

        public static void RunItemBashAndExit()
        {
            SessionState.SetInt("RRM.PlayCheck.Lamps", 4);
            RunAndExit();
        }

        public static void RunCameraBashAndExit()
        {
            SessionState.SetInt("RRM.PlayCheck.Lamps", 5);
            RunAndExit();
        }

        public static void RunLampsAAndExit()
        {
            SessionState.SetInt("RRM.PlayCheck.Lamps", 2);
            RunAndExit();
        }

        public static void BuildLampVerification()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { CombatPrototypeBuilder.ScenePath },
                locationPathName = "Builds/LampVerification/RRM.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Lamp verification player failed to build.");
            Debug.Log("RRM LAMP VERIFICATION BUILD PASSED: " + report.summary.totalErrors + " errors");
        }

        public static void RunLightDiagnosticAndExit()
        {
            SessionState.SetInt("RRM.PlayCheck.Lamps", 1);
            RunAndExit();
        }

        private static void TickLamps(float age)
        {
            int mode = SessionState.GetInt("RRM.PlayCheck.Lamps", 0);
            if (mode >= 6 && mode <= 14)
            {
                if (handCheck == null)
                {
                    if (age < 0.8f) return;
                    handCheck = mode == 14 ? CheckCharCrafter() : mode == 13 ? CheckBleeding() : mode == 12 ? CheckSevering() : mode == 11 ? CheckAxe() : mode == 10 ? CheckDuel() : mode == 9 ? CheckFeet() : mode == 8 ? CheckWallBlock() : mode == 7 ? CheckGrapple() : CheckContextualHands();
                    step = 700;
                }
                if (age < handWait) return;
                if (handCheck.MoveNext()) { handWait = handCheck.Current; since = Time.time; }
                else
                {
                    handCheck.Dispose(); handCheck = null;
                    if (mode == 14)
                    {
                        SessionState.SetInt("RRM.PlayCheck.Lamps", 0);
                        Finish(true, "RRM CHARCRAFTER PLAY CHECK PASSED (detached proxy only; live skin is not segmented)");
                        return;
                    }
                    SessionState.SetInt("RRM.PlayCheck.Lamps", mode > 6 ? mode - 1 : 4);
                    handWait = 0f;
                    step = 0; since = Time.time;
                    started = EditorApplication.timeSinceStartup;
                    Debug.Log(mode == 13 ? "RRM BLEEDING PASSED; continuing severing and previous combat regressions."
                        : mode == 12 ? "RRM SEVERING PASSED; continuing axe and previous combat regressions."
                        : mode == 11 ? "RRM AXE PASSED; continuing duel and previous combat regressions."
                        : mode == 10 ? "RRM DUEL PASSED; continuing feet, wall block, grapple, hands and item-bash regressions."
                        : mode == 9 ? "RRM FEET PASSED; continuing wall block, grapple, hands and item-bash regressions."
                        : mode == 8 ? "RRM WALL BLOCK PASSED; continuing grapple, hands and item-bash regressions."
                        : mode == 7 ? "RRM GRAPPLE PASSED; continuing contextual hands and item-bash regressions."
                        : "RRM CONTEXTUAL HANDS PASSED; continuing item-bash regression checks.");
                }
                return;
            }
            if (SessionState.GetInt("RRM.PlayCheck.Lamps", 0) != 1) { TickLampScenario(age); return; }
            if (age < 0.8f) return;
            var preview = Object.FindFirstObjectByType<LightLevelPreview>();
            var overlay = preview.transform.Find("Light Level Preview").GetComponent<MeshRenderer>();
            var texture = (Texture2D)overlay.sharedMaterial.GetTexture("_BaseMap");
            var alphas = texture.GetPixels().Select(color => color.a).ToArray();
            var on = Capture("light-diagnostic-on.png", 1280, 800);
            overlay.enabled = false;
            var off = Capture("light-diagnostic-off.png", 1280, 800);
            overlay.enabled = true;
            int changed = 0, maxDelta = 0;
            double sum = 0;
            for (int i = 0; i < on.Length; i++)
            {
                int delta = Math.Abs(on[i].r - off[i].r) + Math.Abs(on[i].g - off[i].g) + Math.Abs(on[i].b - off[i].b);
                if (delta > 3) { changed++; sum += delta; }
                maxDelta = Math.Max(maxDelta, delta);
            }
            Debug.Log($"RRM LIGHT DIAGNOSTIC: scene={player.gameObject.scene.path}; sources={Object.FindObjectsByType<LightSource>().Length}; "
                + $"alpha={alphas.Min():F3}..{alphas.Max():F3}; changed={changed}; meanDelta={sum / Math.Max(1, changed):F2}; maxDelta={maxDelta}; "
                + $"west={LightSource.At(new Vector3(-5.1f, 0, -3.1f)):F2}; dark={LightSource.At(new Vector3(-2.7f, 0, 4.8f)):F2}; "
                + $"material={preview.unlitTemplate.name}; HUD={recorder.LightReadout}");
            SessionState.SetInt("RRM.PlayCheck.Lamps", 0);
            Finish(true, "RRM LIGHT DIAGNOSTIC COMPLETE");
        }

        private static void TickLampScenario(float age)
        {
            if (step == 0 && SessionState.GetInt("RRM.PlayCheck.Lamps", 0) == 5)
            {
                if (age < 0.8f) return;
                lampSources = Object.FindObjectsByType<LightSource>().Length;
                step = 600; since = Time.time;
            }
            if (step >= 600) { TickCameraBash(age); return; }
            if (step >= 420) { TickLampStrike(age); return; }
            var controller = player ? player.GetComponent<PlayerController>() : null;
            var attack = player ? player.GetComponent<MeleeAttack>() : null;
            if (step == 0 && age > 0.8f)
            {
                Require(Object.FindObjectsByType<PortableLamp>().Length == 3, "Three starting lamps");
                Require(controller.LeftHand.Item?.Source is CameraDevice && controller.RightHand.State == HandState.Empty,
                    "Camera stays left; right hand really empty");
                Require(!attack.weaponPivot.Find("Baton") && attack.weaponTip.localPosition.z < 0.6f, "No baton mesh or hidden baton reach");
                Require(GameObject.Find("Room Divider North") && GameObject.Find("Room Divider South"), "Both rooms preserved");
                Require(dummy.GetComponent<MeleeAttack>().damage == 60f, "Enemy damage untouched");
                lampSources = Object.FindObjectsByType<LightSource>().Length;
                Require(lampSources == 11, "Eight existing sources and three lamp sources");
                testLamp = GameObject.Find("Portable Lamp 1").GetComponent<PortableLamp>();
                StageRange(new Vector3(-3.3f, 0.02f, -4.1f), new Vector3(6.5f, 0.02f, 5f), false);
                player.rotation = Quaternion.identity;
                player.transform.rotation = player.rotation;
                LightPreviewProof();
                lightBefore = LightSource.At(LightProbe);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                step = 400; since = Time.time;
            }
            else if (step == 400 && age > 0.25f)
            {
                Require(controller.RightHand.Item?.Source == testLamp && testLamp.Owner == controller, "E picks same lamp into right hand");
                Require(!testLamp.TryPickup(controller, HandSide.Left), "Occupied hand cannot replace camera");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                StageRange(new Vector3(-1.5f, 0.02f, -2f), new Vector3(6.5f, 0.02f, 5f), false);
                player.rotation = Quaternion.Euler(0, 90, 0); player.transform.rotation = player.rotation;
                beforeLens = recorder.lens.position;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                Next();
            }
            else if (step == 401 && age > 0.95f && (player.position.x >= 1.3f || age > 2f))
            {
                Require(player.position.x > 0.9f && player.position.x < 2.4f, $"W carries lamp through actual doorway: {player.position}");
                Require(testLamp.transform.IsChildOf(player.transform) && recorder.lens.position.x > beforeLens.x + 2f, "Lamp and camera follow moving player");
                lightNear = LightSource.At(LightProbe);
                Require(lightNear > lightBefore + 20f, $"Fixed point brightens: {lightBefore:F1} -> {lightNear:F1}");
                Require(testLamp.lightSource.pointLight.enabled && Mathf.Abs(testLamp.lightSource.pointLight.range - testLamp.lightSource.radius) < 0.001f,
                    "Realtime light shares source range and active state");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Next();
            }
            else if (step == 402 && age > 0.25f)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                Next();
            }
            else if (step == 403 && age > 0.25f)
            {
                Require(!testLamp.Owner && controller.RightHand.State == HandState.Empty, "E places lamp and clears only right hand");
                lampPlacedPosition = testLamp.transform.position;
                Require(Mathf.Abs(lampPlacedPosition.y - 0.02f) < 0.08f, "Lamp sits on floor, not at hand height");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                StageRange(new Vector3(1.5f, 0.02f, -3f), new Vector3(1.5f, 0.02f, -2f), false);
                recorder.SetViewRotation(Quaternion.LookRotation(LightProbe - recorder.lens.position));
                Physics.SyncTransforms();
                RecordLightProbe();
                savedQuality = controller.HeldCamera.Events.Last().LightLevel;
                Capture("lamps-a-placed.png", 1280, 800);
                ReadLiveFrame("lamps-a-feed.png");
                Require(testLamp.transform.position == lampPlacedPosition, "Player can move independently of placed lamp");
                Require(testLamp.TryPickup(controller, HandSide.Right), "Same placed instance can be picked again");
                Next();
            }
            else if (step == 404 && age > 0.25f)
            {
                StageRange(new Vector3(-3.3f, 0.02f, -4.1f), new Vector3(1.5f, 0.02f, -2f), false);
                recorder.SetViewRotation(Quaternion.LookRotation(LightProbe - recorder.lens.position));
                Physics.SyncTransforms();
                float after = LightSource.At(LightProbe);
                Require(after < savedQuality - 20f && Mathf.Abs(after - lightBefore) < 0.1f, "Old position loses moved lamp contribution");
                RecordLightProbe();
                var records = controller.HeldCamera.Events;
                Require(Mathf.Abs(records[records.Count - 2].LightLevel - savedQuality) < 0.001f
                    && Mathf.Abs(records.Last().LightLevel - after) < 0.01f, "Historical quality unchanged; new damage uses new event-point light");
                Debug.Log($"RRM LAMP LIGHT: fixed point={LightProbe}, before={lightBefore:F2}, carried near={lightNear:F2}, placed event={savedQuality:F2}, removed/new event={after:F2}");
                Require(testLamp.TryPlace(controller), "Place after returning to left room");
                Require(controller.HeldCamera.TryPlace(controller), "Camera places normally");
                Require(!recorder.LiveTexture && !recorder.liveCamera.enabled && recorder.IsRecording, "Placed camera records privately, no remote feed");
                Require(testLamp.TryPickup(controller, HandSide.Left), "Same lamp fits free left hand");
                Require(testLamp.transform.localPosition.x < 0, "Lamp visibly mounted left");
                Require(!recorder.GetComponent<CameraDevice>().TryPickup(controller, HandSide.Left), "Camera cannot replace lamp in occupied hand");
                Require(recorder.GetComponent<CameraDevice>().TryPickup(controller, HandSide.Right), "Other hand can hold camera while left carries lamp");
                Capture("lamps-a-left-hand.png", 1280, 800);
                Require(testLamp.TryPlace(controller), "Left hand places lamp");
                StageRange(new Vector3(-6.5f, 0.02f, -3.8f), new Vector3(-6.75f, 0.02f, -3.12f), false);
                player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
                dummy.ResetHealth(); damageEvents = 0;
                recorder.SetViewRotation(Quaternion.LookRotation(dummyBody.position + Vector3.up - recorder.lens.position));
                itemTapReleased = false;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
                Next();
            }
            else if (step == 405 && age > 0.06f && !itemTapReleased)
            {
                Require(!attack.IsBusy, "Fist waits for release");
                itemTapReleased = true;
                InputSystem.QueueStateEvent(mouse, new MouseState());
            }
            else if (step == 405 && age > 1.35f)
            {
                Require(damageEvents == 1 && dummy.Health < 90f && dummy.Health >= 90f - attack.fistDamage,
                    $"Left click punch damages once, less than weapon: hits={damageEvents}, health={dummy.Health}");
                Require(!attack.IsBusy, "Short release does not queue another punch");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                Next();
            }
            else if (step == 406 && age > 0.25f)
            {
                Require(damageEvents == 1 && !attack.IsBusy, "Release is not another click");
                Require(recorder.LiveTexture && recorder.IsRecording, "Camera in other hand still renders and records");
                Require(Object.FindObjectsByType<LightSource>().Length == lampSources, "Repeated pickup never duplicates light sources");
                CheckLampPlacementEdges(controller);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                Next();
            }
            else if (step == 407 && age > 0.6f)
            {
                player = GameObject.Find("Player").GetComponent<Rigidbody>();
                controller = player.GetComponent<PlayerController>();
                recorder = Object.FindFirstObjectByType<CameraRecorder>();
                camera = Camera.main;
                Require(Object.FindObjectsByType<PortableLamp>().Length == 3 && Object.FindObjectsByType<LightSource>().Length == lampSources,
                    "Restart restores three lamps with no duplicate sources");
                Require(controller.RightHand.State == HandState.Empty && controller.LeftHand.Item?.Source is CameraDevice
                    && recorder.LiveTexture && recorder.RecordedHits == 0, "Restart restores empty right hand and fresh left camera/feed");
                Capture("lamps-a-restart.png", 1280, 800);
                if (SessionState.GetInt("RRM.PlayCheck.Lamps", 0) >= 3)
                {
                    dummy = GameObject.Find("Dummy").GetComponent<Damageable>();
                    dummyBody = dummy.GetComponent<Rigidbody>();
                    damageEvents = 0; dummy.Damaged += ObserveHit;
                    step = 420; since = Time.time;
                    Debug.Log("RRM LAMPS A CHECKPOINT PASSED; starting B.");
                    return;
                }
                SessionState.SetInt("RRM.PlayCheck.Lamps", 0);
                Finish(true, "RRM LAMPS A PLAY CHECK PASSED: native input/doorway/fists, either hand, support, light/quality history, feed/privacy and restart.");
            }
        }

        private static void TickLampStrike(float age)
        {
            var controller = player ? player.GetComponent<PlayerController>() : null;
            var attack = player ? player.GetComponent<MeleeAttack>() : null;
            if (step == 420 && age > 0.2f)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                StageRange(new Vector3(-3.3f, 0.02f, -4.1f), new Vector3(-2.75f, 0.02f, -3.48f), false);
                player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
                testLamp = GameObject.Find("Portable Lamp 1").GetComponent<PortableLamp>();
                otherLamp = GameObject.Find("Portable Lamp 2");
                Require(testLamp.TryPickup(controller, HandSide.Right), "B starts with lamp in right hand");
                Require(controller.HeldCamera.TryPlace(controller), "Place witness camera before fight");
                recorder.transform.SetPositionAndRotation(new Vector3(-3.3f, 1.25f, -5.5f), Quaternion.identity);
                InputSystem.QueueStateEvent(mouse, new MouseState());
                damageEvents = 0;
                Next();
            }
            else if (step == 421 && age > 0.4f)
            {
                Require(testLamp && damageEvents == 0 && dummy.Health == 90, "Carrying against a character does not damage or consume lamp");
                dummyBody.position = new Vector3(6f, 0.02f, 5f); dummyBody.transform.position = dummyBody.position;
                Physics.SyncTransforms();
                itemTapReleased = false;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                Next();
            }
            else if (step == 422 && age > 0.06f && !itemTapReleased)
            {
                Require(!attack.IsBusy, "Lamp waits for short release, not press edge");
                itemTapReleased = true;
                InputSystem.QueueStateEvent(mouse, new MouseState());
            }
            else if (step == 422 && age > 0.25f)
            {
                Require(attack.IsBusy && !attack.TryHandAttack(controller.LeftHand), "Other empty hand cannot bypass shared attack cadence");
                Capture("lamps-b-swing.png", 1280, 800);
                Next();
            }
            else if (step == 423 && age > 1.3f)
            {
                Require(testLamp && testLamp.Owner == controller && damageEvents == 0 && !attack.IsBusy, "Short-click miss keeps same lamp without repeating");
                Require(testLamp.lightSource.pointLight.enabled && testLamp.transform.localPosition.y > 0.8f,
                    "Miss restores lit hand pose");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                Next();
            }
            else if (step == 424 && age > 0.2f)
            {
                Require(!attack.IsBusy && damageEvents == 0, "Release after miss is not a strike");
                StageRange(new Vector3(-3.3f, 0.02f, -4.1f), new Vector3(-2.95f, 0.02f, -3.28f), false);
                player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
                recorder.transform.SetPositionAndRotation(new Vector3(-3.3f, 1.25f, -5.5f), Quaternion.identity);
                dummy.ResetHealth();
                lampHitCount = 0;
                dummy.Damaged += InspectLampContact;
                itemTapReleased = false;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                Next();
            }
            else if (step == 425 && age > 0.06f && !itemTapReleased)
            {
                itemTapReleased = true;
                InputSystem.QueueStateEvent(mouse, new MouseState());
            }
            else if (step == 425 && age > 1.5f)
            {
                dummy.Damaged -= InspectLampContact;
                Require(lampHitCount == 1 && !testLamp && controller.RightHand.State == HandState.Empty,
                    $"First confirmed hit consumes lamp once: hits={lampHitCount}");
                Require(lampLitDuringDamage && lampHitDamage > attack.fistDamage, "Damage callback sees intact light and greater lamp damage");
                Require(recorder.RecordedHits == 1 && Mathf.Abs(recorder.GetComponent<CameraDevice>().Events.Last().LightLevel - lampHitLight) < 0.01f,
                    "Placed witness snapshots contact light before break");
                Require(lampHitContribution > 20f && LightSource.At(lampHitPoint) < lampHitLight - 10f,
                    "Destroyed lamp removes actual contribution immediately after recorded impact");
                Require(Object.FindObjectsByType<LightSource>().Length == lampSources - 1
                    && Object.FindObjectsByType<PortableLamp>().Length == 2, "No invisible source or reusable broken lamp");
                Require(GameObject.Find("Portable Lamp 2") == otherLamp
                    && Object.FindObjectsByType<PortableLamp>().All(lamp => lamp.lightSource.pointLight.enabled), "Other lamps unaffected");
                Require(!recorder.LiveTexture && recorder.IsRecording && !attack.IsBusy, "Placed camera stays private; recovery completes");
                Debug.Log($"RRM LAMP HIT: damage={lampHitDamage:F2}; recorded light={lampHitLight:F2}; lamp contribution={lampHitContribution:F2}; after break={LightSource.At(lampHitPoint):F2}");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                Next();
            }
            else if (step == 426 && age > 0.2f)
            {
                Require(lampHitCount == 1 && !attack.IsBusy, "Release after consumed lamp never triggers a punch");
                StageRange(new Vector3(-3.3f, 0.02f, -4.1f), new Vector3(-3.05f, 0.02f, -3.42f), false);
                player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
                recorder.transform.SetPositionAndRotation(new Vector3(-3.88f, 1.25f, -3.82f), Quaternion.identity);
                Require(recorder.GetComponent<CameraDevice>().TryPickup(controller, HandSide.Left), "Camera returns to other hand after lamp breaks");
                beforeHealth = dummy.Health; damageEvents = 0;
                itemTapReleased = false;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                Next();
            }
            else if (step == 427 && age > 0.06f && !itemTapReleased)
            {
                itemTapReleased = true;
                InputSystem.QueueStateEvent(mouse, new MouseState());
            }
            else if (step == 427 && age > 1.35f)
            {
                Require(damageEvents == 1 && beforeHealth - dummy.Health > 0 && beforeHealth - dummy.Health <= attack.fistDamage,
                    $"Next right click is short fist, not lingering lamp/weapon damage: {beforeHealth - dummy.Health:F2}");
                Require(recorder.LiveTexture && controller.LeftHand.Item?.Source is CameraDevice, "Other-hand camera remains usable");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                Next();
            }
            else if (step == 428 && age > 0.6f)
            {
                player = GameObject.Find("Player").GetComponent<Rigidbody>();
                controller = player.GetComponent<PlayerController>();
                recorder = Object.FindFirstObjectByType<CameraRecorder>(); camera = Camera.main;
                Require(Object.FindObjectsByType<PortableLamp>().Length == 3 && Object.FindObjectsByType<LightSource>().Length == lampSources,
                    "Restart restores consumed lamp exactly once");
                Require(controller.LeftHand.Item?.Source is CameraDevice && controller.RightHand.State == HandState.Empty
                    && recorder.RecordedHits == 0 && recorder.LiveTexture, "Restart restores original equipment and camera history");
                Capture("lamps-b-restart.png", 1280, 800);
                if (SessionState.GetInt("RRM.PlayCheck.Lamps", 0) == 4)
                {
                    dummy = GameObject.Find("Dummy").GetComponent<Damageable>();
                    dummyBody = dummy.GetComponent<Rigidbody>();
                    dummy.Damaged += ObserveHit;
                    step = 600; since = Time.time;
                    Debug.Log("RRM LAMPS A+B CHECKPOINT PASSED; starting camera bash.");
                    return;
                }
                SessionState.SetInt("RRM.PlayCheck.Lamps", 0);
                Finish(true, "RRM LAMPS A+B PLAY CHECK PASSED: one-use confirmed hit, lit event ordering, miss/hold/release, shared cadence, fist fallback, other lamps and restart.");
            }
        }

        private static IEnumerator<float> CheckContextualHands()
        {
            var controller = player.GetComponent<PlayerController>();
            var attack = player.GetComponent<MeleeAttack>();
            var device = controller.HeldCamera;
            Require(Mathf.Abs(controller.handHoldThreshold - 0.22f) < 0.001f, "Default hand threshold is 0.22 s");
            Require(Mathf.Abs(attack.windup + attack.strike + attack.recovery - 1f) < 0.01f,
                "One shared second of attack phases, no extra cooldown");
            GameObject.Find("Portable Lamp 1").transform.position = new Vector3(-6.8f, 0.02f, 4f);
            StageRange(new Vector3(-3.3f, 0.02f, -4.1f), new Vector3(-3.05f, 0.02f, -3.42f), false);
            player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
            controller.SendMessage("OnApplicationFocus", true);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.1f;

            controller.handHoldThreshold = 0.4f;
            Quaternion before = recorder.transform.localRotation;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.12f;
            AimCamera(new Vector2(100f, 0));
            yield return 0.12f;
            Require(Quaternion.Angle(before, recorder.transform.localRotation) < 0.1f && !attack.IsBusy,
                "Mouse motion does not bypass configurable hold threshold");
            yield return 0.22f;
            AimCamera(new Vector2(180f / recorder.mouseSensitivity, 0));
            yield return 0.08f;
            Require(Quaternion.Angle(before, recorder.transform.localRotation) > 170f && !attack.IsBusy,
                "Camera left: real aim starts after configured threshold, not an attack");
            controller.handHoldThreshold = 0.22f;
            before = recorder.transform.localRotation;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
            yield return 0.45f;
            Require(!attack.IsBusy && damageEvents == 0, "Empty right hold has no hidden punch");
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.1f;
            Require(!attack.IsBusy, "Releasing empty-hand hold never punches");
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
            yield return 0.06f;
            Require(!attack.IsBusy, "Empty right waits for short release");
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 1.3f;
            Require(damageEvents == 1 && dummy.Health < 90 && dummy.Health >= 90 - attack.fistDamage && !attack.IsBusy,
                "Right fist damages a multi-Hurtbox target exactly once while left camera aims backward");
            Require(Quaternion.Angle(before, recorder.transform.localRotation) < 0.1f && device.RecordedHits == 0,
                "Fist follows body, not rear-facing camera; aim remains independent");
            AimCamera(new Vector2(30, 0));
            yield return 0.08f;
            Require(Quaternion.Angle(before, recorder.transform.localRotation) > 2, "Camera still aims after opposite-hand attack");
            ReadLiveFrame("contextual-hands-left-feed.png");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.3f;
            Require(device.State == CameraDeviceState.Held && !attack.IsBusy && damageEvents == 1,
                "Aim release neither bashes nor repeats fist");

            // Both placement and pickup must cancel the original press of the affected hand.
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.04f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return 0.05f;
            Require(controller.LeftHand.State == HandState.Empty && device.State == CameraDeviceState.Placed && !recorder.LiveTexture,
                "Q places left camera without duplicating it or providing remote feed");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.06f;
            Require(!attack.IsBusy, "Releasing a placed item's old press cannot punch with empty hand");
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.04f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return 0.06f;
            Require(controller.RightHand.Item?.Source == device && controller.LeftHand.State == HandState.Empty
                && device.transform.localPosition.x > 0 && recorder.LiveTexture,
                "E picks the same device visibly into the right hand");
            before = recorder.transform.localRotation;
            yield return 0.3f;
            AimCamera(new Vector2(100, 0));
            yield return 0.08f;
            Require(Quaternion.Angle(before, recorder.transform.localRotation) < 0.1f && !attack.IsBusy,
                "Old empty-hand press cannot aim newly picked camera");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.08f;
            Require(!attack.IsBusy && device.State == CameraDeviceState.Held, "Pickup cancellation also suppresses release bash");

            StageRange(new Vector3(-3.3f, 0.02f, -4.1f), new Vector3(-3.55f, 0.02f, -3.42f), false);
            player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
            damageEvents = 0; dummy.ResetHealth();
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.27f;
            before = recorder.transform.localRotation;
            AimCamera(new Vector2(30, 0));
            yield return 0.08f;
            Require(Quaternion.Angle(before, recorder.transform.localRotation) > 2 && !attack.IsBusy,
                "Fresh RMB hold aims right-hand camera");
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
            yield return 0.35f;
            Require(!attack.IsBusy, "Empty left hold also reserves grab without attacking");
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.06f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
            yield return 0.06f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 1.3f;
            Require(damageEvents == 1 && dummy.Health < 90 && dummy.Health >= 90 - attack.fistDamage && !attack.IsBusy,
                "Left release punches once while right camera remains aimed");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.25f;
            Require(!attack.IsBusy && device.State == CameraDeviceState.Held, "Right aim release is not CameraBash");

            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.04f;
            controller.SendMessage("OnApplicationFocus", false);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.04f;
            controller.SendMessage("OnApplicationFocus", true);
            yield return 0.25f;
            Require(!attack.IsBusy && damageEvents == 1, "Focus loss cancels pending fist, not a release attack");
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.27f;
            controller.SendMessage("OnApplicationFocus", false);
            yield return 0.03f;
            controller.SendMessage("OnApplicationFocus", true);
            before = recorder.transform.localRotation;
            AimCamera(new Vector2(100, 0));
            yield return 0.3f;
            Require(Quaternion.Angle(before, recorder.transform.localRotation) < 0.1f,
                "Focus return cannot resume old hold; a new press is required");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.1f;
            Require(!attack.IsBusy && damageEvents == 1, "Cancelled camera hold has no release bash");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return 0.1f;
            Require(controller.LeftHand.State == HandState.Empty && controller.RightHand.State == HandState.Empty,
                "E places right camera; two empty hands remain");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            StageRange(new Vector3(-3.3f, 0.02f, -4.1f), new Vector3(6, 0.02f, 5), false);
            damageEvents = 0;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.06f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.05f;
            Require(attack.Phase == MeleeAttack.AttackPhase.Windup && attack.weaponPivot.localPosition.x < 0,
                "Left short release starts shared windup from left hand");
            for (int i = 0; i < 4; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = (ushort)(i % 2 == 0 ? 2 : 1) });
                yield return 0.04f;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return 0.04f;
                Require(attack.IsBusy && attack.weaponPivot.localPosition.x < 0, "Alternating buttons cannot replace/restart active attack");
            }
            // Long configured tap spans recovery: its press occurred while busy, so it must be discarded.
            controller.handHoldThreshold = 2f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.8f;
            Require(!attack.IsBusy, "Miss finishes the existing phases without an extra cooldown");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.1f;
            Require(!attack.IsBusy && damageEvents == 0, "Busy press released after recovery does not queue a future attack");
            controller.handHoldThreshold = 0.22f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.06f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.08f;
            Require(attack.Phase == MeleeAttack.AttackPhase.Windup && attack.weaponPivot.localPosition.x > 0,
                "Fresh right click works immediately after shared recovery");
            yield return 1.3f;
            Require(!attack.IsBusy && damageEvents == 0, "Miss neither repeats nor generates damage");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.7f;
            player = GameObject.Find("Player").GetComponent<Rigidbody>();
            dummy = GameObject.Find("Dummy").GetComponent<Damageable>();
            dummyBody = dummy.GetComponent<Rigidbody>(); dummy.Damaged += ObserveHit;
            recorder = Object.FindFirstObjectByType<CameraRecorder>(); camera = Camera.main;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Require(Object.FindObjectsByType<CameraDevice>().Length == 1 && Object.FindObjectsByType<PortableLamp>().Length == 3
                && player.GetComponent<PlayerController>().HeldCamera.State == CameraDeviceState.Held,
                "Restart restores original items without duplicates");
        }

        private static CameraDevice bashDevice;
        private static CameraRecorder bashWitness, blindBashWitness;
        private static Quaternion bashRestRotation;
        private static Vector3 bashRestPosition;
        private static int bashHits, brokenHistoryCount, brokenBloodCount;
        private static DamageEvent bashHit;
        private static float bashLight, witnessWeight, deviceWeight;
        private static bool cameraIntactDuringHit;

        private static void TickCameraBash(float age)
        {
            var controller = player ? player.GetComponent<PlayerController>() : null;
            var attack = player ? player.GetComponent<MeleeAttack>() : null;
            if (step == 600 && age > 0.3f)
            {
                // Keep pickup unambiguous: the starting lamp is normally nearer than a placed camera.
                GameObject.Find("Portable Lamp 1").transform.position = new Vector3(-6.8f, 0.02f, 4f);
                StageRange(new Vector3(-3.3f, 0.02f, -4.1f), new Vector3(-3.65f, 0.02f, -3.28f), false);
                player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
                bashDevice = controller.HeldCamera;
                deviceId = bashDevice.Id;
                damageEvents = 0;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                Next();
            }
            else if (step == 601 && age > 0.3f)
            {
                Require(damageEvents == 0 && !attack.IsBusy, "Carrying a camera beside a target is not an attack");
                bashRestRotation = recorder.transform.localRotation;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
                Next();
            }
            else if (step == 602 && age > 0.26f)
            {
                AimCamera(new Vector2(50f, 10f));
                Next();
            }
            else if (step == 603 && age > 0.06f)
            {
                float turn = Quaternion.Angle(bashRestRotation, recorder.transform.localRotation);
                Require(!attack.IsBusy && turn > 2f,
                    $"Camera hold aims without starting a bash: turn={turn:F2}, phase={attack.Phase}");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                Next();
            }
            else if (step == 604 && age > 0.3f)
            {
                Require(bashDevice.State == CameraDeviceState.Held && !attack.IsBusy && damageEvents == 0,
                    "Releasing camera hold never attacks or destroys camera");
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
                Next();
            }
            else if (step == 605 && age > 0.35f)
            {
                Require(!attack.IsBusy && damageEvents == 0, "Long stationary hold does not start an attack");
                ReadLiveFrame("camera-bash-aim-feed.png");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                Next();
            }
            else if (step == 606 && age > 0.3f)
            {
                Require(!attack.IsBusy && bashDevice.ReportWeight == 0 && damageEvents == 0, "Aim release grants no attack or report weight");
                dummyBody.position = new Vector3(6f, 0.02f, 5f); dummyBody.transform.position = dummyBody.position;
                Physics.SyncTransforms();
                bashRestPosition = recorder.transform.localPosition;
                bashRestRotation = recorder.transform.localRotation;
                itemTapReleased = false;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
                Next();
            }
            else if (step == 607 && age > 0.06f && !itemTapReleased)
            {
                itemTapReleased = true;
                InputSystem.QueueStateEvent(mouse, new MouseState());
            }
            else if (step == 607 && age > 0.25f)
            {
                Require(attack.IsUsingCamera(bashDevice) && Vector3.Distance(recorder.transform.localPosition, bashRestPosition) > 0.1f,
                    "Left short click swings the actual camera using existing melee windup");
                Capture("camera-bash-swing.png", 1280, 800);
                Next();
            }
            else if (step == 608 && age > 1.3f)
            {
                Require(!attack.IsBusy && bashDevice.State == CameraDeviceState.Held && bashDevice.Id == deviceId
                    && bashDevice.ReportWeight == 0 && damageEvents == 0 && recorder.LiveTexture,
                    "Camera miss keeps same working device and grants no weight");
                Require(Vector3.Distance(recorder.transform.localPosition, bashRestPosition) < 0.001f
                    && Quaternion.Angle(recorder.transform.localRotation, bashRestRotation) < 0.1f,
                    "Miss restores hand pose and previous body-relative camera aim");
                StageRange(new Vector3(-3.3f, 0.02f, -4.1f), new Vector3(-3.05f, 0.02f, -3.42f), false);
                player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
                recorder.SetViewRotation(Quaternion.LookRotation(dummyBody.position + Vector3.up - recorder.lens.position));
                bashWitness = CloneBashWitness("Visible Bash Witness", false);
                blindBashWitness = CloneBashWitness("Blind Bash Witness", true);
                itemTapReleased = false;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                Next();
            }
            else if (step == 609 && age > 0.06f && !itemTapReleased)
            {
                itemTapReleased = true;
                InputSystem.QueueStateEvent(mouse, new MouseState());
            }
            else if (step == 609 && age > 1.35f)
            {
                Require(damageEvents == 1 && bashDevice.RecordedHits == 1 && bashDevice.ReportWeight > 0,
                    "A real other-hand fist hit creates the camera's pre-break history");
                Require(bashWitness.RecordedHits == 1 && blindBashWitness.RecordedHits == 0,
                    "Each placed witness keeps only events actually inside its view");
                deviceWeight = bashDevice.ReportWeight;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                Next();
            }
            else if (step == 610 && age > 0.25f)
            {
                Require(bashDevice.State == CameraDeviceState.Placed && !recorder.LiveTexture && recorder.IsRecording,
                    "Q places the same device, preserving private recording");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                Next();
            }
            else if (step == 611 && age > 0.25f)
            {
                Require(controller.RightHand.Item?.Source == bashDevice && bashDevice.Id == deviceId && bashDevice.RecordedHits == 1
                    && bashDevice.ReportWeight == deviceWeight && recorder.LiveTexture,
                    $"E picks same camera into right hand without changing history/weight: right={controller.RightHand.Item?.Source}, state={bashDevice.State}, hits={bashDevice.RecordedHits}");
                StageRange(new Vector3(-3.3f, 0.02f, -4.1f), new Vector3(-2.95f, 0.02f, -3.28f), false);
                player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
                dummy.ResetHealth(); damageEvents = bashHits = 0;
                bashDevice.hitDamage = 73f;
                bashDevice.bashWeight = 117f;
                witnessWeight = bashWitness.GetComponent<CameraDevice>().ReportWeight;
                dummy.Damaged += InspectCameraContact;
                itemTapReleased = false;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                Next();
            }
            else if (step == 612 && age > 0.06f && !itemTapReleased)
            {
                itemTapReleased = true;
                InputSystem.QueueStateEvent(mouse, new MouseState());
            }
            else if (step == 612 && age > 1.35f)
            {
                dummy.Damaged -= InspectCameraContact;
                Require(bashHits == 1 && damageEvents == 1 && cameraIntactDuringHit && bashHit.Kind == DamageKind.CameraBash
                    && bashHit.BaseWeight == 117f && bashHit.Amount > attack.fistDamage, "One strong CameraBash event occurs before destruction");
                Require(bashDevice.State == CameraDeviceState.Broken && !bashDevice.Owner && controller.RightHand.State == HandState.Empty
                    && !recorder.IsRecording && !recorder.LiveTexture && !recorder.liveCamera.enabled,
                    "Confirmed right-hand camera hit empties the hand and stops feed/recording");
                Require(bashDevice.GetComponentsInChildren<Renderer>().All(renderer => !renderer.enabled)
                    && !bashDevice.TryPickup(controller, HandSide.Left) && !bashDevice.TryPickup(controller, HandSide.Right),
                    "Broken housing/cone are hidden and device cannot be reused or picked up");
                Require(bashDevice.Id == deviceId && bashDevice.RecordedHits >= 1 && bashDevice.ReportWeight == 0,
                    "Broken device retains diagnostic history but contributes zero report weight");
                var witness = bashWitness.GetComponent<CameraDevice>();
                var recorded = witness.Events.Last();
                Require(witness.RecordedCameraBashes == 1 && recorded.Damage.Kind == DamageKind.CameraBash
                    && recorded.Damage.BaseWeight == 117f && Mathf.Abs(recorded.LightLevel - bashLight) < 0.01f
                    && Mathf.Abs(witness.ReportWeight - witnessWeight - 117f * recorded.Clarity) < 0.01f,
                    "Other visible camera keeps CameraBash weight with contact-time clarity after used camera breaks");
                Require(blindBashWitness.GetComponent<CameraDevice>().RecordedCameraBashes == 0
                    && blindBashWitness.GetComponent<CameraDevice>().ReportWeight == 0,
                    "A camera facing away receives no CameraBash bonus");
                Require(!bashWitness.LiveTexture && bashWitness.IsRecording, "Witness stays passive without remote feed");
                brokenHistoryCount = bashDevice.RecordedHits;
                brokenBloodCount = bashDevice.BloodEvents.Count;
                witnessWeight = witness.ReportWeight;
                bashDevice.bashWeight = 900f;
                Require(witness.ReportWeight == witnessWeight, "Changing the used device cannot rewrite another camera's historical weight");
                Debug.Log($"RRM CAMERA BASH: damage={bashHit.Amount:F2}; base weight={recorded.Damage.BaseWeight:F2}; light={bashLight:F2}; witness weight={witnessWeight:F2}; broken weight={bashDevice.ReportWeight:F2}");
                // Health/positions only stage the fixture; the fatal event must still come from a real fist command.
                StageRange(new Vector3(-3.3f, 0.02f, -4.1f), new Vector3(-3.05f, 0.02f, -3.42f), false);
                player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
                dummy.maxHealth = 1f; dummy.ResetHealth(); damageEvents = 0;
                itemTapReleased = false;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                Next();
            }
            else if (step == 613 && age > 0.06f && !itemTapReleased)
            {
                itemTapReleased = true;
                InputSystem.QueueStateEvent(mouse, new MouseState());
            }
            else if (step == 613 && age > 1.35f)
            {
                Require(damageEvents == 1 && dummy.IsDead && !attack.IsBusy && !controller.HeldCamera,
                    "After recovery the same button performs a fatal fist hit, without any working held camera");
                Require(bashDevice.RecordedHits == brokenHistoryCount && bashDevice.BloodEvents.Count == brokenBloodCount,
                    "Broken device collects neither new damage nor blood records");
                Require(recorder.ReportReady && recorder.ReportText.Contains("BROKEN") && bashDevice.ReportWeight == 0
                    && bashWitness.ReportReady && bashWitness.GetComponent<CameraDevice>().DeathRecorded
                    && bashWitness.GetComponent<CameraDevice>().ReportWeight > witnessWeight,
                    "Existing report distinguishes broken diagnostics from the surviving witness's evidence and weight");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
                Next();
            }
            else if (step == 614 && age > 0.2f)
            {
                Require(recorder.ControlsVisible, "F1 remains available even after player's camera breaks");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                Next();
            }
            else if (step == 615 && age > 0.6f)
            {
                player = GameObject.Find("Player").GetComponent<Rigidbody>();
                controller = player.GetComponent<PlayerController>();
                recorder = Object.FindFirstObjectByType<CameraRecorder>();
                Require(!bashDevice && !bashWitness && !blindBashWitness && Object.FindObjectsByType<CameraDevice>().Length == 1
                    && Object.FindObjectsByType<PortableLamp>().Length == 3 && Object.FindObjectsByType<LightSource>().Length == lampSources,
                    "R clears broken/temporary cameras and restores original items without duplicates");
                Require(controller.LeftHand.Item?.Source is CameraDevice fresh && fresh.State == CameraDeviceState.Held
                    && fresh.Id != deviceId && fresh.RecordedHits == 0 && fresh.ReportWeight == 0 && recorder.LiveTexture
                    && controller.RightHand.State == HandState.Empty, "Restart restores fresh working camera and original hands");
                SessionState.SetInt("RRM.PlayCheck.Lamps", 0);
                Finish(true, "RRM ITEM BASH PLAY CHECK PASSED: lamp ordering/light, camera aim/taps/miss, both hands, CameraBash witnesses, broken zero result, fist fallback and restart.");
            }
        }

        private static CameraRecorder CloneBashWitness(string name, bool blind)
        {
            var clone = Object.Instantiate(recorder.gameObject, null);
            clone.name = name;
            clone.transform.SetPositionAndRotation(new Vector3(-3.3f, 1.25f, -5.5f), Quaternion.identity);
            var witness = clone.GetComponent<CameraRecorder>();
            witness.SetViewRotation(Quaternion.LookRotation(dummyBody.position + Vector3.up - witness.lens.position));
            if (blind) clone.transform.Rotate(0f, 180f, 0f, Space.World);
            Require(witness.IsPlaced && witness.IsRecording && !witness.LiveTexture && witness.RecordedHits == 0,
                "Temporary full camera clone starts as a fresh independent placed witness");
            return witness;
        }

        private static void InspectCameraContact(DamageEvent hit)
        {
            bashHits++;
            bashHit = hit;
            bashLight = LightSource.At(hit.Point);
            cameraIntactDuringHit = bashDevice.State == CameraDeviceState.Held && recorder.IsRecording
                && bashDevice.Owner && recorder.LiveTexture;
        }

        private static void InspectLampContact(DamageEvent hit)
        {
            lampHitCount++;
            lampHitPoint = hit.Point;
            lampHitLight = LightSource.At(hit.Point);
            lampHitContribution = testLamp.lightSource.ContributionAt(hit.Point);
            lampHitDamage = hit.Amount;
            lampLitDuringDamage = testLamp && testLamp.Owner && testLamp.lightSource.isActiveAndEnabled
                && testLamp.lightSource.pointLight.enabled;
        }

        private static void RecordLightProbe()
        {
            Require(recorder.CanSee(LightProbe), "Camera geometrically sees test event");
            int before = recorder.RecordedHits;
            Hurtbox torso = dummy.GetComponentsInChildren<Hurtbox>().First(zone => zone.part == BodyPart.Torso);
            Require(dummy.ApplyDamage(player.gameObject, torso, LightProbe, Vector3.forward, 1, 0), "Apply test damage through existing Damageable");
            Require(recorder.RecordedHits == before + 1, "Recorder receives same DamageEvent");
        }

        private static void LightPreviewProof()
        {
            var preview = Object.FindFirstObjectByType<LightLevelPreview>();
            Require(preview.unlitTemplate.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), "Transparent variant exists in saved material");
            var overlay = preview.transform.Find("Light Level Preview").GetComponent<MeshRenderer>();
            var alphas = ((Texture2D)overlay.sharedMaterial.GetTexture("_BaseMap")).GetPixels().Select(color => color.a);
            Require(alphas.Min() < 0.01f && alphas.Max() > 0.17f, "Live light map has clear and dark areas");
            Capture("lamps-a-light.png", 1280, 800);
        }

        private static void CheckLampPlacementEdges(PlayerController controller)
        {
            // Staged geometry cases use real room colliders, never change saved scene geometry.
            testLamp.transform.position = new Vector3(0.4f, 0.02f, 1f);
            player.position = new Vector3(-0.6f, 0.02f, 1f); player.transform.position = player.position;
            player.rotation = Quaternion.Euler(0, 180, 0); player.transform.rotation = player.rotation;
            Physics.SyncTransforms();
            Require(!testLamp.TryPickup(controller, HandSide.Left), "Divider rejects pickup through wall");
            player.position = new Vector3(-4.8f, 0.02f, -1.55f); player.transform.position = player.position;
            player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
            float coverTop = GameObject.Find("Low Cover").GetComponent<Collider>().bounds.max.y;
            testLamp.transform.position = new Vector3(-5.35f, coverTop + 0.02f, -0.9f);
            Physics.SyncTransforms();
            Require(testLamp.TryPickup(controller, HandSide.Left) && testLamp.TryPlace(controller), "Lamp picks and places on low cover");
            Require(Mathf.Abs(testLamp.transform.position.y - coverTop - 0.02f) < 0.01f, "Lamp supported by cover top");
        }
    }
}
