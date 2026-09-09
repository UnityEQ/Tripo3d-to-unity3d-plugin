# Modular Biped Humanoid Rig Standard v1

Character-specific fitting and sparse gameplay animation for Blender → Unity Humanoid. The user's current request overrides defaults. The skeleton schema remains `biped_humanoid_v1`; this revision changes execution and animation guidance, not bone names, parents, or axes.

**Deliverable:** a fitted, skinned character with preserved geometry/UVs/materials/normals, editable sparse Actions, baked FBX, character manifest, and validation against that exact FBX in Unity. Quality is demonstrated by motion and deformation checks; no fixed key count or automatic solver guarantees a perfect animation.

## Efficient execution

Read this file and `Lychee Model GLB - Blender FBX - Unity.md` once per job; subsequently read only changed sections. Read `Tools/Blender/biped_humanoid_v1/README.md` for actual helper capabilities. Pass `Biped Humanoid Rig v1 - Reference.json` to code with `--schema`; never print its matrices into chat.

1. **Inventory → fit → bind → pose gates → Idle → other clips → spline review → export → Unity.** Repair the earliest failing stage. Do not repair a motion error by changing passed rest bones or weights.
2. Use persistent `work/TripoRigJobs/<id>/` or another non-Unity workspace. **Nothing needed for recovery belongs under the project's `Temp/`, including `Temp/tripo-ai-jobs/<id>/`.** The queue JSON and its lifecycle markers remain at their prescribed Temp paths; checkpoints do not.
3. Save `inventory`, `fitted_bound`, `animated`, and `export_copy` checkpoints. Record source/schema/helper/parameter hashes, application versions, and passed checks. Reuse only matching, passed stages. A motion-only repair invalidates that clip and final export/Unity checks, not heat binding. No automatic cache is implied.
4. Reuse `run.py` functions and `agent.py`. The default `run.py` command fits by overall height and then heat-binds: **do not run that full path as a fitted bind**. Import, fit this character, check pivots, then call `build_armature`, `heat_bind`, and `refine_weights`. Do not spend a heat attempt on known misplaced elf joints.
5. Character-specific files hold landmarks, contact intervals, clip timing, targets, and clearance margins. Reuse Blender's IK and existing helpers. If a capability is missing, add one bounded reusable helper with a documented interface; do not invent a fresh solver, collider, or Editor importer for each job. The helpers do not currently supply a complete animation or Unity validator.
6. Batch each stage into a tool call with full logs on disk. Return only stage, pass/fail, failing joint/clip/time, and evidence path (roughly 5–10 lines). A zero Blender exit code is insufficient: require the expected fresh report/checkpoint and inspect exceptions. Never print vertex arrays, complete bone matrices, or collision pairs.
7. **Default visual budget:** five contact sheets (weight diagnostic, Idle, Run side, Jump side, Slash front+side), plus the required crotch diagnostic and one small playback per clip. Open the sheets; play clips at intended speed. Render at review resolution first. Regenerate only changed evidence. Extra close-ups require an identified defect, not a routine frame-by-frame beauty pass.
8. **Repair budget:** one targeted correction, then recheck the affected interval. If it still fails, diagnose reach, coordinate space, contact timing, or skinning before changing parameters again. Stop blind pole/offset searches. Unresolved failures block completion; a time budget never turns a failed rig into a pass.
9. Maintain the queue's status/stage and cancellation checks exactly as its prompt requires. Mark done only after export and Unity checks; never infer success from an old output.

## Transform and naming contract

- Reference coordinates: **+X forward, +Y anatomical left, +Z up**, meters. Bone local **+Y points head→tail**. `.L`/`.R` are anatomical sides, not screen sides.
- Apply object rotation/scale on an **unbound copy**; mesh and armature use consistent transforms. Do not blindly apply transforms to a bound asset. Preserve custom normals and UV seams.
- Fit positions/lengths to anatomy. Calibrate roll from reference rest bases; keep bases orthonormal, right-handed and invertible. The reference is shallow A-pose. Unity T-pose calibration is separate and must not destructively replace the Blender bind.
- Matrices are rows in JSON, operating on column vectors; translation is the last column. For bone `i`, `B_i` is armature-space rest, `P_i` is evaluated pose, `L_i = inverse(B_parent) * B_i`, `Q_i = inverse(P_parent) * P_i`, and `D_i = inverse(L_i) * Q_i`. Root has no parent.
- Skinning is `P_i * inverse(B_i)`. Mesh-to-bone inverse bind is `inverse(M_armature_world * B_i) * M_mesh_world`. Regenerate it for this character; never copy the elf's numerical inverse binds.
- Retarget calibrated rotation deltas. If local bases differ, use `C * D_rotation * inverse(C)` with a documented basis conversion. Handle root/hip translation scaling separately. Do not remap arbitrary Euler ranges.
- Animate normalized rotations, unit bone scale, and no IK stretch. Keep source rotations in one consistent representation per bone.

### Exact core skeleton

| Bone | Parent | Deforms |
|---|---|---|
| `Root` | None | No |
| `Hips` | `Root` | Yes |
| `Spine` | `Hips` | Yes |
| `Chest` | `Spine` | Yes |
| `Neck` | `Chest` | Yes |
| `Head` | `Neck` | Yes |
| `Clavicle.L` | `Chest` | Yes |
| `UpperArm.L` | `Clavicle.L` | Yes |
| `Forearm.L` | `UpperArm.L` | Yes |
| `Hand.L` | `Forearm.L` | Yes |
| `Thumb1.L` | `Hand.L` | Yes |
| `Thumb2.L` | `Thumb1.L` | Yes |
| `Thumb3.L` | `Thumb2.L` | Yes |
| `Index1.L` | `Hand.L` | Yes |
| `Index2.L` | `Index1.L` | Yes |
| `Index3.L` | `Index2.L` | Yes |
| `Middle1.L` | `Hand.L` | Yes |
| `Middle2.L` | `Middle1.L` | Yes |
| `Middle3.L` | `Middle2.L` | Yes |
| `Ring1.L` | `Hand.L` | Yes |
| `Ring2.L` | `Ring1.L` | Yes |
| `Ring3.L` | `Ring2.L` | Yes |
| `Little1.L` | `Hand.L` | Yes |
| `Little2.L` | `Little1.L` | Yes |
| `Little3.L` | `Little2.L` | Yes |
| `Clavicle.R` | `Chest` | Yes |
| `UpperArm.R` | `Clavicle.R` | Yes |
| `Forearm.R` | `UpperArm.R` | Yes |
| `Hand.R` | `Forearm.R` | Yes |
| `Thumb1.R` | `Hand.R` | Yes |
| `Thumb2.R` | `Thumb1.R` | Yes |
| `Thumb3.R` | `Thumb2.R` | Yes |
| `Index1.R` | `Hand.R` | Yes |
| `Index2.R` | `Index1.R` | Yes |
| `Index3.R` | `Index2.R` | Yes |
| `Middle1.R` | `Hand.R` | Yes |
| `Middle2.R` | `Middle1.R` | Yes |
| `Middle3.R` | `Middle2.R` | Yes |
| `Ring1.R` | `Hand.R` | Yes |
| `Ring2.R` | `Ring1.R` | Yes |
| `Ring3.R` | `Ring2.R` | Yes |
| `Little1.R` | `Hand.R` | Yes |
| `Little2.R` | `Little1.R` | Yes |
| `Little3.R` | `Little2.R` | Yes |
| `Thigh.L` | `Hips` | Yes |
| `Shin.L` | `Thigh.L` | Yes |
| `Foot.L` | `Shin.L` | Yes |
| `Toes.L` | `Foot.L` | Yes |
| `Thigh.R` | `Hips` | Yes |
| `Shin.R` | `Thigh.R` | Yes |
| `Foot.R` | `Shin.R` | Yes |
| `Toes.R` | `Foot.R` | Yes |

There are **52 export bones: Root + 51 deform bones**. Root stays as the hierarchy ancestor but has no weights. No extra twist bones. Extensions require `EXT_` names, documented parents and modules; do not add extensions merely to fill optional Unity slots.

| Item | Name / policy |
|---|---|
| Armature object and datablock | `RIG-<slug>` |
| Render mesh object and datablock | `GEO-<slug>` or `GEO-<slug>-<part>` |
| Vertex groups | Exact owning deform-bone name; no Root/control groups |
| Optional controls, each side | `CTRL_Hand.L`, `CTRL_ArmPole.L`, `CTRL_Foot.L`, `CTRL_LegPole.L` and `.R`; parent Root, non-deforming |
| Optional widgets / mechanism | `WGT_*` / `MCH_*`; no skinning or export |
| IK properties on armature | `Arm_IK_L`, `Arm_IK_R`, `Leg_IK_L`, `Leg_IK_R`; 0 = FK default, 1 = IK |
| Actions | `Idle`, `Run`, `Jump`, `SwordSlash` |

Do not Mixamo/Unreal/Rigify-rename the bones or append `.001`. Missing physical digits retain semantic bones with documented unused weights; do not invent geometry. Different anatomy may require a new compatibility profile rather than an invalid human joint fit. Changes to core hierarchy/axes require a new schema version; never overwrite the reference JSON with a character fit.

### Unity Humanoid map

| Unity slot | v1 bone |
|---|---|
| Hips, Spine, Chest, Neck, Head | Same names |
| Left/Right Shoulder, UpperArm, LowerArm, Hand | Clavicle, UpperArm, Forearm, Hand with `.L`/`.R` |
| Left/Right UpperLeg, LowerLeg, Foot, Toes | Thigh, Shin, Foot, Toes with `.L`/`.R` |
| Left/Right Thumb/Index/Middle/Ring/Little Proximal/Intermediate/Distal | Corresponding digit `1`/`2`/`3` with `.L`/`.R` |

No UpperChest, Jaw or eyes in v1. Map all **51** intended Humanoid slots explicitly and verify them on the instantiated Animator. For code, obtain `HumanBone.humanName` from `HumanTrait.BoneName[(int)HumanBodyBones.X]`; finger labels contain spaces. Rebuild the avatar description from the current imported skeleton, not stale data from an older model. A valid avatar alone does not prove every optional finger/chest mapping survived.

## 1. Inventory and backup

Inspect the available Blender scene/source before editing. Record topology/counts, connectivity and separate shells, UV layers, materials/images, custom normals, shape keys, transforms, groups, armatures/modifiers, Actions/NLA, attachments and dependencies. Back up files being replaced, including metadata. Normalize an unbound working copy. Do not modify the source mesh to make counts convenient.

## 2. Fit anatomical landmarks

Measure both sides in front, side and hand/top views. Fit hips, spine/chest/neck/head, shoulders, elbows, wrists, knees, ankles/toe pivots, and each physical digit. Mirror only actual symmetry. A wide/short-limbed robot uses its own proportions; diagnostic angles stay the same as a slender human.

**Before heat:** check joint heads and long-bone mid-shafts inside the intended anatomical envelope. Include first knuckles. Distal tails (crown, fingertips, toe tips) may lie on or just beyond the surface. Root and controls are exempt. On open scans or overlapping armor, ray parity is inconclusive; combine multiple directions with the diagnostic views and record the limitation. A joint can sit in a hollow mechanical socket while remaining correctly inside its outer envelope. Do not chase every fingertip with a bind cycle.

Save the fitted rest matrices and landmark parameters. Elbows/knees belong at the actual bend crease, not the midpoint of the overall limb silhouette.

## 3. Bind

Build the exact hierarchy with reference-calibrated rolls and fitted translations/lengths. Confirm finite transforms, nonzero lengths, 51 deform flags, and scale one before binding.

**Heat is the first pass:** use `heat_bind` / Blender `ARMATURE_AUTO`, then `refine_weights`. Exactly one live Armature modifier, using vertex groups and this rig; envelopes and Preserve Volume off. No Corrective Smooth or twist-bone workaround: Unity must reproduce linear blend skinning.

If heat fails, stop at the first successful rung:

1. Recheck landmarks and scale; repair normals on a temporary bind copy and retry.
2. Temporary proxy: merge only true coincident splits, preserve digits; for dense meshes decimate toward ~25k vertices. Heat there, transfer `VGROUP_WEIGHTS` by `POLYINTERP_NEAREST` to the original, then delete the proxy. Never weld the render mesh or clear its custom normals as a generic fix.
3. If necessary, scale proxy and armature together ×10 for heat, then restore units and regenerate binds; or heat loose islands separately.
4. Use repository `proximity_skin.py` if heat still cannot solve; report the fallback. Nearest-bone weight 1.0 is only for remaining unweighted vertices. Envelope binding is a last resort.

Every fallback still requires §4. Successful heat coverage is not weight-paint approval.

## 4. Finish weights before animation

### 4a. Isolate leaks

Clean dust at the project's 0.01 threshold (keep at least one influence), then isolate **before** pruning:

- Own-side limbs never carry opposite-side weights. Torso groups may remain only near their anatomical junction.
- Remove remote influence: skull→Head/Neck, palm→Hand/wrist, foot→Foot/Toes/ankle, not distant torso groups.
- Crotch center→Hips; inner thigh→own Thigh + Hips, never the opposite Thigh.
- Each digit owns its phalanges; webs may blend adjacent digits + Hand, thumb base→Thumb + Hand. No opposite hand or non-adjacent finger leaks.
- Same-surface coincident UV seams get identical weights; intentionally overlapping body/armor shells do not automatically share them.

The 0.01 cleanup is a project policy, not a universal Unity discard threshold. Normalize after cleanup and isolation; do not round weights to four decimal places before writing them.

### 4b. Pose, paint the joint band, recheck

Lock unrelated groups. Select only the crease and **3–5 neighboring loops** (2–3 for small finger knuckles). Starting distribution along parent→child: 1/0 → .75/.25 → .5/.5 → .25/.75 → 0/1. Adapt to actual loop spacing, joint shape and inner/outer surfaces. A distance ramp is a first pass, not proof that these loops deform correctly.

Smooth only selected band vertices if needed (`factor .2–.3`, 1–2 passes). Compare the posed silhouette before keeping it. Whole-mesh smoothing is prohibited. If a correctly painted band still folds into a plane, refit the pivot; paint cannot repair the wrong hinge.

| Joint | Pose gate | Allowed local blend / inspection |
|---|---|---|
| Elbow | Forearm ~90°, hand forward | UpperArm/Forearm; no fold spike or outside pancake |
| Knee | Shin ~90°, lower leg backward | Thigh/Shin; no calf pulled into thigh or hard collapsing ring |
| Shoulder | UpperArm raised ~80–100°, slightly forward | Clavicle/UpperArm/Chest, local Spine only if needed; ribcage stays with torso |
| Hip/crotch | Both thighs outward ~45°; one thigh 90° sit | Hips + own Thigh; no central tent or opposite-leg pull |
| Wrist | Hand flex ~45–70° | Forearm/Hand; palm stays coherent |
| Ankle/toes | Dorsi/plantarflex and toe bend | Shin/Foot/Toes at their local creases |
| Digits | Each intermediate/distal curl, then fist | Own chain + Hand at knuckle; fingers separate, thumb opposes |
| Neck/head | Turn and slight pitch | Chest/Neck/Head locally; no jaw dragged by clavicle |

Return to rest after each repair and confirm invariance; re-pose the gate. Preserve rigid shell detail where intended rather than smoothing armor into flesh.

### 4c. Four-influence finish

Drop dust, isolate, then prune to **at most four** finite nonnegative deform weights. If a hard cutoff spikes, subtract the fifth-largest weight from all, clamp, and normalize; guard zero totals. Clean again and normalize without decimal truncation. No unweighted vertices. Target sum error ≤1e-5 (the existing helper's 1e-4 check is looser). Unity import/runtime skin quality must retain four influences.

### 4d. Diagnostic evidence

One montage: Reference A-pose, ArmsRaised V, ElbowFlex, KneeFlex, Fist, SpineTwist; bone overlays first, then solid mesh. Front and side views must reveal pivot locations and bend directions. Add crotch split and one-leg sit evidence. Inspect wrists, digits and toes close enough to see defects; use focused crops only if the montage cannot resolve them. Reject spikes, tears, pancakes, glued digits or remote pulls before authoring clips.

## 5. Sparse animation and limb mechanics

### Control strategy

**Feet:** use Blender two-bone IK for contact placement, with stretch disabled and a slight knee pre-bend. Targets specify foot position **and orientation**, not ankle position alone. Separate heel/ball/toe roll from knee bending. Keep the knee pole in a stable anatomical plane, away from collinearity with hip→ankle. Move it with the character's intended space; do not park every pole at an arbitrary global coordinate.

**Arms:** prefer FK shoulder→elbow→wrist rotations for Idle and Run swing; this naturally gives arcs with few keys. Use IK for an explicitly directed hand path/contact, including a slash hilt if useful. IK is not inherently better for every limb. If switching FK/IK, match the whole evaluated chain and hand/foot orientation at the switch; matched endpoints alone can still change roll. Prefer one mode per free-motion interval to avoid extra matching keys. [Autodesk: FK swing and IK directed motion](https://help.autodesk.com/cloudhelp/2020/ENU/3DSMax-Character-Animation/files/GUID-F85AA7B6-5462-47AF-80B0-1C4F2D5A47BB.htm)

**Reach before collision:** validate targets against actual segment lengths and the intended bend. Keep targets comfortably inside full extension except a deliberate brief push-off. Move torso/shoulder or adjust the target if unreachable; do not silently clamp and accept a different pose. IK controls should track within a few millimeters at human scale with no visible rest switch jump; stop once that passes. Blender's pole target determines chain roll, and chain length limits affected ancestors. [Blender IK](https://docs.blender.org/manual/id/5.0/animation/constraints/tracking/ik_solver.html)

**Clearance:** define hand paths in a torso-relative frame. Check against the local body surface at the hand/forearm's height, including limb thickness. Start with roughly a finger-width of clearance, a few centimeters for bulky shells, scaled to the model. Do not force hanging hands forward of the frontmost breastplate/sternum: that can overreach and create raised or rigid arms. Upper arms can lightly graze ribs; hands hang at front pockets. Slash needs a forward cutting plane; normal Run also needs a genuine backswing. Fix penetration by adjusting the local arc and torso motion, not wide shoulder abduction.

**Rotation continuity:** preserve calibrated roll and use the shortest valid swing with separately authored anatomical twist. Choose quaternion signs consistently (`dot(q_previous,q_next) >= 0`), normalize, and inspect evaluated motion. Component F-curves are not automatically spherical interpolation. If a reusable solver computes orientations, use a consistent shortest-arc method. Near-straight chains need a stable previous/reference bend plane. Detect abrupt axial flips; do not classify a legitimate large shoulder swing as a 180° twist merely from its total quaternion angle.

### Authoring rules

1. Establish timing, support foot and force direction from a short suitable reference. Block the main body poses in **stepped** interpolation; judge silhouette before splining. Hips initiate, torso responds, hands/head follow where it reads. These are artistic guidelines, not a universal mocap formula. [Wayne Gilbert: body mechanics](https://www.animationmentor.com/blog/animation-tips-tricks-what-makes-or-breaks-a-good-body-mechanics-shot/)
2. Key only changing channels on the necessary controls/bones. A single constant base value may initialize a required channel. Avoid repeated identical interior keys. Quaternion components for a keyed rotation stay synchronized; do not independently delete one component.
3. Use 4–6 Idle pose times, 8–10 Run, 7–9 Jump, 6–8 Slash, generally ≤12 full-body pose times. Count **pose times**, control keys and final baked samples separately. A finger curl is 2–3 held hand shapes, not noise on every digit at every pose.
4. Convert to spline only after blocking passes. Adjust timing/tangents before adding keys. Flatten stationary bookends, not every moving breakdown. Maintain a flat contact segment where the foot is truly stationary; use controlled monotonic motion for contact travel and deliberate fast interpolation through the slash hit. A smooth scalar curve does not guarantee a safe 3D arc.
5. Add at most one purposeful breakdown at a failed corner-cut, then review that interval. Offset a few existing chest/hand/head keys for overlap only when useful; do not scatter automatic delays across every bone.
6. Keep unit scale. Normal skeletal motion uses rotations; Root/Hips carry body translation. A small explicit spine/chest translation may create the specified breath, but never translate every limb independently to fake a jump or scale the mesh to fake squash.
7. Freeze the passed rest rig and weights while authoring. Store editable sparse Actions. Bake evaluated motion at 30 fps **only on the export copy**; remove its constraints only after pose equivalence passes. Dense export samples are allowed and often needed even though dense authoring is discouraged.

### 5b. Clip contract

The default queued job requires exactly these four clips at **30 fps**. Duration is `(last - first)/fps`. Export frame offsets may change (Blender 1–61 often becomes Unity 0–60); duration and poses must not.

| Clip | Frames / duration | Loop | Sparse pose schedule |
|---|---|---|---|
| Idle | 1–61 / 2.0s | Yes | 1 stand, 21 inhale, 33 shift, 48 exhale, 61 same stand |
| Run | 1–31 / 1.0s; allowed .8–1.0s | Yes, by job contract | 1 Idle, 4 contact L, 7 down L, 10 push L, 13 flight, 16 contact R, 20 down R, 24 push R, 27 flight, 31 Idle |
| Jump | 1–37 / 1.2s; allowed 1.0–1.5s | No | 1 Idle, 8 crouch, 11 takeoff, 16 ascent, 21 apex, 26 descent, 31 landing compression, 37 Idle |
| SwordSlash | 1–31 / 1.0s; allowed .8–1.2s | No | 1 Idle, 9 load, 12 commit, 15 cut, 22 follow-through, 26 recoil, 31 Idle |

**Bookends:** author Idle first; copy its exact frame-1 pose onto the first and last frames of the other three. Match near-zero endpoint velocity as well as pose. Relax the right grip by the final frame. Do not start mid-action, pack the poses into consecutive frames, or merely hold two identical stills for the duration.

**Locomotion limitation:** an Idle→run→Idle clip stops at every loop boundary. Keep this behavior for the current four-clip queue; do not describe it as a seamless sustained run. For a separately requested production locomotion set, use start → contact-to-contact cyclic run → stop, with phase-matched contacts and continuous boundary velocity. Do not force that cycle to zero velocity or add/rename clips without an updated request. Shared Idle endpoints alone also do not guarantee safe crossfades from arbitrary mid-action times.

#### Idle

At ease: weight predominantly on one leg, other foot about 3–8 cm forward at human scale, free knee softer. Knees roughly 5–15°; subtle pelvic tilt and counterbalanced torso, head level. Arms hang near ribs, elbows roughly 10–25°, hands at pockets with relaxed inward palms and slight finger curl. Adapt angles to anatomy rather than forcing identical silhouettes.

One visible but restrained breath: chest rises about 1–2 cm at 21, weight moves about 1–2 cm toward the free leg at 33, chest settles a few millimeters below start at 48. Scale distances to height. Compare 1 versus 21 and 33 at playback size; neither a frozen mannequin nor a bouncing squat passes. No per-finger activity or arm splay.

#### Run

Review **side first**: forward body lean roughly 8–15°, shoulders ahead of hips; absorb → hip-extension push-off → genuine flight. Trailing thigh goes behind the hip; recovery knee travels forward then under the body. A 40–70° swing-thigh angle at flight is a starting range, not a demand for chest-high knees. Keep both steps rhythmically balanced; mirror the second step then adapt only necessary asymmetry. Do not confuse total swing angle with axial twist.

Arms oppose legs: left leg forward ↔ right arm forward. Shoulder swing drives the pump, elbow generally bent around 70–100° in the running portion but freer during entry/exit. Hands move from beside the hip to lower ribs, elbows near the body. Do not park both hands at the chest, lock the backswing elbow, raise poles beside the shoulders, or compensate with a wide A-pose.

**Contact space is explicit.** For a traveling preview, the supporting foot stays fixed on the ground while the body advances. For an in-place loop at intended speed `v`, its stance foot travels backward relative to the character; `world_velocity = character_velocity + relative_foot_velocity ≈ 0`. Store a virtual travel curve/speed and contact intervals; test with that motion applied. Locking an in-place foot at a fixed character-space X while gameplay moves the character causes sliding. For the bookended Run, virtual speed ramps from/to zero. Never add travel twice as both gameplay translation and root motion.

Contact near the center of support, absorb through ankle/knee/hip, then roll toward the ball/toes with heel release. Do not force a heel-first walk or a toe strike regardless of the reference. Toe roll must preserve the actual contact point, including boots/heels. Passing knees stay in their own lanes; no valgus crossing or knee snaps at full extension.

#### Jump

Crouch and landing must photograph as squats. At human scale, lower hips about 12–20 cm before push and 12–22 cm on landing; knees bend and arms counter-swing near thighs. Fast extension through takeoff, then an airborne arc with a clear apex and bent knees, slightly asymmetric. Suggested hip rise 20–35 cm; adapt timing/height to the intended style. The airborne trajectory slows toward the apex and accelerates down; avoid equal-speed hovering. Check the feet actually leave and regain the ground.

Hips/Root carry upward travel, not translated shin/foot bones or stretch. Feet may be targeted to pose bent legs. Choose one game-motion owner: a visual in-place jump or a controller/root-driven jump; do not add the full jump height both in the skeleton and the character controller. Return to exact Idle after compression.

#### SwordSlash

Right-hand handle grip, opposing thumb, wrist aligned; left hand counters close to its own side. Use torso weight transfer and about 20–40° coil rather than a 90° body spin. The high-right load has visible space between fist and skull, elbow below the hand. Avoid reaching so far behind the shoulder that recovery must cross the head.

Commit → cut is a fast 2–4-frame convex arc **in front of the chest**. Follow-through reaches left-front waist height. Keep recoil forward while lowering the hand, then return to the right pocket in the last few frames; otherwise spline can cut through the sternum. Evaluate the hilt orientation and a virtual blade segment (about 70 cm only if appropriate to the intended weapon), not just the wrist point. No weapon mesh unless requested/already present. Clavicle motion assists the shoulder, never substitutes for the whole arm swing.

### 5c. Efficient motion review

Review authored extremes and playback first. Then inspect Run passing/backswing and Slash interpolation midpoints; these are common corner-cut intervals. Use evaluated **mesh**, not stick joints alone. Center-inside tests are cheap screening only: a limb's surface can penetrate while its center remains outside.

Fail visible hand/forearm/leg-through-body or opposite-limb penetration, broken contacts, wrong bend planes, and wide-arm clearance cheats. Permit light surface contact (upper arm against ribs, hand brushing a hip) without treating it as automatically acceptable deep overlap. On hollow/nested shells, a raw triangle-pair count is not penetration depth. Diagnose visually; do not run dense half-frame all-mesh BVH scans as the default.

Only a suspicious interval gets denser sampling or a local distance/mesh test. Fix its arc/reach/torso rotation once, then diagnose if still failing. Report unresolved defects; never ship a known penetrating pose because the review budget expired. Sampling and playback cannot prove collision freedom in every possible blend.

## 6. Validate the actual export and runtime result

| Gate | Required evidence |
|---|---|
| Skeleton and skin | Exact names/parents/deform flags; finite invertible matrices; 0 unweighted/invalid/over-four vertices; seam consistency; no remote leaks; passed §4 poses |
| Rest and FBX round trip | Rest and representative deformed positions compared in the same space, with topology/seam correspondence; target max error ≤1e-5 × character height. Explain accepted format tolerances |
| Motion | Correct duration/pose budgets, contact intervals, arcs, flight/crouch/grip, no unexplained axial flips; exact bookends and small endpoint velocity |
| Deformation | Inspect joint silhouettes and absolute edge changes as well as ratios; tiny source edges inflate ratios. Preserve intentional shells |
| Unity avatar | Create From This Model, valid Human avatar, all 51 mappings including fingers and Chest, separate correct T-pose calibration and verified facing direction |
| Unity animation | Evaluate each clip through an Animator/Playable, not only `SampleAnimation` on a Humanoid clip. Verify time advances, bones and skinned geometry change, and root policy is correct |
| Transitions | Test Idle↔each action at intended entry/exit and any supported interruptions. Inspect feet, shoulders, hands and vertical jump blending. Fix state timing/phase as well as pose |
| Appearance | Existing materials/maps/GUIDs preserved; normals, UVs, tangents and skinning inspected front/back/side under Unity lighting |

Create one compact machine report keyed to **FBX hash + importer settings + validator version**; same FBX with changed import settings needs a fresh Unity check. Record clip ranges/durations, key counts, max errors with the worst bone/time, contact drift, versions and limitations. Keep detailed matrices/samples on disk.

Suggested project tolerances: IK/rest-switch target error a few millimeters at human scale; foot-contact drift around 0.2% of height over a stated planted interval with virtual/game travel applied; endpoint pose agreement near floating-point tolerance before retargeting. These are investigation thresholds, not biomechanical laws or substitutes for visible quality.

**Runtime settings:** configure Loop Time for Idle/Run only. Loop Pose distributes a residual start/end pose difference across the clip; test it rather than using it to repair bad source motion. Root Y, XZ and rotation settings determine what stays in pose versus drives the GameObject. For authored in-place motion, explicitly select and verify the intended bake policy; for a controller-driven jump, avoid duplicating skeletal and controller height. [Unity root motion and loop pose](https://docs.unity3d.com/6000.0/Documentation/Manual/RootMotion.html)

**Compression pass:** first validate an uncompressed import, then try Unity `Optimal`/key reduction for shipping. Compare resulting foot/hand paths, contacts, bookends, grip and transitions; tighten errors only where they visibly matter. Measure runtime clip memory/build impact, not just authored key count or FBX size. Export baking and runtime compression are separate from authoring sparsity. Do not delete contact/event/extreme keys blindly. [Unity animation compression](https://docs.unity3d.com/cn/6000.0/ScriptReference/ModelImporterAnimationCompression.html)

## 7. Export and handoff

Follow `Lychee Model GLB - Blender FBX - Unity.md` for material graphs, PNG/data-map handling and export flags. Preserve original custom normals/UVs, export tangents/triangles, `-Z` forward / `Y` up, Copy+Embed, no leaf bones, deform-only plus Root ancestor. Work on an export duplicate with only the intended mesh/rig/empties and the four allowed Actions; no controls, proxies, widgets, unintended Actions or duplicate NLA takes. Verify evaluated FK bake before removing export-copy constraints.

Save `<slug>_rigged.blend`, `<slug>.fbx`, `<slug>_rig_manifest.json`, and `<slug>_rig_validation.json`, plus required textures. The manifest stores fitted rest/inverse-bind matrices, modules, axes/units, hashes and motion ownership. Keep the editable Blender checkpoint recoverable and textures packed or reliably linked. Place deliverables in the requested folder; preserve `.meta` GUIDs and existing corrected material/texture settings.

Import into the user's actual Unity project using its supported CLI/bridge. A valid avatar, four clip names or a successful reimport alone is insufficient: §6 must pass. Ping the asset and return a compact completion report with any limitations. No promise of perfection in arbitrary poses, weapons or runtime blends.

## Reuse prompt

Read this standard and the FBX companion, then use the numeric schema by path. Resume matching passed checkpoints; fit this mesh before heat, finish §4, author the four sparse clips under §5b, and validate the exact export in Unity. Use FK for free arm arcs, IK for contact/directed targets, stable bend planes, reachable local clearance and explicit contact/root-motion space. Preserve the Idle bookends required by this job. Review the five sheets and playback, investigate only failing intervals, retain editable sparse Actions, and bake/compress only after quality passes. Keep logs and matrices on disk and the queue status current.
