using System;
using System.Collections.Generic;
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
                var token = await TripoApiClient.UploadFileAsync(imagePath, imageBytes, ct).ConfigureAwait(false);
                if (options != null)
                    record.model = options.Model;
                Set("Creating image-to-3D task...", 12f, TripoJobState.Creating, record);
                var taskId = await TripoApiClient.ImageToModelAsync(token, options, ct).ConfigureAwait(false);
                record.taskId = taskId;
                Remember(taskId, record.name, TripoJobKind.ImageToModel);
                var task = await PollAsync(taskId, record, ct).ConfigureAwait(false);
                await ImportAsync(task, record, options, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public static async void RunTextToModel(TripoGenerateOptions options)
        {
            await Run(TripoJobKind.TextToModel, DisplayName(options.Prompt, options), async (record, ct) =>
            {
                if (options != null)
                    record.model = options.Model;
                Set("Creating text-to-3D task...", 8f, TripoJobState.Creating, record);
                var taskId = await TripoApiClient.TextToModelAsync(options, ct).ConfigureAwait(false);
                record.taskId = taskId;
                Remember(taskId, record.name, TripoJobKind.TextToModel);
                var task = await PollAsync(taskId, record, ct).ConfigureAwait(false);
                await ImportAsync(task, record, options, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);
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
                var token = await TripoApiClient.UploadFileAsync(imagePath, imageBytes, ct).ConfigureAwait(false);
                Set("Generating character sheet...", 12f, TripoJobState.Creating, record);
                var taskId = await TripoApiClient.ImageToMultiviewAsync(token, ct).ConfigureAwait(false);
                record.taskId = taskId;
                Remember(taskId, record.name, TripoJobKind.ImageToMultiview);
                LastMultiviewTaskId = taskId;
                var task = await PollAsync(taskId, record, ct).ConfigureAwait(false);
                await OnMain(() =>
                {
                    if (onViews != null)
                        onViews(task.output);
                }).ConfigureAwait(false);
                Set("Character sheet ready.", 100f, TripoJobState.Success, record);
            }).ConfigureAwait(false);
        }

        public static async void RunMultiviewToModel(string front, string left, string back, string right, TripoGenerateOptions options)
        {
            await Run(TripoJobKind.MultiviewToModel, "multiview_model", async (record, ct) =>
            {
                if (options != null)
                    record.model = options.Model;
                Set("Creating multiview-to-3D task...", 10f, TripoJobState.Creating, record);
                var taskId = await TripoApiClient.MultiviewToModelAsync(front, left, back, right, options, ct).ConfigureAwait(false);
                record.taskId = taskId;
                Remember(taskId, record.name, TripoJobKind.MultiviewToModel);
                var task = await PollAsync(taskId, record, ct).ConfigureAwait(false);
                await ImportAsync(task, record, options, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);
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
                    var modelId = await TripoApiClient.MultiviewToModelAsync(viewUrls[0], viewUrls[1], viewUrls[2], viewUrls[3], options, ct).ConfigureAwait(false);
                    record.taskId = modelId;
                    Remember(modelId, record.name, TripoJobKind.MultiviewToModel);
                    var modelTask = await PollAsync(modelId, record, ct).ConfigureAwait(false);
                    await ImportAsync(modelTask, record, options, ct).ConfigureAwait(false);
                    modelInput = modelId;
                }

                if (string.IsNullOrEmpty(modelInput) && !string.IsNullOrEmpty(LastGlbPath))
                {
                    var disk = ResolveGlbDiskPath(LastGlbPath);
                    Set("Uploading GLB for segmentation...", 10f, TripoJobState.Uploading, record);
                    var bytes = File.ReadAllBytes(disk);
                    modelInput = await TripoApiClient.UploadFileAsync(disk, bytes, ct).ConfigureAwait(false);
                }

                if (string.IsNullOrEmpty(modelInput))
                    throw new TripoException("Generate a character sheet (and 3D) first, or import a GLB, then detect props.");

                string refImage = null;
                if (imageBytes != null && imageBytes.Length > 0)
                {
                    Set("Uploading reference image for semantic parts...", 18f, TripoJobState.Uploading, record);
                    refImage = await TripoApiClient.UploadFileAsync(imagePath ?? "character.png", imageBytes, ct).ConfigureAwait(false);
                }

                Set("Segmenting mesh into props...", 25f, TripoJobState.Creating, record);
                var segId = await TripoApiClient.SegmentMeshAsync(modelInput, granularity, splitByConnectivity, refImage, ct).ConfigureAwait(false);
                record.taskId = segId;
                Remember(segId, record.name, TripoJobKind.PropDetect);
                LastSegmentTaskId = segId;
                var segTask = await PollAsync(segId, record, ct).ConfigureAwait(false);

                var names = TripoJson.ExtractPartNames(TripoApiClient.LastRawJson);
                Texture2D preview = null;
                var imported = await ImportSegmentResult(segTask, record, ct).ConfigureAwait(false);
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
                }).ConfigureAwait(false);
            }).ConfigureAwait(false);
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
                var completeId = await TripoApiClient.CompleteMeshAsync(LastSegmentTaskId, partNames, "ai_completion", ct).ConfigureAwait(false);
                record.taskId = completeId;
                var completeTask = await PollAsync(completeId, record, ct).ConfigureAwait(false);

                var results = new List<TripoPropPart>();
                for (var i = 0; i < partNames.Length; i++)
                {
                    var part = partNames[i];
                    Set("Exporting prop " + part + " (" + (i + 1) + "/" + partNames.Length + ")...", 40f + 50f * i / partNames.Length, TripoJobState.Downloading, record);
                    try
                    {
                        var convertId = await TripoApiClient.ConvertAsync(completeId, "GLB", ct, new[] { part }).ConfigureAwait(false);
                        var converted = await PollAsync(convertId, record, ct).ConfigureAwait(false);
                        var url = converted.output != null ? converted.output.BestModelUrl : null;
                        if (string.IsNullOrEmpty(url))
                            url = completeTask.output != null ? completeTask.output.BestModelUrl : null;
                        if (string.IsNullOrEmpty(url))
                            continue;

                        var glb = await TripoApiClient.DownloadAsync(url, ct).ConfigureAwait(false);
                        byte[] preview = null;
                        if (converted.output != null && !string.IsNullOrEmpty(converted.output.rendered_image_url))
                        {
                            try
                            {
                                preview = await TripoApiClient.DownloadAsync(converted.output.rendered_image_url, ct).ConfigureAwait(false);
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
                        }).ConfigureAwait(false);
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
                    var glb = await TripoApiClient.DownloadAsync(url, ct).ConfigureAwait(false);
                    await OnMain(() =>
                    {
                        var imported = WriteAssets((slug ?? "props") + "_completed", glb, null, null);
                        results.Add(new TripoPropPart
                        {
                            name = slug ?? "props",
                            selected = true,
                            assetPath = imported.assetPath
                        });
                    }).ConfigureAwait(false);
                }

                await OnMain(() =>
                {
                    if (onDone != null)
                        onDone(results.ToArray());
                    Set("Extracted " + results.Count + " prop(s).", 100f, TripoJobState.Success, record);
                }).ConfigureAwait(false);
            }).ConfigureAwait(false);
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
            Set(dispatch + "  " + Path.GetFileName(jobPath), 5f, TripoJobState.Creating, record);
            record.assetPath = ResolveJobAssetPath(LastGlbPath, slug) ?? LastGlbPath;
            TripoSession.instance.Persist();
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
                EditorUtility.DisplayDialog("Tripo Studio", "Add your Tripo API key in the Settings tab first.", "OK");
                return;
            }

            _busy = true;
            _cts = new CancellationTokenSource();
            var record = TripoSession.instance.Add(kind, name);
            Notify();
            try
            {
                await work(record, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await OnMain(() => Set("Cancelled.", Progress, TripoJobState.Cancelled, record)).ConfigureAwait(false);
            }
            catch (TripoException ex)
            {
                var message = ex.Message;
                if (!string.IsNullOrEmpty(ex.Suggestion))
                    message += " " + ex.Suggestion;
                await OnMain(() => Fail(record, message)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await OnMain(() => Fail(record, ex.Message)).ConfigureAwait(false);
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
                    }).ConfigureAwait(false);
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
                var task = await TripoApiClient.GetTaskAsync(taskId, ct).ConfigureAwait(false);
                var status = (task.status ?? string.Empty).ToLowerInvariant();
                var progress = Mathf.Clamp(task.progress, 0, 100);
                var mapped = 15f + progress * 0.7f;
                await OnMain(() => Set("Generating... " + status + " " + progress + "%", mapped, TripoJobState.Running, record)).ConfigureAwait(false);

                if (status == "success")
                    return task;
                if (status == "failed" || status == "cancelled" || status == "banned")
                {
                    var error = string.IsNullOrEmpty(task.error_message) ? "Task " + status : task.error_message;
                    throw new TripoException(error, task.error_code);
                }

                await Task.Delay(2000, ct).ConfigureAwait(false);
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
            var glb = await TripoApiClient.DownloadAsync(modelUrl, ct).ConfigureAwait(false);

            byte[] preview = null;
            if (!string.IsNullOrEmpty(output.rendered_image_url))
            {
                try
                {
                    preview = await TripoApiClient.DownloadAsync(output.rendered_image_url, ct).ConfigureAwait(false);
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
                    var convertId = await TripoApiClient.ConvertAsync(task.task_id, "FBX", ct).ConfigureAwait(false);
                    var converted = await PollAsync(convertId, record, ct).ConfigureAwait(false);
                    var fbxUrl = converted.output != null ? converted.output.BestModelUrl : null;
                    if (!string.IsNullOrEmpty(fbxUrl))
                        fbx = await TripoApiClient.DownloadAsync(fbxUrl, ct).ConfigureAwait(false);
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
            }).ConfigureAwait(false);

            if (options.RigInBlender && !string.IsNullOrEmpty(LastGlbPath))
            {
                await OnMain(() =>
                {
                    Set("Queuing Blender rig job for " + TripoSettings.AiProviderLabel + "...", 100f, TripoJobState.Rigging, record);
                    RequestAiRig(LastGlbPath, false);
                }).ConfigureAwait(false);
            }
        }

        static async Task<(string assetPath, Texture2D previewTexture)> ImportSegmentResult(TripoData task, TripoJobRecord record, CancellationToken ct)
        {
            var output = task != null ? task.output : null;
            var url = output != null ? output.BestModelUrl : null;
            if (string.IsNullOrEmpty(url))
                return (null, null);

            Set("Downloading segmented mesh...", 88f, TripoJobState.Downloading, record);
            var glb = await TripoApiClient.DownloadAsync(url, ct).ConfigureAwait(false);
            byte[] preview = null;
            if (output != null && !string.IsNullOrEmpty(output.rendered_image_url))
            {
                try
                {
                    preview = await TripoApiClient.DownloadAsync(output.rendered_image_url, ct).ConfigureAwait(false);
                }
                catch
                {
                }
            }

            var imported = default((string assetPath, string previewPath, Texture2D previewTexture, string glbPath));
            await OnMain(() =>
            {
                imported = WriteAssets("segmented_props", glb, preview, null);
            }).ConfigureAwait(false);
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
                previewPath = folder + "/" + slug + "_preview.png";
                File.WriteAllBytes(ToDisk(previewPath), preview);
                previewTexture = new Texture2D(2, 2);
                previewTexture.LoadImage(preview);
                previewTexture.name = slug + "_preview";
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
                if (state == TripoJobState.Failed || state == TripoJobState.Cancelled)
                    record.error = message;
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
    }
}
