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
        public static void PrepareBleedingAndRun()
        {
            CombatPrototypeBuilder.AddActorBloodEvidence();
            RunBleedingAndExit();
        }

        public static void RunBleedingAndExit()
        {
            SessionState.SetInt("RRM.PlayCheck.Lamps", 13);
            RunAndExit();
        }

        private static void CheckBleedMinute(Damageable victim, int limbs)
        {
            Require(victim.SeveredLimbCount == limbs, "Bleeding sources match actual severed limbs");
            foreach (int fps in new[] { 20, 60, 144, 1000 })
            {
                victim.ResetHealth();
                double total = 0d;
                int events = 0;
                void Count(DamageEvent hit)
                {
                    Require(hit.IsBleeding && !hit.SeveredPart.HasValue && hit.Impulse == 0f && hit.BaseWeight == 0f,
                        "Bleed ticks are not weapon/sever events");
                    total += hit.Amount; events++;
                }
                victim.Damaged += Count;
                try { for (int i = 0; i < fps * 60; i++) victim.TickBleeding(1d / fps); }
                finally { victim.Damaged -= Count; }
                Require(Math.Abs(victim.Health - (100d - limbs * 5d)) < 0.00001d
                    && Math.Abs(total - limbs * 5d) < 0.00001d && events == fps * 60,
                    $"60 seconds at {fps} updates/s: limbs={limbs}, HP={victim.Health:R}, bleeding={total:R}");
            }
            float before = victim.Health;
            foreach (double invalid in new[] { 0d, -1d, double.NaN, double.PositiveInfinity }) victim.TickBleeding(invalid);
            Require(victim.Health == before, "Invalid bleeding time cannot alter health");
            Debug.Log($"RRM BLEED RATE PASSED: {limbs} limbs, exactly {limbs * 5} HP/min at 20/60/144/1000 updates per second.");
        }

        private static void CheckTrailSurface(BloodEvent mark, float expectedY)
        {
            Require(mark.Mark && Mathf.Abs(mark.Position.y - expectedY - 0.015f) < 0.035f
                && Physics.Raycast(mark.Position + Vector3.up * 0.025f, Vector3.down, out _, 0.08f, 1,
                    QueryTriggerInteraction.Ignore), "Trail quad lies on real room/platform support, not in air");
        }

        private static IEnumerable<float> CheckTrailPixels(BloodEvidence evidence, string name)
        {
            var visible = evidence.Marks.Where(mark => mark.Mark && recorder.InspectBlood(mark).Visible).ToArray();
            Require(visible.Length > 0 && recorder.LiveTexture, "Handheld camera can see surface trails");
            var mainWith = Capture(name + "-world.png", 1280, 800);
            var feedWith = ReadLiveFrame(name + "-feed.png");
            var renderers = evidence.Marks.Where(mark => mark.Mark).Select(mark => mark.Mark.GetComponent<Renderer>()).ToArray();
            foreach (var renderer in renderers) renderer.enabled = false;
            try
            {
                yield return 0.1f;
                Require(ChangedPixels(mainWith, Capture(name + "-world-without-marks.png", 1280, 800)) > 8,
                    "Blood marks contribute real pixels to the normal view");
                Require(ChangedPixels(feedWith, ReadLiveFrame(name + "-feed-without-marks.png")) > 8,
                    "Blood marks contribute real pixels to the live handheld feed");
            }
            finally { foreach (var renderer in renderers) if (renderer) renderer.enabled = true; }
            yield return 0.1f;
        }

        private static IEnumerator<float> CheckBleeding()
        {
            PrepareSeverHealth();
            var human = PositionSeverFight(false);
            var enemy = dummy.GetComponent<PlayerController>();
            dummy.maxHealth = 100; dummy.ResetHealth();
            var device = human.HeldCamera;
            Require(device.TryPlace(human), "Free both hands for actual axe gesture");
            var axe = Object.FindFirstObjectByType<TwoHandedAxe>();
            axe.transform.position = human.transform.TransformPoint(new Vector3(0, 0.11f, 0.3f));
            Require(axe.TryPickup(human, HandSide.Right), "Use the saved axe, not injected limb state");
            device.transform.SetPositionAndRotation(new Vector3(-3.3f, 1.25f, -5.5f), Quaternion.identity);
            recorder.SetViewRotation(Quaternion.LookRotation(dummy.transform.position + Vector3.up - recorder.lens.position));
            var blind = CloneBashWitness("Bleed check behind west wall", false);
            blind.transform.position = new Vector3(-9f, 1.25f, -3f);
            blind.SetViewRotation(Quaternion.LookRotation(dummy.transform.position + Vector3.up - blind.lens.position));
            yield return 0.1f;
            Require(!blind.CanSee(dummy.transform.position + Vector3.up), "Existing room wall occludes second camera");
            foreach (float wait in SeverSwing(human, ChopIntent.RightArm)) yield return wait;
            Require(dummy.SeveredLimbCount == 1 && device.RecordedHits == 1 && device.RecordedSeverings == 1
                && blind.GetComponent<CameraDevice>().Events.Count == 0, "Real cut recorded once; camera behind wall records nothing");
            var saved = device.Events[0];
            Require(saved.Damage.Target == dummy && saved.Damage.TargetName == "Dummy"
                && saved.Damage.SeveredPart == BodyPart.RightArm && saved.Visible
                && saved.Damage.GameTime > 0 && recorder.ReportText.Contains("RightArm"), "Cut includes contact time, target, limb and visibility");
            CheckBleedMinute(dummy, 1);
            Require(device.Events.Count == 1 && device.RecordedSeverings == 1, "Thousands of bleed ticks create no camera hit/event spam");

            var evidence = dummy.GetComponent<BloodEvidence>();
            int marks = evidence.Marks.Count;
            dummy.ResetHealth();
            double startTime = Time.timeAsDouble;
            float oldStep = Time.captureDeltaTime;
            Time.captureDeltaTime = 0.1f;
            yield return 60f;
            Time.captureDeltaTime = oldStep;
            double elapsed = Time.timeAsDouble - startTime;
            Require(Math.Abs(dummy.Health - (100d - elapsed * 5d / 60d)) < 0.025d
                && evidence.Marks.Count == marks && device.RecordedHits == 1,
                $"Actual Update clock: {elapsed:F3}s, HP={dummy.Health:R}; standing creates no trail or extra hits");
            Debug.Log($"RRM BLEED GAME MINUTE PASSED: {elapsed:F3}s, HP={dummy.Health:R}, no stationary trail spam.");

            var lights = Object.FindObjectsByType<LightSource>().Where(light => light.isActiveAndEnabled).ToArray();
            foreach (var light in lights) light.enabled = false;
            Require(saved.LightLevel > 0f && LightSource.At(saved.Damage.Point) == 0f,
                "Live light at the old contact really changes to darkness");
            device.transform.position = dummy.transform.TransformPoint(new Vector3(-0.58f, 1.25f, 0.28f));
            Require(device.TryPickup(enemy, HandSide.Left), "Injured target picks camera up with surviving hand");
            Require(device.Id.Length > 0 && device.Events[0].Damage.GameTime == saved.Damage.GameTime
                && device.Events[0].Damage.Point == saved.Damage.Point && device.Events[0].LightLevel == saved.LightLevel
                && device.Events[0].Clarity == saved.Clarity && device.Events[0].Distance == saved.Distance,
                "Removed Hurtbox, changed light, camera move and new owner cannot rewrite cut snapshot");
            foreach (var light in lights) light.enabled = true;
            Require(device.TryPlace(enemy), "Camera remains the same available world object");
            device.transform.SetPositionAndRotation(new Vector3(-3.3f, 1.25f, -5.5f), Quaternion.identity);
            recorder.SetViewRotation(Quaternion.LookRotation(dummy.transform.position + Vector3.up - recorder.lens.position));
            PositionSeverFight(false); dummy.ResetHealth();
            foreach (float wait in SeverSwing(human, ChopIntent.LeftArm)) yield return wait;
            Require(dummy.SeveredLimbCount == 2 && device.RecordedSeverings == 2, "Second real cut adds exactly one source and one recorded limb");
            CheckBleedMinute(dummy, 2);
            PositionSeverFight(false); dummy.ResetHealth();
            foreach (float wait in SeverSwing(human, ChopIntent.RightArm)) yield return wait;
            Require(dummy.SeveredLimbCount == 2 && device.RecordedSeverings == 2,
                "Contact after missing RightArm neither adds bleed source nor redirects a cut");

            Require(axe.TryPlace(human), "Put axe down before taking handheld camera");
            device.transform.position = human.transform.TransformPoint(new Vector3(-0.58f, 1.25f, 0.28f));
            Require(device.TryPickup(human, HandSide.Left), "Human picks up same recorded camera");
            player.position = new Vector3(-3.3f, 0.02f, -5.1f); player.transform.position = player.position;
            enemy.opponent = player.GetComponent<Damageable>(); enemy.moveSpeed = 1.2f;
            evidence.trailSpacing = 0.3f;
            marks = evidence.Marks.Count;
            yield return 1.7f;
            enemy.opponent = null; enemy.moveSpeed = 0;
            yield return 0.3f;
            Require(evidence.Marks.Count >= marks + 2, "Bleeding enemy leaves distance-spaced marks along actual movement");
            foreach (var mark in evidence.Marks.Skip(marks)) CheckTrailSurface(mark, 0f);
            recorder.SetViewRotation(Quaternion.LookRotation(evidence.Marks[marks].Position - recorder.lens.position));
            yield return 0.1f;
            foreach (float wait in CheckTrailPixels(evidence, "bleed-floor")) yield return wait;

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.8f;
            ReacquireGrappleScene(); PrepareSeverHealth();
            var source = PositionSeverFight(true);
            human = player.GetComponent<PlayerController>();
            var health = player.GetComponent<Damageable>(); health.maxHealth = 100; health.ResetHealth();
            axe = Object.FindFirstObjectByType<TwoHandedAxe>();
            axe.transform.position = source.transform.TransformPoint(new Vector3(0, 0.11f, 0.3f));
            Require(axe.TryPickup(source, HandSide.Right), "Enemy uses shared axe command for player wound");
            foreach (float wait in SeverSwing(source, ChopIntent.RightArm)) yield return wait;
            Require(health.SeveredLimbCount == 1 && human.HeldCamera, "Player bleeds from real enemy contact, retains left-hand camera");
            CheckBleedMinute(health, 1); health.ResetHealth();
            evidence = player.GetComponent<BloodEvidence>();
            Require(evidence, "Saved Player has the same BloodEvidence component as Dummy");
            evidence.trailSpacing = 0.25f;
            var platform = GameObject.Find("Camera Platform").GetComponent<Collider>().bounds;
            player.position = new Vector3(platform.min.x + 0.6f, platform.max.y + 0.02f, platform.center.z);
            player.rotation = Quaternion.identity;
            player.transform.SetPositionAndRotation(player.position, player.rotation);
            player.linearVelocity = Vector3.zero;
            dummyBody.position = new Vector3(-6, 0.02f, 4); dummyBody.transform.position = dummyBody.position;
            Physics.SyncTransforms();
            yield return 0.3f;
            marks = evidence.Marks.Count;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return 0.45f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.3f;
            Require(evidence.Marks.Count >= marks + 2 && human.IsGrounded, "Actual player movement leaves trails on saved raised platform");
            foreach (var mark in evidence.Marks.Skip(marks)) CheckTrailSurface(mark, platform.max.y);
            recorder.SetViewRotation(Quaternion.LookRotation(evidence.Marks[marks].Position - recorder.lens.position));
            yield return 0.1f;
            foreach (float wait in CheckTrailPixels(evidence, "bleed-platform")) yield return wait;

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return 0.05f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.2f;
            Require(!human.IsGrounded, "Arm loss still permits a real jump");
            marks = evidence.Marks.Count;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return 0.2f;
            Require(evidence.Marks.Count == marks && !human.IsGrounded, "Moving airborne does not project a trail down onto distant surfaces");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 1.2f;
            marks = evidence.Marks.Count;
            yield return 3f;
            Require(evidence.Marks.Count == marks && !player.GetComponent<AudioSource>().isPlaying
                && player.GetComponent<HitFeedback>().blood.particleCount == 0,
                "Idle bleeding has no marks, repeated impact audio or particle bursts");

            player.position = new Vector3(-3.3f, 0.02f, -3f); player.transform.position = player.position;
            player.linearVelocity = Vector3.zero;
            evidence.maxMarks = 4; evidence.trailSpacing = 0.12f;
            yield return 0.3f;
            var oldMark = evidence.Marks[0].Mark;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
            yield return 0.65f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return 0.3f;
            Require(evidence.Marks.Count == 4 && !oldMark, "Shared per-character mark cap deletes oldest objects, not just references");

            Require(axe.TryPlace(source), "Enemy frees both hands before grab");
            dummyBody.position = player.position + Vector3.right * 0.75f;
            dummyBody.rotation = Quaternion.LookRotation(Vector3.left);
            dummyBody.transform.SetPositionAndRotation(dummyBody.position, dummyBody.rotation);
            Physics.SyncTransforms();
            yield return 0.1f;
            Require(source.TryBeginGrapple(HandSide.Left, human), "Real grab exists before bleeding death");
            var deathWitness = CloneBashWitness("Bleeding death witness", false);
            deathWitness.transform.position = player.position + new Vector3(0, 1.25f, -2f);
            deathWitness.SetViewRotation(Quaternion.LookRotation(player.GetComponent<CapsuleCollider>().bounds.center - deathWitness.lens.position));
            var deathDevice = deathWitness.GetComponent<CameraDevice>();
            device = human.HeldCamera;
            string id = device.Id; int history = device.Events.Count;
            int deaths = 0;
            void CountDeath(DamageEvent hit) { if (hit.IsBleeding && hit.IsFatal) deaths++; }
            health.Damaged += CountDeath;
            health.TickBleeding(health.Health / health.BleedingPerSecond + 1d);
            yield return 0.2f;
            Require(health.IsDead && health.Health == 0 && health.BleedingPerSecond == 0d && deaths == 1
                && !human.InGrapple && !source.InGrapple && !human.GetComponent<MeleeAttack>().IsBusy
                && human.DeathMessage.Contains("YOU DIED"), "Bleeding uses ordinary death, releases pair and cancels actions");
            Require(deathDevice.DeathRecorded && deathDevice.RecordedHits == 0 && deathDevice.RecordedSeverings == 0
                && deathDevice.Events.Count == 1 && deathDevice.Events[0].Damage.Kind == DamageKind.Bleeding
                && !deathDevice.Events[0].Damage.SeveredPart.HasValue && deathDevice.ReportWeight == 0f,
                "Visible bleeding death is one death, not a weapon hit, severing or score farm");
            Require(!device.Owner && device.Id == id && device.Events.Count >= history && recorder.IsRecording
                && !recorder.LiveTexture, "Bleeding death drops intact camera and preserves history without remote feed");
            health.TickBleeding(600d);
            yield return 0.2f;
            health.Damaged -= CountDeath;
            Require(deaths == 1 && deathDevice.Events.Count == 1 && health.Health == 0f, "Dead body cannot bleed or emit another death");
            Debug.Log("RRM BLEED CONSEQUENCES PASSED: floor/platform pixels, air/idle/cap, captured death, camera identity and single fatal record.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
            yield return 0.1f;
            Require(recorder.ControlsVisible, "Updated F1 remains available after bleeding death");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return 0.8f;
            ReacquireGrappleScene();
            foreach (var actor in Object.FindObjectsByType<PlayerController>())
                Require(actor.HasHand(HandSide.Left) && actor.HasHand(HandSide.Right) && actor.HasBothLegs
                    && actor.LimbMovementScale == 1f && actor.GetComponent<Damageable>().BleedingPerSecond == 0d
                    && actor.GetComponent<BloodEvidence>().Marks.Count == 0 && !actor.InGrapple,
                    "Reload restores hands/legs, movement and no wounds/bleeding/trails/grapple");
            Require(SeveredParts().Length == 0 && Object.FindObjectsByType<CameraDevice>().Length == 1
                && Object.FindObjectsByType<TwoHandedAxe>().Length == 1 && Object.FindObjectsByType<PortableLamp>().Length == 3,
                "Reload clears detached limbs and restores exact original items");
            yield return 0.1f;
        }
    }
}
