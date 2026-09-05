using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tripo3D.Editor
{
    internal static class TripoApiClient
    {
        public const string BaseUrl = "https://openapi.tripo3d.ai/v3";

        public static string LastRawJson { get; private set; }

        static readonly HttpClient Http = CreateClient();

        static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(3);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public static async Task<string> UploadFileAsync(string filePath, byte[] bytes, CancellationToken ct)
        {
            RequireKey();
            var name = string.IsNullOrEmpty(filePath) ? "image.png" : Path.GetFileName(filePath);
            using (var form = new MultipartFormDataContent())
            using (var fileContent = new ByteArrayContent(bytes ?? Array.Empty<byte>()))
            {
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(GuessMime(name));
                form.Add(fileContent, "file", name);
                var json = await SendAsync(HttpMethod.Post, "/files", form, ct).ConfigureAwait(false);
                var response = TripoJson.RequireSuccess(json);
                if (string.IsNullOrEmpty(response.data.file_token))
                    throw new TripoException("Upload succeeded but no file_token was returned.");
                return response.data.file_token;
            }
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
            var inputs = "[" +
                         "{\"front\":\"" + front + "\"}," +
                         "{\"left\":\"" + left + "\"}," +
                         "{\"back\":\"" + back + "\"}," +
                         "{\"right\":\"" + right + "\"}" +
                         "]";
            var model = options != null && !string.IsNullOrEmpty(options.Model) ? options.Model : "v3.1-20260211";
            var body = "{\"inputs\":" + inputs +
                       ",\"model\":\"" + model + "\"" +
                       ",\"texture\":" + (options == null || options.Texture ? "true" : "false") +
                       ",\"pbr\":" + (options == null || options.Pbr ? "true" : "false") + "}";
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
            var json = await SendAsync(HttpMethod.Get, "/tasks/" + taskId, null, ct).ConfigureAwait(false);
            return TripoJson.RequireSuccess(json).data;
        }

        public static async Task<TripoData> GetBalanceAsync(CancellationToken ct)
        {
            var json = await SendAsync(HttpMethod.Get, "/account/balance", null, ct).ConfigureAwait(false);
            return TripoJson.RequireSuccess(json).data;
        }

        public static async Task<byte[]> DownloadAsync(string url, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(url))
                throw new TripoException("Missing download URL.");
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            using (var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new TripoException("Download failed (" + (int)response.StatusCode + "): " + Trim(text, 300));
                }

                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        static async Task<string> CreateTaskAsync(string path, string jsonBody, CancellationToken ct)
        {
            using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
            {
                var json = await SendAsync(HttpMethod.Post, path, content, ct).ConfigureAwait(false);
                var response = TripoJson.RequireSuccess(json);
                if (string.IsNullOrEmpty(response.data.task_id))
                    throw new TripoException("Tripo API did not return a task_id.");
                return response.data.task_id;
            }
        }

        static async Task<string> SendAsync(HttpMethod method, string path, HttpContent content, CancellationToken ct)
        {
            RequireKey();
            var url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : BaseUrl + path;
            using (var request = new HttpRequestMessage(method, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TripoSettings.ApiKey);
                if (content != null)
                    request.Content = content;

                HttpResponseMessage response;
                try
                {
                    response = await Http.SendAsync(request, ct).ConfigureAwait(false);
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new TripoException("The Tripo API request timed out.");
                }
                catch (HttpRequestException ex)
                {
                    throw new TripoException("Network error talking to Tripo: " + ex.Message);
                }

                using (response)
                {
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        try
                        {
                            TripoJson.RequireSuccess(json);
                        }
                        catch (TripoException)
                        {
                            throw;
                        }

                        throw new TripoException("Tripo API HTTP " + (int)response.StatusCode + ": " + Trim(json, 400));
                    }

                    LastRawJson = json;
                    return json;
                }
            }
        }

        static string BuildModelBody(string input, TripoGenerateOptions options, bool includePrompt)
        {
            options = options ?? new TripoGenerateOptions();
            var model = string.IsNullOrEmpty(options.Model) ? TripoSettings.DefaultModel : options.Model;
            var quality = string.IsNullOrEmpty(options.TextureQuality) ? "standard" : options.TextureQuality;

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
            if (!TripoSettings.HasApiKey)
                throw new TripoException("Add your Tripo API key in Tripo Studio > Settings.");
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
