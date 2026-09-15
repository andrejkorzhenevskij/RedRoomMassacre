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
        public static void RunWallBlockAndExit()
        {
            SessionState.SetInt("RRM.PlayCheck.Lamps", 8);
            RunAndExit();
        }

        private static IEnumerator<float> CheckWallBlock()
        {
            var controller = player.GetComponent<PlayerController>();
            var enemy = dummy.GetComponent<PlayerController>();
            var attack = player.GetComponent<MeleeAttack>();
            var enemyAttack = dummy.GetComponent<MeleeAttack>();
            var health = player.GetComponent<Damageable>();
            var device = controller.HeldCamera;
            Vector3 start = new Vector3(-3.3f, 0.02f, -4.1f);
            StageGrapple(start, start + Vector3.forward * 0.75f);
            yield return 0.1f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
            yield return 0.3f;
            Require(!controller.IsBlocking && !controller.InGrapple && !controller.TryBlock(), "An item forbids block, including shared command");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Require(!controller.HeldCamera, "Q leaves both hands empty for defense");
            foreach (PortableLamp lamp in Object.FindObjectsByType<PortableLamp>()) lamp.transform.position += Vector3.left * 3;

            // Both simultaneous and slightly staggered threshold crossings arbitrate before grapple.
            foreach (ushort first in new ushort[] { 3, 1, 2 })
            {
                yield return 0.7f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = first });
                yield return 0.06f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
                yield return 0.25f;
                Require(controller.IsBlocking && !controller.InGrapple && !enemy.InGrapple && !attack.IsBusy,
                    "Both holds choose block rather than either grapple; symmetric chord arbitration");
                Require(!attack.TryHandAttack(controller.LeftHand) && !attack.TryAttack() && !controller.TryBeginGrapple(HandSide.Left, enemy),
                    "Block cannot overlap an attack or grapple");
                yield return 0.8f;
                Require(!controller.IsBlocking && !controller.InGrapple, "Continued hold does not reopen window or acquire target");
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
                yield return 0.3f;
                Require(!controller.InGrapple && !attack.IsBusy, "Old surviving hold does not become a grapple");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return 0.1f;
                Require(!attack.IsBusy && damageEvents == 0, "Block release does not punch either hand");
            }

            // Real enemy swings, including a long strike that outlasts a shorter Inspector-configured window.
            foreach (string scenario in new[] { "front", "long", "expired", "rear", "late" })
            {
                StageGrapple(start, start + Vector3.forward * 0.75f);
                dummyBody.rotation = Quaternion.Euler(0, 180, 0); dummyBody.transform.rotation = dummyBody.rotation;
                if (scenario == "rear") { player.rotation = Quaternion.Euler(0, 180, 0); player.transform.rotation = player.rotation; }
                health.ResetHealth();
                enemyAttack.windup = 0.7f; enemyAttack.strike = scenario == "long" ? 0.45f : 0.18f;
                controller.blockWindow = scenario == "long" ? 0.15f : 0.2f;
                yield return 0.7f;
                float began = Time.time;
                System.Action<DamageEvent> observe = hit => Debug.Log($"RRM BLOCK CONTACT: {scenario}, t={Time.time - began:F3}, "
                    + $"blocking={controller.IsBlocking}, forward={controller.transform.forward}, source={hit.Source.transform.position}, health={health.Health}");
                health.Damaged += observe;
                if (scenario == "expired")
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
                    yield return 0.65f;
                }
                Require(enemyAttack.TryAttack(), "Enemy begins normal phased attack for " + scenario);
                if (scenario != "expired")
                {
                    yield return scenario == "late" ? 1f : 0.4f;
                    InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
                    yield return 0.25f;
                }
                if (scenario == "front")
                {
                    Require(controller.IsBlocking, "Real two-button hold opened the configured short window");
                    ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Verification/block-window-ui.png"));
                }
                yield return 1.6f;
                health.Damaged -= observe;
                Require(scenario == "front" || scenario == "long" ? health.Health == health.maxHealth : health.Health < health.maxHealth,
                    $"Real enemy {scenario} swing: health={health.Health}; front timed protects, expired/rear/late do not");
                Require(!controller.IsBlocking && !controller.InGrapple, "No persistent guard or accidental grapple after incoming strike");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return 0.15f;
                Require(!attack.IsBusy, "Defense release never turns into player punch");
            }
            controller.blockWindow = 0.2f;
            health.ResetHealth();
            StageGrapple(start, start + Vector3.forward * 0.75f);
            dummyBody.rotation = Quaternion.Euler(0, 180, 0); dummyBody.transform.rotation = dummyBody.rotation;
            dummy.ResetHealth(); damageEvents = 0;
            yield return 0.7f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.06f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.2f;
            Require(attack.IsBusy && enemy.TryBlock(), "Enemy uses same defense command against a real player fist windup");
            yield return 1.1f;
            Require(dummy.Health == dummy.maxHealth && damageEvents == 0, "Enemy block stops a real hand attack without damage events");
            StageGrapple(start, start + Vector3.forward * 3);
            yield return 0.7f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.06f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.62f;
            Require(attack.Phase == MeleeAttack.AttackPhase.Recovery, "Miss has entered existing recovery");
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
            yield return 0.25f;
            Require(!controller.IsBlocking && attack.IsBusy && !controller.TryBlock(), "Both holds cannot cancel own attack recovery");
            yield return 0.7f;
            Require(!controller.IsBlocking, "Rejected block is not queued for later");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.1f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
            yield return 0.25f;
            Require(controller.IsBlocking, "Fresh chord restores block after recovery");
            controller.SendMessage("OnApplicationFocus", false);
            controller.SendMessage("OnApplicationFocus", true);
            yield return 0.3f;
            Require(!controller.IsBlocking && !attack.IsBusy, "Focus loss cancels defense, with no input replay on return");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.7f;
            Debug.Log("RRM TIMED BLOCK PASSED: chords, state priority, front/rear/late, one contact, recovery, no replay.");

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall Slam Check Surface";
            wall.transform.position = start + new Vector3(0, 1.2f, 0.75f + 0.32f + 0.02f + 0.1f);
            wall.transform.localScale = new Vector3(2, 2.4f, 0.2f);
            foreach (ushort grip in new ushort[] { 1, 2 })
            {
                StageGrapple(start, start + Vector3.forward * 0.75f);
                dummy.ResetHealth(); damageEvents = 0;
                recorder.transform.position = start + new Vector3(0, 1.25f, -1.5f);
                recorder.SetViewRotation(Quaternion.LookRotation(dummyBody.position + Vector3.up - recorder.lens.position));
                int records = device.RecordedHits, marks = dummy.GetComponent<BloodEvidence>().Marks.Count;
                yield return 0.1f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = grip });
                yield return 1.3f;
                Require(controller.GrappleTarget == enemy, "Real grip established beside test wall");
                Vector3 before = dummyBody.position;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
                yield return 0.25f;
                Require(!controller.IsBlocking && attack.Phase == MeleeAttack.AttackPhase.Windup,
                    "Fresh free hold in existing grapple chooses one phased wall slam, not block");
                yield return 0.65f;
                Require(damageEvents == 1 && 90 - dummy.Health > attack.fistDamage && controller.InGrapple,
                    $"Either free hand delivers one stronger wall contact: health={dummy.Health}, events={damageEvents}");
                Require(Vector3.Distance(before, dummyBody.position) < 0.04f, "Contact-only slam does not teleport or push through wall");
                Require(device.RecordedHits == records + 1 && !recorder.LiveTexture && recorder.IsRecording,
                    "Placed witness records normal slam DamageEvent without remote feed");
                var evidence = dummy.GetComponent<BloodEvidence>();
                Require(evidence.Marks.Count > marks && Mathf.Abs(evidence.Marks[evidence.Marks.Count - 1].Mark.transform.forward.y) < 0.1f,
                    "Existing blood system leaves persistent wall evidence at contact");
                Capture("wall-slam-" + grip + ".png", 1280, 800);
                yield return 1.8f;
                Require(damageEvents == 1 && !attack.IsBusy, "Held free button never repeats a slam");
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = grip });
                yield return 0.08f;
                wall.SetActive(false);
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
                yield return 0.6f;
                Require(damageEvents == 1 && !attack.IsBusy, "No wall means no powered hit, even in valid grapple");
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = grip });
                yield return 0.08f;
                wall.SetActive(true);
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
                yield return 0.25f;
                Require(attack.IsBusy, "Release and fresh hold allows another attempt");
                wall.SetActive(false);
                yield return 1.2f;
                Require(damageEvents == 1, "Wall removed during windup prevents boosted contact");
                wall.SetActive(true);
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = grip });
                yield return 0.08f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
                yield return 0.25f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = (ushort)(3 - grip) });
                yield return 0.5f;
                Require(!controller.InGrapple && !enemy.InGrapple && !controller.IsBlocking && damageEvents == 1,
                    "Release gripping hand cancels slam; old free hold becomes neither block nor grapple");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return 0.7f;
            }
            Object.Destroy(wall);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.7f;
            ReacquireGrappleScene();
            Require(!player.GetComponent<PlayerController>().InGrapple && !player.GetComponent<PlayerController>().BlockRecovering
                && Object.FindObjectsByType<CameraDevice>().Length == 1 && Object.FindObjectsByType<PortableLamp>().Length == 3,
                "Restart restores ordinary state and original items without duplicates");
        }
    }
}
