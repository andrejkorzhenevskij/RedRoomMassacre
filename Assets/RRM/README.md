# RRM technical prototype

Open `Scenes/CombatPrototype.unity` and enter Play Mode.

- WASD: move Player (the original arrow-key bindings also remain available).
- Mouse: turn Player toward the cursor on the floor.
- Hold right mouse button and drag: aim only the handheld camera, horizontally
  and vertically. WASD still moves Player; the character does not turn during
  the drag. Release to keep the camera angle and resume normal character aiming.
- Left mouse button: one committed swing.
- F: leave the camera recording at its current position and angle; the live window disappears.
- F1: show/hide the controls guide, both handheld and after placement.
- R: restart the room, health and tape counters.

The F1 guide temporarily replaces the HUD without pausing the room. It contains
only static controls, never remote camera information, and scrolls in small views.

The right side of the room has a 0.9 m Camera Platform with a shallow approach
from the south (bottom of the top-down view). Walk up and back down with WASD;
no jump or interaction button is needed. The platform and ramp are two static
boxes using Wall.mat. The existing Rigidbody motor and gravity handle the slope;
there is no climbing controller, snapping or teleport. The handheld stays in
the player's hand, so its lens and live feed rise with the player. Hold RMB to
keep the same viewing direction while changing height. The floor cone remains
only a flat direction preview, not a projection onto the raised surface.

CameraRecorder uses its existing IsPlaced flag for two modes. HANDHELD keeps
the camera attached to Player with live video. PLACED / RECORDING keeps the same
housing and lens fixed in the world, but disables the rendering Camera, releases
the RenderTexture and hides both the feed and its diagnostic overlay. There is
no remote REC EVENT, counter or target-health readout after placement.
IsRecording reports an enabled recorder with a valid lens, independently of
rendering. Placed recording still receives DamageEvents and uses the same FOV
and obstruction checks; existing counters update privately, without a new score
system. No video, event archive, files or playback are produced by recording.
Player can walk and turn independently; RMB no longer aims the placed camera
or locks the character's facing. Placement keeps the exact hand height without
dropping or snapping to a surface. Repeated F does nothing; re-enabling a placed
recorder does not restore live video. There is no pickup, inventory or equipment
system. R removes the placed recorder and restores one handheld camera and feed.

Dummy wanders slowly with a target speed of 1.2 units/second. It picks a random
clear direction, walks for 2-4 seconds, then pauses for 0.4-0.9 seconds before
choosing again. A low, body-width sphere cast detects Default-layer room geometry
ahead, including the short south wall. An obstacle ends the current walk early;
up to eight candidate directions are tried after the pause. If none is clear,
Dummy waits 0.4 seconds and retries. This is local steering, not pathfinding:
it does not plan routes through complicated enclosures or deliberately use ramps.
The existing PlayerController and Rigidbody still supply all movement and
physical collisions with walls and actors. Dummy turns toward its movement;
there is no player pursuit, NavMesh or attack component. Its motor stops on
death. R starts a fresh moving target. IJKL controls remain replaced by wandering.

The existing baton sweep hits Hurtbox, Damageable emits DamageEvent, and
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
Player through the existing transform hierarchy. CameraRecorder displays it in
the existing OnGUI interface, including when the diagnostic overlay is hidden.
The texture is released on placement, disable or restart. The render camera has a 10000-unit
far clip for rendering only; this is not a visibility/scoring distance cap.
The floor preview is excluded from this camera. There are no VHS effects or
post-processing. CameraRecorder listens to Dummy's existing Damageable.Damaged
event. If the actual hit point is inside the lens's FOV and not behind a wall,
REC increases by one. While handheld, REC EVENT appears for one second, then
returns to STANDBY; placed recording shows neither message nor counter.
Off-camera/occluded hits do not increment the counter, change the
message or refresh its timer. There is no missed-event feedback, scoring system
or saved video. Visibility still has no short maximum distance.

CameraRecorder rotates the existing VHS housing and its child lens, not the
main top-down camera. Its mount stays at the same position in the player's hand.
Local yaw is limited to +/-90 degrees; pitch ranges from 25 degrees up to
70 degrees down. Tune mouseSensitivity, minPitch, maxPitch and maxYaw on
CameraRecorder. Mouse movement is a drag without cursor locking; release and
re-grab if the pointer reaches the edge of the Game View. R restores the prefab
angle. The floor cone is only a horizontal direction preview; the actual
visibility check uses the lens's full yaw/pitch and rectangular FOV.

Both actors use one upright Rigidbody. Fatal damage releases its rotation;
there is no articulated ragdoll or dismemberment. The baton has a fixed-height
swing, so head hitboxes are infrastructure, not a separate aimed attack yet.

Run `RRM > Run Combat Checks` in the editor for the automated checks. The checks
use a temporary additive scene and close it afterwards. The original scene is
preserved. `RRM > Open Combat Prototype` opens the room without rebuilding it.

Game logic subscribes directly to Damageable.Damaged. DamageEvent includes the
source, target, body part, world point, direction, applied damage, impulse and
fatal flag. CameraRecorder subscribes to the explicitly assigned subjects in
this room; there is no global event bus or scene-wide polling.

For the full input/physics/render check, close this project's editor and run:

```sh
Unity -batchmode -projectPath /path/to/RRM -executeMethod RRM.Editor.CombatPrototypePlayCheck.Run -logFile /path/to/play-check.log
```

Do not add `-quit`: the check exits after movement, a real mouse attack against
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
Repeated F creates no duplicates; R restores one handheld camera with a fresh
live texture and cleared counter. Captures are `Verification/placed-before-feed.png`,
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
placement, passive events, F1 guide and restart scenario, plus the core checks.
The Play Mode check uses random seed 12345; set RRM_PLAY_CHECK_SEED to an integer
to exercise another walk. The game's previous random state is restored afterwards.
`Verification/dummy-wander-room.png` captures the end of the wandering check.
Unity 6000.6.0f1 can log a SearchDatabase exception during editor startup;
the check reports that editor-index error separately from gameplay failures.
