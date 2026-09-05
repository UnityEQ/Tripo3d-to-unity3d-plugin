"""Re-skin a saved *_rigged.blend after a failed proxy weight transfer."""
from __future__ import annotations

import argparse
import json
import os
import sys
import traceback

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import run as pipeline


def parse_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--out", required=True)
    p.add_argument("--slug", default="character")
    return p.parse_args(argv)


def log(msg):
    print("[repair_weights]", msg, flush=True)


def count_unweighted(mesh, arm):
    stats = pipeline.validate_weights([mesh], arm)
    return stats["unweighted"], stats["vertices"]


def heat_original(mesh, arm):
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    log("ARMATURE_AUTO on original (%s verts)" % len(mesh.data.vertices))
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")


def heat_proxy_and_transfer(mesh, arm):
    nverts = len(mesh.data.vertices)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    proxy = mesh.copy()
    proxy.data = mesh.data.copy()
    proxy.name = mesh.name + "_wproxy"
    bpy.context.collection.objects.link(proxy)
    bpy.ops.object.select_all(action="DESELECT")
    proxy.select_set(True)
    bpy.context.view_layer.objects.active = proxy
    dec = proxy.modifiers.new("Decimate", "DECIMATE")
    dec.ratio = max(0.02, min(1.0, 30000.0 / float(max(nverts, 1))))
    bpy.ops.object.modifier_apply(modifier=dec.name)
    log("proxy verts %s" % len(proxy.data.vertices))

    bpy.ops.object.select_all(action="DESELECT")
    proxy.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    log("proxy groups %s" % len(proxy.vertex_groups))

    pipeline.transfer_weights(proxy, mesh, arm)
    unweighted, total = count_unweighted(mesh, arm)
    log("after transfer unweighted %s / %s" % (unweighted, total))
    bpy.data.objects.remove(proxy, do_unlink=True)
    return unweighted, total


def main():
    args = parse_args()
    try:
        meshes = [o for o in bpy.context.scene.objects if o.type == "MESH" and "_wproxy" not in o.name]
        arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
        if not meshes or not arms:
            raise RuntimeError("need mesh + armature")
        arm = arms[0]
        for mesh in meshes:
            unweighted, total = heat_proxy_and_transfer(mesh, arm)
            if unweighted > total * 0.05:
                log("transfer weak, heat original")
                heat_original(mesh, arm)
                unweighted, total = count_unweighted(mesh, arm)
                log("after original heat unweighted %s / %s" % (unweighted, total))
            pipeline.prune_influences(mesh, 4, arm)
            unweighted, total = count_unweighted(mesh, arm)
            log("final unweighted %s / %s" % (unweighted, total))

        os.makedirs(args.out, exist_ok=True)
        fbx = os.path.join(args.out, args.slug + ".fbx")
        pipeline.export_fbx(meshes, arm, fbx)
        blend = os.path.join(args.out, args.slug + "_rigged.blend")
        bpy.ops.wm.save_as_mainfile(filepath=blend)
        report = pipeline.validate(meshes, arm, {})
        report.update(report["weights"])
        report.update(fbx=fbx, blend=blend)
        pipeline.write_repair_result(args.out, args.slug, report, fbx, blend)
        with open(os.path.join(args.out, args.slug + "_weight_repair.json"), "w", encoding="utf-8") as f:
            json.dump(report, f, indent=2)
        log("wrote " + fbx)
        sys.exit(0 if report["ok"] else 2)
    except Exception:
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
