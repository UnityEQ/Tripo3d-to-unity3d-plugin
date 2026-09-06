using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Tripo3D.Editor
{
    [FilePath("UserSettings/Tripo3DSession.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class TripoSession : ScriptableSingleton<TripoSession>
    {
        public List<TripoJobRecord> jobs = new List<TripoJobRecord>();

        public void Persist()
        {
            Save(true);
        }

        public TripoJobRecord Add(TripoJobKind kind, string name)
        {
            var record = new TripoJobRecord
            {
                id = Guid.NewGuid().ToString("N"),
                name = name,
                kind = kind.ToString(),
                status = TripoJobState.Creating.ToString(),
                createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                progress = 0
            };
            jobs.Insert(0, record);
            if (jobs.Count > 50)
                jobs.RemoveRange(50, jobs.Count - 50);
            Persist();
            return record;
        }

        public void Clear()
        {
            if (jobs == null)
                jobs = new List<TripoJobRecord>();
            else
                jobs.Clear();
            Persist();
        }
    }

    internal static class TripoJobRunner
    {
        const string ActiveTaskKey = "Tripo3D.ActiveTaskId";
        const string ActiveNameKey = "Tripo3D.ActiveName";
        const string ActiveKindKey = "Tripo3D.ActiveKind";
        const string LastModelTaskKey = "Tripo3D.LastModelTaskId";
        const string LastMultiviewTaskKey = "Tripo3D.LastMultiviewTaskId";
        const string LastSegmentTaskKey = "Tripo3D.LastSegmentTaskId";

        static CancellationTokenSource _cts;
        static bool _busy;
        static bool _watching;
        static double _nextWatch;

        public static bool IsBusy => _busy;
        public static string StatusMessage { get; private set; } = "Ready.";
        public static float Progress { get; private set; }
        public static string LastAssetPath { get; private set; }
        public static string LastGlbPath { get; private set; }
        public static string LastBlendPath { get; private set; }
        public static string LastPreviewPath { get; private set; }
        public static Texture2D LastPreviewTexture { get; private set; }
        public static string LastModelTaskId
        {
            get => SessionState.GetString(LastModelTaskKey, string.Empty);
            private set => SessionState.SetString(LastModelTaskKey, value ?? string.Empty);
        }
        public static string LastMultiviewTaskId
        {
            get => SessionState.GetString(LastMultiviewTaskKey, string.Empty);
            private set => SessionState.SetString(LastMultiviewTaskKey, value ?? string.Empty);
        }
        public static string LastSegmentTaskId
        {
            get => SessionState.GetString(LastSegmentTaskKey, string.Empty);
            private set => SessionState.SetString(LastSegmentTaskKey, value ?? string.Empty);
        }
        public static event Action Changed;

        public static void Cancel()
        {
            if (_cts != null)
                _cts.Cancel();
        }

        public static async void RunImageToModel(string imagePath, byte[] imageBytes, TripoGenerateOptions options)
        {
            options = options ?? new TripoGenerateOptions();
            options.Model = TripoSettings.BestModel;
            options.Pbr = false;
            options.TextureQuality = "detailed";
            await Run(TripoJobKind.ImageToModel, DisplayName(imagePath, options), async (record, ct) =>
            {
                Set("Uploading image...", 5f, TripoJobState.Uploading, record);
                var token = await TripoApiClient.UploadFileAsync(imagePath, imageBytes, ct);
                if (options != null)
                    record.model = options.Model;
                Set("Creating image-to-3D task...", 12f, TripoJobState.Creating, record);
                var taskId = await TripoApiClient.ImageToModelAsync(token, options, ct);
                record.taskId = taskId;
                Remember(taskId, record.name, TripoJobKind.ImageToModel);
                var task = await PollAsync(taskId, record, ct);
                await ImportAsync(task, record, options, ct);
            });
        }

        public static async void RunTextToModel(TripoGenerateOptions options)
        {
            await Run(TripoJobKind.TextToModel, DisplayName(options.Prompt, options), async (record, ct) =>
            {
                if (options != null)
                    record.model = options.Model;
                Set("Creating text-to-3D task...", 8f, TripoJobState.Creating, record);
                var taskId = await TripoApiClient.TextToModelAsync(options, ct);
                record.taskId = taskId;
                Remember(taskId, record.name, TripoJobKind.TextToModel);
                var task = await PollAsync(taskId, record, ct);
                await ImportAsync(task, record, options, ct);
            });
        }

        public static void RunBlenderRig(string glbAssetOrDiskPath)
        {
            RequestAiRig(glbAssetOrDiskPath);
        }

        public static async void RunImageToMultiview(string imagePath, byte[] imageBytes, Action<TripoOutput> onViews)
        {
            await Run(TripoJobKind.ImageToMultiview, DisplayName(imagePath, null) + "_sheet", async (record, ct) =>
            {
                Set("Uploading image...", 5f, TripoJobState.Uploading, record);
                var token = await TripoApiClient.UploadFileAsync(imagePath, imageBytes, ct);
                Set("Generating character sheet...", 12f, TripoJobState.Creating, record);
                var taskId = await TripoApiClient.ImageToMultiviewAsync(token, ct);
                record.taskId = taskId;
                Remember(taskId, record.name, TripoJobKind.ImageToMultiview);
                LastMultiviewTaskId = taskId;
                var task = await PollAsync(taskId, record, ct);
                await OnMain(() =>
                {
                    if (onViews != null)
                        onViews(task.output);
                });
                Set("Character sheet ready.", 100f, TripoJobState.Success, record);
            });
        }

        public static async void RunMultiviewToModel(string front, string left, string back, string right, TripoGenerateOptions options)
        {
            await Run(TripoJobKind.MultiviewToModel, "multiview_model", async (record, ct) =>
            {
                if (options != null)
                    record.model = options.Model;
                Set("Creating multiview-to-3D task...", 10f, TripoJobState.Creating, record);
                var taskId = await TripoApiClient.MultiviewToModelAsync(front, left, back, right, options, ct);
                record.taskId = taskId;
                Remember(taskId, record.name, TripoJobKind.MultiviewToModel);
                var task = await PollAsync(taskId, record, ct);
                await ImportAsync(task, record, options, ct);
            });
        }

        public static async void RunDetectProps(
            string[] viewUrls,
            string imagePath,
            byte[] imageBytes,
            string granularity,
            bool splitByConnectivity,
            Action<string[], Texture2D> onParts)
        {
            await Run(TripoJobKind.PropDetect, "props_detect", async (record, ct) =>
            {
                var modelInput = LastModelTaskId;
                if (string.IsNullOrEmpty(modelInput) && viewUrls != null && viewUrls.Length >= 4 && !string.IsNullOrEmpty(viewUrls[0]))
                {
                    Set("Generating 3D from character sheet before props extract...", 8f, TripoJobState.Creating, record);
                    var options = TripoSettings.CurrentOptions();
                    options.PlaceInScene = false;
                    options.RigInBlender = false;
                    options.ConvertToFbx = false;
                    var modelId = await TripoApiClient.MultiviewToModelAsync(viewUrls[0], viewUrls[1], viewUrls[2], viewUrls[3], options, ct);
                    record.taskId = modelId;
                    Remember(modelId, record.name, TripoJobKind.MultiviewToModel);
                    var modelTask = await PollAsync(modelId, record, ct);
                    await ImportAsync(modelTask, record, options, ct);
                    modelInput = modelId;
                }

                if (string.IsNullOrEmpty(modelInput) && !string.IsNullOrEmpty(LastGlbPath))
                {
                    var disk = ResolveGlbDiskPath(LastGlbPath);
                    Set("Uploading GLB for segmentation...", 10f, TripoJobState.Uploading, record);
                    var bytes = File.ReadAllBytes(disk);
                    modelInput = await TripoApiClient.UploadFileAsync(disk, bytes, ct);
                }

                if (string.IsNullOrEmpty(modelInput))
                    throw new TripoException("Generate a character sheet (and 3D) first, or import a GLB, then detect props.");

                string refImage = null;
                if (imageBytes != null && imageBytes.Length > 0)
                {
                    Set("Uploading reference image for semantic parts...", 18f, TripoJobState.Uploading, record);
                    refImage = await TripoApiClient.UploadFileAsync(imagePath ?? "character.png", imageBytes, ct);
                }

                Set("Segmenting mesh into props...", 25f, TripoJobState.Creating, record);
                var segId = await TripoApiClient.SegmentMeshAsync(modelInput, granularity, splitByConnectivity, refImage, ct);
                record.taskId = segId;
                Remember(segId, record.name, TripoJobKind.PropDetect);
                LastSegmentTaskId = segId;
                var segTask = await PollAsync(segId, record, ct);

                var names = TripoJson.ExtractPartNames(TripoApiClient.LastRawJson);
                Texture2D preview = null;
                var imported = await ImportSegmentResult(segTask, record, ct);
                if (imported.previewTexture != null)
                    preview = imported.previewTexture;
                if (names.Length == 0)
                    names = MeshNamesFromAsset(imported.assetPath);

                if (names.Length == 0)
                    throw new TripoException("Segmentation finished but no part names were returned. Try a different detail level.");

                await OnMain(() =>
                {
                    if (onParts != null)
                        onParts(names, preview);
                    Set("Detected " + names.Length + " prop(s).", 100f, TripoJobState.Success, record);
                });
            });
        }

        public static async void RunExtractProps(string[] partNames, string slug, Action<TripoPropPart[]> onDone)
        {
            if (partNames == null || partNames.Length == 0)
            {
                EditorUtility.DisplayDialog("Tripo Studio", "Select at least one prop.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(LastSegmentTaskId))
            {
                EditorUtility.DisplayDialog("Tripo Studio", "Detect props first.", "OK");
                return;
            }

            await Run(TripoJobKind.PropExtract, (slug ?? "props") + "_extract", async (record, ct) =>
            {
                Set("Completing " + partNames.Length + " prop(s) (watertight)...", 12f, TripoJobState.Creating, record);
                var completeId = await TripoApiClient.CompleteMeshAsync(LastSegmentTaskId, partNames, "ai_completion", ct);
                record.taskId = completeId;
                var completeTask = await PollAsync(completeId, record, ct);

                var results = new List<TripoPropPart>();
                for (var i = 0; i < partNames.Length; i++)
                {
                    var part = partNames[i];
                    Set("Exporting prop " + part + " (" + (i + 1) + "/" + partNames.Length + ")...", 40f + 50f * i / partNames.Length, TripoJobState.Downloading, record);
                    try
                    {
                        var convertId = await TripoApiClient.ConvertAsync(completeId, "GLB", ct, new[] { part });
                        var converted = await PollAsync(convertId, record, ct);
                        var url = converted.output != null ? converted.output.BestModelUrl : null;
                        if (string.IsNullOrEmpty(url))
                            url = completeTask.output != null ? completeTask.output.BestModelUrl : null;
                        if (string.IsNullOrEmpty(url))
                            continue;

                        var glb = await TripoApiClient.DownloadAsync(url, ct);
                        byte[] preview = null;
                        if (converted.output != null && !string.IsNullOrEmpty(converted.output.rendered_image_url))
                        {
                            try
                            {
                                preview = await TripoApiClient.DownloadAsync(converted.output.rendered_image_url, ct);
                            }
                            catch
                            {
                            }
                        }

                        await OnMain(() =>
                        {
                            var imported = WriteAssets(TripoPaths.Sanitize(part), glb, preview, null);
                            results.Add(new TripoPropPart
                            {
                                name = part,
                                selected = true,
                                assetPath = imported.assetPath,
                                preview = imported.previewTexture
                            });
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[Tripo3D] Prop export failed for " + part + ": " + ex.Message);
                    }
                }

                if (results.Count == 0)
                {
                    var url = completeTask.output != null ? completeTask.output.BestModelUrl : null;
                    if (string.IsNullOrEmpty(url))
                        throw new TripoException("Prop completion succeeded but no model URL was returned.");
                    var glb = await TripoApiClient.DownloadAsync(url, ct);
                    await OnMain(() =>
                    {
                        var imported = WriteAssets((slug ?? "props") + "_completed", glb, null, null);
                        results.Add(new TripoPropPart
                        {
                            name = slug ?? "props",
                            selected = true,
                            assetPath = imported.assetPath
                        });
                    });
                }

                await OnMain(() =>
                {
                    if (onDone != null)
                        onDone(results.ToArray());
                    Set("Extracted " + results.Count + " prop(s).", 100f, TripoJobState.Success, record);
                });
            });
        }

        public static void RequestAiRig(string glbAssetOrDiskPath, bool showDialog = true)
        {
            string disk;
            try
            {
                disk = ResolveGlbDiskPath(glbAssetOrDiskPath);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Tripo Studio", ex.Message, "OK");
                return;
            }

            LastGlbPath = ToAssetPath(disk);
            var jobPath = TripoAiBridge.EnqueueRigJob(disk);
            var slug = TripoPaths.Sanitize(Path.GetFileNameWithoutExtension(disk));
            var record = TripoSession.instance.Add(TripoJobKind.BlenderRig, slug + "_ai_rig");
            var agent = TripoSettings.AiProviderLabel;
            var dispatch = string.IsNullOrEmpty(TripoAiDispatcher.LastSummary)
                ? "Queued for " + agent + "."
                : TripoAiDispatcher.LastSummary;
            record.taskId = Path.GetFileNameWithoutExtension(jobPath);
            record.assetPath = ResolveJobAssetPath(LastGlbPath, slug) ?? LastGlbPath;
            Set(dispatch + " Waiting on " + agent + ".", 10f, TripoJobState.Queued, record);
            TripoSession.instance.Persist();
            EnsureWatch();
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Tripo Studio",
                    "GLB queued for " + agent + ".\n\n"
                    + dispatch + "\n\n"
                    + "It will read the spec files and drive Blender (skeleton, bone weights, Idle/Run/Jump/SwordSlash, Unity FBX).\n\n"
                    + jobPath,
                    "OK");
            }
        }

        static async Task Run(TripoJobKind kind, string name, Func<TripoJobRecord, CancellationToken, Task> work)
        {
            if (_busy)
            {
                EditorUtility.DisplayDialog("Tripo Studio", "A generation is already running.", "OK");
                return;
            }

            if (kind != TripoJobKind.BlenderRig && !TripoSettings.HasApiKey)
            {
                EditorUtility.DisplayDialog("Tripo Studio", "Add your Tripo3D API key in the Settings tab first.", "OK");
                return;
            }

            TripoApiClient.UseKey(TripoSettings.ApiKey);
            _busy = true;
            EnsureWatch();
            _cts = new CancellationTokenSource();
            var record = TripoSession.instance.Add(kind, name);
            Notify();
            try
            {
                if (kind != TripoJobKind.BlenderRig)
                    await EnsureCreditsAsync(_cts.Token);
                await work(record, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                await OnMain(() => Set("Cancelled.", Progress, TripoJobState.Cancelled, record));
            }
            catch (TripoException ex)
            {
                var message = ex.Message;
                if (!string.IsNullOrEmpty(ex.Suggestion))
                    message += " " + ex.Suggestion;
                await OnMain(() => Fail(record, message));
            }
            catch (Exception ex)
            {
                Debug.LogError("[Tripo3D] " + ex);
                await OnMain(() => Fail(record, ex.Message));
            }
            finally
            {
                _busy = false;
                if (_cts != null)
                {
                    _cts.Dispose();
                    _cts = null;
                }

                try
                {
                    await OnMain(() =>
                    {
                        ClearActive();
                        Notify();
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Tripo3D] cleanup: " + ex.Message);
                }
            }
        }

        static async Task<TripoData> PollAsync(string taskId, TripoJobRecord record, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var task = await TripoApiClient.GetTaskAsync(taskId, ct);
                var status = (task.status ?? string.Empty).ToLowerInvariant();
                var progress = Mathf.Clamp(task.progress, 0, 100);
                var mapped = 15f + progress * 0.7f;
                await OnMain(() => Set("Generating... " + status + " " + progress + "%", mapped, TripoJobState.Running, record));

                if (TripoJson.IsCreditFailure(task.error_code, task.error_message))
                    throw TripoJson.ToException(task.error_code, task.error_message, null);
                if (status == "success")
                    return task;
                if (status == "failed" || status == "cancelled" || status == "banned")
                {
                    var error = string.IsNullOrEmpty(task.error_message) ? "Task " + status : task.error_message;
                    throw TripoJson.ToException(task.error_code, error, null);
                }

                await Task.Delay(2000, ct);
            }
        }

        static async Task ImportAsync(TripoData task, TripoJobRecord record, TripoGenerateOptions options, CancellationToken ct)
        {
            options = options ?? new TripoGenerateOptions();
            var output = task.output ?? new TripoOutput();
            var modelUrl = output.BestModelUrl;
            if (string.IsNullOrEmpty(modelUrl))
                throw new TripoException("Task succeeded but no model URL was returned. Download immediately — URLs expire after 5 minutes.");

            Set("Downloading GLB...", 88f, TripoJobState.Downloading, record);
            var glb = await TripoApiClient.DownloadAsync(modelUrl, ct);

            byte[] preview = null;
            if (!string.IsNullOrEmpty(output.rendered_image_url))
            {
                try
                {
                    preview = await TripoApiClient.DownloadAsync(output.rendered_image_url, ct);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Tripo3D] Preview download failed: " + ex.Message);
                }
            }

            byte[] fbx = null;
            if (options.ConvertToFbx && !string.IsNullOrEmpty(task.task_id))
            {
                try
                {
                    Set("Converting to FBX...", 92f, TripoJobState.Downloading, record);
                    var convertId = await TripoApiClient.ConvertAsync(task.task_id, "FBX", ct);
                    var converted = await PollAsync(convertId, record, ct);
                    var fbxUrl = converted.output != null ? converted.output.BestModelUrl : null;
                    if (!string.IsNullOrEmpty(fbxUrl))
                        fbx = await TripoApiClient.DownloadAsync(fbxUrl, ct);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Tripo3D] FBX convert failed, importing GLB instead: " + ex.Message);
                }
            }

            await OnMain(() =>
            {
                Set("Importing into project...", 96f, TripoJobState.Importing, record);
                var imported = WriteAssets(record.name, glb, preview, fbx);
                record.assetPath = imported.assetPath;
                record.previewPath = imported.previewPath;
                if (options != null && !string.IsNullOrEmpty(options.Model))
                    record.model = options.Model;
                LastAssetPath = imported.assetPath;
                LastPreviewPath = imported.previewPath;
                LastPreviewTexture = imported.previewTexture;
                LastGlbPath = imported.glbPath;
                if (!string.IsNullOrEmpty(task.task_id))
                    LastModelTaskId = task.task_id;
                if (options.PlaceInScene)
                    PlaceInScene(imported.assetPath);
                Set("Imported " + imported.assetPath, 100f, TripoJobState.Success, record);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(imported.assetPath));
            });

            if (options.RigInBlender && !string.IsNullOrEmpty(LastGlbPath))
            {
                await OnMain(() =>
                {
                    Set("Queuing Blender rig job for " + TripoSettings.AiProviderLabel + "...", 100f, TripoJobState.Rigging, record);
                    RequestAiRig(LastGlbPath, false);
                });
            }
        }

        static async Task<(string assetPath, Texture2D previewTexture)> ImportSegmentResult(TripoData task, TripoJobRecord record, CancellationToken ct)
        {
            var output = task != null ? task.output : null;
            var url = output != null ? output.BestModelUrl : null;
            if (string.IsNullOrEmpty(url))
                return (null, null);

            Set("Downloading segmented mesh...", 88f, TripoJobState.Downloading, record);
            var glb = await TripoApiClient.DownloadAsync(url, ct);
            byte[] preview = null;
            if (output != null && !string.IsNullOrEmpty(output.rendered_image_url))
            {
                try
                {
                    preview = await TripoApiClient.DownloadAsync(output.rendered_image_url, ct);
                }
                catch
                {
                }
            }

            var imported = default((string assetPath, string previewPath, Texture2D previewTexture, string glbPath));
            await OnMain(() =>
            {
                imported = WriteAssets("segmented_props", glb, preview, null);
            });
            return (imported.assetPath, imported.previewTexture);
        }

        static string[] MeshNamesFromAsset(string assetPath)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(assetPath))
                return names.ToArray();
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (go == null)
                return names.ToArray();
            CollectMeshNames(go.transform, go.name, names);
            return names.ToArray();
        }

        static void CollectMeshNames(Transform t, string rootName, List<string> names)
        {
            if (t == null)
                return;
            var hasMesh = t.GetComponent<MeshFilter>() != null || t.GetComponent<SkinnedMeshRenderer>() != null;
            if (hasMesh && !string.Equals(t.name, rootName, StringComparison.OrdinalIgnoreCase))
            {
                var n = t.name;
                if (!string.IsNullOrEmpty(n) && n.IndexOf("Scene", StringComparison.OrdinalIgnoreCase) < 0 && n != "Node")
                    names.Add(n);
            }

            for (var i = 0; i < t.childCount; i++)
                CollectMeshNames(t.GetChild(i), rootName, names);
        }

        static (string assetPath, string previewPath, Texture2D previewTexture, string glbPath) WriteAssets(string name, byte[] glb, byte[] preview, byte[] fbx)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var slug = TripoPaths.Sanitize(name);
            var folder = TripoSettings.OutputFolder.TrimEnd('/') + "/" + stamp + "_" + slug;
            EnsureFolder(folder);

            var glbPath = folder + "/" + slug + ".glb";
            File.WriteAllBytes(ToDisk(glbPath), glb ?? Array.Empty<byte>());

            string previewPath = null;
            Texture2D previewTexture = null;
            if (preview != null && preview.Length > 0)
            {
                var written = WritePreview(folder, slug, preview);
                previewPath = written.path;
                previewTexture = written.texture;
            }

            string fbxPath = null;
            if (fbx != null && fbx.Length > 0)
            {
                fbxPath = folder + "/" + slug + ".fbx";
                File.WriteAllBytes(ToDisk(fbxPath), fbx);
            }

            AssetDatabase.Refresh();

            var assetPath = !string.IsNullOrEmpty(fbxPath) ? fbxPath : glbPath;
            return (assetPath, previewPath, previewTexture, glbPath);
        }

        static (string path, Texture2D texture) WritePreview(string folder, string slug, byte[] bytes)
        {
            var tex = new Texture2D(2, 2);
            var loaded = tex.LoadImage(bytes);
            if (loaded)
            {
                var png = tex.EncodeToPNG();
                var path = folder + "/" + slug + "_preview.png";
                File.WriteAllBytes(ToDisk(path), png ?? Array.Empty<byte>());
                tex.name = slug + "_preview";
                return (path, tex);
            }

            UnityEngine.Object.DestroyImmediate(tex);
            var ext = DetectImageExtension(bytes);
            if (ext == ".png" || ext == ".jpg")
            {
                var path = folder + "/" + slug + "_preview" + ext;
                File.WriteAllBytes(ToDisk(path), bytes);
                return (path, null);
            }

            Debug.LogWarning("[Tripo3D] Preview is " + ext.Trim('.') + ", not PNG/JPEG. Skipping Project import (Tripo often returns WebP).");
            return (null, null);
        }

        static string DetectImageExtension(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 12)
                return ".bin";
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return ".png";
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return ".jpg";
            if (bytes.Length >= 12 && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
                && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
                return ".webp";
            if (bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F')
                return ".gif";
            return ".bin";
        }

        static void PlaceInScene(string assetPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
                return;

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                instance = UnityEngine.Object.Instantiate(prefab);

            instance.name = Path.GetFileNameWithoutExtension(assetPath);
            Undo.RegisterCreatedObjectUndo(instance, "Place Tripo Model");
            Selection.activeGameObject = instance;
        }

        static void EnsureFolder(string assetFolder)
        {
            var parts = assetFolder.Replace('\\', '/').Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static string ResolveGlbDiskPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new TripoException("No GLB path. Generate a model first.");
            if (File.Exists(path))
                return Path.GetFullPath(path);
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase))
            {
                var disk = ToDisk(path);
                if (File.Exists(disk))
                    return disk;
                if (Path.GetExtension(disk).ToLowerInvariant() != ".glb")
                {
                    var sibling = Path.ChangeExtension(disk, ".glb");
                    if (File.Exists(sibling))
                        return sibling;
                }
            }

            throw new TripoException("Could not find GLB: " + path);
        }

        internal static string ResolveJobAssetPath(string stored, string jobName = null)
        {
            var fromStored = ResolveExistingAsset(stored);
            if (!string.IsNullOrEmpty(fromStored))
                return PreferFbx(fromStored);

            var slug = Path.GetFileNameWithoutExtension(stored ?? string.Empty);
            if (string.IsNullOrEmpty(slug) && !string.IsNullOrEmpty(jobName))
                slug = jobName;
            slug = StripJobSuffix(TripoPaths.Sanitize(slug));
            if (string.IsNullOrEmpty(slug))
                return null;

            var found = FindProjectModel(slug);
            return PreferFbx(found) ?? found;
        }

        static string ResolveExistingAsset(string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return null;

            var normalized = stored.Replace('\\', '/');
            if (IsLoadableAsset(normalized))
                return normalized;

            if (File.Exists(stored))
            {
                var inside = ToAssetPath(stored);
                if (IsLoadableAsset(inside))
                    return inside;
            }

            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                var disk = ToDisk(normalized);
                if (File.Exists(disk))
                    return normalized;
            }

            return null;
        }

        static string PreferFbx(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;
            if (assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) && IsLoadableAsset(assetPath))
                return assetPath;

            var fbx = Path.ChangeExtension(assetPath, ".fbx").Replace('\\', '/');
            if (IsLoadableAsset(fbx))
                return fbx;
            return IsLoadableAsset(assetPath) ? assetPath : null;
        }

        static string FindProjectModel(string slug)
        {
            if (string.IsNullOrEmpty(slug))
                return null;

            var folder = TripoSettings.OutputFolder.TrimEnd('/').Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
                folder = TripoPaths.OutputRoot;
            var diskRoot = folder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                ? ToDisk(folder)
                : folder;
            if (!Directory.Exists(diskRoot))
                return null;

            string best = null;
            var bestWrite = DateTime.MinValue;
            string[] patterns = { slug + ".fbx", slug + ".glb" };
            for (var p = 0; p < patterns.Length; p++)
            {
                string[] matches;
                try
                {
                    matches = Directory.GetFiles(diskRoot, patterns[p], SearchOption.AllDirectories);
                }
                catch
                {
                    continue;
                }

                for (var i = 0; i < matches.Length; i++)
                {
                    var write = File.GetLastWriteTimeUtc(matches[i]);
                    if (best == null || write > bestWrite)
                    {
                        best = ToAssetPath(matches[i]);
                        bestWrite = write;
                    }
                }

                if (!string.IsNullOrEmpty(best))
                    return best;
            }

            return null;
        }

        static string StripJobSuffix(string slug)
        {
            if (string.IsNullOrEmpty(slug))
                return slug;
            string[] suffixes = { "_ai_rig", "_rig", "_sheet", "_preview", "_model" };
            for (var i = 0; i < suffixes.Length; i++)
            {
                if (slug.EndsWith(suffixes[i], StringComparison.OrdinalIgnoreCase))
                    return slug.Substring(0, slug.Length - suffixes[i].Length);
            }

            return slug;
        }

        static bool IsLoadableAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;
            if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return false;
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null
                   || File.Exists(ToDisk(assetPath));
        }

        static string ToAssetPath(string diskPath)
        {
            if (string.IsNullOrEmpty(diskPath))
                return null;
            var full = Path.GetFullPath(diskPath).Replace('\\', '/');
            var root = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/') + "/";
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return full.Substring(root.Length);
            return diskPath.Replace('\\', '/');
        }

        internal static string ToDisk(string assetPath)
        {
            var project = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(project, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        static string DisplayName(string source, TripoGenerateOptions options)
        {
            if (options != null && !string.IsNullOrEmpty(options.OutputName))
                return TripoPaths.Sanitize(options.OutputName);
            if (string.IsNullOrEmpty(source))
                return "tripo_model";
            return TripoPaths.Sanitize(Path.GetFileNameWithoutExtension(source));
        }

        static void Set(string message, float progress, TripoJobState state, TripoJobRecord record)
        {
            StatusMessage = message;
            Progress = progress;
            if (record != null)
            {
                record.status = state.ToString();
                record.progress = Mathf.RoundToInt(progress);
                record.message = message;
                if (state == TripoJobState.Failed || state == TripoJobState.Cancelled)
                    record.error = message;
                else if (state == TripoJobState.Success)
                    record.error = string.Empty;
                TripoSession.instance.Persist();
            }

            Notify();
        }

        static void Fail(TripoJobRecord record, string message)
        {
            Debug.LogError("[Tripo3D] " + message);
            Set(message, Progress, TripoJobState.Failed, record);
        }

        static void Remember(string taskId, string name, TripoJobKind kind)
        {
            SessionState.SetString(ActiveTaskKey, taskId ?? string.Empty);
            SessionState.SetString(ActiveNameKey, name ?? string.Empty);
            SessionState.SetString(ActiveKindKey, kind.ToString());
            if (kind == TripoJobKind.ImageToModel || kind == TripoJobKind.TextToModel || kind == TripoJobKind.MultiviewToModel)
                LastModelTaskId = taskId;
            if (kind == TripoJobKind.ImageToMultiview)
                LastMultiviewTaskId = taskId;
            if (kind == TripoJobKind.PropDetect)
                LastSegmentTaskId = taskId;
        }

        static void ClearActive()
        {
            SessionState.EraseString(ActiveTaskKey);
            SessionState.EraseString(ActiveNameKey);
            SessionState.EraseString(ActiveKindKey);
        }

        static Task OnMain(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            EditorApplication.delayCall += () =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            };
            return tcs.Task;
        }

        static void Notify()
        {
            EditorApplication.delayCall += () =>
            {
                var handler = Changed;
                if (handler != null)
                    handler();
            };
        }

        static async Task EnsureCreditsAsync(CancellationToken ct)
        {
            try
            {
                var data = await TripoApiClient.GetBalanceAsync(ct);
                if (data != null && data.balance <= 0)
                    throw new TripoException("Out of Tripo credits (balance 0). Top up at platform.tripo3d.ai.", 2010, "Add credits, then click Generate again.");
            }
            catch (TripoException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Tripo3D] Could not check credit balance: " + ex.Message);
            }
        }

        public static void ClearJobs()
        {
            TripoSession.instance.Clear();
            ClearActive();
            StatusMessage = "Jobs cleared.";
            Progress = 0;
            try
            {
                var dir = TripoAiBridge.JobsDir;
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir);
                    for (var i = 0; i < files.Length; i++)
                        File.Delete(files[i]);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Tripo3D] Could not clear Temp/tripo-ai-jobs: " + ex.Message);
            }

            Notify();
        }

        [MenuItem("Tripo 3D/Clear Jobs", false, 2)]
        static void ClearJobsMenu()
        {
            ClearJobs();
        }

        [MenuItem("Tripo 3D/Refresh Job Status", false, 3)]
        static void RefreshJobsMenu()
        {
            ReconcileOpenJobs();
        }

        public static void ReconcileOpenJobs()
        {
            var jobs = TripoSession.instance.jobs;
            if (jobs == null)
                return;
            var changed = false;
            for (var i = 0; i < jobs.Count; i++)
            {
                if (SyncStuckJob(jobs[i], true))
                    changed = true;
            }

            if (changed)
            {
                TripoSession.instance.Persist();
                Notify();
            }

            EnsureWatch();
        }

        static void EnsureWatch()
        {
            if (_watching)
                return;
            _watching = true;
            EditorApplication.update += WatchTick;
        }

        static void WatchTick()
        {
            if (EditorApplication.timeSinceStartup < _nextWatch)
                return;
            _nextWatch = EditorApplication.timeSinceStartup + 2.0;
            var jobs = TripoSession.instance.jobs;
            if (jobs == null)
                return;
            var open = false;
            var changed = false;
            for (var i = 0; i < jobs.Count; i++)
            {
                var blender = string.Equals(jobs[i].kind, TripoJobKind.BlenderRig.ToString(), StringComparison.Ordinal);
                var recoverFailed = blender && jobs[i].status == TripoJobState.Failed.ToString();
                if (IsTerminal(jobs[i].status) && !recoverFailed)
                    continue;
                if (blender && SyncBlenderJob(jobs[i]))
                    changed = true;
                if (!IsTerminal(jobs[i].status))
                    open = true;
            }

            if (changed)
            {
                TripoSession.instance.Persist();
                Notify();
            }

            if (!open)
            {
                EditorApplication.update -= WatchTick;
                _watching = false;
            }
        }

        static bool SyncStuckJob(TripoJobRecord job, bool allowFailedRecovery = false)
        {
            if (job == null)
                return false;
            if (string.Equals(job.kind, TripoJobKind.BlenderRig.ToString(), StringComparison.Ordinal))
            {
                if (IsTerminal(job.status) && !(allowFailedRecovery && job.status == TripoJobState.Failed.ToString()))
                    return false;
                return SyncBlenderJob(job);
            }

            if (IsTerminal(job.status))
                return false;
            if (string.IsNullOrEmpty(job.taskId) && JobAgeMinutes(job) >= 2)
            {
                Fail(job, "Stopped before a Tripo task id was created. Often out of credits, a network error, or the editor reloaded.");
                return true;
            }

            return false;
        }

        static bool SyncBlenderJob(TripoJobRecord job)
        {
            var fileId = ResolveAiJobFileId(job);
            if (!string.IsNullOrEmpty(fileId) && fileId != job.taskId)
                job.taskId = fileId;

            var alreadyFailed = job.status == TripoJobState.Failed.ToString();
            var tokenError = ReadDispatchTokenError(fileId);
            if (!string.IsNullOrEmpty(tokenError))
            {
                if (alreadyFailed && job.error == tokenError)
                    return false;
                Fail(job, tokenError);
                return true;
            }

            var fileStatus = ReadAiJobStatus(fileId);
            if (fileStatus == "done" || fileStatus == "success" || BlenderOutputsComplete(fileId))
            {
                MarkAiJobDone(fileId);
                TryAttachRiggedAsset(job);
                if (job.status == TripoJobState.Success.ToString() && job.progress >= 100)
                    return false;
                Set("Blender rig finished.", 100f, TripoJobState.Success, job);
                return true;
            }

            if (fileStatus == "failed" || fileStatus == "error" || fileStatus == "cancelled")
            {
                if (alreadyFailed)
                    return false;
                Fail(job, "Blender rig failed. The AI agent may have run out of tokens, or Blender returned an error. Check Temp/tripo-ai-jobs/.");
                return true;
            }

            if (fileStatus == "claimed" || fileStatus == "running" || fileStatus == "working")
            {
                if (job.status == TripoJobState.Running.ToString() || job.status == TripoJobState.Rigging.ToString())
                    return false;
                Set("ChatGPT/Grok is rigging in Blender...", 40f, TripoJobState.Running, job);
                return true;
            }

            if (JobFileStaleMinutes(fileId, job) >= 12)
            {
                if (alreadyFailed)
                    return false;
                Fail(job, "Blender rig job got no AI update for 12+ minutes. The ChatGPT/Grok session likely ran out of tokens or stopped. Top up, then send the GLB again.");
                return true;
            }

            if (alreadyFailed)
            {
                Set("Blender rig queued for " + TripoSettings.AiProviderLabel + ". Waiting on that session to pick up Temp/tripo-ai-jobs/" + (fileId ?? "") + ".json.", 10f, TripoJobState.Queued, job);
                return true;
            }

            if (job.status != TripoJobState.Queued.ToString() || string.IsNullOrEmpty(job.message))
            {
                Set("Blender rig queued for " + TripoSettings.AiProviderLabel + ". Waiting on that session to pick up Temp/tripo-ai-jobs/" + (fileId ?? "") + ".json.", 10f, TripoJobState.Queued, job);
                return true;
            }

            return false;
        }

        static bool BlenderOutputsComplete(string jobFileId)
        {
            if (string.IsNullOrEmpty(jobFileId))
                return false;
            var jsonPath = Path.Combine(TripoAiBridge.JobsDir, jobFileId + ".json");
            if (!File.Exists(jsonPath))
                return false;
            string json;
            try
            {
                json = File.ReadAllText(jsonPath);
            }
            catch
            {
                return false;
            }

            var outDir = GetJsonString(json, "out");
            var slug = GetJsonString(json, "slug");
            if (string.IsNullOrEmpty(outDir) || string.IsNullOrEmpty(slug))
                return false;
            var fbx = Path.Combine(outDir, slug + ".fbx");
            var roundtrip = Path.Combine(outDir, slug + "_roundtrip_validation.json");
            if (!File.Exists(fbx) || !File.Exists(roundtrip))
                return false;
            string report;
            try
            {
                report = File.ReadAllText(roundtrip);
            }
            catch
            {
                return false;
            }

            if (string.IsNullOrEmpty(report))
                return false;
            if (GetJsonBool(report, "ok") != true)
                return false;
            return report.IndexOf("\"Idle\"", StringComparison.Ordinal) >= 0
                   && report.IndexOf("\"Run\"", StringComparison.Ordinal) >= 0
                   && report.IndexOf("\"Jump\"", StringComparison.Ordinal) >= 0
                   && report.IndexOf("\"SwordSlash\"", StringComparison.Ordinal) >= 0;
        }

        static void MarkAiJobDone(string jobFileId)
        {
            if (string.IsNullOrEmpty(jobFileId))
                return;
            var jsonPath = Path.Combine(TripoAiBridge.JobsDir, jobFileId + ".json");
            try
            {
                if (File.Exists(jsonPath) && !File.Exists(jsonPath + ".done"))
                    File.WriteAllText(jsonPath + ".done", "");
            }
            catch
            {
            }
        }

        static double JobFileStaleMinutes(string jobFileId, TripoJobRecord job)
        {
            if (!string.IsNullOrEmpty(jobFileId))
            {
                var jsonPath = Path.Combine(TripoAiBridge.JobsDir, jobFileId + ".json");
                if (File.Exists(jsonPath))
                    return (DateTime.Now - File.GetLastWriteTime(jsonPath)).TotalMinutes;
            }

            return JobAgeMinutes(job);
        }

        static string GetJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return null;
            var token = "\"" + key + "\"";
            var index = json.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
                return null;
            var colon = json.IndexOf(':', index + token.Length);
            if (colon < 0)
                return null;
            var q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0)
                return null;
            var q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0)
                return null;
            return json.Substring(q1 + 1, q2 - q1 - 1).Replace("\\\\", "\\");
        }

        static bool? GetJsonBool(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return null;
            var token = "\"" + key + "\"";
            var index = json.LastIndexOf(token, StringComparison.Ordinal);
            if (index < 0)
                return null;
            var colon = json.IndexOf(':', index + token.Length);
            if (colon < 0)
                return null;
            var rest = json.Substring(colon + 1).TrimStart();
            if (rest.StartsWith("true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (rest.StartsWith("false", StringComparison.OrdinalIgnoreCase))
                return false;
            return null;
        }

        static string ResolveAiJobFileId(TripoJobRecord job)
        {
            if (job == null)
                return null;
            if (!string.IsNullOrEmpty(job.taskId))
            {
                var named = Path.Combine(TripoAiBridge.JobsDir, job.taskId + ".json");
                if (File.Exists(named))
                    return job.taskId;
            }

            DateTime created;
            if (!TryParseJobTime(job, out created))
                return job.taskId;
            var slug = StripJobSuffix(job.name);
            if (string.IsNullOrEmpty(slug))
                slug = "model";
            for (var delta = -3; delta <= 3; delta++)
            {
                var id = created.AddSeconds(delta).ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + slug;
                if (File.Exists(Path.Combine(TripoAiBridge.JobsDir, id + ".json")))
                    return id;
            }

            return job.taskId;
        }

        static string ReadDispatchTokenError(string jobFileId)
        {
            if (string.IsNullOrEmpty(jobFileId))
                return null;
            var logPath = Path.Combine(TripoAiBridge.JobsDir, jobFileId + ".dispatch.log");
            if (!File.Exists(logPath))
                return null;
            string text;
            try
            {
                text = File.ReadAllText(logPath);
            }
            catch
            {
                return null;
            }

            if (string.IsNullOrEmpty(text))
                return null;
            var lower = text.ToLowerInvariant();
            if (lower.Contains("insufficient") || lower.Contains("out of credit") || lower.Contains("quota")
                || lower.Contains("rate limit") || lower.Contains("token") && (lower.Contains("limit") || lower.Contains("exceed") || lower.Contains("usage")))
                return "Blender rig stopped: the AI agent ran out of tokens or hit a quota. Top up ChatGPT/Grok, then send the GLB again.";
            return null;
        }

        static void TryAttachRiggedAsset(TripoJobRecord job)
        {
            if (job == null)
                return;
            var resolved = ResolveJobAssetPath(job.assetPath, job.name);
            if (!string.IsNullOrEmpty(resolved))
                job.assetPath = resolved;
            if (!string.IsNullOrEmpty(resolved) && resolved.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                LastAssetPath = resolved;
        }

        static string ReadAiJobStatus(string jobFileId)
        {
            if (string.IsNullOrEmpty(jobFileId))
                return null;
            var jsonPath = Path.Combine(TripoAiBridge.JobsDir, jobFileId + ".json");
            if (File.Exists(jsonPath + ".done"))
                return "done";
            if (File.Exists(jsonPath + ".failed") || File.Exists(jsonPath + ".error"))
                return "failed";
            if (!File.Exists(jsonPath))
                return null;
            return StatusFromJobJson(File.ReadAllText(jsonPath));
        }

        static string StatusFromJobJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;
            const string token = "\"status\"";
            var index = json.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
                return null;
            var colon = json.IndexOf(':', index + token.Length);
            if (colon < 0)
                return null;
            var q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0)
                return null;
            var q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0)
                return null;
            return json.Substring(q1 + 1, q2 - q1 - 1).ToLowerInvariant();
        }

        static bool IsTerminal(string status)
        {
            return status == TripoJobState.Success.ToString()
                   || status == TripoJobState.Failed.ToString()
                   || status == TripoJobState.Cancelled.ToString();
        }

        static double JobAgeMinutes(TripoJobRecord job)
        {
            DateTime created;
            if (!TryParseJobTime(job, out created))
                return 0;
            return (DateTime.Now - created).TotalMinutes;
        }

        static bool TryParseJobTime(TripoJobRecord job, out DateTime created)
        {
            created = DateTime.MinValue;
            if (job == null || string.IsNullOrEmpty(job.createdAt))
                return false;
            if (DateTime.TryParseExact(job.createdAt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out created))
                return true;
            return DateTime.TryParse(job.createdAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out created);
        }
    }
}
