using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Tripo3D.Editor
{
    internal static class TripoApiClient
    {
        public const string BaseUrl = "https://openapi.tripo3d.ai/v3";

        public static string LastRawJson { get; private set; }

        static string _key;

        public static void UseKey(string key)
        {
            _key = key ?? string.Empty;
        }

        static string Key
        {
            get { return !string.IsNullOrEmpty(_key) ? _key : TripoSettings.ApiKey; }
        }

        public static async Task<string> UploadFileAsync(string filePath, byte[] bytes, CancellationToken ct)
        {
            RequireKey();
            var name = string.IsNullOrEmpty(filePath) ? "image.png" : Path.GetFileName(filePath);
            var sections = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", bytes ?? Array.Empty<byte>(), name, GuessMime(name))
            };
            var req = UnityWebRequest.Post(Url("/files"), sections);
            var json = await SendText(req, ct);
            var response = TripoJson.RequireSuccess(json);
            if (response.data == null || string.IsNullOrEmpty(response.data.file_token))
                throw new TripoException("Upload succeeded but no file_token was returned.");
            return response.data.file_token;
        }

        public static Task<string> ImageToModelAsync(string input, TripoGenerateOptions options, CancellationToken ct)
        {
            var body = BuildModelBody(input, options, includePrompt: false);
            return CreateTaskAsync("/generation/image-to-model", body, ct);
        }

        public static Task<string> TextToModelAsync(TripoGenerateOptions options, CancellationToken ct)
        {
            if (options == null || string.IsNullOrWhiteSpace(options.Prompt))
                throw new TripoException("Enter a text prompt first.");
            var body = BuildModelBody(null, options, includePrompt: true);
            return CreateTaskAsync("/generation/text-to-model", body, ct);
        }

        public static Task<string> ImageToMultiviewAsync(string input, CancellationToken ct)
        {
            var body = TripoJson.Object(("input", input));
            return CreateTaskAsync("/generation/image-to-multiview", body, ct);
        }

        public static Task<string> MultiviewToModelAsync(string front, string left, string back, string right, TripoGenerateOptions options, CancellationToken ct)
        {
            var model = options != null && !string.IsNullOrEmpty(options.Model) ? options.Model : "v3.1-20260211";
            var body = "{\"inputs\":[" +
                       "{\"front\":\"" + front + "\"}," +
                       "{\"left\":\"" + left + "\"}," +
                       "{\"back\":\"" + back + "\"}," +
                       "{\"right\":\"" + right + "\"}" +
                       "],\"model\":\"" + model + "\"" +
                       ",\"texture\":" + (options == null || options.Texture ? "true" : "false") +
                       ",\"pbr\":" + (options != null && options.Pbr ? "true" : "false") + "}";
            return CreateTaskAsync("/generation/multiview-to-model", body, ct);
        }

        public static Task<string> ConvertAsync(string input, string format, CancellationToken ct, string[] partNames = null)
        {
            var body = TripoJson.Object(
                ("input", input),
                ("format", format),
                ("texture_size", 2048),
                ("texture_format", "PNG"),
                ("pivot_to_center_bottom", true),
                ("bake", true),
                ("fbx_preset", "bake_scale"),
                ("part_names", partNames)
            );
            return CreateTaskAsync("/models/convert", body, ct);
        }

        public static Task<string> SegmentMeshAsync(string input, string granularity, bool splitByConnectivity, string refImage, CancellationToken ct)
        {
            var body = TripoJson.Object(
                ("input", input),
                ("model", "v2.0-20260430"),
                ("segmentation_granularity", string.IsNullOrEmpty(granularity) ? "balanced" : granularity),
                ("split_by_connectivity", splitByConnectivity),
                ("ref_image", string.IsNullOrEmpty(refImage) ? null : refImage)
            );
            return CreateTaskAsync("/mesh/segment", body, ct);
        }

        public static Task<string> CompleteMeshAsync(string segmentTaskId, string[] partNames, string completionMode, CancellationToken ct)
        {
            var body = TripoJson.Object(
                ("input", segmentTaskId),
                ("model", "v1.0-20250506"),
                ("part_names", partNames),
                ("completion_mode", string.IsNullOrEmpty(completionMode) ? "ai_completion" : completionMode)
            );
            return CreateTaskAsync("/mesh/complete", body, ct);
        }

        public static async Task<TripoData> GetTaskAsync(string taskId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(taskId))
                throw new TripoException("Missing task id.");
            var req = UnityWebRequest.Get(Url("/tasks/" + taskId));
            var json = await SendText(req, ct);
            return TripoJson.RequireSuccess(json).data;
        }

        public static async Task<TripoData> GetBalanceAsync(CancellationToken ct)
        {
            var req = UnityWebRequest.Get(Url("/account/balance"));
            var json = await SendText(req, ct);
            return TripoJson.RequireSuccess(json).data;
        }

        public static Task<byte[]> DownloadAsync(string url, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(url))
                throw new TripoException("Missing download URL.");
            var req = UnityWebRequest.Get(url);
            return SendBytes(req, ct);
        }

        static async Task<string> CreateTaskAsync(string path, string jsonBody, CancellationToken ct)
        {
            var json = await SendText(JsonPost(path, jsonBody), ct);
            var response = TripoJson.RequireSuccess(json);
            if (response.data == null || string.IsNullOrEmpty(response.data.task_id))
                throw new TripoException("Tripo API did not return a task_id.");
            return response.data.task_id;
        }

        static UnityWebRequest JsonPost(string path, string jsonBody)
        {
            var req = new UnityWebRequest(Url(path), UnityWebRequest.kHttpVerbPOST);
            var raw = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            return req;
        }

        static string Url(string path)
        {
            if (!string.IsNullOrEmpty(path) && path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return path;
            return BaseUrl + path;
        }

        static Task<string> SendText(UnityWebRequest req, CancellationToken ct)
        {
            return Send(req, ct, true);
        }

        static async Task<byte[]> SendBytes(UnityWebRequest req, CancellationToken ct)
        {
            try
            {
                await Send(req, ct, false);
                return req.downloadHandler != null && req.downloadHandler.data != null
                    ? req.downloadHandler.data
                    : Array.Empty<byte>();
            }
            finally
            {
                req.Dispose();
            }
        }

        static Task<string> Send(UnityWebRequest req, CancellationToken ct, bool asText)
        {
            RequireKey();
            req.timeout = 180;
            req.SetRequestHeader("Authorization", "Bearer " + Key);
            req.SetRequestHeader("Accept", "application/json");
            if (req.downloadHandler == null)
                req.downloadHandler = new DownloadHandlerBuffer();

            var tcs = new TaskCompletionSource<string>();
            var op = req.SendWebRequest();
            CancellationTokenRegistration reg = default;
            if (ct.CanBeCanceled)
            {
                reg = ct.Register(() =>
                {
                    try { req.Abort(); } catch { }
                });
            }

            op.completed += _ =>
            {
                try
                {
                    reg.Dispose();
                    var json = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                    LastRawJson = json;
                    if (ct.IsCancellationRequested)
                    {
                        tcs.TrySetCanceled();
                        return;
                    }

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        if (asText && !string.IsNullOrEmpty(json))
                        {
                            try
                            {
                                TripoJson.RequireSuccess(json);
                            }
                            catch (TripoException ex)
                            {
                                tcs.TrySetException(ex);
                                return;
                            }
                        }

                        tcs.TrySetException(new TripoException(
                            "Tripo API HTTP " + req.responseCode + ": " +
                            (string.IsNullOrEmpty(req.error) ? Trim(json, 400) : req.error)));
                        return;
                    }

                    tcs.TrySetResult(asText ? json : string.Empty);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    if (asText)
                        req.Dispose();
                }
            };

            return tcs.Task;
        }

        static string BuildModelBody(string input, TripoGenerateOptions options, bool includePrompt)
        {
            options = options ?? new TripoGenerateOptions();
            var model = string.IsNullOrEmpty(options.Model) ? TripoSettings.DefaultModel : options.Model;
            var quality = string.IsNullOrEmpty(options.TextureQuality) ? "detailed" : options.TextureQuality;

            if (includePrompt)
            {
                if (options.FaceLimit > 0)
                {
                    return TripoJson.Object(
                        ("prompt", options.Prompt),
                        ("model", model),
                        ("face_limit", options.FaceLimit),
                        ("texture", options.Texture),
                        ("pbr", options.Pbr),
                        ("texture_quality", quality),
                        ("auto_size", options.AutoSize),
                        ("quad", options.Quad)
                    );
                }

                return TripoJson.Object(
                    ("prompt", options.Prompt),
                    ("model", model),
                    ("texture", options.Texture),
                    ("pbr", options.Pbr),
                    ("texture_quality", quality),
                    ("auto_size", options.AutoSize),
                    ("quad", options.Quad)
                );
            }

            if (options.FaceLimit > 0)
            {
                return TripoJson.Object(
                    ("input", input),
                    ("model", model),
                    ("face_limit", options.FaceLimit),
                    ("texture", options.Texture),
                    ("pbr", options.Pbr),
                    ("texture_quality", quality),
                    ("auto_size", options.AutoSize),
                    ("enable_image_autofix", options.EnableImageAutofix),
                    ("quad", options.Quad)
                );
            }

            return TripoJson.Object(
                ("input", input),
                ("model", model),
                ("texture", options.Texture),
                ("pbr", options.Pbr),
                ("texture_quality", quality),
                ("auto_size", options.AutoSize),
                ("enable_image_autofix", options.EnableImageAutofix),
                ("quad", options.Quad)
            );
        }

        static void RequireKey()
        {
            if (string.IsNullOrEmpty(Key))
                throw new TripoException("Add your Tripo3D API key in Tripo Studio > Settings.");
        }

        static string GuessMime(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".webp":
                    return "image/webp";
                case ".glb":
                    return "model/gltf-binary";
                case ".png":
                default:
                    return "image/png";
            }
        }

        static string Trim(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }
    }
}
