"""Proximity skinning fallback when Blender bone-heat fails."""
from __future__ import annotations

import argparse
import json
import os
import sys
import traceback
from collections import defaultdict

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import run as pipeline


DEFORM_SKIP = {"Root"}
CTRL_PREFIX = "CTRL_"


def parse_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--out", required=True)
    p.add_argument("--slug", default="character")
    return p.parse_args(argv)


def log(msg):
    print("[proximity_skin]", msg, flush=True)


def bone_segments(arm):
    mw = arm.matrix_world
    segs = []
    names = []
    for bone in arm.data.bones:
        if not bone.use_deform or bone.name in DEFORM_SKIP or bone.name.startswith(CTRL_PREFIX):
            continue
        head = mw @ bone.head_local
        tail = mw @ bone.tail_local
        names.append(bone.name)
        segs.append((head.x, head.y, head.z, tail.x, tail.y, tail.z))
    return names, np.array(segs, dtype=np.float64)


def mesh_world_verts(mesh):
    n = len(mesh.data.vertices)
    co = np.empty(n * 3, dtype=np.float64)
    mesh.data.vertices.foreach_get("co", co)
    co = co.reshape(n, 3)
    m = np.array(mesh.matrix_world, dtype=np.float64)
    homo = np.ones((n, 4), dtype=np.float64)
    homo[:, :3] = co
    world = homo.dot(m.T)
    return world[:, :3]


def dist_points_to_segments(pts, segs):
    a = segs[:, 0:3]
    b = segs[:, 3:6]
    ab = b - a
    ab2 = np.sum(ab * ab, axis=1)
    ab2 = np.maximum(ab2, 1e-12)
    ap = pts[:, None, :] - a[None, :, :]
    t = np.sum(ap * ab[None, :, :], axis=2) / ab2[None, :]
    t = np.clip(t, 0.0, 1.0)
    closest = a[None, :, :] + t[:, :, None] * ab[None, :, :]
    diff = pts[:, None, :] - closest
    return np.sqrt(np.sum(diff * diff, axis=2))


def assign_weights(pts, names, segs, k=4, chunk=40000):
    n = pts.shape[0]
    k = min(k, segs.shape[0])
    idx = np.empty((n, k), dtype=np.int32)
    wts = np.empty((n, k), dtype=np.float64)
    for start in range(0, n, chunk):
        stop = min(n, start + chunk)
        d = dist_points_to_segments(pts[start:stop], segs)
        part = np.argpartition(d, kth=k - 1, axis=1)[:, :k]
        row = np.arange(d.shape[0])[:, None]
        nearest = d[row, part]
        inv = 1.0 / np.power(nearest + 1e-5, 2)
        inv /= np.sum(inv, axis=1, keepdims=True)
        idx[start:stop] = part
        wts[start:stop] = inv
        log("skinned verts %s / %s" % (stop, n))
    return idx, wts


def apply_groups(mesh, names, idx, weights):
    existing = {g.name: g for g in mesh.vertex_groups}
    for name in names:
        if name not in existing:
            existing[name] = mesh.vertex_groups.new(name=name)
    buckets = defaultdict(list)
    n, k = idx.shape
    for i in range(n):
        for j in range(k):
            w = float(weights[i, j])
            if w < 1e-5:
                continue
            q = round(w, 3)
            buckets[(int(idx[i, j]), q)].append(i)
    for (bi, q), verts in buckets.items():
        existing[names[bi]].add(verts, q, "REPLACE")


def ensure_armature_mod(mesh, arm):
    mesh.parent = arm
    found = None
    for m in mesh.modifiers:
        if m.type == "ARMATURE":
            found = m
            break
    if found is None:
        found = mesh.modifiers.new("Armature", "ARMATURE")
    found.object = arm
    found.use_vertex_groups = True
    found.use_bone_envelopes = False


def count_unweighted(mesh, arm):
    stats = pipeline.validate_weights([mesh], arm)
    return stats["unweighted"], stats["vertices"]


def main():
    args = parse_args()
    try:
        meshes = [o for o in bpy.context.scene.objects if o.type == "MESH" and "_wproxy" not in o.name]
        arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
        if not meshes or not arms:
            raise RuntimeError("need mesh + armature")
        arm = arms[0]
        names, segs = bone_segments(arm)
        if not names:
            raise RuntimeError("armature has no deform bones")
        log("deform bones %s" % len(names))
        last_u, last_t = 0, 0
        for mesh in meshes:
            for g in list(mesh.vertex_groups):
                mesh.vertex_groups.remove(g)
            pts = mesh_world_verts(mesh)
            log("%s verts %s" % (mesh.name, pts.shape[0]))
            idx, w = assign_weights(pts, names, segs, k=4)
            apply_groups(mesh, names, idx, w)
            pipeline.prune_influences(mesh, 4, arm)
            ensure_armature_mod(mesh, arm)
            last_u, last_t = count_unweighted(mesh, arm)
            log("unweighted %s / %s" % (last_u, last_t))
        os.makedirs(args.out, exist_ok=True)
        fbx = os.path.join(args.out, args.slug + ".fbx")
        pipeline.export_fbx(meshes, arm, fbx)
        blend = os.path.join(args.out, args.slug + "_rigged.blend")
        bpy.ops.wm.save_as_mainfile(filepath=blend)
        report = pipeline.validate(meshes, arm, {})
        report.update(report["weights"])
        report.update(fbx=fbx, blend=blend)
        report["warnings"].append("Proximity weights require visual joint and opposite-limb leakage checks.")
        pipeline.write_repair_result(args.out, args.slug, report, fbx, blend)
        with open(os.path.join(args.out, args.slug + "_proximity_skin.json"), "w", encoding="utf-8") as f:
            json.dump(report, f, indent=2)
        sys.exit(0 if report["ok"] else 2)
    except Exception:
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
