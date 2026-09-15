# RRM technical prototype

## Player visual and screen movement (2026-09-16)

Player now uses the same saved CharCrafter visual/Avatar as Dummy, with its own
original gameplay root and six bone-mounted Hurtboxes. No duplicate controller,
health, physics, camera or skeleton is added. `RRM > CharCrafter > Integrate Player`
updates only Player.prefab; repeating it validates the result. The existing Dummy
menu still targets only Dummy. Neither menu rebuilds the room or edits vendor assets.

W/S move toward the top/bottom of the main game view; A/D toward its left/right,
without turning the body. The directions are projected onto the floor, independently
of body facing and handheld view. Mouse body turning, hand aim, relative camera
angles, attack pacing, jump and crouch rules are unchanged. F1 reflects this change.
Held items follow CharCrafter wrists; a placed camera still has no remote feed.

The previous assignment deliberately excluded Player. Its main remaining limitation
also applies here: the imported whole body/clothing meshes do not support hiding
individual limbs. Gameplay severing and detached physical proxies work, but the
live skin remains intact. Separate prepared body/clothing limb meshes are still
needed for visible amputation; no Blender edits, bone-zeroing or slicing were added.
Locomotion/attack animation and finger IK remain outside this prototype; crouch
uses the existing vertical body compression, not a new skeletal pose.

`RRM.Editor.CombatPrototypePlayCheck.RunCharCrafterAndExit` now includes Player:
screen WASD under different body/view orientations, actual mouse hand aim and body
turning, moving crouch/jump, wrist-mounted camera, Q/E placement/pickup and reset.
Older body-relative control checks and red-blockout pixel assertions are historical,
not verification of this screen-relative CharCrafter version.

Graphics-enabled Play Mode passed on 2026-09-16 in an isolated copy of the saved
CombatPrototype (Unity 6000.6.0f1). `Logs/charcrafter-player-play.log` contains
`RRM CHAR PLAYER CHECK PASSED` and `RRM CHARCRAFTER PLAY CHECK PASSED`.
The run used queued native keyboard/mouse input and the shared enemy attack command,
not injected DamageEvents. It checked eight WASD cases (two main-view orientations),
rear camera aim with mouse body turning, crouch speed/collider/wrist height, moving
jump and landing, Q/E with the same device ID, and scene reload. The Dummy visual,
real fist/axe hits, left/right arm and leg sever states/proxies, item drops, light,
bleeding trails, visible/occluded camera records and reset checks passed again.
Additional actual enemy axe strikes checked both Player arms and one leg: same
camera dropped intact, no remote feed, one bleed source/proxy, unavailable hand,
0.1 leg speed multiplier and denied Space action, followed by full reset.

Actual renders are in `Verification/charcrafter-player.png`,
`charcrafter-player-feed.png`, `charcrafter-player-crouch.png`,
`charcrafter-player-jump.png`, `charcrafter-player-cut-*.png`, plus the refreshed
Dummy/light/recording images. Main-camera captures omit the IMGUI overlay; they
are not proof of F1 layout. No fresh full door/obstacle/platform navigation suite
or head-specific strike test was run. The saved scene, Dummy prefab, vendor files
and package configuration were unchanged. Existing Editor SearchDatabase and
shadow-atlas diagnostics remain separate from the passing gameplay checks.

Files changed in this follow-up: `Prefabs/Player.prefab`,
`Scripts/PlayerController.cs`, `Scripts/CharCrafterVisual.cs`,
`Scripts/CameraRecorder.cs` (F1 text only), `Editor/CharCrafterIntegration.cs`,
`Editor/CombatPrototypeCharCrafterCheck.cs`, and this README.

## CharCrafter Dummy experiment (2026-09-15)

In that first experiment only Dummy used the imported CharCrafter v1.3 visual. Player and the saved
two-room CombatPrototype geometry remain unchanged. The existing Dummy root,
Rigidbody/capsule, controller/AI, MeleeAttack, Damageable, blood and camera events
remain the gameplay model. No character-controller or combat code from the asset
is installed on the actor.

`RRM > CharCrafter > Integrate One Dummy` uses the package's ready-made
`Prefabs/Male.prefab` (plain Shirt and Pants_1) and its real
`CharacterCustomization.SaveCleanedCharacter()` operation. The native saver writes
to Assets/Prefabs/Characters; the tool moves that new output into
`Assets/RRM/Characters/CharCrafterDummy.prefab`. Shirt/Pants use the existing
DarkBrown/Black palette materials. The package defaults to URP Lit; its converter
is not needed and no vendor assets are edited. Body height is 1.8 m, normalized
from baked vertices instead of the importer's enlarged culling bounds.

The imported rig is Generic, with no Avatar. `CharCrafterDummyAvatar.asset` is a
valid Humanoid Avatar created by Unity AvatarBuilder using the inspected original
bones. Runtime references are resolved with Animator.GetBoneTransform. There is
one original skeleton; the Animator is disabled, its controller cleared and root
motion off. This is a static posed visual following the existing moving actor,
not an animated combat/locomotion integration.

| RRM zone | Humanoid mapping | Original bones |
| --- | --- | --- |
| Head | Head | Armature/Hip/Spine/Chest/Neck/Head |
| Torso | Hips to Neck | Armature/Hip through Spine/Chest to Neck |
| LeftArm | LeftUpperArm to LeftHand | Arm_Upper.L / Arm_Lower.L / Hand.L |
| RightArm | RightUpperArm to RightHand | Arm_Upper.R / Arm_Lower.R / Hand.R |
| LeftLeg | LeftUpperLeg to LeftFoot | Leg_Upper.L / Leg_Lower.L / Foot.L |
| RightLeg | RightUpperLeg to RightFoot | Leg_Upper.R / Leg_Lower.R / Foot.R |

The six existing Hurtbox objects are attached to those bones and resized around
the corresponding segments. Their old renderers and the facing-marker cube are
hidden. Hand-held items remain owned/parented by RRM and follow the actual wrists;
camera rotation remains body-relative and independent. The axe uses an approximate
two-wrist rest pose instead of moving the bone hurtboxes as if they were cube arms.
Attack motion still comes from the existing gameplay pivot, with no new IK.

**Dismemberment is PROXY ONLY for this visual.** BaseBody (1597 vertices), Shirt
(696) and Pants_1 (483) are three whole SkinnedMeshRenderer meshes, each with one
submesh, not detachable limb pieces. The customizer's BodyPartsToggle controls
ears, not arms/legs. Real axe contact still marks a limb Severed, disables its
hurtbox, drops the same held item, restricts hands/movement and starts bleeding.
A visible physical blockout `Severed Proxy <part>` falls and remains until R.
The corresponding part of the live skin DOES NOT disappear. Bones are retained
without zero scaling or procedural cuts; a stump is not drawn over the intact skin.
For real visual amputation, prepare separate arm/leg body AND clothing pieces with
closed attachment surfaces outside Unity, retaining this same skeleton.

Use `RRM.Editor.CombatPrototypePlayCheck.RunCharCrafterAndExit` in a dedicated
Editor session to run the focused Play Mode check through native hand inputs and
the shared melee command. It reloads CombatPrototype, creates temporary witnesses
and a second test axe, and exits its Editor. Never run it in an unsaved working
Editor. The integration menu edits only Dummy prefab; it never calls the old room
builder. Repeating the menu validates the existing integration without duplicates.
Reopening the saved scene inherits the saved Dummy prefab. Controls/F1 are unchanged.

Graphics-enabled Play Mode passed on 2026-09-15 in an isolated copy of the saved
project, using Unity 6000.6.0f1. `Logs/charcrafter-play.log` ends with
`RRM CHARCRAFTER PLAY CHECK PASSED`; no C# compile errors or gameplay exceptions.
The existing core combat/light/recording checks also passed. The focused scenario
verified actual skin height/floor contact, six bone zones, rendered world/feed
pixels, native URP materials, real fist damage/blood, LeftArm/RightArm/one Leg
cuts, physical proxies, camera and two-handed-item drops, missing-hand rejection,
leg movement restrictions, distance-based bleeding trails on the real floor,
visible versus wall-occluded camera witnesses, retained ID/contact/light snapshot
after pickup, restart cleanup and the original enemy approaching/attacking Player.
An isolated existing portable lamp changed 26701 masked body-view pixels and
logical light from 72.78 to 0. Other sources are disabled only for that test and
restored afterwards. Temporary witnesses/axe/positions are discarded by R.

Evidence: `Verification/charcrafter-inspection.txt`, `charcrafter-world.png`,
`charcrafter-feed.png`, `charcrafter-light-near.png`, `charcrafter-light-far.png`,
`charcrafter-proxy-*.png`, `charcrafter-bleeding-trail.png`, `charcrafter-duel.png`.
These are actual Unity renders, not a manual test in the user's already-open
Editor. The full doorway/obstacle/platform traversal suite was not rerun; scene
and Player prefab bytes stayed identical, as did the entire third-party asset.
Unity still logs its pre-existing SearchDatabase startup exception and URP
shadow-atlas warnings. Actual skin amputation, animated locomotion/attacks,
retargeted animation and precise finger/two-hand grips are not implemented.
No additional UPM dependencies are required; the temporary Pipeline installation
was removed from the working project after verification.

Open `Scenes/CombatPrototype.unity` and enter Play Mode. This is the single working
prototype: the two-room layout, cover, platform, handheld camera, evidence and
logical light sources all live here. Both `RRM > Open Combat Prototype` and
`RRM > Open Camera Test Range` open this same scene.

`Scenes/CameraTestRange.unity` is the preserved layout snapshot from the earlier
sprint, not the current playable prototype. Do not use it as the starting point
for subsequent features. `RRM > Restore Two Room Prototype` restores that layout
into CombatPrototype only if its room divider is missing, retaining current actors
and evidence. It does not rebuild either scene once the divider exists.

Use `RRM.Editor.CombatPrototypePlayCheck.RunItemBashAndExit` for the lamp checkpoints
and CameraBash: real hand input, safe aim/short clicks, one-use items, camera witnesses,
broken-device reporting and restart. Witness cameras exist only during the check;
the saved scene still has one camera. No builder is called.
`RunContextualHandsAndExit` first checks configurable click/hold timing, both hands,
focus/item-change cancellation and shared attack cadence using native mouse/keyboard
input, then runs the lamp and CameraBash regressions. It never rebuilds the scene.
The earlier sprint-2 item bashes remain enabled; this input sprint does not remove them.
On 2026-09-13 this complete run passed with graphics (exit 0), without C# compile
errors or gameplay exceptions: `Logs/contextual-hands-final.log`. It checks a changed
0.4 s threshold, empty-hand holds with no punches, left/right punches while the
opposite camera aims, body-based hits with a rear-facing camera, Q/E cancellation,
focus loss/return, alternating clicks and a busy press released after recovery.
Focus callbacks are invoked by the check; actual desktop Alt-Tab was not exercised.
Lamp and CameraBash regressions also pass with release-based fists. Scene, prefabs
and ProjectSettings were unchanged. That pre-grapple run added no new weapon,
grab, block, kick or enemy behavior, and desktop builds were not rebuilt.
`RunCameraBashAndExit` runs only the camera portion. On 2026-09-13 the full
`RunItemBashAndExit` passed with graphics in `Logs/item-bash-final.log` (exit 0,
no C# compile or runtime errors). That log predates unified fist click/hold input.
Attacks used native mouse input through
PlayerController/MeleeAttack, not injected DamageEvents. The checks cover safe
quick-drag/hold aiming, left camera miss, right camera hit, camera transfer without
credit/history changes, light-before-break ordering, visible/blind placed witnesses,
zero broken-device result, no later broken-device damage/blood records, fist fallback,
F1 and R restoring exactly one camera and three lamps. Temporary witnesses and
fixture positions are discarded on reload; no scene or prefab was rebuilt.
Frames: `Verification/camera-bash-aim-feed.png`, `camera-bash-swing.png`,
and `lamps-b-swing.png`. Item damage and report weights are prototype tuning;
there are no debris, repairs, saved recordings, new weapons or global score account.
Use `RRM.Editor.CombatPrototypePlayCheck.RunLampsAndExit` for both lamp checkpoints,
or `RunLampsAAndExit` for only the portable-light
checkpoint: real input, doorway traversal, either-hand lamp pickup, surface placement,
fists, event-point light snapshots, private placed recording and restart.
The older `RunCombinedAndExit` hand scenarios still assume the removed baton and
automatic transfer; they are historical checks, not verification of current equipment.

The previous `RRM.Editor.CombatPrototypePlayCheck.RunCombinedAndExit` covered
end-to-end check: both rooms and cover, doorway traversal, placed reporting,
real melee evidence, continuous LightLevel and scene restart in ONE scene.
Screenshots include `Verification/combined-prototype.png` and `combined-prototype-ui.png`.

## Two-handed axe and gestures

CombatPrototype now contains exactly one `Two Handed Axe` root: two cube visuals
using existing Weapon/Metal materials. It relocates itself on each scene start,
without spawning copies. Four Inspector `spawnPoints` cover both rooms; each uses
a flat supported footprint, away from the divider, cover and doorway.
`spawnSeed = 0` chooses a fresh random seed; any nonzero seed reproduces the choice.
`ChooseSpawn(seed)` uses the same selection/support checks for repeatable tests.

Q or E picks it up only with both living blockout arms present and both hands
empty, outside grapple/block/attack recovery. Other items are never unequipped
automatically. Both PlayerHand entries reference the very same HandItem and axe.
Q or E puts that instance down and clears both hands. Its existing arm primitives
point to two grip locations on one handle; no rig, IK, duplicate fists or inventory.
Placement uses nearby floor/support, or retains the visible world pose if none is
reachable. Ordinary placement/death keeps the existing supported placement;
limb loss drops the same item with simple Rigidbody physics.

While this axe occupies both hands, its single gesture context replaces independent
hand click/hold actions. Press, accumulate screen-space mouse delta, release:
- RMB + left: `RightArm` intent.
- LMB + right: `LeftArm` intent.
- Both + down: `Leg` intent.
- Plain click, small motion or any other direction: `Normal`.

Arm names are the target's anatomical sides, not screen sides. Inspector defaults:
`gesturePixels` 55, `directionTolerance` 30 degrees, `chordWindow` 0.12 s.
The second button may arrive within that window. The first participating release
resolves once; BOTH buttons must be released before another gesture. Late second
presses do not convert the initial gesture into a chord. Focus/item/state changes
cancel unfinished gestures. Busy presses never queue a post-recovery attack.

All intentions use the same `MeleeAttack.TryHandAttack` contact pipeline,
45 base damage (`hitDamage`), 1.05 m tip reach (`reach`) and shared windup/strike/
recovery. Misses recover too; kicks cannot bypass the same gate. Unlike the
one-use camera/lamp bash, the axe does not break on its first hit. The existing
local combat debug display shows `AXE / Normal|RightArm|LeftArm|Leg` and the phase.
Directed gestures can now sever a contacted limb (see below); Normal cannot.
The low Leg sweep and mirrored LeftArm sweep still need real blade contact.
There is no homing swing or extra AI decision to seek/use the axe.

`RRM.Editor.CombatPrototypePlayCheck.RunAxeAndExit` checks the saved scene, then
runs the duel, feet, wall/block, grapple, hands and item-bash regressions.
Use the CLI form below with that entry point and `Logs/axe-check.log`.
`AddAxe` / menu `RRM > Add Two Handed Axe` is idempotent and adds only the axe to a
scene lacking it. It never calls the old builder or overwrites existing room geometry.

Before limb severing, graphics-enabled Unity Play Mode passed in `Logs/axe-play-3.log` and the expanded
`Logs/axe-final.log` (both exit 0). Final checks use queued native mouse/keyboard
input for actual pickup, movement, gesture release and contact: occupied/missing
arm rejection, Q/E symmetry, shared instance, all four accessible spawn points,
repeatable seeds, three intents, ordinary/noisy/diagonal clicks, staggered and late
chords, second-release suppression, shared recovery, busy/focus/item cancellation,
supported placement, crouch height, identical real contact damage and placed-camera
recording. Controller interruption releases both hands; R restores one axe, one
camera and three lamps. All previous duel/feet/block/grapple/hand/item-bash checks
also passed. No C# compile errors or gameplay exceptions; existing Unity/URP and
startup service warnings remain. Focus loss was tested via the actual callback,
not a manual desktop Alt-Tab. No new desktop build or long manual playtest.

Frames: `Verification/axe-held.png`, `axe-grip-close.png` and
`axe-contact-Normal.png` / `axe-contact-RightArm.png` / `axe-contact-LeftArm.png` /
`axe-contact-Leg.png`. The scene diff against the sprint baseline is additions
only: one root, two primitives and configuration. Existing rooms, cover, light,
actors and prefabs were not regenerated.

## Limb severing (sprint 2)

Damageable owns Attached/Severed state for LeftArm, RightArm, LeftLeg and RightLeg.
There are no limb HP. Only a held two-handed axe in its actual strike phase with
a matching gesture may sever an attached limb of a living character. Its swept
contact must reach that limb without a world obstacle. Matching contacted limbs
take priority over ordinary contacts; the nearest contact to the blade tip wins,
with anatomical BodyPart order breaking ties rather than PhysX enumeration order.
Leg selects the nearest contacted surviving leg. A missing requested arm NEVER
redirects the cut to the other arm. A reachable ordinary body contact still takes
ordinary damage; no character contact is a miss, with normal recovery.

One axe swing applies ordinary weapon damage once and may sever at most one limb.
The same DamageEvent carries optional `SeveredPart`; removal does not send extra
damage. A fatal contact may sever because the victim was alive at contact, but
subsequent corpse contacts cannot. Normal axe swings, fists, kicks and item bashes
never sever. Bleeding consequences are described in sprint 3 below.

Only the existing primitive mesh/material is copied into a separate visible
Rigidbody/BoxCollider. It falls onto world geometry and remains until R. It has
no actor/hand/damage scripts and ignores character colliders. The original limb
renderer/Hurtbox is disabled, its transform is retained, and a small closed red
cap marks the attachment. Player/Dummy prefabs now have leg Hurtboxes (0.7 damage
multiplier, like arms); their main capsules and gravity are unchanged.

An absent arm is `HandState.Unavailable`, not Empty. It cannot attack, pick up or
operate items; the other arm can. Losing an arm releases a grapple/block and
interrupts the current attack without bypassing recovery. The held camera, lamp
or axe falls as the SAME instance. An axe clears both hand references and cannot
be picked up with only one arm. Pickup disables the simple drop physics before
reattaching an item. Cameras retain ID and actual history, stop handheld feed,
and continue placed recording. An intact dropped lamp keeps both kinds of light.

One or two missing legs apply exactly one 0.1 base movement multiplier through
the existing player/enemy motor, without modifying moveSpeed. Existing crouch
and attack factors still apply. The injured capsule uses zero friction so the
floor does not cancel this slow motor; the existing motor still handles braking.
Jump and kick are rejected; airborne injury does
not teleport, disable gravity or prevent normal landing. The main standing
capsule/torso pose remains a blockout approximation: no crawl animation or altered
body support shape. ResetHealth changes health only; scene restart restores limbs.

Recorders observe the original damage event and capture light/visibility before
forced item drops. Each device counts only witnessed sever events in its own
existing report (`Severings recorded`), without an extra hit/event or score system.

`RRM.Editor.CombatPrototypePlayCheck.RunSeveringAndExit` runs actual native hand
input/shared enemy melee commands, then axe/duel/feet/block/grapple/hands/item-bash
regressions. It does not regenerate the scene. Tests temporarily raise health,
stage combat positions, add witnesses and a second held axe, and discard these
fixtures on R. `RRM > Add Limb Hurtboxes` only updates the two actor prefabs using
Unity prefab APIs; the ordinary builder also configures legs for newly made actors.
The aerial fixture faces the player away from the enemy (Space must choose jump,
not kick) and shortens that enemy's windup to 0.05 s to arrange contact in flight.
The autonomous enemy does not yet choose chopping gestures; its sever tests call
the same public hand-attack command with a directed intent, never ApplyDamage.

Graphics-enabled Play Mode verification on 2026-09-14:
- `Logs/sever-play-4.log` passed all severing and axe scenarios. Both actors lost
  each arm and both legs through real contacts, with one damage/event per swing;
  ordinary attacks, a solid wall, missing arms and corpses never gained invalid
  cuts. Item identity/history, either-hand loss, two-hand drop, grapple release,
  slow actual movement, no jump/kick, airborne loss/landing and R cleanup passed.
  This run then stopped at an old duel test aiming at the chest while the kick
  now contacted a leg below that camera view. Only that test's aim was corrected
  to the actual eligible kick zone; camera visibility was not broadened.
- `Logs/sever-regressions.log` then passed the complete duel, feet, wall/block,
  grapple, contextual-hands, lamp and CameraBash regressions (exit 0). No gameplay
  code changed between these two runs. No C# compile errors or gameplay exceptions
  remain in the final regression run; startup package-service timeouts and existing
  URP shadow-atlas warnings are separate environment/rendering limitations.
- Frames: `Verification/sever-dummy.png`, `sever-player.png`, `sever-airborne.png`.
  These are rendered game-camera images, not a manual desktop playtest. Native
  queued inputs/shared actor commands perform the attacks; scene placement,
  health and the aerial timing are explicitly controlled test fixtures.

CombatPrototype scene content is unchanged from this sprint's baseline. Only
Player/Dummy prefabs gained the two existing-leg Hurtboxes; native Unity saving
also serialized previously implicit Player component defaults. Scene reload
verified all six zones on each actor. No desktop builds were produced this sprint.

## Bleeding and trails (sprint 3)

Every actually severed limb contributes `maxHealth * 0.05 / 60` HP per game second.
Damageable counts its existing severed-limb bits, so a missing limb cannot add a
duplicate source. Update passes scaled `Time.deltaTime` to `TickBleeding(seconds)`;
the existing health value now retains double precision for small fractional losses.
At maxHealth 100, one source removes 5 HP/minute, two remove 10. Bleeding stops at
zero HP and uses the same death path as an attack. R reloads healthy original actors;
ResetHealth is still only the existing health-reset helper, not wound treatment.

`DamageKind.Bleeding` separates these ticks from weapon contacts. They cannot sever,
recoil, play impact audio, burst particles, spend an attack or reset grapple escape
timing. A fatal tick still releases grapples, stops actions, drops held items and
uses the ordinary dead-body feedback. Its point is the current body capsule center,
not the old limb contact; no killer attribution or wound-by-wound simulation is added.

BloodEvidence reuses its original quad/material and Created event for trails. It
accumulates travelled surface distance while bleeding; default `trailSpacing` is
0.55 m, `trailSize` is 0.24 x 0.18 m. A short probe at the actual capsule bottom
requires nearby real support, including the existing platform/ramp. Airborne gaps
and teleports reset the trail distance; small physics jitter is ignored. It emits
at most one mark per rendered frame, without interpolating across fast movement.
No stationary pools or fluid simulation. `maxMarks` defaults to 128 PER ACTOR,
shared by impact marks and trails; the oldest objects are deleted at the limit.
Other marks remain until scene reload. Camera blood observations retain snapshots
even when an old mark is retired; their diagnostic history has no separate size cap.
The saved CombatPrototype now has BloodEvidence on BOTH actors (previously Dummy
only). `RRM > Add Actor Blood Evidence` attaches this existing component without
rebuilding geometry; the ordinary actor builder also includes it. Prefabs were
not rewritten in this sprint.

A cut remains ONE original DamageEvent, containing target name/reference, anatomical
limb, real contact point and game timestamp. A witnessing device stores that event
with visibility, distance and LightLevel/clarity at contact, before limb removal.
Later light changes, camera movement and ownership do not rewrite that snapshot.
The report lists the count and names of witnessed limbs. Ordinary bleed ticks are
not recorded. A visible bleed death adds one event and Death recorded YES, but no
weapon hit, severing or report weight. A camera behind geometry adds no event.
Held/placed feed privacy and camera identity/history remain unchanged.

`RRM.Editor.CombatPrototypePlayCheck.RunBleedingAndExit` exercises this in the saved
CombatPrototype, then runs severing and all previous combat regressions. Numerical
checks feed 60 seconds into the same bleeding update at several frequencies AFTER
actual axe contact; one scene check also waits a full simulated game minute through
normal Update. Test fixtures set health/positions, briefly change lights and create
temporary witness cameras; no DamageEvent is injected to substitute for an attack.
Trail rendering checks hide/restore only test marks and wait for a new live frame.
Bleeding death is accelerated through TickBleeding itself after a real wound;
the check does not wait twenty real-time minutes. The nonfatal minute check does
run 60 game seconds through ordinary Update. A native scene-save/bootstrap entry
is `RRM.Editor.CombatPrototypePlayCheck.PrepareBleedingAndRun`; subsequent checks
can use RunBleedingAndExit without any scene changes.

Graphics-enabled Play Mode on 2026-09-14: `Logs/bleed-play-2.log`, exit 0.
- Real axe contacts produce one/two sources. At maxHP 100, isolated bleeding is
  5/10 HP per 60 seconds at 20, 60, 144 and 1000 updates/s (tolerance 0.00001 HP).
  Ordinary Update also ran 60.000 game seconds and finished at exactly 95 HP.
- A placed camera records each real cut once; the witness behind the saved west
  wall records nothing. Later darkness, limb removal, movement and new ownership
  leave the captured contact/time/light/clarity unchanged.
- Actual Dummy floor movement and queued player movement on the existing raised
  platform leave supported trails. Both normal and actual handheld rendering
  change when marks are hidden/restored. Airborne movement and idle time add no
  trails; a reduced test cap deletes old mark objects. Frames are in
  `Verification/bleed-floor-*.png` and `Verification/bleed-platform-*.png`.
- Bleeding death releases a real grapple, cancels actions and drops the same
  camera without remote feed or lost history. A witness records one death with
  zero weapon hits, severings and added weight. Dead actors stop ticking.
- R restores all limbs, hands, base movement, one axe, one camera and three lamps;
  it removes detached parts/trails and resets bleeding. All existing severing,
  axe, duel, feet, wall/block, grapple, hand-input and item-bash regressions pass.
- No C# compile errors or gameplay exceptions in the final run. Existing URP
  shadow-atlas and editor startup/shutdown service warnings remain. The first
  run (`bleed-play-1.log`) stopped at a missing Player BloodEvidence component;
  this was fixed in the saved scene before the passing complete run.
The final editor-only save-confirmation guard was compiled and its idempotent
scene setup rerun in `Logs/bleed-final-compile.log` (exit 0). Runtime scripts did
not change after the complete Play Mode run.

This was automated native Play Mode, not a joint manual playtest. No Windows/Linux
build was made. Simplifications remain: flat quads, distance sampling with one
mark/frame maximum, per-actor cap, uncapped diagnostic blood-observation history,
no healing, fluid physics, killer attribution or crawling animation.

Files changed in sprint 3, relative to Assets/RRM:
- Scripts: Damageable.cs, Damageable.Limbs.cs, DamageEvent.cs, BloodEvidence.cs,
  HitFeedback.cs, PlayerController.Grapple.cs, CameraDevice.cs, CameraRecorder.cs.
- Editor: CombatPrototypeBuilder.cs, CombatPrototypeTests.cs,
  CombatPrototypeLampCheck.cs, CombatPrototypeSeverCheck.cs; new
  CombatPrototypeBleedCheck.cs and its .meta.
- Scenes/CombatPrototype.unity and README.md. Scene changes only attach existing
  BloodEvidence to Player and serialize trail defaults; geometry and both actor
  prefabs are identical to this sprint's starting files.

## Shared-rule duel (sprint 6)

The existing autonomous PlayerController now chooses shared `TryHandAttack`,
`TryBeginGrapple`, `TryBlock` and `BeginEscapeAttempt` / `CompleteEscapeAttempt`
commands. No bot-only damage is applied. Its decision code is a partial of the
same controller, not another AI component. Fists, kicks and item bashes still use
one MeleeAttack windup/strike/recovery; misses also spend that recovery.

Dummy keeps the slow 0.7 / 0.18 / 1.15 s phases. Its saved `fistDamage` is 36
(player 18), before Hurtbox multipliers. Generic `damage` 60 remains only for the
older generic attack command; autonomous attacks now use the hand's actual item
or fist damage/reach. It preserves a held camera when the other hand can attack.
Movement stops and facing stays committed during the swing and recovery.

With two empty hands it sometimes grabs, then punches with the free hand only
after the victim's full escape opportunity. It never searches for a wall slam.
Inspector defaults on PlayerController: `grappleChance` 0.2, `blockChance` 0.35,
`blockReactionDelay` 0.25 s, `escapeChance` 0.55, `escapeReactionDelay` 0.12 s,
`decisionInterval` 0.25 s, `decisionSeed` 173. A private seeded random stream keeps
decisions independent of random particles/light effects. Block observes an
already-started opposing windup; escape gets at most one delayed attempt per
shared cycle. No future local button input is inspected. Defaults are fallible;
checks can set probabilities to 0/1 to reproduce either outcome.

Death or disabling the holder's controller cancels attacks, releases the pair and
detaches held items. Whole-scene teardown does not try to reparent dying objects.
Camera ID and recording remain on that same device; it records privately until
picked up. A lamp remains visible and lit on nearby support, or at its last world
pose when no supported position is reachable (no falling-item physics). Ordinary
Q/E pickup works for a surviving player after the enemy dies. The existing report
is explicitly a PREVIEW: ownership is read live, never frozen at the fatal event.
Fatal damage observers run before normal death cleanup, preserving contact-time
lighting/recording. Broken items retain their existing destruction rules.

`RRM.Editor.CombatPrototypePlayCheck.RunDuelAndExit` runs the duel in the saved
scene, then feet, wall/block, grapple, hands, lamp and CameraBash regressions.
`ConfigureDuelAndRun` additionally saves only Dummy's fistDamage through the Unity
prefab API; neither entry rebuilds room geometry. Tests stage actor positions and
temporary blockers, but actual hits come from AI commands or queued native input.

Complete graphics-enabled Play Mode runs passed on 2026-09-13 in
`Logs/duel-play-4.log` and `Logs/duel-final-2.log` (both exit 0): closed/open passage, existing low-cover traversal,
committed AI windup, real retreat/counterstrike, timely/late block, probabilistic
escape, AI capture/punch, fresh player input during the visible BREAK FREE window,
both deaths, dropped lamp/camera, same-device history/owner across pickup, holder
controller disable and scene restart. The final repeat also checks walking away,
returning and picking up the camera while the death report remains open. Both runs passed all feet, wall/block,
grapple timing, contextual hands, lamp/light and CameraBash regressions. No C#
compile errors or gameplay exceptions; pre-existing obsolete-API/URP warnings
remain. Screenshot evidence: `Verification/duel-counter.png`,
`duel-escape-window.png`, `duel-recovered-camera-feed.png`. Scene, Player and lamp
prefab hashes remained unchanged. No Windows/Wine run or desktop rebuild.

Close this project's Editor before running the check from a terminal; do not add
`-quit`, since the asynchronous runner exits the Editor itself:

```bash
XDG_CONFIG_HOME="$HOME/.var/app/com.unity.UnityHub/config" \
XDG_DATA_HOME="$HOME/.var/app/com.unity.UnityHub/data" \
"$HOME/Unity/Hub/Editor/6000.6.0f1/Editor/Unity" \
  -batchmode -force-glcore -projectPath "$HOME/RRM/RRM" \
  -executeMethod RRM.Editor.CombatPrototypePlayCheck.RunDuelAndExit \
  -logFile "$HOME/RRM/RRM/Logs/duel-check.log" --burst-disable-compilation
```

Limits: local tangent steering around small cover, no pathfinding through unseen
rooms. The current saved scene has an open passage, not a closing door; the check
uses a temporary blocker there. That sprint had only fists/camera/lamps; the newer
axe occupies both hands, so camera-plus-weapon is still not a supported combination.
No AI kicks, automatic wall slam, new items or desktop build are part of this sprint.

## Contextual Space and feet

Short Space (release before 0.2 s): kick a living reachable Hurtbox in front of the
BODY; without such a target, jump. Selection happens on release, not press.
Hold Space: crouch with no release kick/jump. Left Ctrl also crouches; either held
crouch key keeps the stance lowered. Releasing both restores height only with
clear headroom. The same existing capsule, ground probe, gravity, movement and
camera-height handling remain in use.

Both actions require ground support and an uninterrupted, focused short gesture.
Busy attack/block recovery rejects the gesture, without a fallback jump or queued
attack. A kick works with either/both hands occupied, without swinging or breaking
their items. No kick in grapple, aerial kick, finishing move or new enemy decision.

MeleeAttack has Inspector parameters: `kickDamage` 24, `kickReach` 0.9 m from the
foot to a Hurtbox, `kickHalfAngle` 35 degrees, `kickHeightTolerance` 0.45 m above or
below the foot, and `kickRadius` 0.14 m. Grounded actor at a platform edge can kick
a reachable lower target; the platform itself never counts as a kick target.
Wall/low-obstacle checks sweep from the foot, not the chest or handheld camera.
Target selection ignores cooldown; rejecting an attack cannot reinterpret it as jump.
The public `FindKickTarget` / `TryKick` path uses existing melee phases, physical
contacts, shared recovery, damage, blocking, blood and camera observation. A target
can evade during windup: the foot follows a committed body-relative path, never
homes onto the target. The existing primitive right leg supplies the visible pose;
no new assets, rig, scene components or hand-item system are added.

`RRM.Editor.CombatPrototypePlayCheck.RunFeetAndExit` checks real Space/mouse input,
movement, low-cover landing, camera height/privacy and common attack commands,
then wall/block, grapple, hands, lamps and CameraBash regressions. No builder is used.
Verified 2026-09-13 in graphics-enabled Unity Play Mode: `Logs/feet-final.log`,
exit 0, no C# compile errors or gameplay exceptions. Queued keyboard/mouse actions
checked occupied-hand kicks with a rear-facing camera, actual single-hit contact,
evaded kicks, shared hand/foot recovery and no fallback/queued jump. Targets behind
the body, too high, behind a wall or behind low cover did not steal jump input.
Standing/moving/raised jumps, no double jump, landing on saved 45 cm cover, a real
kick to an accessible target below its edge, crouch speed and actual capsule height,
both orders of Ctrl/Space release, ceiling clearance, and a short crouched jump
with Ctrl still held passed. Handheld lens movement/live pixels and placed-camera
position/privacy were checked; grapple forbids both Space and the kick command.
Restart and subsequent wall/block, grapple/escape, hands, lamp and CameraBash checks
all passed. Screenshots: `Verification/kick-contact.png`, `feet-cover-landing.png`,
`feet-jump-feed.png`. Scene/prefab hashes and ProjectSettings remained unchanged.
Leg movement is a primitive pose, not an authored animation. No aerial/grapple
kicks, new enemy decisions, desktop build or manual long playtest were added.

## Grapple prototype

Put down both items with Q/E. With BOTH hands empty, hold just LMB or just RMB for the
configured hand threshold near a living character in front. The pressed hand
grips; the other remains free. Releasing the gripping button releases the target.
An unsuccessful hold does not keep searching or queue a grab: release and retry.
There is no automatic unequipping, target teleport or camera requirement.

Both actors remain at the actual contact positions, with horizontal position and
rotation constrained. Gravity/colliders remain active; there is no dragging or
joint simulation. Movement, jumping, crouch changes and item interaction are
blocked while paired. Death, disable, focus loss, separated/obstructed actors,
or an external hit on the captor release both. Original living-body constraints
are restored; death feedback keeps its existing fall behavior. This first
standing-height prototype rejects crouching and appreciable vertical motion.

A short click of the FREE hand uses the existing fist damage, physical contact
sweep, windup/strike/recovery and one-hit-per-target guard, restricted to the held
target. The gripping hand cannot punch. A fresh free-hand hold attempts one
wall slam (see below). Releasing during a grapple strike ends its contacts
but retains recovery, rather than resetting the attack rate. Holding alone never
damages. CameraRecorder observes the same real DamageEvent independently.

Inspector parameters live on the existing PlayerController for either actor:
- `grappleRange`: 0.9 m; `grappleHalfAngle`: 40 degrees. Existing obstruction mask
  checks both waist and chest between actors. No wall/cover pull-through.
- `escapeDelay`: 0.65 s before the first opening.
- `escapeWindow`: 0.3 s to begin AND release a short LMB/RMB input.
- `escapeRepeatDelay`: 0.45 s after the opening, then the delay/opening repeats.
  Default cycle is 1.4 s; windows are 0.65..0.95, 2.05..2.35, etc.
- `escapeProtection`: 1.2 s of protection against re-grab after successful escape,
  NOT protection against damage.

The victim sees a small local GRABBED / BREAK FREE indicator above the existing
HUD. One fresh press per cycle is allowed across both buttons, including an early
failed press. A held button, a long hold or a second button cannot retry that cycle.
No attempt is stored for the future. Missing a window never prevents the next
cycle. A real hit resets the escape cycle; the captor cannot start the next punch
until the victim's entire new opening has elapsed, even if melee recovery is shorter.

The logic is another source-file portion of PlayerController, not a new attached
component or AI system. Shared commands: `TryBeginGrapple`, `ReleaseGrapple`,
`BeginEscapeAttempt`, `CompleteEscapeAttempt`; punches use `MeleeAttack.TryHandAttack`.
Local buttons call these same methods. Sprint 6 also calls them from enemy
decisions; the older grapple check below exercises the commands explicitly.
`RRM.Editor.CombatPrototypePlayCheck.RunGrappleAndExit` checks grapples and escapes,
then runs the contextual-hand and item-bash regressions. It never invokes a builder.
On 2026-09-13 the full graphics-enabled run passed (exit 0):
`Logs/grapple-play-3.log`. Actual hand input checks both gripping hands, free-hand
punches, no hold damage, item rejection, stationary pairs and normal release.
The same command path checks blocked/behind/distant targets, state interruption,
unexpected separation, enemy-as-captor, preheld/early/spam escape rejection,
both escape buttons in repeated/post-hit windows, anti-grab protection with real
enemy damage, fatal attacks on either participant, deletion and clean restart.
No test DamageEvent replaces these attacks. Actor positions/facing and passive
enemy settings are fixtures, not saved scene changes; enemy commands are issued
explicitly, not chosen by AI. Frames: `Verification/grapple-1.png`, `grapple-2.png`,
`grapple-escape-ui.png`. Contextual hands, lamp and CameraBash checks then also pass.
No C# compile errors or gameplay exceptions. Startup was intermittently delayed
before Play Mode; no second Editor was run on the occupied project.
Scene/prefab hashes and ProjectSettings match the pre-sprint files. There are no
grip animations, and no new desktop build/manual long playtest. This historical
verification predates the wall-slam/block checks below.

## Wall slam and timed block

Outside a grapple, hold BOTH mouse buttons with empty hands for a timed block.
The chord wins over a single-hand grab, including staggered presses whose holds
begin together. In an established grapple, a NEW hold of the free hand instead
attempts a wall slam. Cancelled/preheld input never becomes a new action after
release, focus loss or a state transition. Release both buttons before reblocking.

Block parameters on PlayerController: `blockWindow` 0.2 s, `blockRecovery` 0.35 s
after the window, `blockHalfAngle` 70 degrees either side of body forward. The
camera direction is irrelevant. Front contacts during the window cause no damage,
blood or recording event. Rear/late contacts hurt normally. One stopped contact
consumes that target for the entire swing, not merely the current physics step.
Holding never refreshes the window, release never punches, and a rejected attempt
never queues. Attacks/grabs/item changes cannot overlap block recovery; block
cannot interrupt an existing attack/recovery. The small local label shows BLOCK
WINDOW / BLOCKED. The shared `TryBlock` command also works for an enemy; sprint 6
adds a delayed, probabilistic decision without changing that command's rules.

Wall slam uses `MeleeAttack.TryWallSlam`, the same windup/strike/recovery, hand
sweep, escape-window gate, Damageable and observers as a punch. Default base
damage is `wallSlamDamage` 48 (before the existing Hurtbox multiplier). The target
must already be against a near-vertical wall behind them, with at most
`wallContactGap` 0.04 m clearance. This is a contact-only proof, not a shove or
dragging system. Neither actor is snapped or teleported. The same wall must still
be there at actual strike contact. Blood/evidence uses the wall contact point;
placed cameras can witness it as an ordinary melee event. No wall means no slam,
not a boosted free-space punch. Continued hold never repeats; a fresh free-hand
hold is needed. Releasing the gripping hand cancels remaining contacts but keeps
attack recovery.

`RRM.Editor.CombatPrototypePlayCheck.RunWallBlockAndExit` runs queued mouse/keyboard
checks and ordinary enemy attack commands, then grapple, hands and item regressions.
Temporary test geometry and positions are Play Mode fixtures, never saved assets.
Verified 2026-09-13 with graphics enabled in Unity Play Mode: exit 0,
`Logs/wall-block-play-3.log`. Real queued input passed simultaneous/staggered chords,
both gripping hands, no release punches, no hold repeat, focus cancellation,
front/expired/rear/late incoming melee, a long swing outlasting its block window,
enemy defense against a real player fist, and rejection during own recovery.
Wall checks passed stronger single hits, persistent wall blood, placed recording,
no actor displacement, no wall, wall removed during windup, grip release during
windup, and restart. Subsequent grapple/escape, contextual-hand, lamp and CameraBash
regressions passed too. No C# compile errors or gameplay exceptions. Two preceding
runs exposed an incorrectly timed test gesture (after the first enemy contact);
only the fixture timing was corrected, not the gameplay block duration.
Screenshots: `Verification/block-window-ui.png`, `wall-slam-1.png`, `wall-slam-2.png`.
Scene/prefab hashes and ProjectSettings stayed unchanged. No builder, desktop build,
automatic enemy block/slam decisions or manual long playtest was added.

## Persistent evidence and light conditions

CombatPrototype's Dummy has a BloodEvidence component subscribed to its existing
Damageable.Damaged event. A successful hit leaves one permanent quad using Blood.mat:
the nearest wall found by four horizontal probes within 0.85 m takes priority;
otherwise a downward probe finds the floor within 2 m. No surface means no mark.
The mark is offset 1.5 cm above the surface, has no collider, is independent of the
Dummy and survives until scene reload. Existing short blood particles are unchanged.
This is a surface projection, not simulated splatter, a wound or a decal system.

Eight fixed logical LightSource components provide the existing bright pockets,
dim transitions and dark areas in both rooms. Three portable lamps add moving light:
- West: position (-5.1, 1, -3.1), intensity 100, radius 3 m.
- North: position (-5.6, 1, 3.7), intensity 100, radius 3 m.
- East: position (2.8, 1.05, 0.4), intensity 100, radius 3.5 m.
- Doorway: position (-1.9, 1, -1.8), intensity 75, radius 2.6 m.
- West Mid: position (-6.2, 1, 0.5), intensity 48, radius 2.2 m.
- East South: position (3, 1, -4.5), intensity 100, radius 2.4 m.
- East North: position (5.8, 1, 4.2), intensity 90, radius 3.2 m.
- Platform: position (5.5, 1.85, -0.4), intensity 60, radius 2.2 m.

Each contribution is `intensity * (1 - SmoothStep(0, 1, distance / radius))`, using
3D distance. Contributions add and clamp to 0..100; outside all sources the level
is zero. Radius is the light's reach, NOT the camera's detection range. Select a
source in Editor to see its radius gizmo; there are no rectangular zone patches or
source colliders. `RRM > Update Evidence and Light Sources` migrates old zones or
adds missing sources without rebuilding the room or changing existing sources.
`RRM > Retune Light Test Layout` explicitly reapplies the above light positions and
parameters without altering room geometry, actors or their prefabs.
Moving a source, changing intensity/radius, or disabling its component/GameObject
affects the next query immediately. Each PortableLamp has one of these same sources
at its bulb, plus an ordinary realtime Point Light. Source intensity (0..100) drives
point-light intensity (0..2), and source radius drives its range. There is no separate
lamp brightness/range tuning model. The prototype's LightLevel is normalized, not lux;
the physical renderer's falloff is not numerically identical to the diagnostic field.

CameraRecorder keeps its existing unbounded FOV/occlusion check. Each witnessed
damage record snapshots numeric LightLevel at the impact point and a prototype
clarity value of `LightLevel / 100`. Existing labels are derived: Normal >= 65,
Low >= 25, Dark < 25. The field itself is continuous, not three fixed levels.
`CameraRecorder.GetLightLevel(Vector3)` samples any event/object position, e.g.
an object's transform or Hurtbox center. DamageEvent.Point is sampled automatically.
The handheld HUD shows LIGHT HERE at the player's feet, TARGET LIGHT at the visible
target's torso, and predicted recording QUALITY in percent. Hidden/off-camera targets
show N/A; a placed camera still exposes no live diagnostics. Last event light/quality
are historical values from the actual DamageEvent.Point, also shown in the report.
Quality is the existing per-event Clarity: e.g. a witnessed death at LightLevel 15
has 15% quality, versus 90% at LightLevel 90. This multiplies the event's base weight
in the device's report, not a global points counter; hit counts and Death recorded
remain factual event data.
The cone does NOT shrink in darkness. Darkness lowers clarity but
does not turn geometric visibility off or change actual rendering. Logical light
does not yet cast shadows or stop at walls; the existing camera wall/FOV checks
remain separate and unchanged. Point-light shadows do NOT prove logical occlusion.
There is no postprocessing.

In Play Mode, Floor's LightLevelPreview displays the same field as a soft red
gradient: darker logical areas have more red, capped at 18% opacity by default.
The contrast curve is `1 - SmoothStep(0, 1, LightLevel / 70)`: ground at 70+ is
untinted, zero stays at the same 18% maximum, and values between vary smoothly.
The preview curve changes only the tint, never the underlying LightLevel or quality.
The 128 x 96 bilinear texture samples LightSource at floor height every 0.2 seconds,
including moved/disabled sources. Adjust `opacity` or disable that component in
Inspector to reduce/hide the preview. It is a flat, nonphysical overlay beneath
blood marks, visible in both the main view and handheld feed. Actors, walls and
raised surfaces are not tinted. The saved transparent LightLevelPreview.mat is cloned
at runtime. Previously the opaque Blood material was switched to transparent only
at runtime: Editor rendered a gradient, but standalone stripped the transparent
shader variant and produced an opaque pink floor despite correct logical/HUD values.
Persisting the transparent template makes the build include its shader variant.
Blood.mat and combat effects stay intact.
This is a diagnostic color map, not real illumination or a screen-wide filter.

Each CameraDevice also owns its BloodEvents: mark position/reference, visible
YES/NO at creation, surface light level and clarity (zero when not visible).
They are separate from hit counts: one hit plus its mark is still one recorded
hit. An active placed recorder collects the same observations privately, without
restoring live feed or remote HUD. `CameraRecorder.InspectBlood(bloodEvent)` checks
whether that surviving mark is visible NOW, without changing historical evidence
or adding another event. Later discovery is query-only for this prototype; there
is no periodic scanning, automatic discovery log, save file or replay. Pickup keeps
the device's history; R destroys the marks and starts empty histories.

For an automated graphics-enabled Play Mode check use
`RRM.Editor.CombatPrototypePlayCheck.RunEvidenceAndExit` (no `-quit`). It drives
a real mouse melee hit, checks persistent floor evidence and its actual feed
pixels after the burst, places the camera and checks later inspection, validates
the same Dummy at bright/low/dark positions and wall evidence using test-injected DamageEvents,
checks occlusion and clears everything with R. Screenshots: `Verification/evidence-*.png`.
The checks also cover additive/clamped smooth falloff, moving/disabled sources and
unchanged historical event values after illumination changes. Blood marks sample
their own surface height, which can have a different value from the torso impact.
The combined check validates five visibly brighter ground locations across both
rooms, live HUD changes, hidden-target/placed privacy and low-quality fatal recording.

CombatPrototype preserves the previous 16 x 12 m blockout.
Two rooms connect through a 2.4 m
opening in the lower part of the divider (x = 0, z = -2). Player starts on the
left and the hostile Dummy starts on the right.

The left room has 0.45 m low cover and an L-shaped blind corner; the right has
2.2 m high cover, the existing concrete block, and the 0.9 m platform/ramp.
Walk through the opening, compare shots above low cover with blocked views
behind tall cover, or place the camera facing the doorway and leave it recording.
Walls and covers have real colliders and use the existing visibility checks.
Controls below apply here; R restarts the combined CombatPrototype.
Dummy approaches a nearby unobstructed player, but cannot plan a route through
the doorway or around cover. Both rooms, light preview and platform are unchanged.

The low cover in front of the spawn is reachable with the existing W + Space jump
(0.6 m); no jump tuning or climbing assist was added. Its width/depth and footprint
are unchanged. `RRM.Editor.CombatPrototypePlayCheck.RunStarterCoverAndExit` saves
the narrow height adjustment, reopens CombatPrototype and checks jumping onto it,
stable landing, working handheld feed and walking back off with S.

- W/S: toward the top/bottom edge of the main game view. A/D strafe toward
  its left/right edge without turning the body. Diagonals are normalized.
  Movement never uses body facing or the handheld's direction.
- With both mouse buttons released, the body turns toward the cursor on a
  horizontal plane at the player's feet. Either hand action locks body turning;
  WASD remains screen-relative. The HUD/outside the game viewport is ignored.
- Space (short tap, under 0.2 seconds): kick a reachable target in front on release;
  otherwise jump, about 0.6 m, including
  while moving. Both press and release must be grounded; leaving the surface or
  losing focus cancels the pending tap. Attack/block recovery rejects the gesture,
  never converts a rejected kick to jump. WASD keeps working in the air; no double
  jump, aerial kick or queued air action.
- Space (hold 0.2 seconds) or Left Ctrl (hold): crouch to 65% body/collider height
  and move at 50% speed. A long Space hold never jumps or kicks, including on release.
  Release both crouch keys to stand and regain speed; under a low ceiling, stay
  crouched until clear. Ctrl still crouches immediately.
- LMB is the left hand; RMB is the right hand. Except for the two-handed axe
  context described above, release before `handHoldThreshold`
  (default 0.22 seconds on PlayerController) for one click. Reaching the threshold
  starts hold; releasing hold ends it without a click. Mouse motion never bypasses
  this threshold. Hold the camera hand's button, then move the mouse to aim.
  It starts in the LEFT hand. Full yaw
  permits looking backward while walking forward. Release keeps its BODY-relative
  yaw/pitch: turn from north to east, and a rear-facing camera turns from south
  to west. Position still follows the hand. A placed camera stays fixed in the world.
- Short-release an empty hand for a punch. With both hands empty, hold ONE button
  for a grapple on a nearby target in front; hold BOTH for a short frontal block.
  In an existing grapple, fresh free-hand hold attempts one contact-only wall slam.
  These holds never punch on release or auto-repeat.
  MeleeAttack exposes fistDamage (18) and
  fistReach (0.42 m from the hand pivot). The initial baton is removed, including its
  long contact tip. Enemy settings remain unchanged. Holding does not repeat attacks.
- Q: place the LEFT hand's item, or pick a nearby available item if empty. E: RIGHT.
  The nearest accessible camera/lamp/axe is tried first, within its pickup distance,
  not through walls. The axe needs BOTH hands empty and occupies both with one object;
  either Q or E puts it down. Its mouse gesture context replaces separate hand actions.
  An occupied hand never automatically transfers/replaces its item. Q wins simultaneous
  Q/E. Active swings and grapples block item interaction. The camera starts LEFT; RIGHT starts empty.
- Lamps visibly follow their hand and remain lit when placed. Placement probes nearby
  floor/flat cover tops, checks clearance and support; an invalid placement keeps the
  lamp held. Limb-loss drops use simple physics; no throwing, inventory, batteries
  or lamp hold action.
- Short-click the camera/lamp hand (before the hand hold threshold) to swing on release.
  Lamp base damage is 54; camera base damage is 68, versus fist 18. Tune lamp
  `hitDamage` and camera `hitDamage` separately. The existing body-part multipliers
  still apply. First confirmed character hit consumes the item; a miss keeps it.
  Carrying into characters or touching walls never consumes an item.
  Damageable notifies all recorders/evidence observers before the used lamp loses
  its light or the used camera breaks. The hand becomes Empty, but the same recovery
  must finish before a new fist attack. Holding does not attack or repeat; presses
  during recovery do not queue another attack, even if released after recovery.
  Both hands share one cadence: windup 0.32 + strike 0.18 + recovery 0.5 seconds,
  all tunable on MeleeAttack. Contact pause is separate impact feedback; there is
  no additional one-second cooldown. Aiming the opposite camera hand remains possible.
  Attacks follow the body/attacking hand, not the camera's viewing direction.
- Focus loss, item changes and successful placement cancel that hand's unfinished
  click/hold. Release the button and press afresh; old input never acts on a new item.
- Arrow-key movement and the old F interaction are removed. R/F1 remain.
- F1: show/hide the controls guide, both handheld and after placement.
- R: restart the room, health and tape counters.

The F1 guide temporarily replaces the HUD without pausing the room. It contains
only static controls, never remote camera information, and scrolls in small views.

After either actor dies, CAMERA REPORT replaces the normal HUD, including for a placed
camera. It shows that device's ID, current owner (NONE when placed/broken), state, witnessed
hits/events, Death recorded YES/NO, and distance from lens to the last recorded
damage point. No witnessed events means distance N/A, not zero. Distance is
captured when the event happens, not recomputed as the player moves.

Camera hits carry `DamageKind.CameraBash` and a snapshot of the used camera's
`bashWeight` (default 100). Ordinary melee events retain weight 1. Each intact
device's `ReportWeight` sums only its witnessed events: `BaseWeight * Clarity`.
The report also shows witnessed CameraBash and severing counts. This is an
in-memory prototype report weight, not a player balance, economy or leaderboard.
No event or weight is granted by pressing a button, missing, carrying or pickup.
Changing an item's tuning later does not rewrite an existing record.

A broken CameraDevice keeps its ID and old events for diagnostics but its report
weight is always zero. It stops collecting both damage and blood observations,
its housing/cone disappear, and feed is released. It cannot be picked up or used
again. CameraRecorder stays available for F1 and the existing end-of-round report;
`IsRecording` is false. No debris or repair system is added. Other active cameras
that actually saw the CameraBash keep their own event and weight, independently
of the attacker or the fate of the attacking device. Off-camera/occluded hits
create no bonus. Combat and empty-hand attacks still work without a live camera.

Each CameraDevice owns its in-memory list of recorded DamageEvents and distances.
CameraRecorder adds an entry only while both recorder and device are active and
the hit point passes the existing FOV/occlusion check. One witnessed DamageEvent
from a weapon is one hit and one event; a fatal hit is not counted twice. Nonfatal
bleeding ticks add neither; a witnessed bleed death adds one event but no hit.
Death recorded is YES
only if the fatal event itself was witnessed. Knowing that an actor died opens the
end-of-round report but never adds evidence for a camera that missed the death.
Placement, pickup and ownership changes keep that device's list. R clears it by
creating a new device. There are no global/shared recording counters. The report
window can list multiple devices, but CombatPrototype still contains only one.
F1 temporarily replaces the report with the controls guide, without resetting it.

The right side of the room has a 0.9 m Camera Platform with a shallow approach
from the south (bottom of the top-down view). Walk up and back down with WASD;
no jump or interaction button is needed. The platform and ramp are two static
boxes using Wall.mat. The existing Rigidbody motor and gravity handle the slope;
there is no climbing controller, snapping or teleport. The handheld stays in
the player's hand, so its lens and live feed rise with the player. Hold the camera hand's mouse button to
adjust the viewing direction while changing height; releasing keeps it. The floor cone remains
only a flat direction preview, not a projection onto the raised surface.

PlayerController now exposes LeftHand and RightHand. Each has Unavailable/Empty/HoldingItem
state, an IHandItem reference, and separate WasClickedThisFrame / IsHoldActive
actions (LMB for left, RMB for right). All hands use `WasTappedThisFrame`
(`WasClickedThisFrame` is its alias) on a short release. `IsHoldActive` means
the physical button is down (also locks body turning); `IsHoldActionActive` means
the threshold-based hold used for camera aim. Gestures are sampled once per game
frame regardless of controller/recorder update order. Focus loss, disable/death
and item changes cancel pending taps and require release before a new press.
Disabled/dead/unfocused players cannot produce local hand input; the autonomous
Dummy does not read the local mouse. Its shared grapple/melee commands remain available.

CameraDevice and PortableLamp use the existing HandItem adapter (Source and Grip).
CameraRecorder reads the button of the device's actual hand, never a global RMB
binding. Pickup restores the same instance, camera ID/history and hand mount.
MeleeAttack supplies the shared attack cadence and Hurtbox/Damageable path; fists
are actions of Empty hands, not invisible equipped weapons. Camera and lamp bashes
reuse the same windup/strike/recovery, contact sweep and one-hit-per-target gate.
The actual held mesh follows that sweep; a camera miss restores its prior local
aim and hand height. A camera cannot be aimed or placed during its own bash.
There is no second input, camera or damage system.

`RRM > Update Portable Lamps` updates only Player's fist setup, the floor's preview
material and missing named lamp instances. It preserves both rooms, covers, the
platform, actors and fixed lights. Three instances use one PortableLamp.prefab:
(-3.3, 0.02, -3.5), (-6.8, 0.02, 2), (3.2, 0.02, -2.1). Repeat setup preserves
existing instance positions. R reloads the saved scene; no runtime spawner is used.

Jump and crouch use PlayerController's existing Rigidbody motor, not a second
movement component. A short sphere probe under the capsule checks supporting
Default-layer room geometry (`groundMask`); wall normals cannot authorize a jump.
The upward launch changes only vertical velocity, leaving the same horizontal
motor active. `jumpHeight`, `crouchHeightScale` and `crouchSpeedScale` are tunable.
Crouch resizes the capsule about its feet and compresses Body Visual, including
its hurtboxes, with no animation. The actual camera housing/lens and held lamp
move down with their hands without scaling the items or changing camera aim.
Camera pickup tests reach from that lowered mount; standing up does not move a
placed camera. This is a blockout pose, not a skeleton, parkour or stealth system.

CameraDevice is a component on the existing VHS housing, alongside CameraRecorder.
It owns the runtime GUID (`Id`), current `Owner` (PlayerController reference), and
`State`: Held when owned, Placed when free, Broken after a confirmed bash hit.
PlayerController.HeldCamera refers to
the actual device being held in either hand. Q/E puts that hand's instance into
the world, clears owner/holder references and keeps its ID. Q/E near the free device picks up that
same instance, assigns the new owner and restores the hand mount and live feed.
The 1.5 m pickup check uses the hand position and rejects intervening Default-layer
geometry. Pickup preserves the placed world angle and recorded events.
IDs last for the device's runtime lifetime, not across scene reloads or saves.
Broken retains only diagnostic data/reporting, with no reusable world item or repair.

CameraRecorder reads CameraDevice's state; there is no second placement flag.
HELD keeps the camera attached to Player with live video. PLACED keeps the same
housing and lens fixed in the world, but disables the rendering Camera, releases
the RenderTexture and hides both the feed and its diagnostic overlay. There is
no remote REC EVENT, counter or target-health readout after placement.
IsRecording reports an enabled recorder with a valid lens, independently of
rendering. Placed recording still receives DamageEvents and uses the same FOV
and obstruction checks; existing counters update privately, without a new score
system. No video, files or playback are produced; events live only in device memory.
Player can walk and turn independently; the placed camera no longer consumes hand
input. Camera placement keeps the exact hand height without
dropping or snapping to a surface. Q/E from outside pickup reach does nothing;
re-enabling a placed recorder does not restore live video. There is no inventory,
multi-camera selection or equipment system. R removes the device and creates
one new held device and feed with a new ID. TryPlace/TryPickup reject another
holder's occupied device; no automatic stealing or transfer behavior is added.

Dummy's existing PlayerController has five simple states: Idle, Approach,
Windup, Attack, Recovery. Its assigned `opponent` is Player's Damageable.
Within 6 m and with a clear line of sight, it approaches at 1.2 m/s using the
existing Rigidbody motor and low obstacle probe. It stops within the current hand's reach, faces the
player, then commits to a 0.7 s windup, 0.18 s strike and 1.15 s recovery.
Windup tints its body amber and draws back its existing right arm. The arm uses
MeleeAttack's existing pivot/tip sweep; no weapon item or second damage system
was added. During the entire swing/recovery the enemy stops and cannot retarget.
Move away during windup, then counter during recovery. Sprint 6 hand contact deals
36 base fist damage before the existing body-part multiplier; limb hits are weaker.
Blood, recoil and the fatal fall use HitFeedback.
There is no pathfinding, target memory, flanking or vertical combat trajectory.
Local tangent steering goes around small low cover; complex cover can still stop it.
A controller without an assigned opponent
retains the old random wandering for archived noncombat test scenes.

Player death disables movement/hand actions through the existing health checks,
releases the body's rotation and shows `YOU DIED / R: restart`. R reloads the
scene independently of the camera, including when it is placed or disabled.
The scene recorder subscribes to BOTH Damageables. A placed camera can witness
player hits, counterstrikes and either actor's death, with no live feed during
combat. The recording code has no dependency on enemy decisions or state.

Historical `RRM.Editor.CombatPrototypePlayCheck.RunEnemyAndExit` ran the focused native
Play Mode check (graphics enabled, no `-quit`): blocked/far idle, approach,
visible windup, actual S retreat, mouse counterstrike during recovery, real
enemy damage/blood/player death, placed recording and R restart. Counter range
is arranged by the test; hits themselves come from actual physics sweeps, not
injected DamageEvents. `Verification/enemy-*.png` contains native scene/feed
captures; `enemy-death-ui.png` additionally needs a non-batch graphical run.
Use `RunDuelAndExit` for current hand-based enemy behavior instead.
`RRM > Enable Enemy Prototype` reapplies only the existing Dummy arm/attack and
scene actor references; it never rebuilds the room.

The existing melee sweep hits Hurtbox, Damageable emits DamageEvent, and
HitFeedback immediately emits 20 blood particles (32 on a fatal hit), alongside
the existing flash, recoil and sound. Blood uses the existing Blood.mat and
world-space particles, enlarged for the top-down view; it disappears within
0.7 seconds. There are no decals, wounds or new damage systems.

Tune moveSpeed and obstacleLookAhead on Dummy's PlayerController; windup, strike, recovery, damage and impulse
on MeleeAttack; health on Damageable; zone multipliers on Hurtbox; horizontal
fieldOfView on CameraRecorder. Visibility uses the live feed's rectangular FOV
and wall occlusion at the damage point, with no maximum detection distance.
conePreviewLength limits only the floor outline, never gameplay detection.

While handheld, the bottom-right live window is a real Camera at the Lens Origin,
rendering the same scene continuously into a 640x480 RenderTexture. It follows
Player's hand position and body rotation through the existing transform hierarchy;
CameraRecorder adjusts its local yaw/pitch. CameraRecorder displays it in
the existing OnGUI interface, including when the diagnostic overlay is hidden.
The texture is released on placement, disable or restart. The render camera has a 10000-unit
far clip for rendering only; this is not a visibility/scoring distance cap.
The recording-cone line is excluded from this camera; the light-map floor remains
visible. There are no VHS effects or
post-processing. CameraRecorder listens to both actors' existing Damageable.Damaged
events. If the actual hit point is inside the lens's FOV and not behind a wall,
REC increases by one. While handheld, REC EVENT appears for one second, then
returns to STANDBY; placed recording shows neither message nor counter during
the fight, only the final report after either actor dies.
Off-camera/occluded hits do not increment the counter, change the
message or refresh its timer. There is no missed-event feedback, scoring system
or saved video. Visibility still has no short maximum distance.

CameraRecorder rotates the existing VHS housing and its child lens, not the
main top-down camera. Its mount stays at the same position in the player's hand.
Yaw is unrestricted; pitch ranges from 25 degrees up to 70 degrees down.
Tune mouseSensitivity, minPitch and maxPitch on
CameraRecorder. Mouse movement is a drag without cursor locking; release and
re-grab if the pointer reaches the edge of the Game View. R restores the prefab
angle. The floor cone is only a horizontal direction preview; the actual
visibility check uses the lens's full yaw/pitch and rectangular FOV.

Both actors use one upright Rigidbody. Fatal damage releases its rotation;
there is no articulated ragdoll or dismemberment. Melee uses a fixed-height
swing, so head hitboxes are infrastructure, not a separate aimed attack yet.

Run `RRM > Run Combat Checks` in the editor for the automated checks. The checks
use a temporary additive scene and close it afterwards. The original scene is
preserved. `RRM > Open Combat Prototype` opens the room without rebuilding it.

Game logic subscribes directly to Damageable.Damaged. DamageEvent includes the
source, target, body part, world point, direction, applied damage, impulse and
fatal flag. CameraRecorder subscribes to the explicitly assigned subjects in
this room; there is no global event bus or scene-wide polling.

## Current lamp verification

Close this project's Editor and run a graphics-enabled check (no `-quit`):

```sh
Unity -batchmode -force-glcore -projectPath /path/to/RRM -executeMethod RRM.Editor.CombatPrototypePlayCheck.RunLampsAndExit -logFile /path/to/lamps.log
```

`PrepareLampsAndExit` first reapplies the narrow setup twice and reopens the saved
CombatPrototype before running A+B. `RunLampsAAndExit` checks A alone. Logs for the
passed native runs: `Logs/lamps-a.log` and `Logs/lamps-ab.log`. The old one-room
and baton-specific scenarios below are historical; use the lamp entry points for
current equipment. Core temporary-scene combat/FOV/light tests run first in both.

Standalone light regression checked separately in Linux:
`Builds/LampVerification/RRM.x86_64`, built by
`RRM.Editor.CombatPrototypePlayCheck.BuildLampVerification`, zero build errors.
It launches without Editor, renders the smooth map, three lamps, HUD and live feed.
Screenshot: `Verification/lamps-linux-player.png`; logs: `Logs/lamps-linux-build.log`
and `Logs/lamps-linux-player.log`. This is a graphics/launch check, not a Windows
test or a replacement of the existing Windows/Linux distribution bundles.

Native A+B evidence: real keyboard doorway movement and mouse strikes; pickup in
both hands; supported floor/cover placement; rejecting pickup when a hand lies
inside the divider; stationary light probe 47.35 -> 100 -> 47.35; immutable old
record quality; a lamp limb hit of 37.8 versus a fist's 12.6 (base damage 54/18).
The placed camera captured contact light 100 before lamp removal reduced it to
41.61. Misses, held/released buttons, shared recovery, fist fallback, unaffected
other lamps, live/private feed and R equipment/source reset passed. Geometry and
Dummy prefab were preserved. Screenshots: `Verification/lamps-a-*.png` and
`Verification/lamps-b-*.png`. Event-quality comparisons in A inject small test
DamageEvents through Damageable; fist and lamp hits use actual input/physics.

## Historical verification scenarios

Files changed for the portable-lamp sprint (existing unrelated changes retained):
- `Scripts/PlayerController.cs`, `Scripts/CameraDevice.cs`: item routing and reachable pickup.
- `Scripts/CameraRecorder.cs`: F1 controls only; recording/FOV logic unchanged.
- `Scripts/MeleeAttack.cs`: context parameters, fist and single-use lamp sweep.
- `Scripts/LightSource.cs`, `Scripts/LightLevelPreview.cs`: linked point light and saved transparent template.
- `Scripts/PortableLamp.cs` (new): hand mount, local surface placement and consumption.
- `Editor/CombatPrototypeBuilder.cs`, `Editor/CombatPrototypeTests.cs`,
  `Editor/CombatPrototypeLampCheck.cs` (new): narrow setup and runnable checks.
- `Prefabs/Player.prefab`, `Prefabs/PortableLamp.prefab` (new),
  `Materials/LightLevelPreview.mat` (new), `Scenes/CombatPrototype.unity` and this README.
- Unity-generated `.meta` files accompany each new script/material/prefab.

DamageEvent, Damageable, Hurtbox, HitFeedback, Dummy.prefab, Input Actions and room
geometry were not changed by this sprint. Logical light still ignores walls; visual
point-light shadows and camera occlusion are separate. Placement is only for flat
floor/cover support; there is no lamp physics, destruction debris or video recording.
The older baton-dependent checks below were not adapted or claimed to pass.

For the older combined input/physics/render check:

```sh
Unity -batchmode -projectPath /path/to/RRM -executeMethod RRM.Editor.CombatPrototypePlayCheck.RunCombinedAndExit -logFile /path/to/play-check.log
```

Do not add `-quit`: the combined check closes its dedicated editor when finished.
The command uses no Editor windows, but keeps graphics enabled (do not add
`-nographics`). This still checks real camera pixels,
input, damage and recording. Game View layout and the F1 screenshot require a
separate graphical run; batch mode skips the window-layout check and preserves
`Verification/hands-controls-f1.png` from that graphical run.
The enemy sprint's full batch Play Mode check passed, including both actual enemy
hits, the fatal fall, private camera evidence and R restart. The graphical combined
run exceeded its 240-second limit during earlier movement checks on this Linux
Editor. The death-message state is checked, but its on-screen layout has not yet
been visually verified. Use normal manual Play Mode for that final UI inspection.
That historical combined check starts with independent controls: actual LMB drag
and the hand model (left-camera/right-weapon, distinct click/hold signals,
Q/E switching via placement, visible weapon transfer both ways, RMB aiming after
right-hand pickup, actual left-hand melee/blood/recording, rejected-pickup rollback,
no attack with the camera hand, disabled input, no arrows/F,
simultaneous Q/E, and F1). It checks looking backward, body-forward W at
90-degree character rotation, camera-button release,
S/A/D/diagonal movement without auto-turn, free mouse turning while idle,
north-to-east body turning with the camera still behind the body, pitch limits,
place/pickup and restart. The camera's local angle and hand position are
checked through body turns. For only these checks use
`RRM.Editor.CombatPrototypePlayCheck.RunControlsAndExit`. Captures include
`Verification/controls-backward-walk-ui.png` and the `controls-*-feed.png` images.
Close-up native scene renders of both item arrangements: `hands-camera-left.png`
and `hands-camera-right.png`. These are real runtime meshes, not UI hand labels.
It now also runs the existing height scenario in the current two-room scene:
W ascent onto the 0.9 m platform, a stationary pause, and S descent. It checks
matching lens movement, unchanged aim and real feed pixels. Lowering only the
lens at the same horizontal position hides the torso behind the block again,
isolating height from forward movement. Captures: `Verification/height-ground-feed.png`,
`height-platform-feed.png`, `height-return-feed.png`, and ground/platform `-ui.png`.
The focused locomotion check exercises standing/moving/elevated Space taps, a
rejected air tap, stationary/moving Space-hold crouch, Ctrl crouch, no jump on long
release, 50% speed and recovery, ceiling clearance, independent mouse aim in flight,
crouched camera place/pickup, reset and pending-tap cancellation on focus loss.
Captures: `movement-standing.png`, `movement-jump.png`,
`movement-crouch.png`, `movement-crouch-right-camera.png` and `movement-*-feed.png`.
Use `RRM.Editor.CombatPrototypePlayCheck.RunLocomotionAndExit` to run this current
movement/camera scenario plus the existing edit-mode checks, without rebuilding
the scene. It no longer assumes a baton in the other hand. The broader combined
hand check remains historical as noted at the top of this README. The focused
run ends with a real W + short Space jump onto the saved 45 cm starter cover,
stable landing, live feed and S descent, without running the scene builder.
On 2026-09-13 the movement/camera portion passed in graphics-enabled Play Mode
(`Logs/space-controls.log`): real keyboard/mouse input, tap/hold separation,
jumps, crouch, speed recovery, headroom, camera aim/feed, Q/E pickup, F1, R and
focus cancellation. No combat changes were made or newly playtested in this step.
The subsequent cover run stalled during Editor startup. Its retry eventually
entered Play Mode but ended in a native Unity crash after a stop request, before
reaching the cover check (`Logs/space-controls-cover*.log`). The new-input cover
check is still unverified. The saved scene, prefabs and ProjectSettings remained
unchanged; no test Editor was left running.
For just that scenario, use `RRM.Editor.CombatPrototypePlayCheck.RunHeightAndExit`
as the execute method. It opens the same CombatPrototype scene without rebuilding it.
The older individual scenarios documented below were written for the original
one-room geometry and cursor/world-space controls; use the combined check above
for the current prototype.

The legacy full sequence checks movement, a real mouse attack against
the moving Dummy, an immediate DamageEvent/blood check, death, R-restart,
a 40-second random walk starting near a room corner, wall collisions and
live-feed checks. The walk checks travel, turns, short pauses, corner escape,
room bounds and penetration against the existing room colliders. It reads
the running camera's texture before/after Dummy moves and verifies that the
image changes. At the first hit it compares the same rendered frame
with/without the blood renderer to verify that the burst is actually visible.
The death/fall check uses a separate direct fatal damage event; the first hit
and blood burst come from the real weapon and mouse input, not injected damage.
Screenshots are written to `Verification/`, including `prototype-contact.png`
and, in a graphical editor, `combat-blood-ui.png`.
The camera check also drags left/right/up/down using real Input System events,
checks the target's image position, angular limits, independent character
orientation, movement while filming and angle retention after releasing RMB.
It also performs two real mouse-driven melee hits on the same target:
one with the handheld aimed at the hit (REC EVENT and REC +1), then one with
the camera turned away (damage still occurs, REC stays unchanged and no message
appears). These cases use no injected damage. Their UI screenshots are
`Verification/rec-visible-ui.png` and `Verification/rec-hidden-ui.png`.
It also walks up the ramp with W, stands still on the platform, and returns
with S. It checks the 0.9 m rise, matching lens movement, unchanged viewing angle,
live texture changes, and a target torso becoming visible over the concrete
block only from above. Dummy is stationary only during this test. Height
captures are `Verification/height-ground-feed.png`, `height-platform-feed.png`,
`height-return-feed.png` and, in a graphical editor, the matching ground/platform
`-ui.png` screenshots.
Finally it frames a wandering Dummy, presses F, and walks/turns Player away.
It checks the camera's unchanged world pose, stopped rendering/released texture,
continued Dummy motion and private DamageEvent handling. Test-only damage also
checks unsubscribe/re-subscribe on disable/enable without restoring remote video.
F from too far away creates no duplicates. The player then walks back and presses
F to pick up the same device; ID, owner, retained events, hand mount, live feed
and following movement are checked. A test-only transfer to Dummy and back checks
owner replacement, occupied-device rejection and obstruction at pickup. Dummy
gets no new gameplay behavior. R restores one handheld device with a new ID,
fresh live texture and cleared counter. Pickup captures are
`Verification/device-pickup-feed.png` and `device-pickup-ui.png`.
Other captures are `Verification/placed-before-feed.png`,
`placed-restart-feed.png` and, in a graphical editor, `passive-handheld-ui.png`
and `passive-recording-ui.png` (no feed or recording diagnostics).
F1 is then opened and closed in both modes, with screenshots
`Verification/controls-placed-ui.png` and `controls-handheld-ui.png`.
Use a graphics-enabled editor for the live-feed checks.
Test-only timing and input-focus settings are restored afterwards.
For a Game View screenshot including the UI, omit `-batchmode` and use
`RRM.Editor.CombatPrototypePlayCheck.RunAndExit` as the execute method. This
opens a dedicated test editor and closes it when the checks finish.
Use `RRM.Editor.CombatPrototypePlayCheck.RunCameraAndExit` for just the camera
placement/pickup, ownership, passive events, F1 guide, reports and restart, plus the core checks.
The report check sends damage through the existing Damageable in Play Mode:
held and placed cameras each see or miss the fatal event. It also checks a
disabled device, an earlier hit followed by an unseen death, N/A with no evidence,
stored distances, owner/ID text, and fresh evidence after R. Screenshots are
`Verification/report-held-yes.png`, `report-held-no.png`, `report-placed-no.png`
and `report-placed-yes.png`. Independent recording by two devices is checked in
the temporary additive test scene, never by adding another camera to the room.
Use `RRM.Editor.CombatPrototypePlayCheck.RunReportsAndExit` to run only those
report scenarios and the core checks, without the longer movement scenarios.
Use `RRM.Editor.CombatPrototypePlayCheck.RunRangeAndExit` only for the archived two-room layout snapshot:
it checks Dummy motion, keyboard traversal through the doorway both ways,
collision with the divider, actual live images through the doorway and behind
low/high cover, a placed camera's report, and R restarting the same scene with
one fresh handheld device. For controlled occlusion checks it temporarily stops
and positions Dummy; the fatal report uses test damage through Damageable, not
a mouse-driven melee hit. Captures are `Verification/range-*.png`.
For repeated Linux checks, launch the Editor executable directly (not Hub), wrap
the command with `timeout -k 15s 420s`, and wait for that process to exit before
opening the project again. The timeout bounds the test's own process group if it
hangs; do not run this check alongside an open editor for the same project.
Keep Package Manager enabled: `-noUpm` also removes required package references.
The Play Mode check uses random seed 12345; set RRM_PLAY_CHECK_SEED to an integer
to exercise another walk. The game's previous random state is restored afterwards.
`Verification/dummy-wander-room.png` captures the end of the wandering check.
Unity 6000.6.0f1 can log a SearchDatabase exception during editor startup;
the check reports that editor-index error separately from gameplay failures.

## Desktop builds

`Editor/DesktopBuild.cs` builds the saved CombatPrototype with Unity 6000.6.0f1,
Mono, the desktop Player subtarget, non-development settings and LZ4HC compression.
Only CombatPrototype is included at index 0, so R reloads the same room. The
script does not rebuild the scene or edit the saved Build Profiles/global scene list.
The existing Windows/Linux profiles inherit the three-scene list: CombatPrototype,
SampleScene and the archived CameraTestRange. CLI builds bypass that list.

Windows Mono and Linux Mono support are available on this machine; installed
Linux IL2CPP support is not used. The commands also disable optional Burst
compilation. Mono and the ordinary Unity Player libraries travel with the build;
the destination needs neither Editor/Hub nor a separate Mono/.NET SDK. Supported
OS libraries and graphics drivers are still required, see
[Unity Player requirements](https://docs.unity3d.com/6000.6/Documentation/Manual/system-requirements.html).

Close this project's Editor first. Run these commands sequentially, not at the
same time. They use the installed Unity CLI and this machine's Flatpak Hub
license/configuration location:

```bash
XDG_CONFIG_HOME="$HOME/.var/app/com.unity.UnityHub/config" \
XDG_DATA_HOME="$HOME/.var/app/com.unity.UnityHub/data" \
unity build "$HOME/RRM/RRM" --target StandaloneWindows64 \
  --execute-method RRM.Editor.DesktopBuild.Windows --allow-dirty-build \
  --timeout 1800 --log-file "$HOME/RRM/RRM/Logs/build-Windows.log" \
  --provenance-path "$HOME/RRM/RRM/Logs/build-Windows.provenance.json" \
  --args '-standaloneBuildSubtarget Player -force-glcore --burst-disable-compilation' --no-tail

XDG_CONFIG_HOME="$HOME/.var/app/com.unity.UnityHub/config" \
XDG_DATA_HOME="$HOME/.var/app/com.unity.UnityHub/data" \
unity build "$HOME/RRM/RRM" --target StandaloneLinux64 \
  --execute-method RRM.Editor.DesktopBuild.Linux --allow-dirty-build \
  --timeout 1800 --log-file "$HOME/RRM/RRM/Logs/build-Linux.log" \
  --provenance-path "$HOME/RRM/RRM/Logs/build-Linux.provenance.json" \
  --args '-standaloneBuildSubtarget Player -force-glcore --burst-disable-compilation' --no-tail
```

On another build machine, use its matching licensed Editor/modules and normal
Hub configuration instead of these XDG overrides. Each platform requires its
own Editor process, see [Unity CLI builds](https://docs.unity3d.com/6000.6/Documentation/Manual/build-command-line.html).

- Outputs: `Builds/Windows/RRM.exe` and `Builds/Linux/RRM.x86_64`.
- Distribute the ENTIRE corresponding folder, not just its executable.
- Each command recreates its output folder. Keep personal files and old ZIPs
  elsewhere. `Builds/` and `Logs/` are already ignored by Git.
- Player folders contain no project source, Editor or Library directory.
- Failed builds/missing expected Player files return a nonzero CLI exit.
  Success logs contain `RRM DESKTOP BUILD PASSED` and zero build errors.
- The Standalone scripting backend is restored after building.
- Reproducibility means the same saved inputs/configuration, not byte-identical
  output: Unity build identifiers/timestamps vary. CLI provenance records the
  Editor, packages and Git state, including uncommitted changes.

The 2026-09-12 graphics-enabled Play Mode checks passed with the revised controls:
`Logs/controls-relative-camera.log` covers cursor-driven turning, body-relative
strafe/backpedal, rear camera following body rotation, real feed pixels, both
hands, pitch limits, placement/pickup and restart. `Logs/locomotion-relative-camera.log`
covers standing/moving/elevated jumps, no double jump, crouch, headroom, reduced
speed and recovery, camera height and lowered pickup.

Pre-lamp Player verification on 2026-09-12 (the existing Windows/Linux bundles below
do not contain the lamp sprint):
- Windows x64: 187 Player files, 99,858,121 bytes (95.23 MiB), PE32+ x86-64.
  Includes RRM.exe, UnityPlayer.dll, UnityCrashHandler64.exe, D3D12, dstorage
  DLLs, RRM_Data (Managed/resources/plugins) and MonoBleedingEdge.
- Linux x64: 182 files, 97,589,522 bytes (93.07 MiB), ELF x86-64, executable mode
  755. Includes RRM.x86_64, UnityPlayer.so, bundled libdecor libraries and
  RRM_Data (Managed/resources/MonoBleedingEdge). Native ldd dependencies resolve.
- Exact inventories: `Logs/desktop-windows-files.txt`, `Logs/desktop-linux-files.txt`.
  No project Assets/Library/Editor, C# source files or symlinks in either Player.
- Both BuildReports succeeded with zero errors (existing obsolete-API warnings).
- Both full folders were copied outside the project and launched with graphics,
  with no Editor running. Linux on Fedora 44/Mesa OpenGL renders the current
  two-room scene, HUD and live feed. Windows via Wine 11/DXVK renders the scene
  and live feed, but reports a LegacyRuntime font error and does not render HUD
  text. Native Windows is NOT verified; the Wine font issue remains unresolved.
- Both runtime logs also warn about stripped/unsupported unused URP postprocess
  shaders. No gameplay exception was observed. Windows falls back to D3D11 when
  Wine cannot create D3D12. These are launch checks, not a full platform QA pass.
- Evidence: `Verification/desktop-linux-controls.png`,
  `Verification/desktop-windows-controls-wine.png`, `Logs/desktop-linux-player.log`,
  `Logs/desktop-windows-wine-player.log`.
- The pre-existing `Builds/Windows/RRM alpha.zip` is unchanged and contains the
  OLD version, not the new controls. It is excluded from the 95.23 MiB Player
  size/inventory. Including that old ZIP the Windows folder is 133.49 MiB.
  A complete pre-update copy remains in `Builds/Windows-before-controls/`.
