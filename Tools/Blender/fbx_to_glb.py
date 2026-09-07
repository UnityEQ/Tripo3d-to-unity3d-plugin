"""Convert a textured FBX (P2 quad export) to a packed GLB for Unity / Grok rig."""
from __future__ import annotations

import os
import sys

import bpy


def args_map():
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    out = {}
    key = None
    for item in raw:
        if item.startswith("--"):
            key = item[2:]
            out[key] = True
        elif key:
            out[key] = item
            key = None
    return out


def enable_io():
    for addon in ("io_scene_fbx", "io_scene_gltf2"):
        try:
            bpy.ops.preferences.addon_enable(module=addon)
        except Exception:
            pass


def find_color(folder):
    names = ("color.jpg", "color.png", "color.jpeg")
    for root, _dirs, files in os.walk(folder):
        lower = {f.lower(): os.path.join(root, f) for f in files}
        for name in names:
            if name in lower:
                return lower[name]
        for f, path in lower.items():
            if f.endswith("_texture.jpg") or f.endswith("_texture.png"):
                return path
    return None


def reload_images(folder):
    for img in bpy.data.images:
        if img.size[0] != 0:
            continue
        name = os.path.basename(img.filepath or img.name)
        if not name:
            continue
        for root, _dirs, files in os.walk(folder):
            if name in files:
                img.filepath = os.path.join(root, name)
                try:
                    img.reload()
                except Exception:
                    pass
                break


def assign_albedo(path):
    if not path or not os.path.isfile(path):
        return
    img = bpy.data.images.load(path, check_existing=True)
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        nodes = mat.node_tree.nodes
        if any(n.type == "TEX_IMAGE" and n.image and n.image.size[0] > 0 for n in nodes):
            continue
        principled = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
        if principled is None:
            continue
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = img
        mat.node_tree.links.new(tex.outputs["Color"], principled.inputs["Base Color"])


def pack_images():
    for img in bpy.data.images:
        try:
            img.pack()
        except Exception:
            pass


def main():
    args = args_map()
    fbx = args.get("fbx")
    glb = args.get("glb")
    if not fbx or not glb:
        raise SystemExit("usage: blender --background --python fbx_to_glb.py -- --fbx IN.fbx --glb OUT.glb")
    if not os.path.isfile(fbx):
        raise SystemExit("FBX not found: " + fbx)

    enable_io()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=fbx, use_image_search=True)
    folder = os.path.dirname(os.path.abspath(fbx))
    reload_images(folder)
    assign_albedo(find_color(folder))
    pack_images()
    os.makedirs(os.path.dirname(os.path.abspath(glb)) or ".", exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=os.path.abspath(glb),
        export_format="GLB",
        export_texcoords=True,
        export_normals=True,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
    )
    if not os.path.isfile(glb) or os.path.getsize(glb) < 64:
        raise SystemExit("GLB was not written: " + glb)
    print("[fbx_to_glb] wrote", glb, "bytes", os.path.getsize(glb), flush=True)


if __name__ == "__main__":
    main()
