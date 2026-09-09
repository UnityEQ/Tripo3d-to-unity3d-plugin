# Biped helper commands and limits

Run from the project root. Replace the quoted placeholders with the job's paths. `blender.exe` means the installed Blender executable; it need not be on PATH.

## Legacy proportional bind (not the fitted production path)

```text
blender.exe --background --factory-startup --python Tools/Blender/biped_humanoid_v1/run.py -- --glb "INPUT.glb" --out "WORK_DIRECTORY" --slug "CHARACTER" --schema "Biped Humanoid Rig v1 - Reference.json"
```

For a production job, import and fit landmarks first, then call the existing helper functions on that fitted skeleton as specified in the standard. The command above has no landmark-fit stage and is retained for legacy diagnostics; do not spend an automatic bind attempt on known misplaced joints.

This resets the headless scene, imports the GLB, **proportionally scales the elf skeleton** to mesh height, then heat-binds and `refine_weights` (opposite-limb / finger isolation, hinge-band spread, four-influence elf prune). That proportional bind is an import + naming seed, **not** the fitted character. Heat often fails here because elbows/knees missed this mesh. Fit landmarks on this mesh (§2 of the standard), then call `heat_bind` / `refine_weights` again. Do not write a replacement pipeline because the seed heat-failed.

It does **not** author the four gameplay clips or finish posed joint-band painting (§4b). Use `work/TripoRigJobs/<id>/` (persistent, outside Unity Temp) or another non-Unity workspace — never the Unity Editor `Temp/` folder. Review material graphs before accepting the helper's flattening behavior.

## Inspect or validate a saved checkpoint

```text
blender.exe --background "CHECKPOINT.blend" --python Tools/Blender/biped_humanoid_v1/agent.py -- --op inventory
blender.exe --background "CHECKPOINT.blend" --python Tools/Blender/biped_humanoid_v1/agent.py -- --op validate --out "REPORT_DIRECTORY" --slug "CHARACTER"
```

Inventory reports mesh/group/material and bone names. Supplement it with UVs, normals, transforms, shape keys, modifiers and Actions/NLA required by the standard. Validation currently checks core hierarchy, nonzero bone lengths, weight coverage/normalization/influence budget and armature modifiers. It does not establish anatomical enclosure, seam consistency, inverse-bind correctness, motion quality, intersections, loop correctness or Unity Humanoid validity. Keep those required checks separate. The validation command overwrites `<slug>_rig_validation.json`; use a separate report directory to preserve a fuller report.

## Execute a focused repair or export

```text
blender.exe --background "CHECKPOINT.blend" --python Tools/Blender/biped_humanoid_v1/agent.py -- --op exec --file "REPAIR.py"
blender.exe --background "EXPORT_COPY.blend" --python Tools/Blender/biped_humanoid_v1/agent.py -- --op export --out "OUTPUT_DIRECTORY" --slug "CHARACTER"
```

The repair script must save its intended checkpoint explicitly. Export currently selects scene meshes/armatures/empties except `WGT_*`, enables all Actions and NLA when animation exists, and does not enforce the four-clip allowlist. Prepare an isolated export copy containing only the intended character and requested baked Actions; remove duplicate NLA exports from that copy. Inspect bake sampling/simplification settings and validate the resulting clip names, durations and poses. Do not use an unprepared working scene as the export copy.

## Efficient operation

Redirect full process output to a per-stage log, check the exit code, and read a compact report or the relevant error excerpt. Reuse imported Python functions for a bounded repair when appropriate instead of copying entire scripts into a new job. Landmarks and clip targets are the per-character files; do not reimplement IK, heat, or a per-job collision dumper. There is currently no automatic checkpoint cache or complete animation/Unity validator in these helpers; follow the standard's dependency and final-validation rules.

Regression command (Blender required):

```text
blender.exe --background --factory-startup --python Tools/Blender/biped_humanoid_v1/tests/regression.py
```

This tests helper behavior. Passing it is not per-character validation.
