using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Tripo3D.Editor
{
    internal sealed class TripoSetupItem
    {
        public string Label;
        public string Detail;
        public bool Ok;
        public bool CanInstall;
        public string PackageId;
    }

    [InitializeOnLoad]
    internal static class TripoSetup
    {
        public const string GltfPackage = "com.unity.cloud.gltfast";
        public const string UrpPackage = "com.unity.render-pipelines.universal";

        const string PrefsPrompted = "Tripo3D.SetupPrompted";
        const string PrefsInstallPending = "Tripo3D.InstallPending";

        static AddAndRemoveRequest _batch;
        static AddRequest _add;
        static readonly Queue<string> Queue = new Queue<string>();

        public static bool IsBusy { get; private set; }
        public static string Status { get; private set; } = "Ready.";
        public static event Action Changed;

        static TripoSetup()
        {
            EditorApplication.delayCall += AfterReload;
        }

        static void AfterReload()
        {
            if (EditorPrefs.GetBool(PrefsInstallPending, false))
            {
                EditorPrefs.DeleteKey(PrefsInstallPending);
                var leftover = MissingPackageIds();
                if (leftover.Count > 0)
                {
                    Status = "Finishing package install…";
                    StartInstall(leftover);
                }
                else
                {
                    EnsureProjectFolders();
                    Status = "Required Unity packages are installed.";
                    Notify();
                }
            }

            if (SessionState.GetBool(PrefsPrompted, false))
                return;
            SessionState.SetBool(PrefsPrompted, true);

            var missing = MissingPackageIds();
            if (missing.Count == 0)
                return;

            var names = string.Join(", ", missing.ToArray());
            if (EditorUtility.DisplayDialog(
                    "Tripo Studio",
                    "This project is missing Unity packages Tripo Studio needs:\n\n" + names
                    + "\n\nInstall them from the Unity registry now? You can also use Tripo 3D → Setup and Install Packages later.",
                    "Install now",
                    "Later"))
            {
                StartInstall(missing);
                TripoSetupWindow.Open();
            }
        }

        public static List<TripoSetupItem> Scan()
        {
            var items = new List<TripoSetupItem>();
            var packages = RegisteredNames();

            items.Add(PackageItem(
                "glTFast (GLB import)",
                GltfPackage,
                packages.Contains(GltfPackage) || packages.Contains("com.atteneder.gltfast"),
                "Required to import Tripo GLB files into the Project window."));

            var hasUrp = packages.Contains(UrpPackage);
            var hasHdrp = packages.Contains("com.unity.render-pipelines.high-definition");
            items.Add(new TripoSetupItem
            {
                Label = "Universal Render Pipeline",
                PackageId = UrpPackage,
                Ok = hasUrp || hasHdrp,
                CanInstall = !hasUrp && !hasHdrp,
                Detail = hasUrp
                    ? "URP is installed."
                    : hasHdrp
                        ? "HDRP is installed; URP will not be added."
                        : "Recommended. FBX materials use URP Lit when available, otherwise Standard."
            });

            var output = TripoSettings.OutputFolder.Replace('\\', '/');
            var outputOk = AssetDatabase.IsValidFolder(output);
            items.Add(new TripoSetupItem
            {
                Label = "Output folder (" + output + ")",
                Ok = outputOk,
                CanInstall = !outputOk,
                Detail = outputOk ? "Folder exists." : "Will be created so imports have a place to land."
            });

            var blenderScript = Path.Combine(TripoBlenderRunner.ProjectRoot, TripoPaths.BlenderScript.Replace('/', Path.DirectorySeparatorChar));
            items.Add(FileItem("Blender rig scripts", blenderScript, "Copy Tools/Blender/biped_humanoid_v1/ into the Unity project root."));

            var schema = Path.Combine(TripoBlenderRunner.ProjectRoot, TripoPaths.BipedSchema);
            items.Add(FileItem("biped_humanoid_v1 schema", schema, "Copy Biped Humanoid Rig v1 - Reference.json into the project root."));

            var standard = Path.Combine(TripoBlenderRunner.ProjectRoot, "Modular Biped Humanoid Rig Standard v1.md");
            items.Add(FileItem("Biped rig standard", standard, "Copy Modular Biped Humanoid Rig Standard v1.md into the project root."));

            var fbxDoc = Path.Combine(TripoBlenderRunner.ProjectRoot, "Lychee Model GLB - Blender FBX - Unity.md");
            items.Add(FileItem("FBX / Unity export spec", fbxDoc, "Copy Lychee Model GLB - Blender FBX - Unity.md into the project root."));

            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(TripoPaths.StyleSheet);
            items.Add(new TripoSetupItem
            {
                Label = "Studio stylesheet",
                Ok = uss != null,
                Detail = uss != null ? TripoPaths.StyleSheet : "Missing " + TripoPaths.StyleSheet + ". Keep Assets/Tripo3D/Editor/ together."
            });

            var blender = TripoBlenderRunner.FindBlender();
            var blenderOk = !string.IsNullOrEmpty(blender) && File.Exists(blender);
            if (blenderOk && string.IsNullOrEmpty(TripoSettings.BlenderPath))
                TripoSettings.BlenderPath = blender;
            items.Add(new TripoSetupItem
            {
                Label = "Blender 4.2+",
                Ok = blenderOk,
                Detail = blenderOk
                    ? blender
                    : "Not found. Install Blender and set Blender.exe in Studio Settings. https://www.blender.org/download/"
            });

            items.Add(new TripoSetupItem
            {
                Label = "Tripo3D API key",
                Ok = TripoSettings.HasApiKey,
                Detail = TripoSettings.HasApiKey
                    ? "Saved in EditorPrefs / UserSettings (not in Assets)."
                    : "Needed for generate / credits. Add it in Studio → Settings. https://platform.tripo3d.ai"
            });

            var grok = TripoAiDispatcher.FindGrok();
            items.Add(new TripoSetupItem
            {
                Label = "Grok CLI (optional)",
                Ok = !string.IsNullOrEmpty(grok) && File.Exists(grok),
                Detail = !string.IsNullOrEmpty(grok) && File.Exists(grok)
                    ? grok
                    : "Optional. Used when AI agent is Grok. Keep a Grok session open, or set Grok.exe in Settings."
            });

            var codex = TripoAiDispatcher.FindCodex();
            items.Add(new TripoSetupItem
            {
                Label = "ChatGPT Codex CLI (optional)",
                Ok = !string.IsNullOrEmpty(codex) && File.Exists(codex),
                Detail = !string.IsNullOrEmpty(codex) && File.Exists(codex)
                    ? codex
                    : "Optional. Used when AI agent is ChatGPT. Install Codex desktop/CLI, or set Codex.exe in Settings."
            });

            return items;
        }

        public static bool HasInstallableMissing()
        {
            if (MissingPackageIds().Count > 0)
                return true;
            return !AssetDatabase.IsValidFolder(TripoSettings.OutputFolder.Replace('\\', '/'));
        }

        public static List<string> MissingPackageIds()
        {
            var packages = RegisteredNames();
            var missing = new List<string>();
            if (!packages.Contains(GltfPackage) && !packages.Contains("com.atteneder.gltfast"))
                missing.Add(GltfPackage);

            var hasUrp = packages.Contains(UrpPackage);
            var hasHdrp = packages.Contains("com.unity.render-pipelines.high-definition");
            if (!hasUrp && !hasHdrp)
                missing.Add(UrpPackage);

            return missing;
        }

        public static void InstallMissing()
        {
            EnsureProjectFolders();
            var missing = MissingPackageIds();
            if (missing.Count == 0)
            {
                Status = "Nothing to install from the Unity registry. Folders are ready.";
                Notify();
                EditorUtility.DisplayDialog("Tripo Studio", Status, "OK");
                return;
            }

            StartInstall(missing);
        }

        public static void EnsureProjectFolders()
        {
            var folder = TripoSettings.OutputFolder.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(folder))
                folder = TripoPaths.OutputRoot;
            EnsureAssetFolder(folder);
            AssetDatabase.Refresh();
        }

        static void StartInstall(List<string> ids)
        {
            if (ids == null || ids.Count == 0)
                return;
            if (IsBusy)
                return;

            IsBusy = true;
            EditorPrefs.SetBool(PrefsInstallPending, true);
            Queue.Clear();
            for (var i = 0; i < ids.Count; i++)
                Queue.Enqueue(ids[i]);

            Status = "Installing " + ids.Count + " Unity package(s)…";
            Notify();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            PumpQueue();
        }

        static void Tick()
        {
            if (_batch != null)
            {
                if (!_batch.IsCompleted)
                    return;
                FinishRequest(_batch.Status, _batch.Error, "packages");
                _batch = null;
                return;
            }

            if (_add != null)
            {
                if (!_add.IsCompleted)
                    return;
                var id = _add.Result != null ? _add.Result.name : "package";
                FinishRequest(_add.Status, _add.Error, id);
                _add = null;
                if (IsBusy)
                    PumpQueue();
            }
        }

        static void PumpQueue()
        {
            if (Queue.Count == 0)
            {
                CompleteOk();
                return;
            }

            if (Queue.Count > 1)
            {
                var all = Queue.ToArray();
                Queue.Clear();
                Status = "Installing " + string.Join(", ", all) + "…";
                Notify();
                try
                {
                    _batch = Client.AddAndRemove(all, null);
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Tripo3D] AddAndRemove unavailable, installing one by one: " + ex.Message);
                    for (var i = 0; i < all.Length; i++)
                        Queue.Enqueue(all[i]);
                }
            }

            var next = Queue.Dequeue();
            Status = "Installing " + next + "…";
            Notify();
            _add = Client.Add(next);
        }

        static void FinishRequest(StatusCode status, Error error, string label)
        {
            if (status == StatusCode.Failure)
            {
                IsBusy = false;
                EditorPrefs.DeleteKey(PrefsInstallPending);
                EditorApplication.update -= Tick;
                Status = "Failed to install " + label + (error != null ? ": " + error.message : ".");
                Notify();
                Debug.LogError("[Tripo3D] " + Status);
                EditorUtility.DisplayDialog("Tripo Studio", Status, "OK");
                return;
            }

            if (_batch != null || Queue.Count == 0)
                CompleteOk();
        }

        static void CompleteOk()
        {
            IsBusy = false;
            EditorPrefs.DeleteKey(PrefsInstallPending);
            EditorApplication.update -= Tick;
            EnsureProjectFolders();
            Status = "Install finished. Unity may recompile.";
            Notify();
        }

        static HashSet<string> RegisteredNames()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var all = PackageInfo.GetAllRegisteredPackages();
                if (all == null)
                    return set;
                for (var i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && !string.IsNullOrEmpty(all[i].name))
                        set.Add(all[i].name);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Tripo3D] Could not list packages: " + ex.Message);
            }

            return set;
        }

        static TripoSetupItem PackageItem(string label, string id, bool ok, string detail)
        {
            return new TripoSetupItem
            {
                Label = label,
                PackageId = id,
                Ok = ok,
                CanInstall = !ok,
                Detail = ok ? id + " is installed." : detail + " Will install " + id + " from the Unity registry."
            };
        }

        static TripoSetupItem FileItem(string label, string path, string how)
        {
            var ok = File.Exists(path);
            return new TripoSetupItem
            {
                Label = label,
                Ok = ok,
                Detail = ok ? path : "Missing. " + how
            };
        }

        static void EnsureAssetFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder))
                return;
            var parts = assetFolder.Replace('\\', '/').Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                return;
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static void Notify()
        {
            var handler = Changed;
            if (handler != null)
                handler();
        }
    }

    public class TripoSetupWindow : EditorWindow
    {
        Vector2 _scroll;
        List<TripoSetupItem> _items;

        [MenuItem("Tripo 3D/Setup and Install Packages", false, 1)]
        [MenuItem("Window/Tripo 3D/Setup and Install Packages", false, 1)]
        public static void Open()
        {
            var window = GetWindow<TripoSetupWindow>();
            window.titleContent = new GUIContent("Tripo Setup");
            window.minSize = new Vector2(460, 420);
            window.Show();
            window.RefreshScan();
        }

        void OnEnable()
        {
            TripoSetup.Changed += Repaint;
            RefreshScan();
        }

        void OnDisable()
        {
            TripoSetup.Changed -= Repaint;
        }

        void RefreshScan()
        {
            _items = TripoSetup.Scan();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Tripo Studio setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "If you dropped this plugin into a new Unity project, use this window to detect and install what Studio needs. Unity registry packages (glTFast, URP) can be downloaded here. Blender, Grok, Codex, and the Tripo3D API key are local and are only detected.",
                MessageType.Info);

            EditorGUILayout.LabelField(TripoSetup.IsBusy ? "Working…" : TripoSetup.Status, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_items != null)
            {
                for (var i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.BeginHorizontal();
                    var color = GUI.color;
                    GUI.color = item.Ok ? new Color(0.55f, 0.85f, 0.55f) : new Color(0.95f, 0.75f, 0.4f);
                    GUILayout.Label(item.Ok ? "OK" : "NEED", GUILayout.Width(48));
                    GUI.color = color;
                    GUILayout.Label(item.Label, EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.LabelField(item.Detail ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndScrollView();

            GUI.enabled = !TripoSetup.IsBusy;
            if (GUILayout.Button("Scan again", GUILayout.Height(24)))
                RefreshScan();

            GUI.enabled = !TripoSetup.IsBusy && TripoSetup.HasInstallableMissing();
            if (GUILayout.Button("Install missing Unity packages", GUILayout.Height(32)))
            {
                TripoSetup.InstallMissing();
                RefreshScan();
            }

            GUI.enabled = !TripoSetup.IsBusy;
            if (GUILayout.Button("Create output folders"))
            {
                TripoSetup.EnsureProjectFolders();
                RefreshScan();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Tripo3D API"))
                Application.OpenURL("https://platform.tripo3d.ai");
            if (GUILayout.Button("Blender download"))
                Application.OpenURL("https://www.blender.org/download/");
            if (GUILayout.Button("Open Studio"))
                TripoStudioWindow.Open();
            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;
        }
    }
}
