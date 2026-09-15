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
        public static void RunFeetAndExit()
        {
            SessionState.SetInt("RRM.PlayCheck.Lamps", 9);
            RunAndExit();
        }

        private static IEnumerator<float> CheckFeet()
        {
            var controller = player.GetComponent<PlayerController>();
            var attack = player.GetComponent<MeleeAttack>();
            var device = controller.HeldCamera;
            Vector3 start = new Vector3(-3.3f, 0.02f, -4.1f);
            StageGrapple(start, start + Vector3.forward * 0.85f);
            GameObject.Find("Portable Lamp 1").transform.position = start + new Vector3(0.55f, 0, 0.35f);
            yield return 0.15f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            var lamp = controller.RightHand.Item?.Source as PortableLamp;
            Require(lamp && device, "Both hands occupied: camera and lamp");
            recorder.SetViewRotation(Quaternion.LookRotation(Vector3.back));
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.3f;
            Quaternion view = recorder.lens.rotation;
            var leg = player.GetComponent<HitFeedback>().visual.Find("Right Leg");
            Vector3 legRest = leg.localPosition;
            Require(attack.FindKickTarget(), "Close torso is reachable from foot, independent of rear camera view");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.06f;
            Require(!attack.IsBusy && controller.IsGrounded && damageEvents == 0, "Space press waits, no premature kick/jump");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.25f;
            Require(attack.IsKicking && damageEvents == 0 && controller.IsGrounded, "Short release starts kick windup instead of jumping");
            yield return 0.35f;
            Require(damageEvents == 1 && dummy.Health < 90 && controller.IsGrounded && attack.IsBusy,
                "Real foot sweep delivers exactly one ordinary damage event and enters recovery");
            Require(controller.HeldCamera == device && controller.RightHand.Item?.Source == lamp
                && Quaternion.Angle(recorder.lens.rotation, view) < 0.1f && recorder.LiveTexture
                && Vector3.Distance(leg.localPosition, legRest) > 0.01f,
                "Kick moves existing leg but preserves occupied hands, camera orientation and live feed");
            Capture("kick-contact.png", 1280, 800);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
            yield return 0.06f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.2f;
            Require(damageEvents == 1 && controller.IsGrounded && !attack.TryHandAttack(controller.RightHand),
                "Space/hand spam cannot bypass kick recovery or fall back to a jump");
            yield return 0.7f;
            Require(!attack.IsBusy && damageEvents == 1 && Vector3.Distance(leg.localPosition, legRest) < 0.001f,
                "No queued repeats; leg returns to original pose");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            foreach (PortableLamp item in Object.FindObjectsByType<PortableLamp>()) item.transform.position += Vector3.left * 3;

            StageGrapple(start, start + Vector3.forward * 0.75f);
            dummy.ResetHealth(); damageEvents = 0;
            yield return 0.1f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.06f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.62f;
            Require(attack.Phase == MeleeAttack.AttackPhase.Recovery, "Hand attack entered recovery");
            int hits = damageEvents;
            Require(attack.FindKickTarget(), "Suitable kick target still exists during hand recovery");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.06f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.12f;
            Require(!attack.IsKicking && controller.IsGrounded && damageEvents == hits, "Rejected kick during hand recovery is not replaced by jump");
            yield return 0.7f;
            Require(!attack.IsBusy && damageEvents == hits, "Rejected kick is not queued after recovery");

            StageGrapple(start, start + Vector3.forward * 0.85f);
            dummy.ResetHealth(); damageEvents = 0;
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.06f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.1f;
            Require(attack.IsKicking, "Evade setup starts through real Space input");
            dummyBody.position += Vector3.right * 2;
            dummyBody.transform.position = dummyBody.position;
            yield return 0.7f;
            Require(damageEvents == 0 && attack.IsBusy, "Target can evade committed foot trajectory; miss still recovers");
            yield return 0.5f;

            // The selection rejects geometry, facing and unreachable heights instead of stealing jump input.
            foreach (string context in new[] { "none", "behind", "high", "wall", "low obstacle" })
            {
                Vector3 target = start + (context == "none" ? Vector3.forward * 3
                    : context == "behind" ? Vector3.back * 0.85f
                    : context == "high" ? Vector3.forward * 0.85f + Vector3.up * 2 : Vector3.forward * 0.9f);
                StageGrapple(start, target);
                player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
                dummyBody.useGravity = context != "high";
                GameObject obstacle = null;
                if (context == "wall" || context == "low obstacle")
                {
                    obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    float height = context == "wall" ? 2.5f : 0.65f;
                    obstacle.transform.position = start + new Vector3(0, height * 0.5f, 0.45f);
                    obstacle.transform.localScale = new Vector3(1.2f, height, 0.08f);
                }
                yield return 0.15f;
                Require(controller.IsGrounded && !attack.FindKickTarget(), "Jump context: " + context);
                Vector3 lensBefore = recorder.lens.position, feetBefore = player.position;
                Quaternion lensAngle = recorder.lens.rotation;
                var pixelsBefore = ReadLiveFrame("feet-before-jump.png");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return 0.06f;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return 0.2f;
                Require(!controller.IsGrounded && player.position.y > feetBefore.y + 0.3f && !attack.IsBusy,
                    "Short release jumps, not air-kicks: " + context);
                Require(Vector3.Distance(recorder.lens.position - lensBefore, player.position - feetBefore) < 0.04f
                    && Quaternion.Angle(recorder.lens.rotation, lensAngle) < 0.1f
                    && ChangedPixels(pixelsBefore, ReadLiveFrame("feet-jump-feed.png")) > 500,
                    "Handheld lens follows real jump height without steering; rendered feed changes");
                float velocity = player.linearVelocity.y;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return 0.06f;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return 0.08f;
                Require(player.linearVelocity.y < velocity && !attack.IsBusy, "No double jump or airborne kick");
                yield return 0.9f;
                Require(controller.IsGrounded && Mathf.Abs(player.position.y - feetBefore.y) < 0.04f, "Normal landing, no queued air action");
                if (obstacle) Object.Destroy(obstacle);
                dummyBody.useGravity = true;
            }

            StageGrapple(start, start + Vector3.forward * 0.85f);
            damageEvents = 0;
            yield return 0.15f;
            float fullHeight = player.GetComponent<CapsuleCollider>().height;
            float fullLens = recorder.lens.position.y;
            view = recorder.lens.rotation;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.3f;
            Require(controller.IsCrouching && controller.IsGrounded && !attack.IsBusy && damageEvents == 0
                && player.GetComponent<CapsuleCollider>().height < fullHeight * 0.7f && recorder.lens.position.y < fullLens - 0.3f
                && Quaternion.Angle(recorder.lens.rotation, view) < 0.1f, "Held Space crouches actual collider and camera, without any attack");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.LeftCtrl));
            yield return 0.08f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            yield return 0.1f;
            Require(controller.IsCrouching && !attack.IsBusy, "Releasing Space keeps Ctrl crouch and does not kick");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.Space));
            yield return 0.3f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.1f;
            Require(controller.IsCrouching, "Releasing Ctrl keeps held Space crouch");
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.transform.position = player.position + Vector3.up * 1.4f;
            ceiling.transform.localScale = new Vector3(1.2f, 0.2f, 1.2f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.25f;
            Require(controller.IsCrouching && !attack.IsBusy && damageEvents == 0, "Cannot stand inside low ceiling; long release has no delayed kick/jump");
            Object.Destroy(ceiling);
            yield return 0.15f;
            Require(!controller.IsCrouching && Mathf.Abs(player.GetComponent<CapsuleCollider>().height - fullHeight) < 0.001f,
                "Standing collider restores when headroom clears");

            StageGrapple(new Vector3(-7, 0.02f, -4), new Vector3(6, 0.02f, 5));
            player.rotation = Quaternion.Euler(0, 90, 0); player.transform.rotation = player.rotation;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return 0.6f;
            float normalSpeed = Vector3.ProjectOnPlane(player.linearVelocity, Vector3.up).magnitude;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
            yield return 0.6f;
            float slowSpeed = Vector3.ProjectOnPlane(player.linearVelocity, Vector3.up).magnitude;
            Require(normalSpeed > 3 && slowSpeed > 1.3f && slowSpeed < normalSpeed * 0.6f && controller.IsCrouching,
                $"Moving crouch slows actual velocity: {normalSpeed:F2} -> {slowSpeed:F2}");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return 0.4f;
            Require(controller.IsGrounded && !controller.IsCrouching && player.linearVelocity.x > 2.9f, "Long release restores movement speed without jumping");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());

            Bounds cover = GameObject.Find("Low Cover").GetComponent<Collider>().bounds;
            StageGrapple(new Vector3(cover.center.x, 0.02f, cover.min.z - 0.85f), new Vector3(6, 0.02f, 5));
            player.rotation = Quaternion.identity; player.transform.rotation = player.rotation;
            yield return 0.2f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
            yield return 0.06f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return 0.25f;
            Require(!controller.IsGrounded && player.linearVelocity.z > 2 && player.position.y > 0.3f, "Moving jump retains horizontal control");
            float end = Time.time + 1.2f;
            while (player.position.z < cover.center.z - 0.2f && Time.time < end) yield return 0.02f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.7f;
            Require(controller.IsGrounded && Mathf.Abs(player.position.y - cover.max.y) < 0.04f, "Real moving jump lands on saved low cover");
            Capture("feet-cover-landing.png", 1280, 800);
            Require(!attack.FindKickTarget(), "Standing on a platform alone does not create a kick target");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.06f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.2f;
            Require(player.position.y > cover.max.y + 0.3f && !attack.IsBusy, "Short Space on platform without a target remains jump");
            yield return 1f;

            StageGrapple(new Vector3(cover.center.x, cover.max.y + 0.02f, cover.min.z + 0.34f),
                new Vector3(cover.center.x, 0.02f, cover.min.z - 0.55f));
            dummy.ResetHealth(); damageEvents = 0;
            yield return 0.2f;
            Require(controller.IsGrounded && attack.FindKickTarget(), "Reachable lower target at platform edge is a valid kick");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.06f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.7f;
            Require(damageEvents == 1 && controller.IsGrounded && Mathf.Abs(player.position.y - cover.max.y) < 0.04f,
                "Real kick hits accessible target below without jumping off platform");
            yield return 0.7f;

            StageGrapple(start, start + Vector3.forward * 3);
            yield return 0.15f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            yield return 0.15f;
            float crouchedHeight = player.GetComponent<CapsuleCollider>().height;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.Space));
            yield return 0.06f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            yield return 0.2f;
            Require(!controller.IsGrounded && controller.IsCrouching && !attack.IsBusy
                && Mathf.Abs(player.GetComponent<CapsuleCollider>().height - crouchedHeight) < 0.001f,
                "Short Space without target remains jump while Ctrl preserves crouched collider");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return 0.15f;
            Vector3 placed = recorder.transform.position; view = recorder.transform.rotation;
            Require(recorder.IsPlaced && !recorder.LiveTexture, "Placed camera remains private");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.06f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.25f;
            Require(!controller.IsGrounded && Vector3.Distance(placed, recorder.transform.position) < 0.001f, "Placed camera does not jump with player");
            yield return 1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.3f;
            Require(controller.IsCrouching && Vector3.Distance(placed, recorder.transform.position) < 0.001f
                && Quaternion.Angle(view, recorder.transform.rotation) < 0.1f && recorder.IsRecording, "Placed camera does not crouch or turn with player");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.15f;
            StageGrapple(start, start + Vector3.forward * 0.75f);
            damageEvents = 0;
            yield return 0.15f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.35f;
            Require(controller.InGrapple, "Both empty hands still establish ordinary grapple");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.06f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.7f;
            Require(controller.InGrapple && !attack.IsBusy && damageEvents == 0
                && !attack.TryKick(attack.FindKickTarget()), "Neither actual Space nor common kick command attacks inside grapple");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.7f;
            ReacquireGrappleScene();
            Require(Object.FindObjectsByType<CameraDevice>().Length == 1 && Object.FindObjectsByType<PortableLamp>().Length == 3
                && !player.GetComponent<PlayerController>().IsCrouching && !player.GetComponent<MeleeAttack>().IsBusy,
                "Restart restores original stance, items and idle attack without duplicates");
        }
    }
}
