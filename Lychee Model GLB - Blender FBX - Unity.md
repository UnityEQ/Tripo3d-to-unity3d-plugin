# Lychee Model GLB > Blender FBX > Unity

Pipeline for exporting a Lychee / Tripo / Hunyuan model from Blender to Unity with its UVs, textures, and smooth shading intact.

## Destination

Use the user's requested export folder. In this Tripo Studio project the default is `Assets/TripoModels/<slug>/`. Otherwise export into the target Unity project's `Assets/` tree under a per-character folder.

Use a lowercase model name for `<slug>`. Keep the FBX and PNGs together. A `Textures` subfolder may contain matching copies for compatibility; assign one consistent set in Unity to avoid duplicate texture assets.

## 1. Inspect and back up

Connect to the existing Blender scene. Inventory mesh objects, UV layers, materials, image paths, custom normals, armatures, and Actions/NLA. Skip cameras, lights, and `WGT_*` widgets. Back up before replacing an export.

Compare the model in Blender material preview against Unity. If Blender is smooth and Unity shows patches under lighting, check normal-map import and mesh normals/tangents before re-unwrapping. Changing UVs requires rebaking all existing maps.

## 2. Prepare materials

For a simple image material, connect the albedo Image Texture Color directly to Principled BSDF Base Color, with its default color white. Use sRGB for albedo.

Do not arbitrarily select the first image in a graph: it may be a normal or packed data map. Do not bypass Mix, Ramp, or procedural nodes if they affect the intended appearance; bake their base-color result to the existing UVs, or preserve the graph and flatten only an export copy. Use unique filenames for distinct material images so they do not overwrite each other.

Normal maps must use Non-Color data and feed a Normal Map node before Principled Normal. Preserve the source map's tangent-space convention; the elf uses OpenGL (+Y).

FBX does not reliably transfer full Blender shader graphs or Unity-specific texture channel assignments. Assign the exported maps in the Unity material explicitly.

## 3. Save textures before export

Save image assets to actual PNG files and point each image filepath at its saved file. Keep these beside the FBX, with matching copies in `Textures` if needed:

- `<slug>_texture.png`: albedo, sRGB.
- `<slug>_normal.png`: tangent-space normal, data.
- `<slug>_orm.png`: original packed R=occlusion, G=roughness, B=metallic, data.
- `<slug>_metallic_smoothness.png`: Unity Standard/URP metallic workflow; R=source ORM B, A=1 minus source ORM G. Save RGBA with alpha.
- `<slug>_occlusion.png`: source ORM R copied to RGB, for Unity's green-channel occlusion input.

Channel conversions must operate on raw non-color data without gamma conversion. Do not plug the original ORM directly into a Unity metallic/smoothness slot. HDRP requires a different mask packing and is not covered by the Standard/URP conversion above.

## 4. Export FBX

Select intended meshes, armature, and associated empties. Preserve smooth shading, custom split normals, UVs, and scale. Do not weld seams or clear custom normals as a generic fix.

| Blender FBX option | Value |
|---|---|
| use_selection | True |
| object_types | MESH, ARMATURE, EMPTY |
| use_mesh_modifiers | True |
| mesh_smooth_type | FACE |
| use_tspace | True |
| use_triangles | True |
| add_leaf_bones | False |
| use_armature_deform_only | True |
| axis_forward / axis_up | -Z / Y |
| apply_unit_scale | True |
| apply_scale_options | FBX_SCALE_ALL |
| bake_space_transform | False |
| path_mode | COPY |
| embed_textures | True |
| bake_anim | True when Idle / Run / Jump / SwordSlash (or other intended clips) exist |
| bake_anim_use_all_actions | True for those intended clips only |
| bake_anim_use_nla_strips | Match the intended clip workflow |

Tangents require valid UVs. For n-gon source meshes, triangulate an export copy before generating tangents, then validate the result. The current elf is already triangulated.

Restore temporary selection, material, and parenting changes after export. Do not alter constrained prop parenting without checking the intended animation.

## 5. Unity import and material setup

Copy/import the export folder into the Unity project's Assets folder. An export outside Assets does not update Unity automatically. Preserve existing .meta files when replacing project assets so GUID references remain intact.

For the FBX Model tab:

- Animation Type: Human, Avatar Create From This Model.
- Import Animation: On. Loop **Idle** and **Run**; do not loop **Jump** or **SwordSlash**.
- Normals: Import, preserving Blender's authored normals.
- Tangents: Import to use the exported basis. If a mismatch persists, test Calculate Mikktspace with imported normals and compare visually.
- Mesh Compression: Off for the initial shading validation.
- Do not enable Swap UVs. Lightmap UV generation is separate from the material UV layout.

For the normal PNG:

- Texture Type: Normal map.
- Create from Grayscale: Off.
- Flip Green Channel: Off for this OpenGL (+Y) export.
- Use 4096 maximum size and no compression for initial validation; optimize only after confirming clean shading.

For a Standard/URP Lit material in Metallic workflow:

- Base Map: `<slug>_texture.png`; tint white.
- Normal Map: `<slug>_normal.png`; strength 1 initially.
- Metallic Map: `<slug>_metallic_smoothness.png`; sRGB off, alpha source Input Texture Alpha, smoothness source Metallic Alpha, smoothness multiplier 1.
- Occlusion Map: `<slug>_occlusion.png`; sRGB off.
- Original ORM: retain as source data, not a direct Unity metallic map.

If materials already exist, update their texture assignments rather than deleting them. Extract embedded textures only when needed; external PNGs are already provided.

## 6. Validate

- Verify FBX contains UVs, normals, tangents, and smoothing data.
- Check external PNGs and their channel packing.
- Inspect front, back, and sides under directional light in Unity.
- Temporarily disable the normal map: if patches disappear, investigate normal-map import, channel convention, and tangent basis. If patches remain, inspect imported mesh normals, material assignments, and baked lighting.
- Do not claim a Unity shading fix is verified from Blender alone.

## Elf export, 2026-09-04

Exported `elf.fbx` from the open Blender mesh with original UVs and custom normals, face smoothing, triangles, and tangent export. Saved albedo, OpenGL normal, source ORM, Unity metallic/smoothness, and occlusion PNGs beside the FBX and in `Textures`.

The source has 1,983,726 triangles and no armature or animation. This export preserves source detail; it is not a game-resolution optimization. Unity import settings and final lighting must still be checked in the target project.

References:
- https://docs.unity3d.com/Manual/StandardShaderMaterialParameterNormalMap.html
- https://docs.unity3d.com/ScriptReference/ModelImporterTangents.html

## Diagnosed Unity import issue, 2026-09-04

The project's `Assets/Prefabs/Client/Models/elf/fbx/elf_normal.png.meta` and its `Textures` duplicate both had `textureType: 0` (Default) and `sRGBTexture: 1`. This is incorrect for tangent-space normal data. A Blender unlit albedo test showed a continuous torso without the large lighting patches in the Unity screenshot.

Corrected both normal-map importers to Normal Map (`textureType: 1`) with sRGB disabled. Corrected ORM, metallic/smoothness, and occlusion importers to non-color data in both locations. Set these data maps to 4096 and uncompressed for diagnosis. Existing GUIDs were preserved. These changes were applied in the Unity project, not only in the external export folder.

Return focus to Unity so it refreshes the changed import settings. If auto-refresh is disabled, reimport the elf texture assets. Then inspect the same view. This diagnoses and corrects an actual importer defect; final visual confirmation in Unity remains necessary. Do not re-export or re-unwrap solely to fix a texture importer setting: an FBX export does not override existing PNG .meta settings.

## Rigged elf revision, 2026-09-04

The current `elf.fbx` is now skinned: 51 deform bones plus the root, including three joints for all ten fingers. It replaces the earlier static export described above. The Blender source includes optional non-stretch IK controls, which are excluded from the FBX; bake animation for later animated exports.

Packed source: the working `elf_rigged.blend` saved with the export.
Rig controls, validation results, and deformation limits: the accompanying rig notes / validation JSON.

The FBX was reimported into Blender to verify all weights, rest positions, and a posed elbow. Unity Humanoid avatar configuration and retargeted animations still require validation in Unity. Preserve existing texture importer settings when replacing the FBX.

## Reusable humanoid rig standard

For future bipedal humanoid rigging, read `Modular Biped Humanoid Rig Standard v1.md` and `Biped Humanoid Rig v1 - Reference.json` at the project root. They define the exact bone names/hierarchy, matrix conventions, modular fitting, skinning/baking rules, and validation. Use this document for the accompanying texture and FBX pipeline. The current user's request controls scope.
