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
        public static void ConfigureDuelAndRun()
        {
            const string path = "Assets/RRM/Prefabs/Dummy.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.GetComponent<MeleeAttack>().fistDamage = 36f;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            RunDuelAndExit();
        }

        public static void RunDuelAndExit()
        {
            SessionState.SetInt("RRM.PlayCheck.Lamps", 10);
            RunAndExit();
        }

        private static PlayerController StageDuel(Vector3 position, Vector3 target)
        {
            StageGrapple(position, target);
            var enemy = dummy.GetComponent<PlayerController>();
            enemy.opponent = player.GetComponent<Damageable>();
            enemy.moveSpeed = 1.2f; enemy.turnSpeed = 360f; enemy.attackDistance = 1.05f;
            enemy.grappleChance = enemy.blockChance = enemy.escapeChance = 0f;
            dummyBody.rotation = Quaternion.LookRotation(position - target);
            dummyBody.transform.rotation = dummyBody.rotation;
            Physics.SyncTransforms();
            return enemy;
        }

        private static IEnumerator<float> CheckDuel()
        {
            Vector3 start = new Vector3(-3.3f, 0.02f, -4.1f);
            var controller = player.GetComponent<PlayerController>();
            var attack = player.GetComponent<MeleeAttack>();
            var health = player.GetComponent<Damageable>();
            var device = controller.HeldCamera;
            Require(dummy.GetComponent<MeleeAttack>().fistDamage == 36f, "Saved enemy fist is dangerous; uses shared hand attack");
            Require(device.TryPlace(controller), "Place initial camera without discarding history");
            var enemy = StageDuel(new Vector3(-2, 0.02f, -2), new Vector3(2, 0.02f, -2));
            var enemyAttack = dummy.GetComponent<MeleeAttack>();
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Temporary closed passage check";
            door.transform.position = new Vector3(0, 1.5f, -2);
            door.transform.localScale = new Vector3(0.3f, 3, 2.4f);
            Physics.SyncTransforms();
            Vector3 before = dummyBody.position;
            yield return 1f;
            Require(enemy.State == PlayerController.EnemyState.Idle && !enemyAttack.IsBusy
                && Vector3.Distance(before, dummyBody.position) < 0.05f && health.Health == health.maxHealth,
                "Closed passage blocks detection, approach and melee");
            Object.Destroy(door);
            yield return 2.7f;
            Require(dummyBody.position.x < -0.25f && health.Health == health.maxHealth,
                $"Bot walks through open passage using room physics: {dummyBody.position}");

            enemy = StageDuel(new Vector3(-5.5f, 0.02f, 1.4f), new Vector3(-5.5f, 0.02f, -2.2f));
            float deadline = Time.time + 8f;
            while (dummyBody.position.z < 0.4f && Time.time < deadline) yield return 0.1f;
            Require(dummyBody.position.z > 0.4f, $"Local steering clears existing low cover: {dummyBody.position}");
            Debug.Log("RRM DUEL SPACE PASSED: closed/open passage and low-cover steering.");

            enemy = StageDuel(start, start + Vector3.forward * 0.7f);
            health.ResetHealth(); dummy.ResetHealth();
            yield return 0.1f;
            Require(enemyAttack.Phase == MeleeAttack.AttackPhase.Windup && health.Health == health.maxHealth,
                "Visible close target starts readable AI windup, not direct damage");
            Quaternion committed = enemy.transform.rotation;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
            yield return 0.2f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.7f;
            Require(enemyAttack.Phase == MeleeAttack.AttackPhase.Recovery && health.Health == health.maxHealth
                && Quaternion.Angle(committed, enemy.transform.rotation) < 0.1f,
                "Retreat evades real sweep; enemy keeps committed facing and recovers");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return 0.2f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.06f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.6f;
            Require(dummy.Health < dummy.maxHealth && enemyAttack.Phase == MeleeAttack.AttackPhase.Recovery,
                $"Player counterpunch lands during AI miss recovery: {dummy.Health}");
            Capture("duel-counter.png", 1280, 800);
            yield return 0.6f;

            foreach (float delay in new[] { 0.25f, 1f })
            {
                enemy = StageDuel(start, start + Vector3.forward * 0.7f);
                enemy.attackDistance = 0.1f; enemy.moveSpeed = 0;
                enemy.blockChance = 1f; enemy.blockReactionDelay = delay;
                dummy.ResetHealth();
                yield return 0.1f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                yield return 0.06f;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return 1.3f;
                Require(delay < 1f ? dummy.Health == dummy.maxHealth : dummy.Health < dummy.maxHealth,
                    $"AI reacts only to existing windup; block delay={delay}, health={dummy.Health}");
            }
            Debug.Log("RRM DUEL ATTACK/BLOCK PASSED: actual AI attack, evasion/counter, timely and late reaction.");

            foreach (float chance in new[] { 0f, 1f })
            {
                enemy = StageDuel(start, start + Vector3.forward * 0.75f);
                enemy.attackDistance = 0.1f; enemy.moveSpeed = 0; enemy.escapeChance = chance;
                enemy.escapeReactionDelay = 0.12f;
                dummy.ResetHealth();
                yield return 0.1f;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                yield return 0.4f;
                Require(controller.GrappleTarget == enemy, "Actual held RMB captures AI using shared grapple");
                yield return 0.8f;
                Require(chance == 0 ? controller.GrappleTarget == enemy : !controller.InGrapple && !enemy.InGrapple,
                    $"AI escape obeys chance={chance} and delayed shared input window");
                if (chance == 0)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 });
                    yield return 0.06f;
                    InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
                    yield return 0.7f;
                    Require(dummy.Health < dummy.maxHealth && controller.GrappleTarget == enemy,
                        "Player free-hand strike damages held bot without releasing it");
                }
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return 1.3f;
                Require(!controller.InGrapple && !enemy.InGrapple, "Player release or successful AI escape frees both actors");
            }

            enemy = StageDuel(start, start + Vector3.forward * 0.7f);
            enemy.grappleChance = 1f;
            health.ResetHealth();
            deadline = Time.time + 1f;
            while (!controller.IsCaptured && Time.time < deadline) yield return 0.02f;
            Require(controller.Captor == enemy, "Bot decision actually captures the player");
            Require(!controller.EscapeWindowOpen && !controller.BeginEscapeAttempt(HandSide.Left),
                "Early shared escape command consumes one attempt but does not free player");
            // First cycle intentionally missed. Bot's punch opens a new window; it cannot punch before one existed.
            deadline = Time.time + 3f;
            while (health.Health == health.maxHealth && Time.time < deadline) yield return 0.02f;
            Require(health.Health < health.maxHealth && controller.IsCaptured, "Bot punches held player through normal melee contact");
            deadline = Time.time + 1.2f;
            while (!controller.EscapeWindowOpen && Time.time < deadline) yield return 0.02f;
            Require(controller.EscapeWindowOpen && controller.EscapeHint.Contains("BREAK FREE"),
                "Post-hit escape window exists before the next bot strike and has local feedback");
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Verification/duel-escape-window.png"));
            yield return 0.04f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return 0.06f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 0.1f;
            Require(!controller.InGrapple && !enemy.InGrapple && !enemy.TryBeginGrapple(HandSide.Right, controller),
                "Fresh player click escapes bot and grants only re-grab protection");
            Debug.Log("RRM DUEL GRAPPLE PASSED: both captor directions, free-hand damage, chance and real player window escape.");

            // Fatal event must finish observation with the lamp lit, before the next Update drops items.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.8f;
            ReacquireGrappleScene();
            controller = player.GetComponent<PlayerController>(); attack = player.GetComponent<MeleeAttack>();
            health = player.GetComponent<Damageable>(); device = controller.HeldCamera;
            enemy = StageDuel(start, start + Vector3.forward * 0.7f);
            enemy.opponent = null; enemy.moveSpeed = enemy.turnSpeed = 0;
            enemyAttack = enemy.GetComponent<MeleeAttack>();
            var lamp = GameObject.Find("Portable Lamp 1").GetComponent<PortableLamp>();
            lamp.transform.position = start + new Vector3(0.55f, 0, 0.35f);
            Physics.SyncTransforms();
            Require(lamp.TryPickup(controller, HandSide.Right), "Player holds lamp and camera before fatal duel");
            var kickTarget = attack.FindKickTarget();
            Require(kickTarget, "Occupied-hand kick has a real reachable body zone");
            recorder.SetViewRotation(Quaternion.LookRotation(kickTarget.transform.position - recorder.lens.position));
            string id = device.Id;
            var witness = CloneBashWitness("Duel witness", false);
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.06f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 1.3f;
            Require(damageEvents == 1 && device.RecordedHits > 0,
                "Held camera aimed at kick zone starts fatal duel with genuine history: hits=" + damageEvents + ", recorded=" + device.RecordedHits);
            enemy.opponent = health; enemy.moveSpeed = 1.2f; enemy.turnSpeed = 360f;
            deadline = Time.time + 12f;
            while (!health.IsDead && Time.time < deadline) yield return 0.1f;
            yield return 0.2f;
            Require(health.IsDead && controller.DeathMessage.Contains("YOU DIED"), "Idle player dies from autonomous shared fist attacks");
            Require(!controller.InGrapple && !enemy.InGrapple && !attack.IsBusy
                && controller.LeftHand.State == HandState.Empty && controller.RightHand.State == HandState.Empty,
                "Death cancels actions and releases both hands");
            Require(device.Id == id && !device.Owner && !device.transform.parent && recorder.IsRecording && !recorder.LiveTexture
                && !lamp.Owner && !lamp.transform.parent && lamp.lightSource.isActiveAndEnabled && lamp.lightSource.pointLight.enabled,
                "Death leaves same camera/history and visible working lamp detached in world");
            Require(witness.GetComponent<CameraDevice>().Events.Any(hit => hit.Damage.Source == enemy.gameObject
                && hit.Damage.Target == health && hit.Damage.IsFatal),
                "Placed witness records player death from enemy contact, independent of winning");
            int history = device.RecordedHits;
            Require(!device.TryPickup(enemy, HandSide.Left), "Fatal strike recovery still rejects immediate pickup");
            yield return 1.4f;
            Require(device.TryPickup(enemy, HandSide.Left) && device.Id == id && device.RecordedHits == history
                && recorder.ReportText.Contains("Camera owner: Dummy"), "Other actor picks same device/history after player death; preview owner updates");
            enemy.enabled = false;
            Require(!device.Owner && !device.transform.parent && device.RecordedHits == history,
                "Disabling the holder also frees camera without destroying records");
            Debug.Log($"RRM DUEL PLAYER DEATH PASSED: history={history}; camera/lamp survive; next owner updated.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.8f;
            ReacquireGrappleScene();
            controller = player.GetComponent<PlayerController>(); device = controller.HeldCamera;
            enemy = StageDuel(start, start + Vector3.forward * 0.7f);
            foreach (PortableLamp item in Object.FindObjectsByType<PortableLamp>()) item.transform.position += Vector3.left * 3f;
            enemy.opponent = null; enemy.moveSpeed = enemy.turnSpeed = 0;
            yield return 0.1f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.06f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 1.3f;
            Require(device.RecordedHits > 0, "Device has genuine history before passing to enemy");
            Require(device.TryPlace(controller) && device.TryPickup(enemy, HandSide.Left), "Enemy can own existing camera through normal pickup command");
            recorder.SetViewRotation(Quaternion.LookRotation(player.position + Vector3.up - recorder.lens.position));
            id = device.Id;
            health = player.GetComponent<Damageable>();
            bool checkedContact = false, correctObservation = false;
            int priorHits = device.RecordedHits;
            System.Action<DamageEvent> inspect = hit =>
            {
                checkedContact = true;
                bool visible = recorder.CanSee(hit.Point);
                correctObservation = recorder.IsRecording && device.RecordedHits == priorHits + (visible ? 1 : 0);
                Debug.Log($"RRM DUEL HELD WITNESS: visible={visible}; hits={priorHits}->{device.RecordedHits}; point={hit.Point}; lens={recorder.lens.position}");
            };
            health.Damaged += inspect;
            enemy.opponent = health; enemy.moveSpeed = 1.2f; enemy.turnSpeed = 360f;
            deadline = Time.time + 3f;
            while (health.Health == health.maxHealth && Time.time < deadline) yield return 0.02f;
            health.Damaged -= inspect;
            Require(health.Health < health.maxHealth && checkedContact && correctObservation,
                $"Enemy free hand deals actual damage; its camera records exactly visible contacts: health={health.Health}, observed={checkedContact}, correct={correctObservation}");
            enemy.opponent = null; enemy.moveSpeed = enemy.turnSpeed = 0;
            yield return 1.4f;
            // Re-stage contact range only, preserving the held device and its nonempty history.
            player.position = dummyBody.position + Vector3.back * 0.7f;
            player.transform.position = player.position; player.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            dummy.maxHealth = 1f; dummy.ResetHealth();
            yield return 0.1f;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 });
            yield return 0.06f;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return 1.3f;
            Require(dummy.IsDead && !device.Owner && !device.transform.parent && device.Id == id && recorder.IsRecording,
                "Player's real fatal punch releases enemy-owned camera alive");
            history = device.RecordedHits;
            Require(history > 0 && recorder.ReportReady && recorder.ReportText.Contains("Camera owner: NONE"),
                "Fatal hit is retained and report remains a current-owner preview");
            before = player.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
            deadline = Time.time + 1f;
            while (Vector3.Distance(before, player.position) < 0.35f && Time.time < deadline) yield return 0.02f;
            Debug.Log($"RRM DUEL REPORT MOVEMENT: before={before}; after={player.position}; velocity={player.linearVelocity}; "
                + $"constraints={player.constraints}; health={health.Health}; speed={controller.moveSpeed}; crouch={controller.IsCrouching}; "
                + $"attack={player.GetComponent<MeleeAttack>().Phase}; paused={player.GetComponent<MeleeAttack>().IsPaused}");
            Require(Vector3.Distance(before, player.position) > 0.15f, "Living player can walk while death report is open");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            deadline = Time.time + 1.5f;
            while (Vector3.Distance(before, player.position) > 0.12f && Time.time < deadline) yield return 0.02f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return 0.1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Require(controller.HeldCamera == device && device.Id == id && device.RecordedHits == history
                && recorder.LiveTexture && recorder.ReportText.Contains("Camera owner: Player"),
                "After enemy death Q collects same device; existing report reflects new owner/history");
            ReadLiveFrame("duel-recovered-camera-feed.png");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
            yield return 0.1f;
            Require(recorder.ControlsVisible, "F1 available after duel");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.8f;
            ReacquireGrappleScene();
            Require(Object.FindObjectsByType<CameraDevice>().Length == 1 && Object.FindObjectsByType<PortableLamp>().Length == 3
                && Object.FindObjectsByType<LightSource>().Length == 11
                && Object.FindObjectsByType<PlayerController>().All(actor => !actor.InGrapple)
                && recorder.GetComponent<CameraDevice>().RecordedHits == 0, "R restores original scene without pairs/items/lights/history duplicates");
            Debug.Log("RRM DUEL DEATH/OWNERSHIP PASSED: both winners, same-device report/pickup, disable and clean restart.");
        }
    }
}
