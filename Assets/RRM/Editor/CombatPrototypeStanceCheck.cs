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
        public static void RunStanceAndExit()
        {
            SessionState.SetInt("RRM.PlayCheck.Lamps", 15);
            RunAndExit();
        }

        private static IEnumerable<float> SpaceTap(bool twice = false)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.04f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.05f;
            if (twice)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return 0.04f;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }
            yield return 0.3f;
        }

        private static IEnumerator<float> CheckStanceAndDragging()
        {
            Vector3 start = new Vector3(-3.3f, 0.02f, -3.4f);
            StageGrapple(start, start + Vector3.forward * 3f);
            var human = player.GetComponent<PlayerController>();
            var enemy = dummy.GetComponent<PlayerController>();
            var shape = player.GetComponent<CapsuleCollider>();
            var device = human.HeldCamera;
            CharCrafterIntegration.Validate(player.gameObject);
            CharCrafterIntegration.Validate(dummy.gameObject);
            yield return 0.15f;
            float standingLens = recorder.lens.position.y;
            Quaternion cameraAngle = recorder.transform.localRotation;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.05f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.08f;
            Require(human.Stance == PlayerController.BodyStance.Standing, "Single tap waits, no transient crouch before double detection");
            yield return 0.22f;
            Require(human.IsCrouching && shape.height < 1.2f && recorder.lens.position.y < standingLens - 0.2f
                && Quaternion.Angle(cameraAngle, recorder.transform.localRotation) < 0.1f,
                "Single Space lowers capsule, CharCrafter wrists and held camera without changing aim");
            Capture("stance-crouch.png", 1280, 800);
            foreach (float wait in SpaceTap()) yield return wait;
            Require(human.Stance == PlayerController.BodyStance.Standing, "Crouch single tap returns to standing");
            foreach (float wait in SpaceTap(true)) yield return wait;
            Require(human.IsProne && shape.direction == 2 && shape.bounds.size.y < 0.65f,
                "Double Space makes a low horizontal physical capsule");
            Require(player.transform.localScale == Vector3.one && recorder.lens.position.y < 0.65f
                && recorder.LiveTexture && Quaternion.Angle(cameraAngle, recorder.transform.localRotation) < 0.1f,
                "Prone keeps root scale and lens direction; live handheld camera follows low wrist");
            Capture("stance-prone.png", 1280, 800);
            ReadLiveFrame("stance-prone-feed.png");
            Require(!human.TryBeginGrapple(HandSide.Left, enemy), "Prone cannot begin a grapple");
            foreach (float wait in SpaceTap()) yield return wait;
            Require(human.IsCrouching, "Prone single tap rises to crouch");
            foreach (float wait in SpaceTap()) yield return wait;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            yield return 0.35f;
            Require(human.Stance == PlayerController.BodyStance.Standing, "Legacy Ctrl no longer controls stance");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.05f;
            foreach (var stance in new[] { PlayerController.BodyStance.Standing, PlayerController.BodyStance.Crouching, PlayerController.BodyStance.Prone })
            {
                Require(human.TrySetStance(stance), "Shared stance command accepts clear space: " + stance);
                player.position = start; player.transform.position = start;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
                yield return 0.45f;
                float speed = Vector3.ProjectOnPlane(player.linearVelocity, Vector3.up).magnitude;
                float expected = human.moveSpeed * (stance == PlayerController.BodyStance.Prone ? human.crawlSpeedScale
                    : stance == PlayerController.BodyStance.Crouching ? human.crouchSpeedScale : 1f);
                Require(speed > expected - 0.65f && speed > 0.2f && speed <= expected + 0.1f && Mathf.Abs(player.position.y) < 0.08f,
                    $"Screen movement {stance}: {speed:F2}/{expected:F2}, no upward impulse");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return 0.15f;
            }
            player.position = start; player.transform.position = start;
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Temporary crawl clearance";
            roof.transform.position = start + new Vector3(0, 0.85f, 1.25f);
            roof.transform.localScale = new Vector3(1.8f, 0.25f, 1.4f);
            Physics.SyncTransforms();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return 2.4f;
            Require(player.position.z > start.z + 0.8f && shape.bounds.max.y < roof.GetComponent<Collider>().bounds.min.y,
                "Actual crawl motor enters below supported clearance, without moving root through geometry");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.1f;
            foreach (float wait in SpaceTap()) yield return wait;
            Require(human.IsProne && !human.TrySetStance(PlayerController.BodyStance.Standing), "Ceiling blocks both prone-to-crouch and standing");
            Capture("stance-under-cover.png", 1280, 800);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
            yield return 2.8f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.1f;
            foreach (float wait in SpaceTap()) yield return wait;
            Require(human.IsCrouching, "Crawl exits and can rise when clear");
            roof.transform.position = player.position + Vector3.up * 1.4f;
            Physics.SyncTransforms();
            foreach (float wait in SpaceTap()) yield return wait;
            Require(human.IsCrouching, "Crouch-to-standing stays crouched under a low ceiling");
            Object.Destroy(roof);
            yield return 0.1f;
            foreach (float wait in SpaceTap()) yield return wait;
            Require(human.Stance == PlayerController.BodyStance.Standing, "Standing restored after leaving overhead obstacle");
            player.position += Vector3.up * 1.5f; player.transform.position = player.position;
            yield return 1f;
            Require(human.IsGrounded && Mathf.Abs(player.position.y) < 0.08f, "Gravity and landing still work without any jump command");
            Debug.Log("RRM STANCE CHECK: delayed taps, double taps, speed, collider, wrist, ceiling, crawl passage and falling passed");

            PrepareSeverHealth();
            PositionSeverFight(false, 0.72f);
            yield return 0.1f;
            recorder.SetViewRotation(Quaternion.LookRotation(dummyBody.position + Vector3.up - recorder.lens.position));
            yield return 0.1f;
            foreach (float wait in SeverSwing(human, ChopIntent.Normal)) yield return wait;
            Require(severEvents.Count == 1 && device.RecordedHits > 0 && dummy.GetComponent<BloodEvidence>(),
                "Real hand strike produces damage and a witnessed recording");
            string id = device.Id;
            int records = device.Events.Count;
            var recorded = device.Events[0];
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return 0.1f;
            Require(recorder.IsPlaced && recorder.LiveTexture && recorder.liveCamera.enabled && recorder.IsRecording,
                "Q leaves same active camera rendering to its real RenderTexture");
            Vector3 placed = recorder.transform.position;
            Quaternion placedAngle = recorder.transform.rotation;
            var before = ReadLiveFrame("stance-placed-before.png");
            enemy.moveSpeed = 0.7f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
            yield return 0.55f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.1f;
            Require(Vector3.Distance(placed, recorder.transform.position) < 0.001f
                && Quaternion.Angle(placedAngle, recorder.transform.rotation) < 0.1f
                && ChangedPixels(before, ReadLiveFrame("stance-placed-after.png")) > 30,
                "Player leaves; moving Dummy changes placed live pixels while camera stays fixed");
            enemy.moveSpeed = 0;
            foreach (PortableLamp nearbyLamp in Object.FindObjectsByType<PortableLamp>())
                nearbyLamp.transform.position = new Vector3(5f, 0.02f, 4f);
            Object.FindFirstObjectByType<TwoHandedAxe>().transform.position = new Vector3(5f, 0.02f, 3f);
            player.position = placed + new Vector3(0.58f, -placed.y + 0.02f, -0.28f);
            player.transform.position = player.position;
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Require(device.Owner == human && device.Id == id && device.Events.Count == records
                && device.Events[0].LightLevel == recorded.LightLevel && device.Events[0].Damage.GameTime == recorded.Damage.GameTime,
                "Q pickup retains device, owner and immutable contact/light evidence");
            PositionSeverFight(false, 0.72f);
            yield return 0.1f;
            foreach (float wait in SeverSwing(human, ChopIntent.Normal, 1)) yield return wait;
            Require(device.State == CameraDeviceState.Broken && !recorder.LiveTexture && !recorder.IsRecording,
                "Real camera bash breaks device and stops live feed/recording");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.8f;
            ReacquireGrappleScene();
            human = player.GetComponent<PlayerController>(); enemy = dummy.GetComponent<PlayerController>();
            device = human.HeldCamera;
            Require(device.TryPlace(human), "Free both hands explicitly");
            foreach (ushort grip in new ushort[] { 1, 2 })
            {
                StageGrapple(start, start + Vector3.forward * 0.75f);
                yield return 0.1f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = grip });
                yield return 0.35f;
                Require(human.GrappleTarget == enemy, "Native hold starts grapple from either hand");
                Vector3 from = player.position, victimFrom = dummyBody.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                yield return 0.5f;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return 0.1f;
                Require(human.InGrapple && Vector3.Distance(from, player.position) > 0.25f
                    && Vector3.Distance(dummyBody.position - player.position, victimFrom - from) < 0.1f,
                    "Slow physical dragging moves both actors and preserves a short connection");
                foreach (float wait in SpaceTap(true)) yield return wait;
                Require(!human.IsProne && human.InGrapple, "Double Space cannot go prone while gripping");
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
                yield return 0.05f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = grip });
                float hp = dummy.Health;
                yield return 0.65f;
                Require(dummy.Health < hp && human.InGrapple, "Free hand lands a real shared melee hit while dragging remains available");
                yield return 0.6f;
                var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barrier.name = "Temporary closed doorway";
                barrier.transform.position = dummyBody.position + new Vector3(0, 0.8f, 0.7f);
                barrier.transform.localScale = new Vector3(2f, 2f, 0.2f);
                Physics.SyncTransforms();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return 0.9f;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return 0.1f;
                Require(human.InGrapple && dummyBody.position.z < barrier.GetComponent<Collider>().bounds.min.z - 0.25f
                    && Vector3.Distance(player.position, dummyBody.position) < human.grappleRange + 0.15f,
                    "Victim sweep stops both at closed doorway/furniture, no stretch or teleport");
                Capture("stance-drag-wall-" + grip + ".png", 1280, 800);
                Object.Destroy(barrier);
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return 0.1f;
                Require(!human.InGrapple && !enemy.InGrapple, "Grip release frees both immediately");
                Require(human.TrySetStance(PlayerController.BodyStance.Crouching), "Prepare crouched initiator");
                StageGrapple(start, start + Vector3.forward * 0.75f);
                yield return 0.1f;
                Require(human.TryBeginGrapple(HandSide.Left, enemy), "Crouching initiator may grab using the same command");
                human.ReleaseGrapple();
                Require(human.TrySetStance(PlayerController.BodyStance.Standing), "Return to standing");
            }
            StageGrapple(new Vector3(-0.4f, 0.02f, 1), new Vector3(0.4f, 0.02f, 1));
            yield return 0.1f;
            Require(!human.TryBeginGrapple(HandSide.Left, enemy), "Actual room divider blocks acquisition");
            StageGrapple(start, start + Vector3.forward * 0.75f);
            dummyBody.rotation = Quaternion.Euler(0, 180, 0); dummyBody.transform.rotation = dummyBody.rotation;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.05f;
            Require(enemy.TryBeginGrapple(HandSide.Right, human), "Enemy uses common command to capture player");
            yield return 0.7f;
            Require(human.IsCaptured && human.Stance == PlayerController.BodyStance.Standing,
                "Pre-held Space never escapes or changes captured stance");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.35f;
            Require(!human.EscapeWindowOpen, "Prepare outside-window attempt");
            foreach (float wait in SpaceTap(true)) yield return wait;
            Require(human.IsCaptured && !human.IsProne && !human.IsCrouching, "Space/double Space outside window do not alter stance or escape");
            while (!human.EscapeWindowOpen) yield return 0.02f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.06f;
            Require(!human.InGrapple && !enemy.InGrapple, "Fresh Space inside next QTE window frees both");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.35f;
            Require(human.Stance == PlayerController.BodyStance.Standing, "Escape press has no delayed stance action");
            foreach (float wait in SpaceTap()) yield return wait;
            Require(human.IsCrouching, "Fresh Space after escape controls stance again");
            Debug.Log("RRM DRAG CHECK: mirrored hold, moving pair, physical barrier, free strike, release, crouch grab and Space QTE passed");

            Require(human.TrySetStance(PlayerController.BodyStance.Standing), "Prepare fatal grapple contact");
            StageGrapple(start, start + Vector3.forward * 0.75f);
            dummy.maxHealth = 1; dummy.ResetHealth();
            yield return 0.1f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 1.3f;
            Require(human.GrappleTarget == enemy, "Acquire a low-health target with real hold input");
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
            yield return 0.05f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.7f;
            Require(dummy.IsDead && !human.InGrapple && !enemy.InGrapple && recorder.LiveTexture && recorder.liveCamera.enabled,
                "Fatal real free-hand punch releases pair; placed camera remains live for report");
            InputSystem.QueueStateEvent(mouse, new MouseState());

            foreach (bool enemyAttacker in new[] { false, true })
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                yield return 0.8f;
                ReacquireGrappleScene(); PrepareSeverHealth();
                var attacker = PositionSeverFight(enemyAttacker);
                human = player.GetComponent<PlayerController>();
                var victim = enemyAttacker ? human : dummy.GetComponent<PlayerController>();
                Require(human.HeldCamera.TryPlace(human), "Set camera down before axe test");
                var axe = Object.FindFirstObjectByType<TwoHandedAxe>();
                axe.transform.position = attacker.transform.TransformPoint(new Vector3(0, 0.1f, 0.3f));
                Require(axe.TryPickup(attacker, HandSide.Right), "Acquire actual two-handed axe");
                yield return 0.15f;
                foreach (float wait in SeverSwing(attacker, ChopIntent.Leg)) yield return wait;
                Require(severEvents.Count == 1 && severEvents[0].SeveredPart.HasValue && !victim.HasBothLegs
                    && victim.LimbMovementScale == 0.1f && !victim.IsProne, "First real leg cut applies upright penalty once");
                Require(victim.TrySetStance(PlayerController.BodyStance.Prone), "One-legged actor can crawl");
                Require(victim.LimbMovementScale == 1f && victim.TrySetStance(PlayerController.BodyStance.Standing), "Crawl uses its own slow speed, not compounded 0.1 penalty");
                PositionSeverFight(enemyAttacker);
                yield return 0.15f;
                foreach (float wait in SeverSwing(attacker, ChopIntent.Leg)) yield return wait;
                Require(severEvents.Count == 1 && severEvents[0].SeveredPart.HasValue && victim.IsProne
                    && !victim.TrySetStance(PlayerController.BodyStance.Crouching)
                    && !victim.TrySetStance(PlayerController.BodyStance.Standing)
                    && !victim.GetComponent<MeleeAttack>().TryKick(victim.GetComponent<MeleeAttack>().FindKickTarget()),
                    "Second actual leg cut forces prone, forbids rising/kicking for either actor");
                Require(victim.GetComponent<Rigidbody>().useGravity && victim.GetComponent<Damageable>().BleedingPerSecond > 0,
                    "Gravity and existing per-limb bleeding remain active");
                if (enemyAttacker)
                {
                    var blood = victim.GetComponent<BloodEvidence>();
                    int marks = blood.Marks.Count;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    yield return 1.8f;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Require(blood.Marks.Count > marks && player.linearVelocity.y < 0.1f,
                        "Actual legless crawl leaves distance-based blood on the floor");
                }
                Capture("stance-leg-loss-" + enemyAttacker + ".png", 1280, 800);
                Debug.Log("RRM LEGS CHECK PASSED: enemy attacker=" + enemyAttacker);
            }
            foreach (float wait in CheckStanceCombat()) yield return wait;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.8f;
            ReacquireGrappleScene();
            human = player.GetComponent<PlayerController>();
            Require(human.Stance == PlayerController.BodyStance.Standing && human.HasBothLegs && !human.InGrapple
                && player.GetComponent<Damageable>().BleedingPerSecond == 0 && SeveredParts().Length == 0
                && Object.FindObjectsByType<CameraDevice>().Length == 1 && Object.FindObjectsByType<TwoHandedAxe>().Length == 1
                && Object.FindObjectsByType<PortableLamp>().Length == 3 && recorder.LiveTexture
                && GameObject.Find("Room Divider North") && GameObject.Find("Platform Ramp"),
                "R restores stance, limbs, camera, items and original two-room scene without duplicates");
            Debug.Log("RRM STANCE LEGS RESET CHECK PASSED");
        }

        private static IEnumerable<float> CheckStanceCombat()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.8f;
            ReacquireGrappleScene();
            var human = player.GetComponent<PlayerController>();
            var enemy = dummy.GetComponent<PlayerController>();
            var health = player.GetComponent<Damageable>();
            health.maxHealth = 1000; health.ResetHealth();
            Require(human.HeldCamera.TryPlace(human), "Placed camera remains the witness for AI combat");
            foreach (var stance in new[] { PlayerController.BodyStance.Standing, PlayerController.BodyStance.Crouching, PlayerController.BodyStance.Prone })
            {
                enemy.opponent = null;
                StageGrapple(new Vector3(-3.3f, 0.02f, -3.4f), new Vector3(-3.3f, 0.02f, -1.5f));
                yield return 0.1f;
                Require(human.TrySetStance(stance), "Prepare target stance for autonomous enemy: " + stance);
                recorder.transform.position = new Vector3(-4.5f, 1.2f, -5.5f);
                recorder.SetViewRotation(Quaternion.LookRotation(player.position + Vector3.up * 0.5f - recorder.lens.position));
                enemy = StageDuel(player.position, dummyBody.position);
                float hp = health.Health;
                bool windup = false;
                float deadline = Time.time + 7f;
                while (health.Health == hp && Time.time < deadline)
                {
                    windup |= enemy.State == PlayerController.EnemyState.Windup;
                    yield return 0.05f;
                }
                Require(windup && health.Health < hp && recorder.RecordedHits > 0,
                    $"Existing AI approaches, winds up and physically hits {stance}; HP {hp}/{health.Health}, enemy {dummyBody.position}");
                Debug.Log("RRM AI STANCE CONTACT PASSED: " + stance);
            }
            enemy.opponent = null;
            yield return 2f;
            Require(human.TrySetStance(PlayerController.BodyStance.Standing), "Surviving player can rise after combat");
            PositionSeverFight(false, 0.72f);
            yield return 0.1f;
            float targetHealth = dummy.Health;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K));
            yield return 0.06f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 1.25f;
            Require(dummy.Health < targetHealth, "K uses the existing physical kick, not a direct damage event");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
            yield return 0.1f;
            Require(recorder.ControlsVisible, "Updated F1 remains accessible from placed camera");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.05f;
        }
    }
}
