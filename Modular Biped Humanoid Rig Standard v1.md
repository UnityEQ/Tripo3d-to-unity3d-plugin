# Modular Biped Humanoid Rig Standard v1

Reusable instructions for future bipedal humanoid rigging tasks. Read this file when the user asks to use the standard biped rig, the elf rig standard, or this document. The current user's request controls scope and overrides this reference.

## Required outcome

Build compatible, reusable humanoid rigs with identical core bone names and hierarchy, consistent matrix conventions, fitted internal joints, smooth skin weights, and a skinned export. Use the completed elf rig as the baseline, including fingers. Preserve mesh detail, UVs, textures, and normals unless a requested repair requires a change.

Do not promise a perfect rig or zero deformation in every possible pose. Verify actual placement, weights, representative animation, and export integrity; document remaining limitations.

## Reference files and versioning

- Machine-readable schema and actual reference matrices: `Biped Humanoid Rig v1 - Reference.json` (project root).
- Working reference rig: the character's `*_rigged.blend` (saved beside the export).
- Reference validation: the character's `*_rig_validation.json` / rig notes beside the export.
- Texture and FBX companion: `Lychee Model GLB - Blender FBX - Unity.md` (project root).

Schema ID: `biped_humanoid_v1`. Never silently overwrite the reference matrices with a newly fitted character. Save each character's fitted manifest separately. Changes to core names, parents, coordinate conventions, or reference pose require a new schema version and an explicit migration.

The reference has 52 export bones (Root plus 51 deform bones) and eight Blender-only IK controls. This document creates a reusable standard; it does not retroactively change or rebake the elf rig.

## Efficient execution (same quality gates)

Read this standard and the FBX companion once per task; reread changed sections when either file changes. Parse `Biped Humanoid Rig v1 - Reference.json` in code and pass its path through `--schema`. Keep its matrices out of chat; request only a specific bone or failed comparison when diagnosing a problem. The JSON remains required input, even when its contents are not printed.

Use the existing helpers before creating another pipeline. Their commands and current limits are documented in `Tools/Blender/biped_humanoid_v1/README.md`. A successful first bind or helper validation is not a completed animated Unity rig.

- Save inventory, fitted/bound, animated, and exported checkpoints outside `Assets` until ready for import. Keep per-character landmarks and motion parameters separate from reusable code.
- Record source, schema, script and parameter hashes plus Blender/Unity versions beside each checkpoint. Reuse a stage only when its inputs match and its recorded checks passed. This is an execution convention, not an automatic cache implemented by the current helpers.
- Repair the earliest failing stage. A clip-only change needs that clip's motion checks and a refreshed final export/Unity check; it does not need another heat bind. Changes to mesh, rest skeleton or weights invalidate all dependent animation and export checks. Material changes require appearance/import checks.
- Keep full logs, matrices, mesh arrays and collision pairs on disk. Return a compact status, failed check/count, affected clip/time and report paths. Read detailed output only for the current failure. Display images through image tools, never as base64 text.
- Review clips in playback and a short contact sheet of the **authored extremes**, not dozens of near-identical full-resolution frames. Preserve the complete motion review and numerical checks below, including natural arm posture; clearance alone does not establish animation quality.
- Run final checks against the actual latest exported FBX and Unity Humanoid deformation. Key reports to that artifact's hash, not just its filename. Sampling can detect penetration but cannot prove continuous collision freedom.
- Keep the queued job's status and stage current using the job prompt's lifecycle contract. Claim completion only after all required stages pass. An existing output or an old successful report is insufficient.

## Matrix contract — use transforms, not rotation-range remapping

Use full rest, pose, and inverse-bind matrices. Do not transfer animation by mapping arbitrary Euler minimum/maximum ranges from one character to another.

- Reference armature coordinates: +Z up, +X character forward, +Y character left. `.L` and `.R` mean the character's anatomical sides.
- Blender bone local +Y follows head to tail. Use the reference rest bases to establish roll and handedness. Do not choose unrelated roll values for each new character.
- JSON matrices are serialized as rows; transform points as column vectors. Translation is the last column. Do not confuse serialization order with multiplication convention.
- Use normalized quaternions or matrices for animation. Keep scale at one on animated deform bones unless a deliberately authored effect requires otherwise. IK stretching is off.
- Normalize mesh and armature object transforms on an unbound working copy: rotation zero, scale one, consistent scene units, and a documented origin. Apply any coordinate conversion to mesh and skeleton together. Do not apply transforms blindly to an already skinned or animated asset.
- Keep the elf's reference rest pose as the v1 calibration pose. It is a shallow A-pose, not a literal T-pose. A Unity avatar T-pose is a separate calibration step.

### Per-character data

Keep the core names, hierarchy, units, axis convention, and calibration method fixed. Fit bone translations and lengths to the new anatomy. Store the resulting rest matrices and regenerated inverse binds in that character's manifest.

Do not copy the elf's numerical inverse-bind matrices onto a differently proportioned mesh. Identical full matrices imply identical joint locations and proportions; that would put joints outside many other humanoids. Standardization means a shared transform contract and calibrated local bases, with character-specific rest transforms.

For bone i:

- `B_i`: rest matrix in armature space.
- `L_i = inverse(B_parent) * B_i`: parent-relative rest matrix; for Root use `B_i`.
- `P_i`: evaluated posed matrix in armature space.
- `Q_i = inverse(P_parent) * P_i`: parent-relative posed matrix.
- `D_i = inverse(L_i) * Q_i`: local pose delta under this convention.
- Reconstruct a target local pose as `Q_target = L_target * D_target`.

Transfer calibrated rotation deltas rather than Euler ranges. If local bases differ, derive a rotation calibration C mapping source bone-local axes into target bone-local axes and use `D_target_rotation = C * D_source_rotation * inverse(C)`. Do not blindly copy matrix entries between different bases. Handle root/hip translations separately with an explicit character-scale policy; do not scale joint rotations.

The armature-space skinning transform is `P_i * inverse(B_i)`. The exported mesh-to-bone inverse bind is `inverse(M_armature_world * B_i) * M_mesh_world`. Regenerate it after fitting and verify the rest pose returns the original mesh.

## Exact bone names and parents

The following 52 names and parents are mandatory in the v1 export skeleton. Capitalization, punctuation, suffixes, and spelling are exact. Do not append character names or Blender `.001` suffixes. Armature object and mesh object names may differ between characters.

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

### Naming convention (v1 — do not invent a second scheme)

This is a **Blender-authored, Unity Humanoid-exported** skeleton. Names are PascalCase anatomical tokens plus Blender’s official side suffix. Do not Mixamo-rename (`mixamorig:LeftArm`), do not Unreal-rename (`upperarm_l`, `calf_l`, `ball_l`), do not Rigify-rename (`DEF-upper_arm.L`). Map in the engine; do not retitle the export bones.

**Tokens**

| Kind | Pattern | Examples |
|---|---|---|
| Deform / Root bones | `Name` or `Name.L` / `Name.R` | `Hips`, `UpperArm.L`, `Thumb2.R` |
| Animator IK controls | `CTRL_` + role + `.L`/`.R` | `CTRL_Hand.L`, `CTRL_ArmPole.R` |
| Widget meshes (not exported) | `WGT_` + the control they draw | `WGT_CTRL_Hand.L` |
| Mechanism (if you add any) | `MCH_` + role + `.L`/`.R` | `MCH_Elbow.L` — **do not export, do not skin** |
| Extra anatomy | `EXT_` + role + optional `.L`/`.R` | `EXT_Tail`, `EXT_Ear.L` |
| IK switches (armature **custom properties**, not bones) | `Arm_IK_L`, `Arm_IK_R`, `Leg_IK_L`, `Leg_IK_R` | `_L`/`_R` here because these are RNA keys, not bone names |
| Actions / Unity clips | exact PascalCase, no spaces | `Idle`, `Run`, `Jump`, `SwordSlash` |
| Armature object + datablock | `RIG-<slug>` | `RIG-elf` |
| Skinned mesh object + mesh datablock | `GEO-<slug>` (one mesh) or `GEO-<slug>-<part>` | `GEO-elf`, `GEO-elf-hair` |
| Vertex groups | **identical** to the deform bone that owns them | group `Forearm.L` ↔ bone `Forearm.L` |
| Armature modifier | `Armature`, one only | |
| Files | `<slug>_rigged.blend`, `<slug>.fbx`, `<slug>_texture.png` | |

**Rules**

- ASCII letters, digits, underscore, and a single `.L` / `.R` side suffix. No spaces, no `Bone`, no `Bone.001`, no character name inside the bone (`Elf_Hips` is wrong).
- Side is always the **suffix** `.L` / `.R` (Blender X-Axis Mirror / Flip Names). Never `LeftUpperArm` on the Blender bone, never `upperarm_l`.
- Numbered fingers start at **1** = proximal (knuckle), **2** = intermediate, **3** = distal (tip). There is no joint 0 and no `Pinky` — Unity’s slot is Little, so the bone is `Little1.L`.
- Vertex groups, pose-bone names, and FBX node names are the same string as the Blender bone. Heat bind already names groups from bones; do not rename groups afterward.
- Only `use_deform` bones are skinned and exported (`use_armature_deform_only=True`). `Root`, `CTRL_*`, `MCH_*`, `WGT_*` stay out of weights and out of the FBX deform set.
- Collections, if you create them: `RIG`, `GEO`, `WGT`. Skip `WGT_*` objects on export.

**Unity Humanoid map (exact — this is how the importer is wired)**

Unity slots are `Left`/`Right` prefixes. Our bones stay `.L`/`.R`. Do not rename the Blender bones to the Unity column.

| Unity `HumanBodyBones` | v1 bone |
|---|---|
| Hips, Spine, Chest, Neck, Head | `Hips`, `Spine`, `Chest`, `Neck`, `Head` |
| Left/Right Shoulder | `Clavicle.L` / `Clavicle.R` |
| Left/Right UpperArm | `UpperArm.L` / `UpperArm.R` |
| Left/Right LowerArm | `Forearm.L` / `Forearm.R` |
| Left/Right Hand | `Hand.L` / `Hand.R` |
| Left/Right UpperLeg | `Thigh.L` / `Thigh.R` |
| Left/Right LowerLeg | `Shin.L` / `Shin.R` |
| Left/Right Foot | `Foot.L` / `Foot.R` |
| Left/Right Toes | `Toes.L` / `Toes.R` |
| Left/Right {Thumb,Index,Middle,Ring,Little}{Proximal,Intermediate,Distal} | `{Thumb,Index,Middle,Ring,Little}{1,2,3}.L` / `.R` |

No `UpperChest`, `Jaw`, or eye bones in v1. Do not add them to “match Unity.” Optional `EXT_` bones are not Humanoid-mapped.

**Blender object names (set these; the helper does on first bind)**

After import, rename so the scene is not `Armature` + `Cube` + `Sketchfab_model`:

1. Armature object and `armature` datablock → `RIG-<slug>`.
2. The skinned mesh object and mesh datablock → `GEO-<slug>` (or `GEO-<slug>-<part>` if several). Weight proxies stay `*_weight_proxy` / `*_wproxy` and are deleted after transfer.
3. IK widgets, if you build them → `WGT_CTRL_Hand.L` and the same pattern for the other seven controls.

`<slug>` is the job slug (sanitized filename stem). It does **not** appear inside bone names.

## Modules

Keep fitting, skeleton construction, controls, skinning, validation, and export as separate stages. Each stage reads the same named-bone schema; anatomical landmarks and measurements are character-specific data, not hard-coded elf coordinates.

- Core body: Root, Hips, Spine, Chest, Neck, Head.
- Left/right arms: Clavicle, UpperArm, Forearm, Hand.
- Left/right legs: Thigh, Shin, Foot, Toes.
- Left/right hands: Thumb, Index, Middle, Ring, Little, with joints 1, 2, and 3 for every digit.
- Optional Blender controls: separate non-deforming bones. They do not replace the export skeleton.
- Optional facial, ear, hair, tail, equipment, or additional-digit bones: use an `EXT_` prefix and attach as children without renaming or reparenting core bones. Record extensions in the character manifest.

For short, tall, broad, thin, or differently proportioned humanoids, fit landmarks and regenerate bind data. Do not merely scale the elf's skeleton and reuse its weights. For missing physical digits, preserve the schema's semantic bones as documented unused bones; do not invent visible geometry or assign stray weights just to give them influence.

Digitigrade legs or substantially different anatomy may require additional mechanism bones or a new compatibility profile. Do not force an anatomically incorrect human knee/ankle placement to keep numerical matrices identical. Preserve the core export mapping where it is valid, and explicitly record any incompatibility.

### Optional control names

Use these names whenever the corresponding IK module is enabled:

- `CTRL_Hand.L` (parent `Root`, non-deforming).
- `CTRL_ArmPole.L` (parent `Root`, non-deforming).
- `CTRL_Foot.L` (parent `Root`, non-deforming).
- `CTRL_LegPole.L` (parent `Root`, non-deforming).
- `CTRL_Hand.R` (parent `Root`, non-deforming).
- `CTRL_ArmPole.R` (parent `Root`, non-deforming).
- `CTRL_Foot.R` (parent `Root`, non-deforming).
- `CTRL_LegPole.R` (parent `Root`, non-deforming).

IK switches on the armature object: `Arm_IK_L`, `Arm_IK_R`, `Leg_IK_L`, `Leg_IK_R`. Zero means FK, one means IK. Default to FK. Use a two-bone chain for each arm/leg, disable stretch, and calibrate pole orientation against the fitted rest pose. Validate target tracking and rest-pose switching. Match target and pole transforms before switching an already posed limb; do not promise automatic seamless switching without a matching implementation.

## Execution workflow

### 1. Inventory and back up

Inspect the live scene before making changes. Record mesh topology/counts, UVs, materials, custom normals, shape keys, existing weights, armatures, modifiers, transforms, and animations. Back up the original. Do not replace an existing rig without checking what animation or attachments depend on it.

### 2. Fit anatomical landmarks

Measure both sides and inspect front, side, and top views. Fit hips, spine, chest, neck/head, shoulders, elbows, wrists, knees, ankles, toe pivots, and every finger joint. Use the actual hand geometry rather than a projected silhouette alone.

Keep deforming bone centerlines inside the corresponding mesh volume. Test heads, tails, and intermediate samples with geometric enclosure checks. Use multiple ray directions where scan topology or local normals are unreliable. Root, IK targets, and pole controls may lie outside the mesh because they are controls, not embedded anatomical bones.

Mirror only when the mesh is sufficiently symmetric. Adapt asymmetrical anatomy while preserving names and the transform contract. Check joint pivot locations visually; enclosure alone does not prove anatomical correctness.

### 3. Construct and bind

Build the exact core hierarchy from the schema and apply fitted rest transforms. Validate names and parents before weighting. Name the armature `RIG-<slug>` and the skinned mesh `GEO-<slug>` (see **Naming convention**). Save a per-character manifest containing rest matrices, inverse binds, module settings, source unit/axis conversions, and reference schema version.

Bind only after mesh and armature object transforms are applied (rotation 0, scale 1). Heat on unapplied scale is a common warp source and a common `Bone Heat Weighting: failed to find solution` cause. `use_deform` is on for the 52 export bones only. `CTRL_*` / `WGT_*` / `Root` do not get vertex groups.

**Heat first pass (Blender, this is possible).** Select the mesh, then the armature as active. `bpy.ops.object.parent_set(type="ARMATURE_AUTO")`. That is bone-heat. One Armature modifier: `object` = this armature, `use_vertex_groups=True`, `use_bone_envelopes=False`. Do not stack a second Armature modifier. Do not enable `use_deform_preserve_volume` (dual quaternion): Unity Humanoid plays **linear blend skinning**, so a Blender-only volume flag will not ship.

For dense meshes (>~40k verts) or a heat failure, use a **temporary proxy**, never the render mesh: duplicate, merge-by-distance only true coincident splits, `DECIMATE` toward ~25k, heat on the proxy, then `bpy.ops.object.data_transfer(data_type="VGROUP_WEIGHTS", vert_mapping="POLYINTERP_NEAREST", layers_select_src="ALL", layers_select_dst="NAME", mix_mode="REPLACE")` onto the original. Preserve thin digits on the proxy. Never weld the original UV seams or strip original custom normals as a generic rigging fix. Helpers: `Tools/Blender/biped_humanoid_v1/run.py` (`heat_bind`) and `repair_weights.py`.

**Heat-failure ladder (stop at the first that weights every deform vert).** Do not research other solvers; these are in Blender or this repo.

1. Recalculate normals outside. Confirm scale 1. Retry `ARMATURE_AUTO`.
2. Proxy path above. If heat still fails on the proxy, scale **mesh and armature together** ×10, heat, scale back to 1, apply, regenerate inverse binds.
3. Separate by loose parts, heat each island, join (keep vertex groups).
4. `Tools/Blender/biped_humanoid_v1/proximity_skin.py` — k=4 nearest deform-bone segments. Then still run §4.
5. Remaining unweighted verts only: assign 1.0 from the nearest deform bone (`vertex_groups[name].add(indices, 1.0, "REPLACE")`). Envelope bind (`ARMATURE_ENVELOPE`) is last resort, then overwrite with §4.

A successful heat bind is **not** a finished skin. Automatic weights leak across the crotch, armpits, and fingers and put a hard 50/50 ring at elbows and knees. That ring is what collapses into a spike or a candy-wrapper when the joint bends.

`Tools/Blender/biped_humanoid_v1/run.py` `heat_bind` (and `repair_weights.py` / `proximity_skin.py`) then runs `refine_weights`: opposite-limb and remote isolation, distance-based hinge spread on parent/child pairs (replaces a 1-loop 50/50 ring with a ~3–5 loop band along the bone), elf four-influence prune, normalize. Do not skip that pass. Do not replace it with a whole-mesh Smooth. Dual quaternion / Corrective Smooth still do not ship.

### 4. Refine weights

Unity export budget: **four influences per vertex**, weights finite, sum = 1, no dust weights. Unity discards influences ≲ 0.01 and then the vertex can underrun 1.0 and stretch. This schema has **no twist bones**; do not add `Thigh_Twist` / `ForeArm_Twist`. Candy-wrapper is controlled with spread joint bands plus the short-twist animation rule in §5b, not extra bones. Do not add `CORRECTIVE_SMOOTH` as a shipped deform: Unity will not play it.

Preserve identical weights on coincident vertices that are the same UV-seam surface. Do not equalize intentionally separate overlapping shells (armor on a body, two thighs that merely touch) just because positions coincide.

**Order matters.** Isolate leaks **before** Limit Total, or a leaked opposite-limb weight can occupy one of the four slots. The helper already does 4a and 4c. **4b is still required** — pose the joints and check the mesh; automatic hinge spread is the first band, not a visual sign-off.

#### 4a. Dust, then isolate (Python / Weight Paint)

In Weight Paint or via `bpy.ops.object.vertex_group_*` on the skinned mesh:

1. `vertex_group_clean(group_select_mode="ALL", limit=0.01, keep_single=True)` — drop dust, keep a vert from going empty.
2. Opposite-limb leak (the usual heat failure). For each vertex, find the nearest deform-bone segment (same idea as `proximity_skin.py`). If that bone is `.L`, set every `.R` group on that vertex to 0; if `.R`, zero `.L`. Hips / Spine / Chest / Neck / Head may remain. Do not “fix” a left thigh by smoothing in the right thigh.
3. Remote leak: a hand vert with Spine weight, a toe with Hips, a skull with Clavicle — zero it. Torso verts may blend Spine/Chest/Hips. Shoulder verts may blend Clavicle/UpperArm/Chest. Crotch verts may blend Hips + the **own-side** Thigh only.
4. Fingers: each digit chain (`Thumb*`, `Index*`, `Middle*`, `Ring*`, `Little*` + side) owns its phalanges. Web verts blend only the two adjacent digits and `Hand`. Zero the opposite hand and non-adjacent fingers. Thumb opposition verts belong to `Thumb*` + `Hand`, not Index.

Re-normalize after isolation (`vertex_group_normalize_all(lock_active=False)`). Unweighted count must stay 0.

#### 4b. Joint bands — pose the bone, then paint (this is the anti-warp pass)

Weights that look fine in rest will still spike, crease, or pancake at 90°. **Pose one joint at a time on the live Armature modifier. Do not apply the pose.** If a 90° bend still folds to a plane after a correct band, the joint is in the wrong place — return to §2. Painting cannot hide a pivot that missed the crease.

Lock every vertex group except the two (or three) bones of that joint. Weight Paint: `Ctrl`-click the bone. Use vertex/face mask on the joint loops only. **Do not** `vertex_group_smooth` on the whole mesh. A global topology-average pass worsened the elf's small-edge deformation and was rejected.

Band recipe (elbow, knee, wrist, ankle — two bones):

- 3–5 loops across the crease, not one. A 1-loop 50/50 ring is the candy-wrapper / spike.
- Crease loop(s) ≈ 0.5 / 0.5. One loop toward the parent ≈ 0.75 parent / 0.25 child. One loop toward the child ≈ 0.25 parent / 0.75 child. Outside that, 1.0 the owner.
- `vertex_group_smooth` on **those selected verts only**, `factor=0.2–0.3`, `repeat=1–2`. Compare the 90° pose before keeping it.
- Inner (fold) loops can be slightly more child-weighted so the flesh tucks instead of intersecting. Outer (stretch) loops keep more parent so the silhouette does not go paper-thin.

| Joint | Pose gate (one side, then mirror if the mesh is symmetric) | Bones to unlock | Failed look | Fix in Blender |
|---|---|---|---|---|
| Elbow | `Forearm` ~90° | `UpperArm`, `Forearm` | Spike in the fold, pancake on the outside, upper arm dragging the forearm mesh | Widen to 3–5 loops; less UpperArm on the forearm surface |
| Knee | `Shin` ~90° | `Thigh`, `Shin` | Calf sucks into the thigh, or a hard ring | Same band; keep Thigh off the lower shin |
| Shoulder / armpit | `UpperArm` raise ~80–100°, a little forward | `Clavicle`, `UpperArm`, `Chest` (Spine if the pec moves) | Armpit spike, chest collapsing onto the arm, deltoid left behind | Chest/Spine keep the ribcage; UpperArm owns the arm volume, not the side of the chest; Clavicle takes the shoulder cap |
| Hip / crotch | Both `Thigh` abduct ~45° and a 90° sit on one thigh | `Hips`, own-side `Thigh` | Crotch dives through the pelvis, or tents; opposite leg pulls | Center line = Hips; inner thigh = own Thigh + a little Hips; opposite Thigh = 0 |
| Wrist | `Hand` ~45–70° flexion | `Forearm`, `Hand` | Sleeve-of-flesh twist, fingers shearing off the palm | Spread 3 loops; Hand owns the palm, Forearm stops at the wrist crease |
| Ankle | `Foot` plantar/dorsiflex | `Shin`, `Foot` | Ankle collapse, toes staying with the shin | Foot owns the foot volume; Shin stops at the ankle |
| Fingers | Each `*2` / `*3` ~90° curl, then a fist | that digit’s 1–3 + `Hand` at the knuckle | Neighbor finger collapses; candy ring at a knuckle | Isolate first; 2–3 loops per knuckle |
| Neck / head | `Neck` and `Head` turn + a little down | `Chest`, `Neck`, `Head` | Head leaves a tube of neck, or clavicles drag the jaw | Neck owns the neck cylinder; Head owns the skull; Chest does not own the jaw |

`bpy.ops.object.vertex_group_mirror()` only if the mesh is actually symmetric. Asymmetric anatomy keeps independent `.L` / `.R` painting.

After each joint, return to rest and confirm rest-pose invariance (skinned rest = unbound mesh). Then re-pose the gate. Keep the better of the two by **looking at the posed mesh**, not the weight colors.

#### 4c. Four-influence prune, then normalize

Only after isolation and joint bands:

1. Prefer the elf prune when a hard cutoff spikes a joint: subtract the fifth-largest weight from all influences on that vertex, clamp at 0, renormalize. Guard zero totals.
2. Otherwise `vertex_group_limit_total(group_select_mode="ALL", limit=4)` then `vertex_group_normalize_all`.
3. `vertex_group_clean(limit=0.01, keep_single=True)` again, then normalize again.

Every intended skinned vertex weighted, sums = 1 within 1e-4, ≤4 deform influences, no NaNs. `run.py` `validate_weights` is the numeric check; it does not prove the pose gates.

#### 4d. Weight pose sheet (required before animation)

Photograph, in solid view, rest plus: both elbows 90°, both knees 90°, both arms raised, a crotch split, a fist, head turn. Reject spikes, pancakes, opposite-limb dragging, fingers glued together, or a 1-loop candy ring. If a gate fails, repair §4b for that joint — do not start Idle/Run/Jump/Slash on a collapsing bind. Animation will magnify it.

### 5. Bake rig to mesh — preserve an animated asset

For this reusable humanoid pipeline, "bake rig to mesh" means:

1. Commit the fitted skin weights and mesh-to-skeleton binding.
2. When clips exist, bake evaluated controller/constraint motion onto the export skeleton as visual pose transforms/keyframes at the intended frame rate and clip ranges.
3. Export the mesh, skeleton, skin weights, bind matrices, and requested baked clips.

Do not apply/remove the Armature modifier to freeze the mesh by default: that produces a posed static surface and removes its live skin deformation. Only create that separate static result when explicitly requested. Retain a working Blender rig in all cases.

Perform animation baking on an export duplicate. Preserve the original controls and constraints. Use explicit clip selection and names; do not export every action automatically. Include root motion consistently, evaluate constraints before recording keys, and verify that the baked skeleton reproduces the source pose matrices before removing export-copy constraints. If there is no animation, export a rest-pose skinned mesh without manufacturing a dummy clip.

### 5b. Default gameplay clips (required for Tripo Studio rig jobs)

After the skeleton and bone weights are committed, author these four Actions on the export skeleton before validate/export. Scene rate **30 fps**. Clip duration is `(end_frame - start_frame) / fps`: a 2-second Idle starting at frame 1 ends at frame 61, including the matching loop endpoint. Bake IK/constraints to FK deform bones. Clip names are exact. Do not export `CTRL_*` or `WGT_*`.

**Idle bookends (hard gate).** Author `Idle` first. Its frame-1 stand is the shared rest pose. `Run`, `Jump`, and `SwordSlash` must **start on that Idle stand (frame 1)** and **end on that Idle stand (last frame / loop endpoint)**, matching pose **and** near-zero velocity on Hips, spine, and limbs. Copy the Idle keys; do not start mid-stride, mid-crouch, or mid-slash. The action lives in the **middle** of the clip. Unity crossfades Idle ↔ Run ↔ Jump ↔ Slash; if the first or last pose is not Idle, the blend pops.

Keep rest skeleton and weights unchanged while authoring clips. See **Animation-bake failure to prevent: reversed knee poles** below.

| Action | Loop | Length | Notes |
|---|---|---|---|
| `Idle` | Yes | **60 frames / 2.0s**, frames 1–61 | Shared rest pose. Living stand. One breath + a readable weight shift. In-place. First and last pose **and velocities** match. Arms hang at the pockets, not an A-pose. Reject a mannequin, locked knees, or a packed <2s clip. |
| `Run` | Yes | 24–30 frames (**0.80–1.00s**; default **30 frames / 1.00s**, frames 1–31) | **Idle → two-step jog-run → Idle.** Flight phase, forward lean from the ankles, hip extension on push-off, opposite arm/leg, hip bounce. In-place. Frame 1 and last frame **are** the Idle stand. Reject a packed ~8-frame / ~0.25s cycle, a mid-stride start, a backward “chair-sit” lean, or a high-knee march. |
| `Jump` | No | 30–45 frames (**1.00–1.50s**; default **36 frames / 1.20s**, frames 1–37) | **Idle → crouch → takeoff → hang → land → Idle.** Vertical motion on Hips/Root only. Crouch and land must *photograph* as squats; hang has bent knees off the ground. Frame 1 and last frame **are** the Idle stand. Reject a hover-lift, a mid-crouch start, or a packed ~8-frame hop. |
| `SwordSlash` | No | 24–36 frames (**0.80–1.20s**; default **30 frames / 1.00s**, frames 1–31) | **Idle → slash → Idle.** One-handed slash with **Hand.R**. Wind-up high-right / rear-right with a gap from the skull, cut in an arc **in front of** the chest, follow-through to the left-front, recover to Idle. Frame 1 and last frame **are** the Idle stand (grip starts after frame 1; released by the last frame). Reject a 90° body-spin, a hand through the head, an arm-only swipe with frozen hips, or a mid-slash start/end. |

#### Authoring contract (browser / game-efficient, AAA body mechanics)

Goal: mocap-like weight and silhouette from a **small set of posed extremes**, not a key on every bone every frame. Interpolation carries the inbetweens. Dense 30-fps baking of every controller is a failure mode for this job.

**Keyframe budget (authored poses, not baked samples)**

- Block in **stepped** extremes first, then convert to spline. Do not start in spline and “feel around.”
- Key only bones that change that pose. Do not key the whole armature on every extreme.
- Fingers: 2–3 poses per clip (rest / grip / release), held. No per-frame finger noise.
- Prefer **breakdowns** (passing / hang / mid-slash) over extra inbetweens. If a motion already arcs correctly, stop.
- After spline, add overlap only where it reads: shoulders after chest, forearms after upper arms, hands after forearms, head after neck, toes after foot.
- Final bake may sample 30 fps for export. The **authored** animation must remain sparse. If a clip needs more than about **8–12 unique full-body poses**, it is over-keyed; simplify timing, not the silhouette.
- Sparse does **not** mean a short clip. Spread those poses across the clip’s required frame range. Packing N poses onto N consecutive frames is a failed Action for every clip (Unity Length ≈ N/30 s). Required lengths: Idle **60f / 2.0s**; Run **24–30f / 0.80–1.00s**; Jump **30–45f / 1.00–1.50s**; SwordSlash **24–36f / 0.80–1.20s**.

**Shared body rules (all four clips)**

- Calibration pose is the fitted shallow A-pose rest. Gameplay clips must **leave** that rest: upper arms near the ribs, elbows softly bent, hands close to the hips or on a real path. A T-pose, scarecrow, or wide A-pose is a failed clip even if nothing intersects.
- Keep deforming bone twists short. Use the smallest swing that aims local +Y along the limb. No 160°+ thigh/arm rolls (see knee-pole note).
- Hips lead weight. Shoulders counter-rotate the ribcage. Head follows late and less than the chest.
- Knees and elbows bend in their anatomical plane. Knees aim roughly forward; they do not collapse inward through the opposite thigh or hyperextend into a lock unless the pose is a brief push-off.
- Feet: plant on the ball in Run; Idle can be whole-foot with one heel slightly light. Toes flex only at push-off and land. No ice-skate sliding on an in-place cycle.
- Hands: fingers slightly curled at rest (not splayed, not a tight fist). Thumbs oppose. Wrists stay near neutral; no broken-wrist flaps.
- In-place policy: **Idle** and **Run** have zero net Root/Hips translation in character-forward. Small vertical and lateral hip travel is required for weight. **Jump** may translate Hips/Root on world up only. **SwordSlash** stays planted; a few centimeters of hip yaw/sway is expected.
- **Idle bookends:** copy Idle frame 1 onto frame 1 and the last frame of `Run`, `Jump`, and `SwordSlash`. Same silhouette, same arm/leg attitude, same hip height, near-zero velocity. “Idle-like” is not enough. Reject a clip that starts or ends mid-action.
- Loops: `Idle` loops on itself. `Run` is closed because first = last = Idle stand. `Jump` and `SwordSlash` are one-shots whose first and last frames are that same Idle stand. Match position **and** first derivative on Hips, spine, and limbs or Idle↔clip blends will tick.
- Do not scale animated deform bones. Do not scale Chest/Hips to fake a breath or a squash. IK stretch stays off.

#### `Idle` — 60 frames / 2.0s, loop, in-place

Reference: standing human **at ease**. Not a mannequin, not attention, not a combat guard, not the rest A-pose with a sine wave on the chest.

**Duration is a hard gate.** Frames **1–61**, 2.0s, 4–6 keys spread on that range. A 4-frame Idle is a failed clip.

**Silhouette (must photograph; if five Idle stills look identical, it failed)**
- Asymmetric stand. ~55–65% of weight on one leg (default: **right**). Hip sits over that foot. The free (left) knee is a few degrees softer; that foot stays planted but 3–8 cm forward or out — not a wide stance, not a matching pair of soldier feet.
- Soft knees (about 5–15°). Never locked. Locked knees = mannequin.
- Pelvis a few degrees toward the weighted side. Spine a slight S-curve (not a broomstick). The unweighted shoulder sits a little lower.
- Head level, chin neither tucked nor craned. Face forward.
- **Leave the A-pose.** Upper arms hang from the clavicles and graze the ribcage with a finger-width of clearance. Elbows bent about 10–25°. Hands fall beside the greater trochanter / front pockets, palms in or slightly back. Fingers relaxed-curl. Thumbs rest along the thighs, not stuck out. If the hands float at mid-thigh away from the hips, it is still the rest pose.

**Sparse poses (4–6 keys on the 60-frame range)**

| Pose | Frame | What it is |
|---|---|---|
| Loop stand | 1 and 61 | Weighted stand as above. Same pose **and** velocity. |
| Inhale peak | 21 | Chest and upper spine rise **1–2 cm** (must be visible on a contact sheet). Shoulders lift a few millimeters. Belly expands less than the chest. Head floats up late, 1–2 frames after the chest. |
| Weight-shift extreme | 33 | Pelvis eases 1–2 cm toward the free leg. Free knee softens a little more. Weighted foot stays planted. This is the farthest the weight goes; it returns by frame 61, not inside this pose. |
| Exhale trough | 48 | Chest drops below the frame-1 height by a few millimeters, then the spline carries it back. Shoulders settle. Head follows down late. |
| Optional look | ~12 or ~40 | Head yaw or pitch 2–4°, returning to frame 1. One offset max. |

**Timing**
- The 2.0s clip is **one** visible breath (game-compressed). Inhale a little longer than exhale.
- Offset: hips first, chest 2–4 frames later, shoulders 1–2 after chest, head last.
- Amplitude must read at playblast scale and disappear as “acting.” If Idle looks like a squat, a dance, or a bodybuilder breath, cut the travel in half. If frame 1 and frame 21 are indistinguishable, double the chest rise until 1–2 cm photographs.

**Do not**
- Leave the rest A-pose and bob Chest on a sine wave. That is the usual failed Idle.
- Bounce the whole Root/Hips on a sine wave, or scale Chest/Hips to fake breathing.
- Lock the knees. Mirror-copy left and right. Stand at attention.
- Sway the arms away from the torso to “add life,” or hold a combat guard.
- Animate each finger. A single relaxed hand pose is correct.
- Pack 4 keys onto frames 1–4.

#### `Run` — 24–30 frames (0.80–1.00s), loop, in-place

Reference: athletic **jog-run**. Not a walk, not a military high-knee march, not a skip/gallop, not a sitting-in-a-chair bounce. A true run has a **flight phase**: both feet off the ground between steps. Full cycle = two steps.

**Duration is a hard gate.** Default: **30 frames, 1.00s, frames 1–31** (frame 31 = Idle = frame 1). Faster option: 24 frames, 0.80s, frames 1–25. At 30 fps that is 120–150 steps/min — a jog. Keep both steps even, or 1-frame uneven at most. **Reject** any Run whose Action/Unity Length is under ~0.80s. Eight keys on eight consecutive frames (≈0.25s) is the usual way this fails. **Reject** a Run that starts or ends mid-stride.

**Side-view silhouette (review this first; it is where a bad run is obvious)**
- **Forward lean from the ankles**, 8–15° from vertical, through hip, chest, and head. The ear–hip–ankle line tilts *forward*. In side view the **shoulders stay in front of the hips**. If the chest is behind the hips, the character is sitting/falling backward — failed clip.
- **Hip extension on push-off:** the stance thigh goes *behind* the hip. The trailing leg is a long line (hip → thigh → shin, knee softly near-straight, heel up, toes down). If both thighs stay in front of the hip, it is a skip or a chair-sit, not a run.
- **Swing leg is a forward reach, not a high march.** At flight the recovery knee is in front of the hip, thigh about 40–70° from vertical, foot under the knee. The knee does **not** come up to the chest, and the foot does **not** kick out in front of the center of mass.
- Landing is on the **ball of the foot**, not a heel-first walk strike.
- Stance is longer than Idle, but on the passing/Down pose the knees stay under the hips — no splits.

**Arms**
- Pump **close to the ribs**. Elbows stay near 70–100°.
- Hands travel a short fore-aft arc **beside** the torso: back extreme beside/behind the greater trochanter; front extreme beside the lower ribs / navel. Hands never rise above the lower chest, never park in front of the sternum, and never stick out in a T.
- **Opposition is mandatory on every frame you can photograph:** left leg forward ↔ right arm forward (and the reverse). Hips yaw toward the back (pushing) leg; shoulders yaw the other way.
- Fingers loose or lightly closed.

**Sparse poses (Idle bookends + two-step cycle, spread on the timeline; mirror the second step)**

Use this Williams/game recipe, then stop. Keys go on the frames below — **not** on frames 1–8. Frame 1 and the last frame are the Idle stand, not a contact.

| Pose | Frame (30f / 1.00s) | Frame (24f / 0.80s) | Legs | Arms | Hips / chest |
|---|---|---|---|---|---|
| Idle stand | 1 | 1 | Copy Idle frame 1. Both feet planted. No lean yet | Idle hang | Idle. Near-zero velocity |
| Contact L | 4 | 3 | Step off Idle: L almost long, ball touches; R trailing **behind** the hip, still off ground | R hand beside front ribs; L hand beside/behind hip | Starting to drop; slight yaw; lean begins |
| Down L | 7 | 6 | L knee bent, weight on ball; R knee passing **under** the hip, not a high march | Arms crossing through the middle, still close | **Lowest** vertical; absorb. Lean still forward |
| Push / takeoff L | 10 | 8 | L extends **behind** the hip, heel up, toes down; R knee forward (thigh < ~70° from vertical) | Opening toward next arm extreme | Rising |
| Flight / peak after L | 13 | 10 | **Both feet off.** Compact: R knee forward, L trailing behind. Not a split, not a high-knee | Near opposite extremes, hands at hip/rib height | **Highest** vertical. Shoulders still in front of hips |
| Contact R | 16 | 13 | Mirror of Contact L | swap L/R | same idea |
| Down R | 20 | 16 | Mirror | | |
| Push / takeoff R | 24 | 19 | Mirror | | |
| Flight / peak after R | 27 | 21 | Mirror | | |
| Idle stand (loop) | 31 | 25 | Copy Idle frame 1. Same pose **and** velocity as frame 1. Settle out of the last step; do not freeze mid-stride | Idle hang | Idle |

**Vertical and in-place motion**
- Hips bounce once per step: down on contact/absorb, up on flight. Amplitude is modest (a few centimeters on a human-height character). Same height on both steps.
- The bounce is a spring through the stance ankle/knee/hip, **not** a sit-stand (pelvis tucking under while the chest leans back).
- No net forward Root travel. Any visual “run distance” is pose only.
- Feet that are “planted” must not slide in the ground plane. Air feet travel on short arcs.

**Knees**
- Sagittal bend only. On Down, the stance knee is bent and the knee point stays in front of the toes’ line, not collapsed into valgus through the other thigh.
- On Push, do not hyperextend. A soft near-straight is enough.
- Reject any thigh Y-roll near 180° (see bake note). A correct run thigh rotates on the order of **10–25°**, not 169°.

**Do not (these are the usual failed Runs)**
- Pack the 8 poses into ~8 frames / ~0.25s.
- Lean the torso **back** (chair-sit, falling away from the run). Side view: shoulders behind hips.
- High-knee march or prance: swing thigh vertical, knee at chest, foot kicking in front of the COM.
- Skip / gallop / hitch: one leg always leads, or same-side arm and leg swing together.
- Dead / T-rex arms: both hands parked in front of the chest, no backswing past the hip, no opposition.
- Hold both feet on the ground for the whole clip (that is a walk).
- Flare elbows or run in a wide A-pose “so nothing hits.”
- Swing arms in a full shoulder circle.
- Keep the spine stiff while only the legs cycle.
- Ice-skate the planted foot, or let the trailing thigh never go behind the hip.
- Start or end mid-stride. Frame 1 and the last frame are the Idle stand.

#### `Jump` — 30–45 frames (1.00–1.50s), not a loop

Reference: standing vertical jump that **starts on Idle and returns to Idle**. Squash-and-stretch is **pose**, not mesh scale. Hang time at the apex is what makes it read as weight. The usual failed Jump is a hover-lift: tiny crouch, straight legs in the air, land that looks like Idle — or a clip that begins already crouched.

**Duration is a hard gate.** Default: **36 frames, 1.20s, frames 1–37**. Short option 30f / 1.00s (frames 1–31); long option 45f / 1.50s. Reject any Jump under ~1.00s, or 7 poses packed onto 7 frames.

**Side-view silhouette (review this; front view hides a shallow crouch)**
- **Crouch must photograph as a squat.** Hips drop **12–20 cm** on a human-height character. Knees ~50–80°. Spine rounds slightly. Weight on the balls of the feet, heels light. Arms go **back and down** close to the thighs, not out. If crouch looks like Idle with a nod, it failed.
- **Takeoff is fast.** Legs extend, heels up, toes down. Last ground frame still explains the push. Arms swing forward/up only as high as the lower ribs / chest — not a Y-pose.
- **Hang is off the ground with bent knees.** Hips **20–35 cm** above Idle. Feet clearly off the floor, pointed down or slightly back. Knees soft (about 20–40°), one knee slightly leading. If hang looks like standing on a box with locked legs, it failed.
- **Land is as deep as crouch, or deeper.** Hips drop **12–22 cm**. Knees and ankles absorb, spine rounds, arms check forward-down near the thighs. If land looks like Idle, it failed.
- **Frame 1 and the last frame are the Idle stand** (copy Idle frame 1). Not idle-like. Not a second crouch. Not a ready squat.

**Sparse poses (8 keys, spread on the timeline)**

| Pose | Frame (36f / 1.20s) | Frame (30f / 1.00s) | What to hit |
|---|---|---|---|
| Idle stand | 1 | 1 | Copy Idle frame 1. Near-zero velocity. |
| Crouch extreme | 8 | 7 | Deepest squat. Slow into this from Idle. |
| Takeoff | 11 | 10 | Fast. Last toe on the ground. |
| Ascent | 16 | 14 | Body opening. Feet off. One knee leads. |
| Hang peak | 21 | 18 | Slowest spacing. Highest hips. Hold the apex (an extra key 2–3 frames later is allowed). |
| Descent | 26 | 22 | Faster. Knees prepare forward/down. Chin slightly down. |
| Land compress | 31 | 26 | Fast hit. Deepest land. Balls or whole feet, 0–1 frame stagger. |
| Idle stand | 37 | 31 | Copy Idle frame 1. Small overshoot of Idle hip height is allowed *before* this last key, not on it. |

**Roots**
- Only Hips/Root move vertically. No forward hop unless the mesh already needs a tiny balance shift; even then, net XZ returns.
- Takeoff and land happen when the feet actually leave / meet the ground plane. Do not “float” the whole clip. Do not translate Root to fake hang height while the legs stay Idle-length.

**Arms and legs**
- Arms are counterweights, not wings. If they leave the silhouette, bring them back.
- Offset the legs a little (one knee leads) so the jump is not a perfectly symmetric frog.
- On land, knees aim forward over the feet. They must not pass through each other or the pelvis.

**Do not**
- Skip anticipation or skip compression on landing.
- Use even spacing the whole way (that reads as a hover-lift).
- Straight-leg hang, or a crouch/land so shallow it does not show up on a contact sheet.
- Recover into a T-pose, or flare the arms to “make clearance.”
- Start already crouched, or end still compressed. Frame 1 and the last frame are Idle.
- Pack 7–9 poses onto 7–9 frames.

#### `SwordSlash` — 24–36 frames (0.80–1.20s), not a loop

Reference: one committed right-handed cut with a short arming sword / long knife. Readable from the **front** (arc across the chest) and the **side** (plane in front of the sternum). The hit is a few frames; the audience reads the **anticipation and follow-through**. The usual failed Slash is either a hand parked on the ear, a 90° body-spin that leaves the character in profile, or an arm-only swipe with frozen hips.

**Duration is a hard gate.** Default: **30 frames, 1.00s, frames 1–31**. Range 24–36f (0.80–1.20s). Reject a packed ~6-frame swipe.

**Grip**
- `Hand.R` and finger joints 1–3 close into a handle grip: thumb opposing the fingers, index slightly less clenched than the lower three if useful, wrist aligned with the forearm. Hold that grip from wind-up through follow-through. `Hand.L` stays open-relaxed or a loose counter-fist, never a copy of the right grip.

**Path (mandatory)**
- The cutting plane is **in front of the sternum**, not through the belly. Treat Hand.R as the hilt: a ~70 cm blade continues along the hand heading, so the hand path must keep that blade off the head and torso.
- **Wind-up:** Hand.R is at about **ear height**, but a **head-width to the character’s right** and slightly rear of the shoulder. From the front you see sky between the fist and the skull. The hand does **not** occupy the ear, horn, or back of the head. Elbow.R is below the hand, outside the ribcage, **not** flared to a T. Weight ~65–75% on the right-back foot.
- **Hips/Chest coil, they do not spin 90°.** From above, the chest yaws so the right shoulder goes back on the load (~20–40°) and comes forward on the cut, passing through facing-front. The contact pose is **not** a full profile. The arm does not do all of the travel; the torso does not do all of it either.
- **Slash:** Hand.R travels a convex arc across the **front** of the chest (high-right → center-front at sternum height → low/mid-left-front). Fast 2–4 frames, long travel.
- **Follow-through:** Hand.R finishes left-front at about hip-to-waist height, still off the body. Weight can pass onto the left foot. Spine and head overshoot a little. Then recover to the Idle stand.
- Left arm counters on the opposite side of the torso, close, never through the stomach and never stuck out.

**Sparse poses (6–7 keys, spread on the timeline)**

| Pose | Frame (30f / 1.00s) | Frame (24f / 0.80s) | What to hit |
|---|---|---|---|
| Idle stand | 1 | 1 | Copy Idle frame 1. Hands still Idle (relaxed curl, not a grip yet). Near-zero velocity. |
| Load / wind-up | 9 | 8 | Slow. Grip closes. Weight right-back. Chest coiled ~20–40°. Hand.R high-right with a gap from the skull. Head may track the future target. |
| Commit | 12 | 10 | Chest starts to unwind. Hand.R still back-right; elbow has a path **forward of** the ribs. |
| Cut / “contact” | 15 | 13 | Fastest. Hand.R crosses in front of the sternum. Hips more toward the cut, **not** parked in profile. |
| Follow-through | 22 | 18 | Hand.R and chest arrive left-front. Weight can pass to the left foot. |
| Recoil | 26 | 21 | Uncoil begins. Right hand lowers. Grip may still be closed. |
| Idle stand | 31 | 25 | Copy Idle frame 1. Fingers released back to Idle curl. Same pose **and** velocity as frame 1. |

Ease into the wind-up and out of the follow-through. Do **not** ease the cut itself (linear interpolation across the cut keys is correct).

**Do not**
- Put Hand.R on the ear, through the head, or behind the skull. “High-right” means high and to the right, with a gap.
- Spin the whole body to profile and hold the arm out. That is not a cut in front of the chest.
- Animate a straight line from high-right through the abdomen to low-left.
- Keep Chest/Hips facing camera the whole time and swing only the arm.
- Abduct the right shoulder to “make clearance.” Move the arc forward and add torso rotation instead.
- Invent a sword mesh.
- Start already in a grip/wind-up, or end still in follow-through. Frame 1 and the last frame are the Idle stand.
- Pack 6 keys onto frames 1–6.

#### Overlap cheat-sheet (apply after extremes read)

- Hips lead. Chest 1–3 frames later. Head later still and smaller.
- UpperArm leads Forearm leads Hand. On the slash cut, the hand can catch up fast; on Idle/Run, the hand lags.
- Clavicles only help the last bit of shoulder motion; do not animate them like extra arms.
- On Run flight and Jump hang, let the trailing shin/foot drag a frame behind the knee.
- On Jump land, hips arrive a frame before the chest; on Slash follow-through, the hand can overshoot the chest by a frame.

### 5c. No self-intersection in clips

Animated appendages must stay outside this character's own mesh. Do not accept a clip in which an arm, elbow, hand, weapon path, leg, knee, or foot passes through the torso, pelvis, head, or the opposite limb.

Do **not** solve that by sticking the arms out. A T-pose, scarecrow, or wide A-pose Idle/Run/Jump is a failure even if nothing intersects. Keep a normal character silhouette: upper arms near the ribs, elbows slightly bent, hands close to the hips or on a real run/slash path. A few centimeters of clearance is enough.

- Evaluate the **deformed mesh**, not only bone dots. Scrub every frame in solid/material view.
- Light contact (a hand brushing a hip on Idle, feet on the ground) is allowed. Volume penetration is not.
- **`SwordSlash` failure to prevent:** a straight cut that drives `UpperArm.R` / `Forearm.R` / `Hand.R` through the belly or chest, or a wind-up that puts Hand.R through the skull/ear. Keep the elbow outside the ribcage **without** flaring it wide. Rotate `Chest` and `Hips` with the swing so the arm can stay in front on a compact arc (~20–40° yaw, not a 90° spin). Place the slash plane in front of the body; never through it. The left arm counterbalances outside the torso, not through it, and not stuck out to the side.
- **`Run` / `Jump`:** knees and feet must not enter the opposite thigh or the pelvis; arms must not clip the torso on the pass, and must not be held away from the body to avoid that pass. Clearing Run by flaring the arms, or by turning it into a high-knee march / chair-sit skip, is still a failed clip — fix the path using the §5b Run recipe. Clearing Jump by skipping the crouch/land or hanging with locked legs is also a failed clip — fix it using the §5b Jump recipe.
- If a pose only works by clipping, change the arc, timing, or torso rotation — move the path a little **forward**, do not abduct the shoulders. Do not ship the penetrating pose.

Report any remaining intersections in the validation notes.

### 6. Validate

Run the following checks before calling the rig complete:

- Schema: exact core names and parents; no duplicate suffixes; expected deform flags.
- Transforms: finite/invertible rest and bind matrices, consistent handedness, no accidental negative/nonuniform scale or sheared bone bases.
- Rest-pose invariance: skinned rest positions match the unbound mesh.
- Placement: all sampled deform centerline points inside their intended body regions; no zero-length bones.
- Skinning: no unweighted vertices, normalized sums, four-influence budget, valid bone references, matching seam weights, no opposite-limb or remote leak. Pose gates in §4d hold (90° elbow/knee, arm raise, crotch split, fist) without spikes or pancakes. Heat-only binds that skip §4 are not done.
- Geometry: inspect physical connectivity and intended separate parts without changing topology solely to satisfy a count.
- Poses: elbows and knees at representative bends; shoulders raised/lowered; hip stride/crouch; torso twist; head turn; wrist bends; individual digits and combined finger curls; thumbs; ankle/toe bends.
- Clip self-intersection: no limb-through-torso or limb-through-limb on Idle, Run, Jump, or SwordSlash. Scrub SwordSlash in particular for the right arm entering the chest or belly. Also reject T-pose / scarecrow / wide A-pose clearance cheats; arms must look like a normal character.
- Clip body mechanics: Idle is 2.0s, at ease, with a photographed breath and weight shift (not an A-pose mannequin); Run is 0.80–1.00s with absorb + hip-extension push-off + flight, forward lean, and opposite arm/leg close to the torso; Jump is 1.00–1.50s with a photographed crouch, bent-knee hang, and land squash; Slash is 0.80–1.20s with a slow load (hand off the skull), a fast front-of-chest cut, and no 90° body-spin. **Idle bookends:** Run, Jump, and SwordSlash frame 1 and last frame match Idle frame 1 (pose and near-zero velocity). Authored pose counts stay in the 5b budget and are spread across each clip’s required length. See §5b side-view gates.
- IK: no stretch, targets track, pole directions are stable, and rest switching does not visibly jump.
- Inspect front, back, side, and close hand views. Check for spikes, tears, collapsed joints, unintended pulls, and unacceptable intersections.
- Measure local edge deformation as a diagnostic, including absolute changes and edge lengths. Do not treat all expected joint-surface stretching as a bug or dismiss a visible defect because a percentile is low.
- Export round trip: reimport into a temporary scene and compare skeleton, groups, rest mesh, a representative pose, and baked clip samples if present.
- Test retargeted animation in Unity when access and task scope permit. Blender validation alone is not Unity avatar validation.

Suggested numerical gates, scaled to character height H: maximum normalized weight-sum error 1e-5; rest/posed round-trip position error no more than 1e-5 * H. Treat failing gates as investigation triggers, and explain any accepted exceptions. Numerical checks supplement visual inspection; they do not prove zero distortion in arbitrary animation.

If tight bends remain unacceptable, move the joint (§2) or rebuild the joint band (§4b). Do not enable Armature Preserve Volume, do not add `CORRECTIVE_SMOOTH`, and do not invent twist bones outside the 52-bone schema to hide a bind that Unity cannot play.

### 7. Export and handoff

Use the companion FBX/texture workflow. Export only the intended character and skeleton; exclude proxies and controller widgets. Preserve smooth/custom normals and UVs. Include tangents; use no added leaf bones, `use_armature_deform_only=True`, and the established `-Z` forward / `Y` up FBX conversion. Keep the skinning modifier active for a skinned export.

Save:

- `<character>_rigged.blend`, with required textures packed or reliably linked.
- `<character>.fbx` with the 52-bone core, declared extensions, and the four gameplay clips (`Idle`, `Run`, `Jump`, `SwordSlash`) when this is a Tripo Studio rig job.
- `<character>_rig_manifest.json` with fitted matrices, schema version, and modules.
- A concise validation report, limitations, controls, and export destination.

Preserve Unity `.meta` GUIDs when replacing project files. Verify Humanoid mapping, every finger assignment, and avatar calibration; identical bone names alone do not guarantee correct retargeting. Preserve corrected normal-map and non-color texture import settings. Do not apply unrelated changes to other project assets.

## Reuse prompt

"Read the Modular Biped Humanoid Rig Standard v1 and its FBX companion; parse the reference JSON in code without dumping its matrices into chat. Rig this character using biped_humanoid_v1: identical core names and hierarchy, calibrated matrix transforms, modular anatomy fitting, full fingers, verified skin weights, then bind with Blender heat as a first pass and finish weights per §4 (isolate opposite-limb leak, 3–5-loop joint bands posed at 90°, four influences, no twist bones / Preserve Volume / Corrective Smooth), then author Idle first and Run, Jump, and SwordSlash from the sparse pose recipes so each of those three **starts and ends on the Idle stand** (not mid-stride / mid-crouch / mid-slash; Unity blends Idle ↔ every clip), not per-frame noise; spread keys across each clip’s required length — Idle 60f/2.0s, Run 24–30f/0.80–1.00s, Jump 30–45f/1.00–1.50s, Slash 24–36f/0.80–1.20s — never pack N poses onto N frames, passing the §5b silhouette gates (Idle at ease, Run forward-lean jog, Jump crouch/hang/land, Slash front-of-chest cut off the skull), with no limb-through-body mesh penetration and no T-pose/scarecrow clearance cheats, and a baked skinned export with those clips. Preserve the working Blender rig and report validation results."

## Animation-bake failure to prevent: reversed knee poles

The initial elf animation generator moved the knee poles forward relative to the near-straight reference legs, then blindly baked the IK solver matrices. Joint positions looked plausible, but both thighs acquired an approximately 169-degree rotation, twisting the skinned legs and lower torso. Successful export round trips reproduced that defect and therefore did not prove animation quality.

When changing a pole or bend plane, inspect axial bone orientation as well as joint endpoints. For the corrected bake, each leg segment retains its inherited reference orientation and receives the shortest swing that aligns its local +Y with the solved segment direction; do not inherit an unintended solver half-turn. Keep intended authored twist separate. This is a bake correction, not an assertion that the reference interactive IK controls automatically enforce it.

Validate actual deformed geometry in neutral animation poses, scan rotations across the complete clips, and reject unexplained near-half-turn limb rotations. Preserve the rest skeleton and weights when animation transforms are the cause.

**Numeric smell tests (not a substitute for a playblast)**
- Idle / recovered Jump / recovered Slash: thigh local swing from rest typically stays small (on the order of the corrected elf Idle, ~15°), never ~170°.
- Run: thighs and upper arms show short opposing swings, not a once-per-cycle flip. Trailing-thigh extension behind the hip is a pose check (§5b), not a 180° roll.
- If a limb’s twist plot jumps through 180°, undo the bake for that chain and re-solve with the short-arc rule before shipping.

## Clip review (keep this compact)

Review in playback and a short contact sheet of the **authored extremes**, not dozens of near-identical full-resolution frames. Include a **side view** of Run and Jump (front hides chair-sit and shallow crouch) and **front + side** of Slash (front shows the chest arc; side shows the plane is in front of the sternum). For each clip confirm:

1. Silhouette is a person, not a scarecrow / A-pose leftover.
2. Weight: Idle 2.0s breath + weight shift (frame 1 vs 21 must differ); Run absorb + push-off behind the hip + flight, 0.80–1.00s, forward lean; Jump photographed crouch, bent-knee hang, land squash; Slash slow load with a gap from the skull and a fast front-of-chest cut (not a 90° spin).
3. Hands, knees, and elbows do the jobs in §5b.
4. **Idle bookends:** Run, Jump, and SwordSlash frame 1 and last frame match Idle frame 1 (pose and velocity). Idle loops on itself. Unity/Action lengths match §5b: Idle ~2.0s, Run ~0.80–1.00s, Jump ~1.00–1.50s, Slash ~0.80–1.20s — not a packed ~0.25s clip.
5. No mesh penetration and no reversed-pole twist.
6. Key count on deform bones stays in the budget; those keys are spread across the required frame range. Export bake may be denser, authored poses must not.

A clip that only “clears” because the arms are posed out, or that only “loops” because two identical stills were copied, is not done.
