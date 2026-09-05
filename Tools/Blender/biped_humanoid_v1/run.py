"""Headless biped_humanoid_v1 rig + Lychee FBX export.

Usage (Blender 4.2+):
  blender --background --python run.py -- --glb MODEL.glb --out OUT_DIR --slug name

Reads Modular Biped Humanoid Rig Standard v1 and the reference JSON.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys
import traceback

import bpy
from mathutils import Vector


SCHEMA_ID = "biped_humanoid_v1"
CORE_PARENTS = {
    "Root": None,
    "Hips": "Root",
    "Spine": "Hips",
    "Chest": "Spine",
    "Neck": "Chest",
    "Head": "Neck",
    "Clavicle.L": "Chest",
    "UpperArm.L": "Clavicle.L",
    "Forearm.L": "UpperArm.L",
    "Hand.L": "Forearm.L",
    "Thumb1.L": "Hand.L",
    "Thumb2.L": "Thumb1.L",
    "Thumb3.L": "Thumb2.L",
    "Index1.L": "Hand.L",
    "Index2.L": "Index1.L",
    "Index3.L": "Index2.L",
    "Middle1.L": "Hand.L",
    "Middle2.L": "Middle1.L",
    "Middle3.L": "Middle2.L",
    "Ring1.L": "Hand.L",
    "Ring2.L": "Ring1.L",
    "Ring3.L": "Ring2.L",
    "Little1.L": "Hand.L",
    "Little2.L": "Little1.L",
    "Little3.L": "Little2.L",
    "Clavicle.R": "Chest",
    "UpperArm.R": "Clavicle.R",
    "Forearm.R": "UpperArm.R",
    "Hand.R": "Forearm.R",
    "Thumb1.R": "Hand.R",
    "Thumb2.R": "Thumb1.R",
    "Thumb3.R": "Thumb2.R",
    "Index1.R": "Hand.R",
    "Index2.R": "Index1.R",
    "Index3.R": "Index2.R",
    "Middle1.R": "Hand.R",
    "Middle2.R": "Middle1.R",
    "Middle3.R": "Middle2.R",
    "Ring1.R": "Hand.R",
    "Ring2.R": "Ring1.R",
    "Ring3.R": "Ring2.R",
    "Little1.R": "Hand.R",
    "Little2.R": "Little1.R",
    "Little3.R": "Little2.R",
    "Thigh.L": "Hips",
    "Shin.L": "Thigh.L",
    "Foot.L": "Shin.L",
    "Toes.L": "Foot.L",
    "Thigh.R": "Hips",
    "Shin.R": "Thigh.R",
    "Foot.R": "Shin.R",
    "Toes.R": "Foot.R",
}
CTRL_PARENTS = {
    "CTRL_Hand.L": "Root",
    "CTRL_ArmPole.L": "Root",
    "CTRL_Foot.L": "Root",
    "CTRL_LegPole.L": "Root",
    "CTRL_Hand.R": "Root",
    "CTRL_ArmPole.R": "Root",
    "CTRL_Foot.R": "Root",
    "CTRL_LegPole.R": "Root",
}


def log(msg):
    print("[biped_humanoid_v1]", msg, flush=True)


def parse_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--glb", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--slug", default="character")
    p.add_argument("--schema", default="")
    p.add_argument("--ik", dest="ik", action="store_true")
    p.add_argument("--no-ik", dest="ik", action="store_false")
    p.set_defaults(ik=True)
    return p.parse_args(argv)


def enable_addons():
    import addon_utils

    for name in ("io_scene_gltf2", "io_scene_fbx"):
        try:
            addon_utils.enable(name, default_set=True)
        except Exception as ex:
            log("addon enable %s: %s" % (name, ex))


def reset_scene():
    enable_addons()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.meshes):
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for block in list(bpy.data.armatures):
        if block.users == 0:
            bpy.data.armatures.remove(block)


def import_glb(path):
    bpy.ops.import_scene.gltf(filepath=path)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        raise RuntimeError("GLB imported no mesh objects: " + path)
    return meshes


def apply_object_transforms(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def world_bbox(meshes):
    mins = Vector((1e9, 1e9, 1e9))
    maxs = Vector((-1e9, -1e9, -1e9))
    for obj in meshes:
        for corner in obj.bound_box:
            w = obj.matrix_world @ Vector(corner)
            mins.x, mins.y, mins.z = min(mins.x, w.x), min(mins.y, w.y), min(mins.z, w.z)
            maxs.x, maxs.y, maxs.z = max(maxs.x, w.x), max(maxs.y, w.y), max(maxs.z, w.z)
    return mins, maxs


def load_schema(path):
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)
    if data.get("schema") != SCHEMA_ID:
        log("warning: schema id is %s, expected %s" % (data.get("schema"), SCHEMA_ID))
    bones = {b["name"]: b for b in data.get("bones", [])}
    return data, bones


def compute_fit(meshes, bones):
    mins, maxs = world_bbox(meshes)
    mesh_h = max(maxs.z - mins.z, 1e-6)
    zs = []
    xs = []
    ys = []
    # Controls/poles are deliberately outside the body and must not bias fitting.
    for name, b in bones.items():
        if name not in CORE_PARENTS or name == "Root":
            continue
        for key in ("head", "tail"):
            v = b.get(key)
            if v:
                xs.append(v[0])
                ys.append(v[1])
                zs.append(v[2])
    ref_min_z, ref_max_z = min(zs), max(zs)
    ref_h = max(ref_max_z - ref_min_z, 1e-6)
    scale = mesh_h / ref_h
    ref_cx = (min(xs) + max(xs)) * 0.5
    ref_cy = (min(ys) + max(ys)) * 0.5
    mesh_cx = (mins.x + maxs.x) * 0.5
    mesh_cy = (mins.y + maxs.y) * 0.5
    offset = Vector((mesh_cx - ref_cx * scale, mesh_cy - ref_cy * scale, mins.z - ref_min_z * scale))
    return {
        "scale": scale,
        "offset": offset,
        "mesh_min": mins,
        "mesh_max": maxs,
        "mesh_height": mesh_h,
        "ref_height": ref_h,
    }


def xform(vec, scale, offset):
    return Vector(vec) * scale + offset


def build_armature(schema_bones, fit, with_ik):
    arm_data = bpy.data.armatures.new("Armature")
    arm_data.display_type = "OCTAHEDRAL"
    arm_obj = bpy.data.objects.new("Armature", arm_data)
    bpy.context.collection.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="EDIT")
    edit = arm_data.edit_bones
    scale, offset = fit["scale"], fit["offset"]

    def add_bone(name, spec, parent_name, deform):
        bone = edit.new(name)
        bone.head = xform(spec.get("head") or [0, 0, 0], scale, offset)
        bone.tail = xform(spec.get("tail") or [0, 0, 0.05], scale, offset)
        if (bone.tail - bone.head).length < 1e-5:
            bone.tail = bone.head + Vector((0, 0, 0.02 * scale))
        bone.use_deform = deform
        bone.use_connect = bool(spec.get("connected"))
        if parent_name and parent_name in edit:
            bone.parent = edit[parent_name]
        rest = spec.get("rest_armature")
        if rest:
            z_axis = Vector((rest[0][2], rest[1][2], rest[2][2]))
            if z_axis.length > 1e-8:
                try:
                    # align_roll aligns local Z, not X.
                    bone.align_roll(z_axis.normalized())
                except Exception:
                    pass
        return bone

    for name, parent in CORE_PARENTS.items():
        spec = schema_bones.get(name) or {}
        add_bone(name, spec, parent, deform=(name != "Root"))

    if with_ik:
        for name, parent in CTRL_PARENTS.items():
            spec = schema_bones.get(name) or {}
            add_bone(name, spec, parent, deform=False)

    bpy.ops.object.mode_set(mode="OBJECT")
    return arm_obj


def add_ik(arm_obj):
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="POSE")
    pose = arm_obj.pose
    chains = [
        ("Forearm.L", "CTRL_Hand.L", "CTRL_ArmPole.L", "Arm_IK_L"),
        ("Forearm.R", "CTRL_Hand.R", "CTRL_ArmPole.R", "Arm_IK_R"),
        ("Shin.L", "CTRL_Foot.L", "CTRL_LegPole.L", "Leg_IK_L"),
        ("Shin.R", "CTRL_Foot.R", "CTRL_LegPole.R", "Leg_IK_R"),
    ]
    for bone_name, target, pole, prop in chains:
        if bone_name not in pose.bones or target not in pose.bones:
            continue
        pb = pose.bones[bone_name]
        con = pb.constraints.new("IK")
        con.name = prop
        con.target = arm_obj
        con.subtarget = target
        con.chain_count = 2
        con.use_stretch = False
        if pole in pose.bones:
            con.pole_target = arm_obj
            con.pole_subtarget = pole
        con.influence = 0.0
        arm_obj[prop] = 0.0
        try:
            fcurve = con.driver_add("influence")
            drv = fcurve.driver
            drv.type = "AVERAGE"
            var = drv.variables.new()
            var.name = "ik"
            var.targets[0].id = arm_obj
            var.targets[0].data_path = '["%s"]' % prop
        except Exception as ex:
            log("IK driver %s: %s" % (prop, ex))
    bpy.ops.object.mode_set(mode="OBJECT")


def heat_bind(meshes, arm_obj):
    for mesh in meshes:
        bpy.ops.object.select_all(action="DESELECT")
        proxy = None
        source = mesh
        nverts = len(mesh.data.vertices)
        if nverts > 40000:
            proxy = mesh.copy()
            proxy.data = mesh.data.copy()
            proxy.name = mesh.name + "_weight_proxy"
            bpy.context.collection.objects.link(proxy)
            bpy.context.view_layer.objects.active = proxy
            proxy.select_set(True)
            dec = proxy.modifiers.new("TripoDecimate", "DECIMATE")
            dec.ratio = max(0.02, min(1.0, 25000.0 / float(nverts)))
            bpy.ops.object.modifier_apply(modifier=dec.name)
            source = proxy
            log("weight proxy for %s: %s -> %s verts" % (mesh.name, nverts, len(proxy.data.vertices)))

        bpy.ops.object.select_all(action="DESELECT")
        source.select_set(True)
        arm_obj.select_set(True)
        bpy.context.view_layer.objects.active = arm_obj
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
        log("heat groups on %s: %s" % (source.name, len(source.vertex_groups)))

        if proxy is not None:
            transfer_weights(proxy, mesh, arm_obj)
            bpy.data.objects.remove(proxy, do_unlink=True)

        prune_influences(mesh, 4, arm_obj)


def transfer_weights(proxy, mesh, arm_obj):
    bpy.ops.object.select_all(action="DESELECT")
    proxy.select_set(True)
    mesh.select_set(True)
    # The active object is the source, selected others are destinations.
    bpy.context.view_layer.objects.active = proxy
    bpy.ops.object.data_transfer(
        data_type="VGROUP_WEIGHTS", use_create=True,
        vert_mapping="POLYINTERP_NEAREST", layers_select_src="ALL",
        layers_select_dst="NAME", mix_mode="REPLACE",
    )
    world = mesh.matrix_world.copy()
    mesh.parent = arm_obj
    mesh.matrix_world = world
    modifiers = [m for m in mesh.modifiers if m.type == "ARMATURE"]
    if len(modifiers) > 1:
        raise RuntimeError("Multiple armature modifiers on " + mesh.name)
    mod = modifiers[0] if modifiers else mesh.modifiers.new("Armature", "ARMATURE")
    mod.object = arm_obj
    mod.use_vertex_groups = True
    mod.use_bone_envelopes = False


def prune_influences(mesh, limit, arm_obj=None):
    vg_index = {g.index: g for g in mesh.vertex_groups}
    if arm_obj is not None:
        deform = {b.name for b in arm_obj.data.bones if b.use_deform}
        vg_index = {i: g for i, g in vg_index.items() if g.name in deform}
    for v in mesh.data.vertices:
        items = []
        for g in v.groups:
            if g.group in vg_index:
                items.append((g.weight, g.group))
        if not items:
            continue
        items.sort(reverse=True)
        keep = items[:limit]
        drop = items[limit:]
        for w, gi in drop:
            if gi in vg_index:
                vg_index[gi].remove([v.index])
        total = sum(w for w, _ in keep)
        if total <= 1e-8:
            continue
        for w, gi in keep:
            if gi in vg_index:
                vg_index[gi].add([v.index], w / total, "REPLACE")


def unpack_and_flatten_materials(meshes, out_dir, slug):
    os.makedirs(out_dir, exist_ok=True)
    saved = []
    backups = []
    img_index = 0
    for mesh in meshes:
        for slot in mesh.material_slots:
            mat = slot.material
            if mat is None or not mat.use_nodes:
                continue
            nt = mat.node_tree
            principled = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
            if principled is None:
                continue
            base_in = principled.inputs.get("Base Color")
            from_node = None
            if base_in and base_in.links:
                from_node = base_in.links[0].from_node
            images = [n for n in nt.nodes if n.type == "TEX_IMAGE" and n.image]
            albedo = None
            if from_node is not None and from_node.type == "TEX_IMAGE" and from_node.image:
                albedo = from_node.image
            elif images:
                albedo = images[0].image
            if albedo is None:
                continue
            img_index += 1
            name = "%s_texture.png" % slug if img_index == 1 else "%s_texture_%d.png" % (slug, img_index)
            dest = os.path.join(out_dir, name)
            try:
                albedo.filepath_raw = dest
                albedo.file_format = "PNG"
                albedo.save()
                saved.append(dest)
            except Exception as ex:
                log("save image failed: " + str(ex))
            tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE" and n.image == albedo), None)
            if tex is None:
                tex = nt.nodes.new("ShaderNodeTexImage")
                tex.image = albedo
            for link in list(base_in.links):
                backups.append((mat.name, "Base Color", link.from_node.name, link.from_socket.name))
                nt.links.remove(link)
            nt.links.new(tex.outputs["Color"], base_in)
    return saved, backups


def has_export_actions():
    return any(not a.name.startswith("WGT") for a in bpy.data.actions)


def export_fbx(meshes, arm_obj, dest):
    bpy.ops.object.select_all(action="DESELECT")
    for m in meshes:
        m.select_set(True)
    arm_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj
    bake = has_export_actions()
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


def validate(meshes, arm_obj, schema_bones):
    report = {"ok": True, "errors": [], "warnings": [], "bones": [], "weights": {}}
    pose_names = set(arm_obj.data.bones.keys())
    for name, parent in CORE_PARENTS.items():
        if name not in pose_names:
            report["errors"].append("missing bone " + name)
            report["ok"] = False
            continue
        bone = arm_obj.data.bones[name]
        got_parent = bone.parent.name if bone.parent else None
        if got_parent != parent:
            report["errors"].append("parent mismatch %s: %s != %s" % (name, got_parent, parent))
            report["ok"] = False
        if (bone.tail_local - bone.head_local).length < 1e-6:
            report["errors"].append("zero-length bone " + name)
            report["ok"] = False
        report["bones"].append({"name": name, "parent": got_parent, "length": (bone.tail_local - bone.head_local).length})

    extra = sorted(n for n in pose_names if n not in CORE_PARENTS and n not in CTRL_PARENTS)
    if extra:
        report["warnings"].append("extra bones: " + ", ".join(extra))

    report["weights"] = validate_weights(meshes, arm_obj)
    for key in ("unweighted", "over_four_influences", "unnormalized", "invalid_weights"):
        if report["weights"][key]:
            report["errors"].append("%d vertices: %s" % (report["weights"][key], key))
    if not report["weights"]["vertices"]:
        report["errors"].append("no skinned vertices")
    for mesh in meshes:
        modifiers = [m for m in mesh.modifiers if m.type == "ARMATURE"]
        if len(modifiers) != 1 or modifiers[0].object != arm_obj or not modifiers[0].use_vertex_groups or not modifiers[0].show_viewport or not modifiers[0].show_render:
            report["errors"].append("invalid armature modifier on " + mesh.name)
    report["ok"] = not report["errors"]
    return report


def validate_weights(meshes, arm_obj):
    """Count actual deform weights across every mesh; utility groups do not skin."""
    counts = dict(vertices=0, unweighted=0, over_four_influences=0,
                  unnormalized=0, invalid_weights=0)
    deform = {b.name for b in arm_obj.data.bones if b.use_deform}
    for mesh in meshes:
        indices = {g.index for g in mesh.vertex_groups if g.name in deform}
        for vertex in mesh.data.vertices:
            counts["vertices"] += 1
            values = [g.weight for g in vertex.groups if g.group in indices]
            if any(not math.isfinite(w) or w < 0 for w in values):
                counts["invalid_weights"] += 1
            weights = [w for w in values if math.isfinite(w) and w > 1e-8]
            if not weights:
                counts["unweighted"] += 1
            elif abs(sum(weights) - 1.0) > 1e-4:
                counts["unnormalized"] += 1
            if len(weights) > 4:
                counts["over_four_influences"] += 1
    return counts


def write_manifest(path, slug, fit, schema, with_ik):
    data = {
        "schema": SCHEMA_ID,
        "character": slug,
        "modules": {
            "core": True,
            "arms": True,
            "legs": True,
            "fingers": True,
            "ik_controls": bool(with_ik),
        },
        "fit": {
            "scale": fit["scale"],
            "offset": list(fit["offset"]),
            "mesh_height": fit["mesh_height"],
            "ref_height": fit["ref_height"],
            "mesh_min": list(fit["mesh_min"]),
            "mesh_max": list(fit["mesh_max"]),
        },
        "reference_schema_version": schema.get("schema"),
        "reference": schema.get("reference"),
        "note": "Rest transforms were fitted by scaling the v1 reference to this mesh AABB, then heat-weighting. Inverse binds are regenerated by Blender on bind. Do not copy elf inverse-bind matrices onto this character.",
    }
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)


def write_result(path, payload):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(payload, f, indent=2)


def write_repair_result(out_dir, slug, report, fbx, blend):
    """Refresh the canonical reports too, so a repair cannot leave stale status."""
    validation = os.path.join(out_dir, slug + "_rig_validation.json")
    result_path = os.path.join(out_dir, slug + "_blender_result.json")
    previous = {}
    if os.path.isfile(result_path):
        try:
            with open(result_path, encoding="utf-8") as stream:
                previous = json.load(stream)
        except (ValueError, OSError):
            pass
    write_result(validation, report)
    write_result(result_path, {
        "ok": report["ok"], "schema": SCHEMA_ID, "fbx": fbx, "blend": blend,
        "validation": validation, "manifest": previous.get("manifest"),
        "textures": previous.get("textures", []),
        "errors": report["errors"], "warnings": report["warnings"],
    })


def default_schema_path():
    here = os.path.dirname(os.path.abspath(__file__))
    candidates = [
        os.path.join(here, "Biped Humanoid Rig v1 - Reference.json"),
        os.path.abspath(os.path.join(here, "..", "..", "..", "Biped Humanoid Rig v1 - Reference.json")),
    ]
    for c in candidates:
        if os.path.isfile(c):
            return c
    return candidates[0]


def main():
    args = parse_args()
    with_ik = args.ik
    slug = args.slug
    out_dir = os.path.abspath(args.out)
    os.makedirs(out_dir, exist_ok=True)
    schema_path = args.schema or default_schema_path()
    result_path = os.path.join(out_dir, slug + "_blender_result.json")
    payload = {"ok": False, "errors": []}
    try:
        if not os.path.isfile(args.glb):
            raise FileNotFoundError(args.glb)
        if not os.path.isfile(schema_path):
            raise FileNotFoundError("schema not found: " + schema_path)

        log("reset scene")
        reset_scene()
        log("import " + args.glb)
        meshes = import_glb(args.glb)
        for m in meshes:
            apply_object_transforms(m)

        schema, bones = load_schema(schema_path)
        fit = compute_fit(meshes, bones)
        log("fit scale=%.4f height=%.4f" % (fit["scale"], fit["mesh_height"]))

        arm = build_armature(bones, fit, with_ik)
        if with_ik:
            add_ik(arm)
        heat_bind(meshes, arm)

        textures, _ = unpack_and_flatten_materials(meshes, out_dir, slug)
        tex_dir = os.path.join(out_dir, "Textures")
        os.makedirs(tex_dir, exist_ok=True)
        for src in textures:
            dst = os.path.join(tex_dir, os.path.basename(src))
            if src != dst and os.path.isfile(src):
                import shutil

                shutil.copy2(src, dst)

        fbx_path = os.path.join(out_dir, slug + ".fbx")
        export_fbx(meshes, arm, fbx_path)

        blend_path = os.path.join(out_dir, slug + "_rigged.blend")
        bpy.ops.wm.save_as_mainfile(filepath=blend_path)

        report = validate(meshes, arm, bones)
        validation_path = os.path.join(out_dir, slug + "_rig_validation.json")
        with open(validation_path, "w", encoding="utf-8") as f:
            json.dump(report, f, indent=2)
        manifest_path = os.path.join(out_dir, slug + "_rig_manifest.json")
        write_manifest(manifest_path, slug, fit, schema, with_ik)

        payload = {
            "ok": report["ok"],
            "fbx": fbx_path,
            "blend": blend_path,
            "manifest": manifest_path,
            "validation": validation_path,
            "textures": textures,
            "errors": report.get("errors", []),
            "warnings": report.get("warnings", []),
            "schema": SCHEMA_ID,
        }
        write_result(result_path, payload)
        if not report["ok"]:
            log("completed with validation issues")
            sys.exit(2)
        log("ok " + fbx_path)
        sys.exit(0)
    except Exception:
        traceback.print_exc()
        payload["errors"] = [traceback.format_exc()]
        write_result(result_path, payload)
        sys.exit(1)


if __name__ == "__main__":
    main()
