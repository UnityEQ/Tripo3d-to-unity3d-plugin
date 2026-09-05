"""Follow-up headless Blender ops on a saved *_rigged.blend.

  blender --background file.blend --python agent.py -- --op inventory
  blender --background file.blend --python agent.py -- --op export --out DIR --slug name
  blender --background file.blend --python agent.py -- --op exec --file extra.py
"""
from __future__ import annotations

import argparse
import json
import os
import runpy
import sys
import traceback

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import run as pipeline


def parse_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--op", required=True, choices=("inventory", "export", "validate", "exec"))
    p.add_argument("--out", default="")
    p.add_argument("--slug", default="character")
    p.add_argument("--file", default="")
    return p.parse_args(argv)


def inventory():
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    data = {
        "meshes": [
            {
                "name": m.name,
                "verts": len(m.data.vertices),
                "polys": len(m.data.polygons),
                "groups": [g.name for g in m.vertex_groups],
                "materials": [slot.material.name if slot.material else None for slot in m.material_slots],
            }
            for m in meshes
        ],
        "armatures": [
            {
                "name": a.name,
                "bones": [b.name for b in a.data.bones],
                "pose_bones": [b.name for b in a.pose.bones],
            }
            for a in arms
        ],
    }
    print(json.dumps(data, indent=2))
    return data


def export_fbx(out_dir, slug):
    os.makedirs(out_dir, exist_ok=True)
    dest = os.path.join(out_dir, slug + ".fbx")
    bpy.ops.object.select_all(action="DESELECT")
    for o in bpy.context.scene.objects:
        if o.type in {"MESH", "ARMATURE", "EMPTY"} and not o.name.startswith("WGT_"):
            o.select_set(True)
    bake = any(not a.name.startswith("WGT") for a in bpy.data.actions)
    bpy.ops.export_scene.fbx(
        filepath=dest,
        use_selection=True,
        object_types={"MESH", "ARMATURE", "EMPTY"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_tspace=True,
        add_leaf_bones=False,
        use_armature_deform_only=True,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=False,
        path_mode="COPY",
        embed_textures=True,
        bake_anim=bake,
        bake_anim_use_all_actions=bake,
        bake_anim_use_nla_strips=bake,
        bake_anim_force_startend_keying=True,
    )
    print(json.dumps({"fbx": dest}))
    return dest


def main():
    args = parse_args()
    try:
        if args.op == "inventory":
            inventory()
        elif args.op == "export":
            if not args.out:
                raise RuntimeError("--out is required for export")
            export_fbx(args.out, args.slug)
        elif args.op == "validate":
            meshes = [o for o in bpy.context.scene.objects if o.type == "MESH" and not o.name.startswith("WGT_")]
            arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
            if len(arms) != 1:
                raise RuntimeError("validate requires exactly one armature")
            report = pipeline.validate(meshes, arms[0], {})
            print(json.dumps(report, indent=2))
            if args.out:
                os.makedirs(args.out, exist_ok=True)
                pipeline.write_result(os.path.join(args.out, args.slug + "_rig_validation.json"), report)
            sys.exit(0 if report["ok"] else 2)
        elif args.op == "exec":
            if not args.file or not os.path.isfile(args.file):
                raise RuntimeError("missing --file")
            runpy.run_path(args.file, run_name="__main__")
        sys.exit(0)
    except Exception:
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
