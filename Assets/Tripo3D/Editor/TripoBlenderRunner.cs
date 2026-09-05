using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Tripo3D.Editor
{
    [Serializable]
    internal sealed class TripoBlenderResult
    {
        public bool ok;
        public string fbx;
        public string blend;
        public string manifest;
        public string validation;
        public string[] textures;
        public string[] errors;
        public string[] warnings;
        public string schema;
        public string log;
        public int exitCode;
        public string fbxAssetPath;
        public string blendAssetPath;
    }

    internal static class TripoBlenderRunner
    {
        public const string ScriptRel = "Tools/Blender/biped_humanoid_v1/run.py";
        public const string AgentRel = "Tools/Blender/biped_humanoid_v1/agent.py";
        public const string SchemaRel = "Biped Humanoid Rig v1 - Reference.json";

        public static string ProjectRoot
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        public static string FindBlender()
        {
            var configured = TripoSettings.BlenderPath;
            if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
                return configured;

            var env = Environment.GetEnvironmentVariable("BLENDER_PATH");
            if (!string.IsNullOrEmpty(env) && File.Exists(env))
                return env;

            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation");
            if (Directory.Exists(root))
            {
                var dirs = Directory.GetDirectories(root, "Blender *");
                Array.Sort(dirs);
                for (var i = dirs.Length - 1; i >= 0; i--)
                {
                    var exe = Path.Combine(dirs[i], "blender.exe");
                    if (File.Exists(exe))
                        return exe;
                }
            }

            return "blender";
        }

        public static string SchemaPath
        {
            get { return Path.Combine(ProjectRoot, SchemaRel); }
        }

        public static string ScriptPath
        {
            get { return Path.Combine(ProjectRoot, ScriptRel.Replace('/', Path.DirectorySeparatorChar)); }
        }

        public static TripoBlenderResult RigGlb(string glbDiskPath, string outDiskDir, string slug, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(glbDiskPath) || !File.Exists(glbDiskPath))
                throw new TripoException("GLB not found for Blender rig: " + glbDiskPath);
            if (!File.Exists(ScriptPath))
                throw new TripoException("Missing Blender pipeline script: " + ScriptPath);
            if (!File.Exists(SchemaPath))
                throw new TripoException("Missing biped_humanoid_v1 schema: " + SchemaPath);

            Directory.CreateDirectory(outDiskDir);
            var blender = FindBlender();
            var args = new StringBuilder();
            args.Append("--background");
            args.Append(" --python ").Append(Quote(ScriptPath));
            args.Append(" --");
            args.Append(" --glb ").Append(Quote(glbDiskPath));
            args.Append(" --out ").Append(Quote(outDiskDir));
            args.Append(" --slug ").Append(Quote(slug));
            args.Append(" --schema ").Append(Quote(SchemaPath));
            if (!TripoSettings.BlenderIk)
                args.Append(" --no-ik");

            var resultFile = Path.Combine(outDiskDir, slug + "_blender_result.json");
            // A failed launch must never reuse a previous successful report.
            if (File.Exists(resultFile))
                File.Delete(resultFile);
            var log = RunProcess(blender, args.ToString(), ct, 20 * 60 * 1000);
            var parsed = ReadResult(resultFile);
            parsed.log = log;
            return parsed;
        }

        public static string RunAgent(string blendDiskPath, string op, string extraFile, CancellationToken ct)
        {
            var blender = FindBlender();
            var agent = Path.Combine(ProjectRoot, AgentRel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(agent))
                throw new TripoException("Missing Blender agent script: " + agent);
            if (!File.Exists(blendDiskPath))
                throw new TripoException("Missing .blend: " + blendDiskPath);

            var args = new StringBuilder();
            args.Append("--background ").Append(Quote(blendDiskPath));
            args.Append(" --python ").Append(Quote(agent));
            args.Append(" -- --op ").Append(Quote(op));
            if (!string.IsNullOrEmpty(extraFile))
                args.Append(" --file ").Append(Quote(extraFile));
            return RunProcess(blender, args.ToString(), ct, 10 * 60 * 1000);
        }

        public static void ImportResults(TripoBlenderResult result)
        {
            if (result == null || !result.ok)
                throw new TripoException("Cannot import a failed Blender result. " +
                    (result?.errors == null ? string.Empty : string.Join("\n", result.errors)));
            AssetDatabase.Refresh();
            if (!string.IsNullOrEmpty(result.fbx) && File.Exists(result.fbx))
            {
                result.fbxAssetPath = ToAssetPath(result.fbx);
                ConfigureFbx(result.fbxAssetPath);
            }

            if (!string.IsNullOrEmpty(result.blend) && File.Exists(result.blend))
                result.blendAssetPath = ToAssetPath(result.blend);

            if (result.textures != null)
            {
                for (var i = 0; i < result.textures.Length; i++)
                    ConfigureTexture(ToAssetPath(result.textures[i]));
            }

            AssignUrpMaterials(result.fbxAssetPath);
        }

        static void ConfigureFbx(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
                return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.resampleCurves = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.Import;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.swapUVChannels = false;
            var clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (var i = 0; i < clips.Length; i++)
                {
                    var n = clips[i].name ?? string.Empty;
                    var loop = n.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0
                               || n.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0;
                    clips[i].loopTime = loop;
                    clips[i].loopPose = loop;
                }

                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();
        }

        static void ConfigureTexture(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return;
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;
            var file = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            if (file.Contains("normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.flipGreenChannel = false;
            }
            else if (file.Contains("orm") || file.Contains("occlusion") || file.Contains("metallic") || file.Contains("roughness"))
            {
                importer.sRGBTexture = false;
                importer.textureType = TextureImporterType.Default;
            }

            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static void AssignUrpMaterials(string fbxAssetPath)
        {
            if (string.IsNullOrEmpty(fbxAssetPath))
                return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                return;

            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxAssetPath);
            for (var i = 0; i < assets.Length; i++)
            {
                var mat = assets[i] as Material;
                if (mat == null)
                    continue;
                if (mat.shader != shader)
                    mat.shader = shader;
            }
        }

        static string ToAssetPath(string diskPath)
        {
            if (string.IsNullOrEmpty(diskPath))
                return null;
            var full = Path.GetFullPath(diskPath).Replace('\\', '/');
            var root = ProjectRoot.Replace('\\', '/') + "/";
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return full.Substring(root.Length);
            return null;
        }

        static TripoBlenderResult ReadResult(string path)
        {
            var result = new TripoBlenderResult();
            if (!File.Exists(path))
            {
                result.ok = false;
                result.errors = new[] { "Blender did not write " + path };
                return result;
            }

            try
            {
                result = JsonUtility.FromJson<TripoBlenderResult>(File.ReadAllText(path));
                if (result == null)
                    throw new FormatException("Empty result object.");
                if (result.errors != null && result.errors.Length > 0)
                    result.ok = false;
                if (result.ok && (string.IsNullOrEmpty(result.fbx) || !File.Exists(result.fbx)))
                {
                    result.ok = false;
                    result.errors = new[] { "Blender reported success but the FBX is missing." };
                }
                return result;
            }
            catch (Exception ex)
            {
                return new TripoBlenderResult { ok = false, errors = new[] { "Invalid Blender result: " + ex.Message } };
            }
        }

        static string RunProcess(string fileName, string arguments, CancellationToken ct, int timeoutMs)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = ProjectRoot
            };
            var log = new StringBuilder();
            using (var proc = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                proc.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                        lock (log) log.AppendLine(e.Data);
                };
                proc.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                        lock (log) log.AppendLine(e.Data);
                };
                if (!proc.Start())
                    throw new TripoException("Failed to start Blender: " + fileName);
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                while (!proc.HasExited)
                {
                    if (ct.IsCancellationRequested)
                    {
                        try { proc.Kill(); } catch { }
                        ct.ThrowIfCancellationRequested();
                    }
                    if (!proc.WaitForExit(500) && timeoutMs > 0 && (DateTime.UtcNow - proc.StartTime.ToUniversalTime()).TotalMilliseconds > timeoutMs)
                    {
                        try { proc.Kill(); } catch { }
                        throw new TripoException("Blender timed out.");
                    }
                }

                // Wait for both asynchronous output handlers to drain.
                proc.WaitForExit();
                string output;
                lock (log) output = log.ToString();
                var tempDir = Path.Combine(ProjectRoot, "Temp");
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(Path.Combine(tempDir, "tripo-blender.log"), output);
                if (proc.ExitCode != 0 && proc.ExitCode != 2)
                    throw new TripoException("Blender exited " + proc.ExitCode + "\n" + Trim(output, 2000));
                return output;
            }
        }

        static string Quote(string path)
        {
            return "\"" + (path ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        static string Trim(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }
    }
}
