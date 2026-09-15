using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace RRM.Editor
{
    public static partial class CombatPrototypePlayCheck
    {
        public static void RunGrappleAndExit()
        {
            SessionState.SetInt("RRM.PlayCheck.Lamps", 7);
            RunAndExit();
        }

        private static void StageGrapple(Vector3 position, Vector3 target)
        {
            StageRange(position, target, false);
            // With placed-camera HUD hidden, mouse (0,0) is inside the expanded game view.
            // Keep this staged facing fixed while queued button events clear the pointer position.
            player.GetComponent<PlayerController>().turnSpeed = 0;
            player.GetComponent<PlayerController>().attackTurnSpeed = 0;
            var enemy = dummy.GetComponent<PlayerController>();
            enemy.opponent = null; enemy.moveSpeed = enemy.turnSpeed = 0;
            enemy.enabled = true;
            InputSystem.QueueStateEvent(mouse, new MouseState());
        }

        private static void ReacquireGrappleScene()
        {
            player = GameObject.Find("Player").GetComponent<Rigidbody>();
            dummy = GameObject.Find("Dummy").GetComponent<Damageable>();
            dummyBody = dummy.GetComponent<Rigidbody>();
            recorder = Object.FindFirstObjectByType<CameraRecorder>(); camera = Camera.main;
            dummy.Damaged += ObserveHit; damageEvents = 0;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
        }

        private static IEnumerator<float> CheckGrapple()
        {
            var controller = player.GetComponent<PlayerController>();
            var enemy = dummy.GetComponent<PlayerController>();
            var attack = player.GetComponent<MeleeAttack>();
            var device = controller.HeldCamera;
            Vector3 start = new Vector3(-3.3f, 0.02f, -4.1f);
            StageGrapple(start, start + Vector3.forward * 0.75f);
            player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.35f;
            Require(!controller.InGrapple && controller.LeftHand.Item?.Source == device && !attack.IsBusy,
                "Camera in either hand forbids starting a grapple; no automatic unequip");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return 0.1f;
            Require(!controller.HeldCamera && recorder.IsPlaced && !recorder.LiveTexture, "Q sets camera down before grapple");
            GameObject.Find("Portable Lamp 1").transform.position = start + new Vector3(0.55f, 0, 0.35f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return 0.1f;
            Require(controller.RightHand.Item?.Source is PortableLamp, "Right hand picks lamp normally");
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.3f;
            Require(!controller.InGrapple && !controller.TryBeginGrapple(HandSide.Right, enemy), "Lamp in right also blocks either gripping hand");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.05f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return 0.1f;
            Require(controller.RightHand.State == HandState.Empty, "E puts lamp down without deleting it");
            foreach (PortableLamp lamp in Object.FindObjectsByType<PortableLamp>()) lamp.transform.position += Vector3.left * 3;

            foreach (ushort grip in new ushort[] { 1, 2 })
            {
                StageGrapple(start, start + Vector3.forward * 0.75f);
                player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
                dummy.ResetHealth(); damageEvents = 0;
                RigidbodyConstraints originalPlayer = player.constraints, originalEnemy = dummyBody.constraints;
                recorder.transform.position = start + new Vector3(0, 1.25f, -1.5f);
                recorder.SetViewRotation(Quaternion.LookRotation(dummyBody.position + Vector3.up - recorder.lens.position));
                int records = device.RecordedHits;
                yield return 0.1f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = grip });
                yield return 0.32f;
                bool grabbed = controller.GrappleTarget == enemy && enemy.Captor == controller
                    && controller.GrappleHand == (grip == 1 ? HandSide.Left : HandSide.Right);
                if (!grabbed) Debug.Log($"RRM GRAPPLE DIAGNOSTIC: hand={grip}, player={player.position}, target={dummyBody.position}, "
                    + $"facing={player.transform.forward}, velocities={player.linearVelocity}/{dummyBody.linearVelocity}, phase={attack.Phase}, "
                    + $"items={controller.LeftHand.Item?.Source}/{controller.RightHand.Item?.Source}, crouch={controller.IsCrouching}/{enemy.IsCrouching}, "
                    + $"enabled={controller.enabled}/{enemy.enabled}, pair={controller.InGrapple}/{enemy.InGrapple}, "
                    + $"direct={controller.TryBeginGrapple(grip == 1 ? HandSide.Left : HandSide.Right, enemy)}");
                Require(grabbed, "Either hand starts real hold grapple");
                Vector3 lockedPlayer = player.position, lockedEnemy = dummyBody.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space, Key.Q, Key.E));
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
                yield return 0.55f;
                Require(controller.InGrapple && enemy.InGrapple && !attack.IsBusy && damageEvents == 0
                    && Vector3.Distance(player.position, lockedPlayer) < 0.03f && Vector3.Distance(dummyBody.position, lockedEnemy) < 0.03f,
                    "Pair stays at contact under movement/jump/item input; free-hand hold has no damage");
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = grip });
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return 0.5f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
                yield return 0.06f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = grip });
                yield return 0.55f;
                Require(damageEvents == 1 && dummy.Health < 90 && dummy.Health >= 90 - attack.fistDamage
                    && controller.GrappleTarget == enemy, "Free-hand real melee hits retained target once without releasing it");
                Require(device.RecordedHits == records + 1 && !recorder.LiveTexture && recorder.IsRecording,
                    "Placed camera witnesses actual grapple DamageEvent without remote feed");
                Require(!attack.TryHandAttack(grip == 1 ? controller.LeftHand : controller.RightHand)
                    && !attack.TryAttack(), "Gripping hand and generic attack command cannot bypass grapple rules");
                Capture("grapple-" + grip + ".png", 1280, 800);
                yield return 0.9f;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return 0.06f;
                Require(!controller.InGrapple && !enemy.InGrapple && player.constraints == originalPlayer && dummyBody.constraints == originalEnemy,
                    "Releasing either gripping button immediately restores both original constraint sets");
            }

            StageGrapple(new Vector3(-0.4f, 0.02f, 1), new Vector3(0.4f, 0.02f, 1));
            yield return 0.08f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.32f;
            Require(!controller.InGrapple && !controller.TryBeginGrapple(HandSide.Right, enemy), "Solid divider blocks both input and shared grapple command");
            StageGrapple(start, start + Vector3.forward * 3);
            yield return 0.08f;
            Require(!controller.TryBeginGrapple(HandSide.Left, enemy), "No distant target pull/teleport");
            StageGrapple(start, start + Vector3.back * 0.75f);
            player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
            yield return 0.08f;
            Require(!controller.TryBeginGrapple(HandSide.Left, enemy), "Target behind body is not grabbable");

            StageGrapple(start, start + Vector3.forward * 0.75f);
            yield return 0.08f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.32f;
            Require(controller.InGrapple, "Pair regrabs with a fresh press");
            attack.enabled = false;
            yield return 0.06f;
            Require(!controller.InGrapple && !enemy.InGrapple, "Disabled attack state releases both participants");
            attack.enabled = true;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.08f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.32f;
            Require(controller.InGrapple, "Fresh hold reacquires after interruption");
            dummyBody.position += Vector3.forward * 2;
            dummyBody.transform.position = dummyBody.position;
            yield return 0.08f;
            Require(!controller.InGrapple && !enemy.InGrapple, "Unexpected separation releases rather than stretching or dragging pair");

            // Enemy uses the same command; there is no automatic decision to grab or escape.
            StageGrapple(start, start + Vector3.forward * 0.75f);
            dummyBody.rotation = Quaternion.Euler(0, 180, 0); dummyBody.transform.rotation = dummyBody.rotation;
            yield return 0.08f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.3f;
            // Player may grab first; explicitly release this setup gesture before the enemy command.
            controller.ReleaseGrapple();
            Require(enemy.TryBeginGrapple(HandSide.Right, controller), "Enemy can use the same grapple command on player");
            yield return 0.7f;
            Require(controller.IsCaptured && controller.EscapeWindowOpen && controller.EscapeHint.Contains("BREAK FREE"),
                "Victim gets a repeating local timing indicator");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.08f;
            Require(controller.IsCaptured, "Button held before capture cannot escape on release");
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Verification/grapple-escape-ui.png"));
            while (controller.EscapeWindowOpen) yield return 0.04f;
            yield return 0.55f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.05f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.06f;
            Require(controller.IsCaptured && controller.EscapeHint.Contains("attempt used"), "Early short input spends the cycle's only attempt");
            while (!controller.EscapeWindowOpen) yield return 0.04f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.05f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.06f;
            Require(controller.IsCaptured, "Switching buttons/spamming cannot retry inside the same opening");
            while (controller.EscapeWindowOpen) yield return 0.04f;
            while (!controller.EscapeWindowOpen) yield return 0.04f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.05f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.06f;
            Require(!controller.InGrapple && !enemy.InGrapple && !enemy.TryBeginGrapple(HandSide.Left, controller),
                "Fresh short input in repeated window escapes; immediate regrab is rejected");
            var playerHealthNow = player.GetComponent<Damageable>();
            float previousHealth = playerHealthNow.Health;
            Require(enemy.GetComponent<MeleeAttack>().TryAttack(), "Escape protection does not reject ordinary enemy melee command");
            yield return 1.25f;
            Require(playerHealthNow.Health < previousHealth && !playerHealthNow.IsDead, "Actual enemy damage still applies during anti-grab protection");
            yield return 0.3f;

            StageGrapple(start, start + Vector3.forward * 0.75f);
            dummyBody.rotation = Quaternion.Euler(0, 180, 0); dummyBody.transform.rotation = dummyBody.rotation;
            playerHealthNow.ResetHealth();
            yield return 0.08f;
            Require(enemy.TryBeginGrapple(HandSide.Left, controller), "Protection expires; opposite enemy hand can grab");
            var enemyMelee = enemy.GetComponent<MeleeAttack>();
            Require(!enemyMelee.TryHandAttack(enemy.RightHand), "Initial escape window must be available before first grapple strike");
            while (!controller.EscapeWindowOpen) yield return 0.03f;
            Require(!enemyMelee.TryHandAttack(enemy.RightHand), "No grapple punch during initial escape window");
            while (controller.EscapeWindowOpen) yield return 0.03f;
            float victimHitAt = -1;
            System.Action<DamageEvent> watchHit = hit => victimHitAt = Time.time;
            playerHealthNow.Damaged += watchHit;
            Require(enemyMelee.TryHandAttack(enemy.RightHand), "Enemy free hand uses same phased melee as player");
            while (victimHitAt < 0) yield return 0.02f;
            playerHealthNow.Damaged -= watchHit;
            Require(controller.IsCaptured && !controller.EscapeWindowOpen, "Real grapple hit starts new escape timing");
            while (!controller.EscapeWindowOpen) yield return 0.02f;
            Require(Time.time - victimHitAt >= controller.escapeDelay - 0.02f
                && !enemyMelee.TryHandAttack(enemy.RightHand), "Post-hit escape window precedes any next attack");
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.05f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.06f;
            Require(!controller.InGrapple && !enemy.InGrapple, "Left input also escapes after a real grapple hit");
            Debug.Log("RRM GRAPPLE TIMING PASSED: prehold, early/spam rejection, repeating windows, both escape hands, hit reset and protection.");

            // Death and removal must not leave horizontal constraints or reciprocal references behind.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.7f;
            ReacquireGrappleScene();
            controller = player.GetComponent<PlayerController>(); enemy = dummy.GetComponent<PlayerController>();
            attack = player.GetComponent<MeleeAttack>();
            StageGrapple(start, start + Vector3.forward * 0.75f);
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            dummy.maxHealth = 1; dummy.ResetHealth(); damageEvents = 0;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 1.3f;
            Require(controller.InGrapple, "Fatal-hit setup uses actual hold grab");
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
            yield return 0.06f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.7f;
            Require(dummy.IsDead && damageEvents == 1 && !controller.InGrapple && !enemy.InGrapple
                && (player.constraints & RigidbodyConstraints.FreezePositionX) == 0,
                "Real fatal free-hand punch releases both and restores survivor movement");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.7f;
            ReacquireGrappleScene();
            controller = player.GetComponent<PlayerController>(); enemy = dummy.GetComponent<PlayerController>();
            StageGrapple(start, start + Vector3.forward * 0.75f);
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.32f;
            Require(controller.InGrapple, "Removal setup grabbed through real input");
            Object.Destroy(dummy.gameObject);
            yield return 0.1f;
            Require(!controller.InGrapple && (player.constraints & RigidbodyConstraints.FreezePositionX) == 0,
                "Deleting participant releases survivor");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.7f;
            ReacquireGrappleScene();
            controller = player.GetComponent<PlayerController>(); enemy = dummy.GetComponent<PlayerController>();
            StageGrapple(start, start + Vector3.forward * 0.75f);
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.32f;
            Require(controller.InGrapple, "Captor death setup grabbed through input");
            var third = Object.Instantiate(dummy.gameObject, start + Vector3.back * 0.75f, Quaternion.identity);
            third.name = "Grapple Check Attacker";
            var thirdController = third.GetComponent<PlayerController>();
            thirdController.opponent = null; thirdController.moveSpeed = thirdController.turnSpeed = 0;
            player.GetComponent<Damageable>().maxHealth = 1; player.GetComponent<Damageable>().ResetHealth();
            yield return 0.08f;
            Require(third.GetComponent<MeleeAttack>().TryAttack(), "Third actor starts existing enemy melee command");
            yield return 1.3f;
            Require(player.GetComponent<Damageable>().IsDead && !controller.InGrapple && !enemy.InGrapple
                && (dummyBody.constraints & RigidbodyConstraints.FreezePositionX) == 0,
                "Real fatal attack on captor also releases victim");
            Object.Destroy(third);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.7f;
            ReacquireGrappleScene();
            Require(Object.FindObjectsByType<PlayerController>().Length == 2 && Object.FindObjectsByType<CameraDevice>().Length == 1
                && Object.FindObjectsByType<PortableLamp>().Length == 3 && !player.GetComponent<PlayerController>().InGrapple
                && !dummy.GetComponent<PlayerController>().InGrapple, "R restores original actors/items without grapple residue or duplicates");
        }
    }
}
