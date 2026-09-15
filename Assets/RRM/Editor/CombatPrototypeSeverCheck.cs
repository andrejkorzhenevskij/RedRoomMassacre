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
        private static readonly List<DamageEvent> severEvents = new List<DamageEvent>();
        public static void RunSeveringAndExit()
        {
            SessionState.SetInt("RRM.PlayCheck.Lamps", 12);
            RunAndExit();
        }

        public static void PrepareSeveringAndRun()
        {
            CombatPrototypeBuilder.AddLimbHurtboxes();
            RunSeveringAndExit();
        }

        private static void ObserveSever(DamageEvent hit) { if (!hit.IsBleeding) severEvents.Add(hit); }

        private static PlayerController PositionSeverFight(bool enemyAttacker, float gap = 1.1f)
        {
            var human = player.GetComponent<PlayerController>();
            var enemy = dummy.GetComponent<PlayerController>();
            human.turnSpeed = human.attackTurnSpeed = 0;
            enemy.opponent = null; enemy.moveSpeed = enemy.turnSpeed = 0; enemy.enabled = true;
            Vector3 target = new Vector3(-3.3f, 0.02f, -3f);
            player.position = enemyAttacker ? target : target - Vector3.forward * gap;
            dummyBody.position = enemyAttacker ? target - Vector3.forward * gap : target;
            player.rotation = Quaternion.Euler(0, enemyAttacker ? 180 : 0, 0);
            dummyBody.rotation = Quaternion.Euler(0, enemyAttacker ? 0 : 180, 0);
            player.transform.SetPositionAndRotation(player.position, player.rotation);
            dummyBody.transform.SetPositionAndRotation(dummyBody.position, dummyBody.rotation);
            player.linearVelocity = dummyBody.linearVelocity = Vector3.zero;
            player.angularVelocity = dummyBody.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            return enemyAttacker ? enemy : human;
        }

        private static void PrepareSeverHealth()
        {
            foreach (var health in new[] { dummy, player.GetComponent<Damageable>() })
            {
                health.maxHealth = 1000;
                health.ResetHealth();
                health.Damaged -= ObserveSever;
                health.Damaged += ObserveSever;
                Require(health.GetComponentsInChildren<Hurtbox>().Length == 6, "Saved actor includes both leg Hurtboxes");
            }
        }

        private static IEnumerable<float> SeverSwing(PlayerController attacker, ChopIntent intent, ushort normalButton = 2)
        {
            severEvents.Clear();
            var attack = attacker.GetComponent<MeleeAttack>();
            if (attacker.autonomousMovement)
                Require(attack.TryHandAttack(attacker.RightHand, intent), "Enemy uses the same hand attack command");
            else
            {
                ushort button = intent == ChopIntent.Leg ? (ushort)3 : intent == ChopIntent.LeftArm ? (ushort)1
                    : intent == ChopIntent.RightArm ? (ushort)2 : normalButton;
                Vector2 delta = intent == ChopIntent.Leg ? new Vector2(0, -100)
                    : intent == ChopIntent.RightArm ? new Vector2(-100, 0)
                    : intent == ChopIntent.LeftArm ? new Vector2(100, 0) : Vector2.zero;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = button });
                yield return 0.05f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = button, delta = delta });
                yield return 0.06f;
                InputSystem.QueueStateEvent(mouse, new MouseState());
            }
            yield return attack.windup + attack.strike + attack.recovery + 0.25f;
            Require(!attack.IsBusy, "Completed windup, contact and recovery");
        }

        private static Rigidbody[] SeveredParts() => Object.FindObjectsByType<Rigidbody>()
            .Where(body => body.name.StartsWith("Severed ")).ToArray();

        private static void CheckLostPart(Damageable victim, BodyPart expected, int beforeParts)
        {
            Require(severEvents.Count == 1 && severEvents[0].Target == victim
                && severEvents[0].SeveredPart == expected && victim.GetLimbState(expected) == LimbState.Severed,
                $"One real hit severs {expected}; events={severEvents.Count}, contact={(severEvents.Count > 0 ? severEvents[0].Part.ToString() : "MISS")}, sever={(severEvents.Count > 0 ? severEvents[0].SeveredPart.ToString() : "NONE")}");
            var zone = victim.GetComponentsInChildren<Hurtbox>().First(part => part.part == expected);
            Require(!zone.enabled && !zone.GetComponent<Renderer>().enabled && !zone.GetComponent<Collider>().enabled,
                "Lost limb is absent from living mesh and contacts, without deleting its transform");
            Require(SeveredParts().Length == beforeParts + 1, "Exactly one new physical limb, no repeated detach");
            foreach (var part in SeveredParts())
                Require(part.GetComponent<Renderer>().enabled && part.GetComponent<Collider>().enabled
                    && !part.GetComponent<Damageable>() && !part.GetComponent<PlayerController>()
                    && part.GetComponents<MonoBehaviour>().Length == 0, "Loose limb is visible physics, never another combat actor");
            Require(Mathf.Approximately(severEvents[0].Amount, 45f * zone.damageMultiplier), "One ordinary weapon damage application, no sever bonus damage");
        }

        private static IEnumerator<float> CheckSevering()
        {
            PrepareSeverHealth();
            var human = PositionSeverFight(false, 0.72f);
            yield return 0.1f;
            foreach (float wait in SeverSwing(human, ChopIntent.Normal)) yield return wait;
            Require(severEvents.Count == 1 && !severEvents[0].SeveredPart.HasValue, "Real fist contact never severs");
            var lamp = Object.FindFirstObjectByType<PortableLamp>();
            PositionSeverFight(false, 0.72f);
            lamp.transform.position = human.transform.TransformPoint(new Vector3(0.55f, 0.85f, 0.35f));
            Require(lamp.TryPickup(human, HandSide.Right), "Prepare real lamp bash");
            foreach (float wait in SeverSwing(human, ChopIntent.Normal)) yield return wait;
            Require(severEvents.Count == 1 && !severEvents[0].SeveredPart.HasValue && !lamp, "Lamp contact breaks only lamp, never a limb");
            PositionSeverFight(false, 0.72f);
            foreach (float wait in SeverSwing(human, ChopIntent.Normal, 1)) yield return wait;
            Require(severEvents.Count == 1 && !severEvents[0].SeveredPart.HasValue && !human.HeldCamera, "Camera bash never severs");
            PositionSeverFight(false, 0.72f);
            yield return 0.1f;
            severEvents.Clear();
            var foot = human.GetComponent<MeleeAttack>();
            Require(foot.TryKick(foot.FindKickTarget()), "Real shared kick starts");
            yield return 1.3f;
            Require(severEvents.Count == 1 && !severEvents[0].SeveredPart.HasValue && SeveredParts().Length == 0,
                "Kick contact never severs; ordinary attacks create no loose parts");

            foreach (bool enemyAttacker in new[] { false, true })
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                yield return 0.8f;
                ReacquireGrappleScene(); PrepareSeverHealth();
                var attacker = PositionSeverFight(enemyAttacker);
                human = player.GetComponent<PlayerController>();
                var victim = enemyAttacker ? player.GetComponent<Damageable>() : dummy;
                var victimController = victim.GetComponent<PlayerController>();
                var device = human.HeldCamera;
                Require(device.TryPlace(human), "Explicitly free human camera hand");
                var axe = Object.FindFirstObjectByType<TwoHandedAxe>();
                axe.transform.position = attacker.transform.TransformPoint(new Vector3(0, 0.11f, 0.3f));
                Require(axe.TryPickup(attacker, HandSide.Right), "Attacker holds the original two-handed axe");
                device.transform.SetPositionAndRotation(new Vector3(-3.3f, 1.25f, -5.5f), Quaternion.identity);
                recorder.SetViewRotation(Quaternion.LookRotation(victim.transform.position + Vector3.up - recorder.lens.position));
                yield return 0.1f;
                foreach (float wait in SeverSwing(attacker, ChopIntent.Normal)) yield return wait;
                Require(device.RecordedHits > 0, "Original camera earns real history before pickup and severed-hand drop");
                PositionSeverFight(enemyAttacker);
                device.transform.position = victim.transform.TransformPoint(new Vector3(0.58f, 1.25f, 0.28f));
                Require(device.TryPickup(victimController, HandSide.Right), "Victim holds original camera in intended arm");
                string cameraId = device.Id;
                int priorHistory = device.RecordedHits;
                var witness = CloneBashWitness("Temporary sever witness", false);
                witness.SetViewRotation(Quaternion.LookRotation(victim.transform.position + Vector3.up * 0.8f - witness.lens.position));
                var witnessDevice = witness.GetComponent<CameraDevice>();
                yield return 0.1f;

                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.transform.position = (attacker.transform.position + victim.transform.position) * 0.5f + Vector3.up;
                blocker.transform.localScale = new Vector3(2, 2, 0.2f);
                Physics.SyncTransforms();
                foreach (float wait in SeverSwing(attacker, ChopIntent.RightArm)) yield return wait;
                Require(severEvents.Count == 0 && SeveredParts().Length == 0, "Axe gesture cannot damage or sever through a solid wall");
                Object.Destroy(blocker);
                yield return 0.05f;
                PositionSeverFight(enemyAttacker);

                foreach (float wait in SeverSwing(attacker, ChopIntent.Normal)) yield return wait;
                Require(severEvents.Count == 1 && !severEvents[0].SeveredPart.HasValue && SeveredParts().Length == 0,
                    "Normal axe has real contact but no sever capability");
                PositionSeverFight(enemyAttacker);
                int before = witnessDevice.RecordedSeverings;
                foreach (var zone in victim.GetComponentsInChildren<Hurtbox>()) zone.GetComponent<Collider>().enabled = false;
                foreach (var zone in victim.GetComponentsInChildren<Hurtbox>().Reverse()) zone.GetComponent<Collider>().enabled = true;
                PlayerController captor = null;
                if (enemyAttacker)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RRM/Prefabs/Dummy.prefab");
                    captor = Object.Instantiate(prefab).GetComponent<PlayerController>();
                    captor.name = "Temporary grapple participant";
                    captor.opponent = null; captor.moveSpeed = captor.turnSpeed = 0;
                    captor.transform.SetPositionAndRotation(victim.transform.position + Vector3.right * 0.75f,
                        Quaternion.LookRotation(Vector3.left));
                    Physics.SyncTransforms();
                    yield return 0.05f;
                    Require(captor.TryBeginGrapple(HandSide.Left, victimController), "Third actor establishes a real grapple before the cut");
                }
                foreach (float wait in SeverSwing(attacker, ChopIntent.RightArm)) yield return wait;
                CheckLostPart(victim, BodyPart.RightArm, 0);
                if (captor)
                {
                    Require(!captor.InGrapple && !victimController.InGrapple, "Arm sever releases both sides of an existing grapple");
                    Object.Destroy(captor.gameObject);
                    yield return 0.05f;
                }
                Require(victimController.RightHand.State == HandState.Unavailable && victimController.LeftHand.IsAvailable
                    && !device.Owner && device.Id == cameraId && device.RecordedHits >= priorHistory
                    && !device.transform.parent && device.GetComponent<Rigidbody>() && !recorder.LiveTexture && recorder.IsRecording,
                    "Right arm drops same camera with ID/history; no handheld feed, recording remains active");
                Require(witnessDevice.RecordedSeverings == before + 1, "Placed camera observes one sever result in the same damage event");
                Require(!device.TryPickup(victimController, HandSide.Right)
                    && !victim.GetComponent<MeleeAttack>().TryHandAttack(victimController.RightHand)
                    && !victimController.TryBlock() && !victimController.TryBeginGrapple(HandSide.Left),
                    "Unavailable hand cannot pick up/punch; missing arm forbids block/grapple");

                PositionSeverFight(enemyAttacker);
                foreach (float wait in SeverSwing(attacker, ChopIntent.RightArm)) yield return wait;
                Require(severEvents.Count == 1 && !severEvents[0].SeveredPart.HasValue
                    && victim.IsAttached(BodyPart.LeftArm) && SeveredParts().Length == 1,
                    "Repeated RightArm does ordinary damage, never re-severs or redirects to left arm");
                PositionSeverFight(enemyAttacker);
                Require(victim.GetComponent<MeleeAttack>().TryHandAttack(victimController.LeftHand), "Remaining hand still starts an ordinary punch");
                yield return victim.GetComponent<MeleeAttack>().windup + victim.GetComponent<MeleeAttack>().strike
                    + victim.GetComponent<MeleeAttack>().recovery + 0.2f;
                device.transform.position = victim.transform.TransformPoint(new Vector3(-0.58f, 1.25f, 0.28f));
                Require(device.TryPickup(victimController, HandSide.Left) && device.Id == cameraId, "Remaining hand picks up same recorded camera");
                Require(device.TryPlace(victimController), "Remaining hand can put camera down");
                device.transform.SetPositionAndRotation(new Vector3(-6.8f, 1.2f, -4.8f), Quaternion.identity);
                lamp = Object.FindFirstObjectByType<PortableLamp>();
                lamp.transform.position = victim.transform.TransformPoint(new Vector3(-0.55f, 0.85f, 0.35f));
                Require(lamp.TryPickup(victimController, HandSide.Left), "Remaining hand holds lamp");
                PositionSeverFight(enemyAttacker);
                foreach (float wait in SeverSwing(attacker, ChopIntent.LeftArm)) yield return wait;
                CheckLostPart(victim, BodyPart.LeftArm, 1);
                Require(!lamp.Owner && lamp.GetComponent<Rigidbody>() && lamp.lightSource.isActiveAndEnabled
                    && lamp.lightSource.pointLight.enabled && victimController.LeftHand.State == HandState.Unavailable,
                    "Left arm drops the same intact lit lamp");

                for (int legs = 0; legs < 2; legs++)
                {
                    PositionSeverFight(enemyAttacker);
                    foreach (float wait in SeverSwing(attacker, ChopIntent.Leg)) yield return wait;
                    Require(severEvents.Count == 1 && severEvents[0].SeveredPart.HasValue, "Low blade actually contacts a surviving leg");
                    BodyPart leg = severEvents[0].SeveredPart.Value;
                    Require(leg == BodyPart.LeftLeg || leg == BodyPart.RightLeg, "Leg gesture selects only an anatomical leg");
                    CheckLostPart(victim, leg, 2 + legs);
                    Require(!victimController.HasBothLegs && victimController.LimbMovementScale == 0.1f
                        && !victim.GetComponent<MeleeAttack>().TryKick(victim.GetComponentsInChildren<Hurtbox>().First()),
                        "One or two missing legs: exactly 0.1 multiplier and shared kick rejection");
                }
                Require(victim.GetComponent<CapsuleCollider>().enabled && victim.GetComponent<Rigidbody>().useGravity
                    && !victim.GetComponent<Rigidbody>().isKinematic && !victim.IsDead, "Living capsule and gravity survive loss of both legs");
                Capture(enemyAttacker ? "sever-player.png" : "sever-dummy.png", 1280, 800);
                Vector3 beforeMove = victim.transform.position;
                float configuredSpeed = victimController.moveSpeed;
                if (enemyAttacker)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                    yield return 0.8f;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    yield return 0.05f;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return 0.15f;
                    Require(player.position.y < 0.08f, "Native Space does not jump after leg loss");
                }
                else
                {
                    victimController.opponent = player.GetComponent<Damageable>();
                    victimController.moveSpeed = 1.2f;
                    configuredSpeed = 1.2f;
                    player.position = new Vector3(-3.3f, 0.02f, -5.1f); player.transform.position = player.position;
                    yield return 0.8f;
                    victimController.opponent = null;
                }
                float travelled = Vector3.ProjectOnPlane(victim.transform.position - beforeMove, Vector3.up).magnitude;
                Require(travelled > 0.015f && travelled < configuredSpeed * 0.2f
                    && victimController.moveSpeed == configuredSpeed, "Actual injured movement is slow without overwriting moveSpeed: "
                        + travelled + " state=" + victimController.State + " target=" + player.position + " victim=" + victim.transform.position);
                Require(SeveredParts().All(part => part.GetComponent<Collider>().bounds.min.y > -0.1f), "Loose parts fall onto room physics and remain");
                Debug.Log("RRM SEVER BODY PASSED: victim=" + victim.name + ", four real cuts, item identity, movement and per-camera evidence.");
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.8f;
            ReacquireGrappleScene(); PrepareSeverHealth();
            var source = PositionSeverFight(true);
            human = player.GetComponent<PlayerController>();
            Require(human.HeldCamera.TryPlace(human), "Free player hands for two-handed victim fixture");
            var sourceAxe = Object.FindFirstObjectByType<TwoHandedAxe>();
            sourceAxe.transform.position = source.transform.TransformPoint(new Vector3(0, 0.11f, 0.3f));
            Require(sourceAxe.TryPickup(source, HandSide.Right), "Enemy takes scene axe");
            var heldAxe = Object.Instantiate(sourceAxe.gameObject, null).GetComponent<TwoHandedAxe>();
            yield return 0.05f;
            heldAxe.transform.position = human.transform.TransformPoint(new Vector3(0, 0.11f, 0.3f));
            Require(heldAxe.TryPickup(human, HandSide.Left), "Temporary second axe tests a two-handed victim, not saved in scene");
            foreach (float wait in SeverSwing(source, ChopIntent.RightArm)) yield return wait;
            CheckLostPart(player.GetComponent<Damageable>(), BodyPart.RightArm, 0);
            Require(!heldAxe.Owner && !heldAxe.transform.parent && heldAxe.GetComponent<Rigidbody>()
                && human.LeftHand.State == HandState.Empty && human.RightHand.State == HandState.Unavailable
                && !heldAxe.TryPickup(human, HandSide.Left), "Losing one arm releases both axe references; one arm cannot pick it back up");
            var dying = player.GetComponent<Damageable>();
            dying.maxHealth = 1; dying.ResetHealth();
            PositionSeverFight(true);
            foreach (float wait in SeverSwing(source, ChopIntent.LeftArm)) yield return wait;
            Require(dying.IsDead && severEvents.Count == 1 && severEvents[0].IsFatal
                && severEvents[0].SeveredPart == BodyPart.LeftArm, "Fatal contact may sever a limb that was alive at contact");
            int deadParts = SeveredParts().Length;
            foreach (float wait in SeverSwing(source, ChopIntent.Leg)) yield return wait;
            Require(severEvents.Count == 0 && SeveredParts().Length == deadParts
                && dying.HasBothLegs, "Subsequent corpse contact never damages or severs remaining limbs");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.8f;
            ReacquireGrappleScene(); PrepareSeverHealth();
            source = PositionSeverFight(true);
            human = player.GetComponent<PlayerController>();
            sourceAxe = Object.FindFirstObjectByType<TwoHandedAxe>();
            sourceAxe.transform.position = source.transform.TransformPoint(new Vector3(0, 0.11f, 0.3f));
            Require(sourceAxe.TryPickup(source, HandSide.Right), "Prepare aerial leg contact");
            var sourceAttack = source.GetComponent<MeleeAttack>();
            sourceAttack.windup = 0.05f;
            player.rotation = Quaternion.identity;
            player.transform.rotation = player.rotation;
            Physics.SyncTransforms();
            yield return 0.1f;
            Require(!human.GetComponent<MeleeAttack>().FindKickTarget(), "Attacker behind player cannot steal Space jump context");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.05f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.04f;
            Require(sourceAttack.TryHandAttack(source.RightHand, ChopIntent.Leg), "Shared low chop during player's real jump");
            float deadline = Time.time + 0.5f;
            while (human.HasBothLegs && Time.time < deadline) yield return 0.02f;
            Require(!human.HasBothLegs && player.position.y > 0.1f && player.useGravity && !player.isKinematic,
                "Leg loss during actual jump leaves the player airborne under gravity: " + player.position);
            Capture("sever-airborne.png", 1280, 800);
            yield return 1f;
            Require(human.IsGrounded && player.position.y < 0.08f, "Injured airborne player lands normally, without teleport");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
            yield return 0.1f;
            Require(recorder.ControlsVisible, "Updated F1 remains available after injury");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.8f;
            ReacquireGrappleScene();
            Require(SeveredParts().Length == 0 && Object.FindObjectsByType<TwoHandedAxe>().Length == 1
                && Object.FindObjectsByType<CameraDevice>().Length == 1 && Object.FindObjectsByType<PortableLamp>().Length == 3,
                "R clears debris and temporary fixtures, restores original items without duplicates");
            foreach (var health in new[] { dummy, player.GetComponent<Damageable>() })
                Require(health.GetComponentsInChildren<Hurtbox>().All(zone => zone.enabled && zone.GetComponent<Renderer>().enabled
                    && health.IsAttached(zone.part)), "Restart restores all six body zones");
            yield return 0.1f;
        }
    }
}
