using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace RRM.Editor
{
    public static partial class CombatPrototypePlayCheck
    {
        [MenuItem("RRM/Add Two Handed Axe")]
        public static void AddAxe()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(CombatPrototypeBuilder.ScenePath);
            var existing = Object.FindObjectsByType<TwoHandedAxe>();
            if (existing.Length != 0)
            {
                Require(existing.Length == 1, "Exactly one saved axe, never duplicate it");
                return;
            }
            var axe = new GameObject("Two Handed Axe").AddComponent<TwoHandedAxe>();
            axe.spawnPoints = new[] { new Vector3(-6.7f, 0f, -4.4f), new Vector3(-6.8f, 0f, 1.3f),
                new Vector3(1.6f, 0f, -4.8f), new Vector3(6.4f, 0f, 3.7f) };
            var handle = AssetDatabase.LoadAssetAtPath<Material>("Assets/RRM/Materials/Weapon.mat");
            var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/RRM/Materials/Metal.mat");
            Part("Handle", new Vector3(0, 0, 0.53f), new Vector3(0.085f, 0.085f, 1.06f), handle);
            Part("Axe Head", new Vector3(0.07f, 0, 1.05f), new Vector3(0.46f, 0.18f, 0.18f), metal);
            Physics.SyncTransforms();
            foreach (Vector3 point in axe.spawnPoints)
                Require(TwoHandedAxe.SupportedPose(point, out _), "Saved axe point has clear supported footprint: " + point);
            Require(axe.ChooseSpawn(173), "Initial editor pose uses a valid spawn");
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene(CombatPrototypeBuilder.ScenePath);
            Require(Object.FindObjectsByType<TwoHandedAxe>().Length == 1
                && Object.FindFirstObjectByType<TwoHandedAxe>().spawnPoints.Length == 4,
                "One axe and its four points survive reopening the scene");
            Debug.Log("RRM AXE ADDED: only one new root; existing geometry and prefabs not rebuilt.");

            void Part(string name, Vector3 position, Vector3 scale, Material material)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = name;
                part.transform.SetParent(axe.transform, false);
                part.transform.localPosition = position;
                part.transform.localScale = scale;
                part.GetComponent<Renderer>().sharedMaterial = material;
                Object.DestroyImmediate(part.GetComponent<Collider>());
            }
        }

        public static void PrepareAxeAndRun() { AddAxe(); RunAxeAndExit(); }

        public static void RunAxeAndExit()
        {
            SessionState.SetInt("RRM.PlayCheck.Lamps", 11);
            RunAndExit();
        }

        private static IEnumerator<float> CheckAxe()
        {
            var axes = Object.FindObjectsByType<TwoHandedAxe>();
            Require(axes.Length == 1, "Start creates exactly one axe");
            var axe = axes[0];
            var controller = player.GetComponent<PlayerController>();
            var attack = player.GetComponent<MeleeAttack>();
            var device = controller.HeldCamera;
            foreach (Vector3 point in axe.spawnPoints)
            {
                Require(TwoHandedAxe.SupportedPose(point, out Vector3 pose), "Clear supported spawn: " + point);
                Vector3 approach = point - Vector3.forward * 0.6f;
                Require(!Physics.CheckCapsule(approach + Vector3.up * 0.34f, approach + Vector3.up * 1.38f,
                    0.32f, 1, QueryTriggerInteraction.Ignore), "Character fits beside spawn: " + point);
                Require(Mathf.Abs(pose.y - 0.11f) < 0.01f, "Spawn is on room floor, not furniture");
            }
            var choices = new HashSet<int>();
            for (int seed = 1; seed <= 32; seed++)
            {
                Require(axe.ChooseSpawn(seed), "Seed chooses valid point");
                int first = axe.SpawnIndex; Vector3 position = axe.transform.position;
                Require(axe.ChooseSpawn(seed) && axe.SpawnIndex == first && axe.transform.position == position,
                    "Same seed repeats the exact spawn");
                choices.Add(first);
            }
            Require(choices.Count == axe.spawnPoints.Length && axe.spawnPoints.Any(point => point.x < 0)
                && axe.spawnPoints.Any(point => point.x > 0), "All checked points in both rooms are selectable");

            Vector3 start = new Vector3(-3.3f, 0.02f, -4.1f);
            StageGrapple(start, start + Vector3.forward * 2.8f);
            foreach (var freeLamp in Object.FindObjectsByType<PortableLamp>())
                freeLamp.transform.position = new Vector3(-7, 0.02f, 4.8f);
            axe.transform.SetPositionAndRotation(start + new Vector3(0, 0.11f, 0.25f), Quaternion.identity);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return 0.1f;
            Require(!axe.Owner && controller.HeldCamera == device && controller.RightHand.State == HandState.Empty,
                "E with camera in the other hand cannot take the axe or hide the camera: axe=" + axe.Owner
                    + " left=" + controller.LeftHand.Item?.Source + " right=" + controller.RightHand.Item?.Source);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.05f;
            Require(device.TryPlace(controller), "Set down original camera first");
            device.transform.position = new Vector3(-7, 1.25f, -5);
            var lamp = Object.FindFirstObjectByType<PortableLamp>();
            lamp.transform.position = start + new Vector3(0.45f, 0.02f, 0.3f);
            Physics.SyncTransforms();
            Require(lamp.TryPickup(controller, HandSide.Right) && !axe.TryPickup(controller, HandSide.Left),
                "Lamp in either hand prevents two-hand pickup");
            Require(lamp.TryPlace(controller), "Lamp is placed explicitly");
            lamp.transform.position = new Vector3(-7, 0.02f, 4.8f);
            var leftArm = controller.GetComponentsInChildren<Hurtbox>().First(zone => zone.part == BodyPart.LeftArm);
            leftArm.gameObject.SetActive(false);
            Require(!axe.TryPickup(controller, HandSide.Left), "A missing/inactive arm forbids two-handed pickup");
            leftArm.gameObject.SetActive(true);
            foreach (Key key in new[] { Key.Q, Key.E })
            {
                axe.transform.SetPositionAndRotation(start + new Vector3(0, 0.11f, 0.25f), Quaternion.identity);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                yield return 0.1f;
                Require(axe.IsHeldBy(controller) && ReferenceEquals(controller.LeftHand.Item, controller.RightHand.Item)
                    && Object.FindObjectsByType<TwoHandedAxe>().Length == 1, key + " holds the same single object in both hands");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return 0.05f;
                Require(!controller.TryBlock() && !controller.TryBeginGrapple(HandSide.Left), "Axe forbids block and grapple");
                Capture("axe-held.png", 1280, 800);
                if (key == Key.Q)
                {
                    Vector3 oldPosition = camera.transform.position;
                    Quaternion oldRotation = camera.transform.rotation;
                    float oldSize = camera.orthographicSize;
                    camera.transform.position = player.position + new Vector3(2f, 3f, -3f);
                    camera.transform.LookAt(player.position + Vector3.up * 0.9f);
                    camera.orthographicSize = 1.6f;
                    Capture("axe-grip-close.png", 960, 720);
                    camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
                    camera.orthographicSize = oldSize;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                yield return 0.1f;
                Require(!axe.Owner && controller.LeftHand.State == HandState.Empty && controller.RightHand.State == HandState.Empty,
                    key + " releases both hands and leaves the original axe in the world");
                Require(Mathf.Abs(axe.transform.position.y - 0.11f) < 0.03f, "Placed axe rests on floor support");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return 0.05f;
            }

            // Every selected spawn is approached with real movement, then Q pickup and E placement.
            foreach (Vector3 point in axe.spawnPoints)
            {
                StageGrapple(point + Vector3.forward * 1.35f + Vector3.up * 0.02f,
                    point - Vector3.forward * 2.5f);
                Require(TwoHandedAxe.SupportedPose(point, out Vector3 pose), "Fixture starts on saved support");
                axe.transform.SetPositionAndRotation(pose, Quaternion.identity);
                yield return 0.08f;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return 0.28f;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                yield return 0.1f;
                Require(axe.IsHeldBy(controller), "Actual approach and Q pickup at " + point + " player=" + player.position);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return 0.1f;
                Require(!axe.Owner, "E releases original axe at " + point);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return 0.05f;
            }

            StageGrapple(start, start + Vector3.forward * 2.8f);
            axe.transform.SetPositionAndRotation(start + new Vector3(0, 0.11f, 0.25f), Quaternion.identity);
            Require(axe.TryPickup(controller, HandSide.Right), "Prepare gesture check with shared pickup command");
            yield return 0.1f;
            var gestures = new[] {
                (button: (ushort)2, delta: new Vector2(-100, 8), intent: ChopIntent.RightArm),
                (button: (ushort)1, delta: new Vector2(100, -8), intent: ChopIntent.LeftArm),
                (button: (ushort)3, delta: new Vector2(8, -100), intent: ChopIntent.Leg),
                (button: (ushort)1, delta: Vector2.zero, intent: ChopIntent.Normal),
                (button: (ushort)2, delta: new Vector2(-5, 0), intent: ChopIntent.Normal),
                (button: (ushort)2, delta: new Vector2(-100, 100), intent: ChopIntent.Normal)
            };
            foreach (var gesture in gestures)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = gesture.button == 3 ? (ushort)1 : gesture.button });
                yield return 0.06f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = gesture.button, delta = gesture.delta });
                yield return 0.08f;
                Require(!attack.IsBusy && !controller.InGrapple && !controller.IsBlocking, "Hold accumulates gesture, no parallel hand action");
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = gesture.button == 3 ? (ushort)2 : (ushort)0 });
                yield return 0.05f;
                Require(attack.Phase == MeleeAttack.AttackPhase.Windup && axe.LastIntent == gesture.intent,
                    "Native gesture starts exactly one windup: " + gesture.intent + " got " + axe.LastIntent);
                Require(!attack.TryHandAttack(controller.LeftHand), "Other hand cannot bypass axe recovery");
                yield return 1.15f;
                Require(!attack.IsBusy && dummy.Health == dummy.maxHealth && axe.Owner,
                    "Miss consumes complete phases, preserves axe and never queues another attack");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return 0.1f;
                Require(!attack.IsBusy, "Second button release after recovery does not attack");
            }
            Debug.Log("RRM AXE INPUT PASSED: both hands, three intents, normal clicks, noise/tolerance and one chord attack.");

            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.2f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3, delta = new Vector2(0, -100) });
            yield return 0.06f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.05f;
            Require(attack.IsBusy && axe.LastIntent == ChopIntent.Normal, "Late second press cannot become a two-button Leg gesture");
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1, delta = new Vector2(100, 0) });
            yield return 1.2f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.1f;
            Require(!attack.IsBusy && axe.LastIntent == ChopIntent.Normal, "Busy press released after recovery never queues another swing");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            yield return 0.15f;
            Require(controller.IsCrouching && Mathf.Abs(axe.transform.localPosition.y - 1.08f * controller.crouchHeightScale) < 0.03f,
                "One shared item follows crouch once, not once per hand");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.15f;

            // Cancel a press on placement/re-pickup and on focus loss.
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2, delta = new Vector2(-100, 0) });
            yield return 0.06f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return 0.08f;
            Require(!axe.Owner && !attack.IsBusy, "Q cancels unfinished axe gesture");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return 0.08f;
            Require(axe.IsHeldBy(controller), "Re-pickup original axe while old mouse press remains held");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.1f;
            Require(!attack.IsBusy, "Old gesture cannot apply to picked-up item");
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.06f;
            controller.SendMessage("OnApplicationFocus", false);
            controller.SendMessage("OnApplicationFocus", true);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.1f;
            Require(!attack.IsBusy, "Focus cancellation cannot create a release attack");

            // Ordinary axe contact regression. Directed contacts and limb loss are checked by CheckSevering.
            float ordinaryDamage = -1f;
            foreach (var gesture in gestures.Where(gesture => gesture.intent == ChopIntent.Normal).Take(1))
            {
                StageGrapple(start, start + Vector3.forward * 1.12f);
                dummy.ResetHealth(); damageEvents = 0;
                recorder.SetViewRotation(Quaternion.LookRotation(dummyBody.position + Vector3.up - recorder.lens.position));
                int recorded = device.RecordedHits;
                yield return 0.1f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = gesture.button });
                yield return 0.05f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = gesture.button, delta = gesture.delta });
                yield return 0.06f;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return 0.6f;
                Require(damageEvents == 1 && dummy.Health < dummy.maxHealth && axe.IsHeldBy(controller),
                    "Actual " + gesture.intent + " sweep damages once and preserves axe: hits=" + damageEvents);
                float dealt = dummy.maxHealth - dummy.Health;
                if (ordinaryDamage < 0f) ordinaryDamage = dealt;
                Require(Mathf.Approximately(dealt, ordinaryDamage) && device.RecordedHits == recorded + 1,
                    "Every intent has identical ordinary contact damage; placed camera witnesses it");
                Capture("axe-contact-" + gesture.intent + ".png", 1280, 800);
                yield return 0.7f;
            }
            controller.enabled = false;
            yield return 0.08f;
            Require(!axe.Owner && !axe.transform.parent && controller.LeftHand.State == HandState.Empty
                && controller.RightHand.State == HandState.Empty, "Holder interruption releases one weapon and both hands");
            controller.enabled = true;
            yield return 0.08f;
            Require(axe.TryPickup(controller, HandSide.Left), "Same axe remains collectible after holder interruption");
            Require(recorder.ControlsVisible == false, "Guide initially closed");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
            yield return 0.1f;
            Require(recorder.ControlsVisible, "F1 still opens while axe occupies both hands");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.8f;
            ReacquireGrappleScene();
            Require(!axe && Object.FindObjectsByType<TwoHandedAxe>().Length == 1
                && Object.FindObjectsByType<CameraDevice>().Length == 1 && Object.FindObjectsByType<PortableLamp>().Length == 3,
                "R restores exactly one axe, camera and original lamps without held duplicates");
            yield return 0.1f;
        }
    }
}
