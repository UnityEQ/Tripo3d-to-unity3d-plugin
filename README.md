# Tripo Studio

Unity editor plugin that generates 3D models with the [Tripo 3D](https://www.tripo3d.ai/) API, imports them into the project, and optionally rigs bipeds in Blender to a shared humanoid standard for Unity.

Open **Tripo 3D → Studio** (or **Window → Tripo 3D → Studio**, shortcut **Ctrl+Shift+T**).

If you drop the plugin into a different Unity project, run **Tripo 3D → Setup and Install Packages**. It detects missing Unity packages (glTFast for GLB import, URP) and can install them from the Unity registry. It also checks Blender, the spec files, Grok/Codex, and the Tripo3D API key. Studio’s header **Setup** button and the Settings tab open the same window. On first load, if required packages are missing, Unity offers to install them.

## What it does

1. **Generate** a GLB from an image, a text prompt, or a four-view character sheet (Tripo API).
2. **Extract props** from that sheet: detect independent parts, then export each selected piece as a watertight GLB.
3. **Import** models into `Assets/TripoModels/` (GLB via glTFast; optional Tripo FBX convert).
4. **Rig** a biped in Blender to `biped_humanoid_v1` (52-bone export skeleton, Idle / Run / Jump / SwordSlash, skinned FBX with textures).

Generation and props extraction talk to Tripo. Rigging is handed to an AI agent (Grok or ChatGPT / Codex) that drives Blender from the spec files in this repo.

## Requirements

| Piece | Notes |
| --- | --- |
| Unity | **6000.4** (project was last opened as `6000.4.10f1`) |
| Render pipeline | URP (already in the project) |
| Tripo API key | From [platform.tripo3d.ai](https://platform.tripo3d.ai). Needed for generate / credits, not for Blender-only rigs |
| Blender | **4.2+** with `blender.exe` on disk. Set the path in Settings if it is not found |
| AI agent | **Grok Build** and/or **ChatGPT Codex** (desktop or CLI) for the rig handoff |

Also in the project: `com.unity.cloud.gltfast` for GLB import.

## First-time setup

1. Open this folder as a Unity project, or copy `Assets/Tripo3D/`, `Tools/Blender/`, and the spec files at the repo root into another project.
2. **Tripo 3D → Setup and Install Packages** (or Studio → **Setup**). Install anything it marks as missing from the Unity registry.
3. **Tripo 3D → Studio → Settings**.
4. Paste your Tripo3D API key and click **Save Tripo3D API Key**. It is stored in EditorPrefs and `UserSettings/Tripo3D.settings.json` (not under `Assets/`). You can also set `TRIPO_API_KEY`.
5. Confirm **Blender.exe**. Browse if the auto-detect misses it.
6. Pick an **AI agent**: Grok or ChatGPT. Optional: point **Grok.exe** / **Codex.exe** if they are not on PATH.

Credits show in the Studio header. Refresh them after saving a key.

## Menus

| Menu | What it opens |
| --- | --- |
| **Tripo 3D → Studio** (`Ctrl+Shift+T`) | Main Studio window |
| **Tripo 3D → Setup and Install Packages** | Detect / install Unity packages and local tools |
| **Window → Tripo 3D → …** | Same two commands |

Studio also has **Setup** in the header and **Setup and install packages** on the Settings tab. On first load, if glTFast or URP is missing, Unity offers to install them.

## Studio tabs

### Image to 3D

Drop a PNG/JPEG/WebP or click **Add Image**. **Generate** uploads to Tripo, polls the task, and writes a timestamped folder under `Assets/TripoModels/`.

Image to 3D always uses **v3.1-20260211**, **PBR off**, and **texture quality detailed**.

Parameters (foldout): model, face limit, texture quality, PBR, real-world size, image autofix, place-in-scene, Tripo FBX convert, auto-rig after generate.

### Text to 3D

Prompt → GLB, same import path.

### Multiview

Uses the image selected on Image to 3D.

1. **Generate Character Sheet** — front / left / back / right views.
2. **Generate 3D from Views** — optional; Detect props will do this first if no 3D task exists yet.
3. **Props Extraction** — detect independent parts (armor, weapons, accessories), pick which ones to keep, then generate watertight meshes.

Props extraction calls Tripo `mesh/segment` (v2 semantic split, using the source image when available) then `mesh/complete` so each selected part is hole-filled and exported as its own GLB under `Assets/TripoModels/` with the pivot recentered. Detail is Minimal / Standard / Detailed. **Merge symmetrical parts** combines left/right items. **Include head & body** is off by default so you get equipment, not the character. **Add a part from the image** lets you name an extra piece.

### Blender Rig

Sends a GLB (last generated, or a file you pick) to the selected AI agent. The agent is expected to:

1. Inventory the mesh.
2. Fit landmarks and build the exact 52-bone `biped_humanoid_v1` skeleton.
3. Skin (every vertex weighted, sums ~1, four influences for export).
4. Author four clips on the export skeleton: **Idle** (loop), **Run** (loop), **Jump**, **SwordSlash** (one-handed right-hand slash).
5. Validate against the schema (including the clips).
6. Export a skinned FBX with textures **and those clips** (Lychee / Unity conventions).
7. Save `*_rigged.blend`, `*_rig_manifest.json`, `*_rig_validation.json`.
8. Import the FBX back into this Unity project (Humanoid; Idle/Run loop).

**Inventory last .blend** runs a headless Blender agent op without going through the AI queue.

For reusable commands, helper limitations and efficient checkpoint use, see [Biped helper commands](Tools/Blender/biped_humanoid_v1/README.md) and the **Efficient execution** section of [the rig standard](Modular%20Biped%20Humanoid%20Rig%20Standard%20v1.md).

### Jobs

Recent generate, props, and rig jobs. Each row shows status, kind, the Tripo model id when it was a generate job (`P1-20260311`, …), and the **project** asset path. **Select** pings that asset. **Refresh job status** re-reads open jobs; **Clear jobs** empties the list (and `Temp/tripo-ai-jobs/`) without deleting imported models.

Jobs that used to store a disk path outside the project (for example `Downloads/dwarf.glb`) are resolved to `Assets/TripoModels/…` when a matching FBX or GLB exists (FBX preferred). Opening the Jobs tab rewrites those references.

### Settings

Project setup button, **Tripo3D API Key**, output folder, Blender path, AI agent, optional Grok/Codex executables. The key is never written under `Assets/`.

## AI agent (Grok vs ChatGPT)

The **AI agent** dropdown is in the Studio header, the Blender Rig tab, and Settings. It only affects **rig jobs**, not Tripo generation.

| Agent | What Unity does |
| --- | --- |
| **Grok** | Writes `Temp/tripo-ai-jobs/<id>.json` and `<id>.prompt.md`. If a Grok session is already open in this project, keep it open — it should pick up the job. If not, Unity starts `grok` (`-c` when history exists). |
| **ChatGPT** | Same job files, then talks to **Codex** (ChatGPT desktop / CLI). If a Codex session is open, Unity runs `codex queue` into that thread. Otherwise it resumes the latest session or starts a new `codex exec`. |

The prompt is also copied to the clipboard. Use **Refresh AI session status** on the Blender tab to see whether an open session was detected.

Keep the chosen agent running when you click **Send … to Grok/ChatGPT**.

Job payload includes `"provider": "grok"` or `"chatgpt"`.

## Generation options

Tripo models exposed in the UI:

- `v3.1-20260211` (default / Image to 3D always)
- `P1-20260311`
- `P2-20260801` (quads on)
- `v3.0-20250812`

Texture quality: `standard` / `detailed` (default) / `extreme`. Image to 3D uses **detailed** and leaves PBR off.

API base: `https://openapi.tripo3d.ai/v3`. Endpoints used:

- Upload, balance, task poll
- Image / text / multiview generation, image-to-multiview
- `mesh/segment` (v2 semantic parts) and `mesh/complete` (watertight props)
- Model convert (FBX / per-part GLB)

Download URLs from Tripo expire quickly; the plugin downloads as soon as the task succeeds.

## Blender pipeline

Headless scripts live in `Tools/Blender/biped_humanoid_v1/`:

| Script | Role |
| --- | --- |
| `run.py` | Import GLB → skeleton → weights → (clips if present) → validate → FBX + `.blend` |
| `agent.py` | Follow-up ops on a saved `*_rigged.blend` (`inventory`, `export`, `validate`, `exec`) |
| `proximity_skin.py` | Weight helper |
| `repair_weights.py` | Weight repair |

Example (Blender 4.2+):

```text
blender --background --python Tools/Blender/biped_humanoid_v1/run.py -- --glb MODEL.glb --out OUT_DIR --slug name --schema "Biped Humanoid Rig v1 - Reference.json"
```

`--no-ik` skips IK controls (FK default is still the export skeleton). Unity’s Blender Rig tab has a matching **Build IK controls** toggle.

### Specs the agent must follow

These three files are the contract. Do not skip them.

| File | What it is |
| --- | --- |
| [Modular Biped Humanoid Rig Standard v1.md](Modular%20Biped%20Humanoid%20Rig%20Standard%20v1.md) | Human-readable standard: 52 export bones, matrix contract, weighting, validation |
| [Biped Humanoid Rig v1 - Reference.json](Biped%20Humanoid%20Rig%20v1%20-%20Reference.json) | Machine-readable schema `biped_humanoid_v1` (names, parents, reference matrices) |
| [Lychee Model GLB - Blender FBX - Unity.md](Lychee%20Model%20GLB%20-%20Blender%20FBX%20-%20Unity.md) | Texture flatten, PNG unpack, FBX export flags, Unity import |

Export skeleton is **52 bones** (`Root` + 51 deform). Names and parents are exact (`Hips`, `Clavicle.L`, `UpperArm.R`, full fingers, …). Optional eight `CTRL_*` IK bones stay Blender-only.

FBX for Unity: **-Z** forward, **Y** up, Copy+Embed textures, no leaf bones, deform-only armature. Unity import: Human avatar from this model, imported normals/tangents, animation on; **Idle** and **Run** loop, **Jump** and **SwordSlash** do not.

## Output layout

Generated assets default to `Assets/TripoModels/<timestamp>_<slug>/`. Example from a dwarf run:

```text
Assets/TripoModels/dwarf/
  dwarf.glb
  dwarf.fbx
  dwarf_texture.png
  dwarf_rigged.blend
  dwarf_rig_manifest.json
  dwarf_rig_validation.json
  Textures/dwarf_texture.png

Assets/TripoModels/<stamp>_helmet/     (example extracted prop)
  helmet.glb
```

Rig jobs and prompts: `Temp/tripo-ai-jobs/` (`LATEST.json` is the newest pending job).

## Project layout

```text
Assets/Tripo3D/Editor/     Unity editor plugin (Studio window, API, jobs, AI dispatch)
Assets/TripoModels/        Imported GLB / FBX / textures / .blend
Tools/Blender/biped_humanoid_v1/   Headless Blender rig + agent
UserSettings/              Local API key and session (not for Assets)
```

Editor scripts (all under `Assets/Tripo3D/Editor/`):

- `TripoStudioWindow` — UI
- `TripoSetup` — package/tool scan and Unity registry install
- `TripoApiClient` — Tripo HTTP (including mesh segment/complete)
- `TripoJobRunner` — generate / import / props / queue rig
- `TripoAiBridge` — write job JSON + prompt
- `TripoAiDispatcher` — Grok / Codex session handoff
- `TripoBlenderRunner` — find Blender, run `run.py` / `agent.py`, import FBX
- `TripoSettings` — EditorPrefs + local key file
- `TripoEditorKeepAlive` — keep the editor pumping while it is in the background (Windows)

## Environment variables

| Variable | Use |
| --- | --- |
| `TRIPO_API_KEY` | Loaded if EditorPrefs is empty |
| `BLENDER_PATH` | `blender.exe` if Settings is empty |
| `GROK_PATH` / `GROK_HOME` | Grok CLI |
| `CODEX_CLI_PATH` | Codex CLI (ChatGPT) |

## Notes

- The API key must never be committed. Keep it in Settings / `UserSettings/`, not in `Assets/`.
- Rig quality depends on the agent actually reading the three spec files and driving Blender. The Unity button queues work; it does not replace those specs.
- `biped_humanoid_v1` is a **shallow A-pose** calibration, not a Unity T-pose. Avatar T-pose is a separate Unity step.
- Do not copy another character’s inverse-bind matrices onto a differently proportioned mesh. Fit translations/lengths; keep names, parents, and axis conventions fixed.

## Pipeline reliability fixes (2026-09-05)

The Blender tools now transfer proxy weights from the active proxy to the original mesh, preserve the reference bone's local Z orientation when setting roll, and exclude external IK controls from the bounding-box fit. Weight transfer retains the mesh world transform and targets the intended armature modifier.

Validation counts only weights belonging to deform bones, across **all** meshes. Missing weights, more than four influences, non-finite/negative weights, sums outside 0.0001 of one, empty geometry, and missing/disabled/incorrect armature modifiers fail validation. `agent.py --op validate` now performs these checks and returns exit code 2 for an invalid rig; `--out DIR --slug NAME` also saves its report. These are structural checks, not a visual deformation or animation-quality certification.

Both repair scripts normalize/prune deform weights, preserve existing animation during export, and refresh the canonical `_rig_validation.json` and `_blender_result.json` alongside their repair report. They no longer report only the final mesh's weight statistics. Proximity skinning remains a fallback requiring visual inspection for joint quality and influence leaking between nearby limbs.

Unity's Blender-result reader now deserializes the full JSON, including textures, warnings and errors, and rejects failed results or a success report with a missing FBX. A new run removes its previous result file first. Cancelling a directly launched Blender process terminates it, and output capture is synchronized and drained before reading the log.

Run the offline Blender regression suite from the project root:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.2\blender.exe' --background --factory-startup --python Tools/Blender/biped_humanoid_v1/tests/regression.py
```

### Remaining functionality gaps found in review

- `run.py` is a first-pass bounding-box fit, not anatomical fitting for arbitrary bipeds. It does not author the four required gameplay animations. The AI workflow must still do that work and check joint placement and deformation.
- The material helper still supports only a simple image-driven base color reliably. Complex shader graphs and full PBR channel conversion need a dedicated export implementation matching the companion MD.
- AI dispatch queues work, but the Jobs UI does not yet monitor a structured completion handshake and automatically validate/import its result. The direct Blender runner's import checks do not themselves complete that AI workflow.
- Existing dwarf reports disagree about its skinning state. The code changes do not rewrite or certify that existing character; rerun validation on its actual saved blend before relying on it.

No generation credits are needed for the offline regression suite.
