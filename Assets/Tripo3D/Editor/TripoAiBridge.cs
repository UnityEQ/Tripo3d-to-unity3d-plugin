using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Tripo3D.Editor
{
    internal static class TripoAiBridge
    {
        public static string JobsDir
        {
            get { return Path.Combine(TripoBlenderRunner.ProjectRoot, "Temp", "tripo-ai-jobs"); }
        }

        public static string EnqueueRigJob(string glbPath)
        {
            if (string.IsNullOrEmpty(glbPath) || !File.Exists(glbPath))
                throw new TripoException("Select a GLB file that exists on disk.");

            var glb = Path.GetFullPath(glbPath);
            var slug = TripoPaths.Sanitize(Path.GetFileNameWithoutExtension(glb));
            var id = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + slug;
            Directory.CreateDirectory(JobsDir);

            var outDir = Path.GetDirectoryName(glb);
            var root = TripoBlenderRunner.ProjectRoot;
            var payload = new StringBuilder();
            payload.Append("{\n");
            payload.Append("  \"id\": \"").Append(Esc(id)).Append("\",\n");
            payload.Append("  \"status\": \"pending\",\n");
            payload.Append("  \"kind\": \"biped_humanoid_v1_rig\",\n");
            payload.Append("  \"glb\": \"").Append(Esc(glb)).Append("\",\n");
            payload.Append("  \"out\": \"").Append(Esc(outDir)).Append("\",\n");
            payload.Append("  \"slug\": \"").Append(Esc(slug)).Append("\",\n");
            payload.Append("  \"schema\": \"").Append(Esc(Path.Combine(root, "Biped Humanoid Rig v1 - Reference.json"))).Append("\",\n");
            payload.Append("  \"standard_md\": \"").Append(Esc(Path.Combine(root, "Modular Biped Humanoid Rig Standard v1.md"))).Append("\",\n");
            payload.Append("  \"fbx_md\": \"").Append(Esc(Path.Combine(root, "Lychee Model GLB - Blender FBX - Unity.md"))).Append("\",\n");
            payload.Append("  \"blender\": \"").Append(Esc(TripoBlenderRunner.FindBlender())).Append("\",\n");
            payload.Append("  \"ik\": ").Append(TripoSettings.BlenderIk ? "true" : "false").Append(",\n");
            payload.Append("  \"provider\": \"").Append(Esc(TripoSettings.AiProviderLabel.ToLowerInvariant())).Append("\",\n");
            payload.Append("  \"ops\": [\"inventory\", \"skeleton\", \"bone_weights\", \"animate\", \"validate\", \"export_fbx\"],\n");
            payload.Append("  \"clips\": [\n");
            payload.Append("    {\"name\": \"Idle\", \"loop\": true, \"kind\": \"idle\"},\n");
            payload.Append("    {\"name\": \"Run\", \"loop\": true, \"kind\": \"run\"},\n");
            payload.Append("    {\"name\": \"Jump\", \"loop\": false, \"kind\": \"jump\"},\n");
            payload.Append("    {\"name\": \"SwordSlash\", \"loop\": false, \"kind\": \"one_handed_sword_slash\"}\n");
            payload.Append("  ],\n");
            payload.Append("  \"createdAt\": \"").Append(DateTime.Now.ToString("o")).Append("\"\n");
            payload.Append("}\n");

            var jsonPath = Path.Combine(JobsDir, id + ".json");
            var promptPath = Path.Combine(JobsDir, id + ".prompt.md");
            File.WriteAllText(jsonPath, payload.ToString());
            File.WriteAllText(Path.Combine(JobsDir, "LATEST.json"), payload.ToString());
            File.WriteAllText(promptPath, BuildPrompt(glb, slug, outDir));

            Debug.Log("[Tripo3D:AI-JOB] pending " + jsonPath);
            TripoAiDispatcher.Dispatch(jsonPath, promptPath);
            return jsonPath;
        }

        static string BuildPrompt(string glb, string slug, string outDir)
        {
            var agent = TripoSettings.AiProviderLabel;
            var root = TripoBlenderRunner.ProjectRoot;
            var standardMd = Path.Combine(root, "Modular Biped Humanoid Rig Standard v1.md");
            var fbxMd = Path.Combine(root, "Lychee Model GLB - Blender FBX - Unity.md");
            var schemaJson = Path.Combine(root, "Biped Humanoid Rig v1 - Reference.json");
            return
                "# " + agent + " — rig this Tripo GLB in Blender\n\n" +
                "The Unity editor queued this job. Drive Blender yourself (MCP `execute_blender_code` and/or headless `blender.exe --background --python`).\n\n" +
                "## FIRST ACTION — the user demands you open these files now\n\n" +
                "This is not optional. Do not inventory the mesh, do not start Blender, do not write Python, do not call `run.py`, and do not author clips until you have **opened these files with your file-read tool in this job**. Do not rely on memory, a prior session, a summary, or this prompt's clip recap. The recap is a reminder; the Markdown files are the contract.\n\n" +
                "Open, in this order:\n" +
                "1. `" + standardMd + "` — entire file. This is the rig + clip contract (§5b duration and silhouette gates included).\n" +
                "2. `" + fbxMd + "` — entire file. This is the Unity FBX / texture export contract.\n" +
                "3. `Tools/Blender/biped_humanoid_v1/README.md` — helper limits.\n\n" +
                "`" + schemaJson + "` is the numeric schema. **Do not load it into the chat** (~125 KB of matrices). Pass that path to Blender / `run.py --schema`.\n\n" +
                "After opening the two Markdown specs, set `stage` on the job JSON to a short note that you opened them (e.g. `specs_read`) before you continue.\n\n" +
                "## Input\n\n" +
                "- GLB: `" + glb + "`\n" +
                "- Slug: `" + slug + "`\n" +
                "- Output folder: `" + outDir + "`\n\n" +
                "Prefer `Tools/Blender/biped_humanoid_v1/run.py --glb GLB --out OUT --slug SLUG --schema SCHEMA` and `agent.py` (`inventory` / `validate` / `export`) over writing new pipeline scripts. Landmark fit and the four clips are still required after the first bind. Keep mesh arrays, `.npy`, matrices and collision-pair dumps on disk; use image tools for visual review, never base64 text. See `Tools/Blender/biped_humanoid_v1/README.md` for helper limits. Do not generate per-job Editor C# importers.\n\n" +
                "## Then do these ops in order\n\n" +
                "1. Inventory the mesh (counts, UVs, materials, transforms). Back up if replacing a rig.\n" +
                "2. Fit anatomical landmarks and build the exact 52-bone export skeleton (names/parents must match the standard). Optional IK controls, FK default.\n" +
                "3. Skin / bone weights: every vertex weighted, sums ~1, four influences for export, no opposite-limb leak. Heat weights are a first pass only.\n" +
                "4. **Animate (required, after skeleton + weights, before validate/export).** Author four named Actions on the export skeleton. Bake IK/constraints to FK deform bones. Do not export `CTRL_*` or `WGT_*`. Scene 30 fps. Clip names are exact:\n" +
                "   - `Idle` — looping, 60 frames (2.0s). Subtle breathing, weight shift, slight head/arm motion. In-place. First and last pose must match. Arms hang relaxed at the sides (soft elbow bend, hands near the hips), not held out.\n" +
                "   - `Run` — looping run cycle, 24–30 frames. Opposite arm/leg, hip bounce, full-body. In-place (no forward travel) so Unity can add root motion. First and last pose must match. Arms pump close to the torso like a real run, not a wide A-pose.\n" +
                "   - `Jump` — one-shot, 30–45 frames, **not** looping. Crouch → takeoff → hang → land, recover to a stance close to Idle. Vertical motion on Hips/Root only. Arms stay near the body (counterbalance), not stuck out to the sides.\n" +
                "   - `SwordSlash` — one-shot one-handed sword slash, 24–36 frames, **not** looping. Use **Hand.R** as if gripping a one-handed sword: wind-up over the **right-rear / high-right** (not through the head or back), then cut in an arc **in front of** the chest, follow-through to the left-front, recover. Do **not** create a sword mesh unless one already exists. Left arm may counterbalance **outside** the torso. Fingers in a grip on the right hand. Keep the right elbow in a natural slash path, not flared wide.\n" +
                "   **No self-intersection (required), without a wacky wide-arm pose.** Limbs must not pass through this character's own mesh — play every clip in solid view. **Do not** clear the mesh by abducting the shoulders or sticking the arms out in a T-pose / scarecrow / wide A-pose. That looks wrong. Keep a normal silhouette: elbows slightly bent, upper arms near the ribs, hands close to the hips or on a real run/slash path. If a frame clips, rotate `Chest`/`Hips` and move the path a little **forward** so there is a small gap (a few centimeters is enough). Light surface contact is OK; penetration is not. For `SwordSlash` the usual failure is a straight line through the belly — put the slash plane in front of the body, never through it, but do not swing the arm out to the side to compensate. Watch the standard's reversed-knee-pole bake failure: inspect axial bone orientation, not just joint endpoints. Keep rest skeleton and weights unchanged while authoring clips.\n" +
                "5. Validate (schema, rest-pose invariance, placement, weights, and the four clips).\n" +
                "6. Export skinned FBX with textures **and these four clips** per the Lychee document (`-Z` forward, `Y` up, Copy+Embed, no leaf bones). `bake_anim=True`, `bake_anim_use_all_actions=True` for Idle / Run / Jump / SwordSlash only.\n" +
                "7. Save `" + slug + "_rigged.blend`, `" + slug + "_rig_manifest.json`, `" + slug + "_rig_validation.json`.\n" +
                "8. Import the FBX back into this Unity project and ping the asset. Humanoid avatar; Idle and Run loop, Jump and SwordSlash do not.\n\n" +
                "## Job file (required)\n\n" +
                "Update `Temp/tripo-ai-jobs/<id>.json` as you go: `status` `working`, `stage` (short), `updatedAt` (ISO time) so Unity does not time out. When Unity import and Humanoid checks succeed, set `status` to `done` and write an empty sibling `<id>.json.done`. On failure: `status` `failed` and `<id>.json.failed`. Do not leave the job on `working` after you are finished.\n\n";
        }

        static string Esc(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
