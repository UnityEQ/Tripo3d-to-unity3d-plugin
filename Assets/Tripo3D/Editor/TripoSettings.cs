using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tripo3D.Editor
{
    [InitializeOnLoad]
    internal static class TripoSettings
    {
        public const string DefaultModel = "v3.1-20260211";
        public const string BestModel = "v3.1-20260211";

        const string PrefsApiKey = "Tripo3D.ApiKey";
        const string PrefsModel = "Tripo3D.Model";
        const string PrefsFaceLimit = "Tripo3D.FaceLimit";
        const string PrefsTexture = "Tripo3D.Texture";
        const string PrefsPbr = "Tripo3D.Pbr";
        const string PrefsTextureQuality = "Tripo3D.TextureQuality";
        const string PrefsAutoSize = "Tripo3D.AutoSize";
        const string PrefsAutofix = "Tripo3D.Autofix";
        const string PrefsPlace = "Tripo3D.PlaceInScene";
        const string PrefsFbx = "Tripo3D.ConvertToFbx";
        const string PrefsOutput = "Tripo3D.OutputFolder";
        const string PrefsBlender = "Tripo3D.BlenderPath";
        const string PrefsAutoRig = "Tripo3D.RigInBlender";
        const string PrefsIk = "Tripo3D.BlenderIk";
        const string PrefsAiProvider = "Tripo3D.AiProvider";
        const string PrefsGrokPath = "Tripo3D.GrokPath";
        const string PrefsCodexPath = "Tripo3D.CodexPath";
        const string PrefsGenDefaults = "Tripo3D.GenDefaultsV2";

        public static readonly string[] Models =
        {
            "v3.1-20260211",
            "P1-20260311",
            "P2-20260801",
            "v3.0-20250812"
        };

        public static readonly string[] TextureQualities =
        {
            "standard",
            "detailed",
            "extreme"
        };

        static TripoSettings()
        {
            LoadApiKeyFromDiskIfEmpty();
            if (EditorPrefs.GetInt(PrefsGenDefaults, 0) < 2)
                ApplyImageTo3dDefaults();
        }

        public static void ApplyImageTo3dDefaults()
        {
            Model = BestModel;
            Pbr = false;
            TextureQuality = "detailed";
            EditorPrefs.SetInt(PrefsGenDefaults, 2);
        }

        public static string ApiKey
        {
            get => EditorPrefs.GetString(PrefsApiKey, string.Empty);
            set => EditorPrefs.SetString(PrefsApiKey, value ?? string.Empty);
        }

        public static string Model
        {
            get => EditorPrefs.GetString(PrefsModel, DefaultModel);
            set => EditorPrefs.SetString(PrefsModel, string.IsNullOrEmpty(value) ? DefaultModel : value);
        }

        public static int FaceLimit
        {
            get => EditorPrefs.GetInt(PrefsFaceLimit, 5000);
            set => EditorPrefs.SetInt(PrefsFaceLimit, Mathf.Clamp(value, 0, 2000000));
        }

        public static bool Texture
        {
            get => EditorPrefs.GetBool(PrefsTexture, true);
            set => EditorPrefs.SetBool(PrefsTexture, value);
        }

        public static bool Pbr
        {
            get => EditorPrefs.GetBool(PrefsPbr, false);
            set => EditorPrefs.SetBool(PrefsPbr, value);
        }

        public static string TextureQuality
        {
            get => EditorPrefs.GetString(PrefsTextureQuality, "detailed");
            set => EditorPrefs.SetString(PrefsTextureQuality, string.IsNullOrEmpty(value) ? "detailed" : value);
        }

        public static bool AutoSize
        {
            get => EditorPrefs.GetBool(PrefsAutoSize, true);
            set => EditorPrefs.SetBool(PrefsAutoSize, value);
        }

        public static bool EnableImageAutofix
        {
            get => EditorPrefs.GetBool(PrefsAutofix, true);
            set => EditorPrefs.SetBool(PrefsAutofix, value);
        }

        public static bool PlaceInScene
        {
            get => EditorPrefs.GetBool(PrefsPlace, true);
            set => EditorPrefs.SetBool(PrefsPlace, value);
        }

        public static bool ConvertToFbx
        {
            get => EditorPrefs.GetBool(PrefsFbx, false);
            set => EditorPrefs.SetBool(PrefsFbx, value);
        }

        public static string BlenderPath
        {
            get => EditorPrefs.GetString(PrefsBlender, string.Empty);
            set => EditorPrefs.SetString(PrefsBlender, value ?? string.Empty);
        }

        public static bool RigInBlender
        {
            get => EditorPrefs.GetBool(PrefsAutoRig, false);
            set => EditorPrefs.SetBool(PrefsAutoRig, value);
        }

        public static bool BlenderIk
        {
            get => EditorPrefs.GetBool(PrefsIk, true);
            set => EditorPrefs.SetBool(PrefsIk, value);
        }

        public static TripoAiProvider AiProvider
        {
            get
            {
                var raw = EditorPrefs.GetString(PrefsAiProvider, "Grok");
                return string.Equals(raw, "ChatGPT", StringComparison.OrdinalIgnoreCase)
                    ? TripoAiProvider.ChatGPT
                    : TripoAiProvider.Grok;
            }
            set => EditorPrefs.SetString(PrefsAiProvider, value == TripoAiProvider.ChatGPT ? "ChatGPT" : "Grok");
        }

        public static string AiProviderLabel
        {
            get { return AiProvider == TripoAiProvider.ChatGPT ? "ChatGPT" : "Grok"; }
        }

        public static string GrokPath
        {
            get => EditorPrefs.GetString(PrefsGrokPath, string.Empty);
            set => EditorPrefs.SetString(PrefsGrokPath, value ?? string.Empty);
        }

        public static string CodexPath
        {
            get => EditorPrefs.GetString(PrefsCodexPath, string.Empty);
            set => EditorPrefs.SetString(PrefsCodexPath, value ?? string.Empty);
        }

        public static string OutputFolder
        {
            get
            {
                var folder = EditorPrefs.GetString(PrefsOutput, TripoPaths.OutputRoot);
                return string.IsNullOrEmpty(folder) ? TripoPaths.OutputRoot : folder.Replace('\\', '/');
            }
            set => EditorPrefs.SetString(PrefsOutput, string.IsNullOrEmpty(value) ? TripoPaths.OutputRoot : value.Replace('\\', '/'));
        }

        public static bool HasApiKey => !string.IsNullOrEmpty(ApiKey);

        public static TripoGenerateOptions CurrentOptions()
        {
            return new TripoGenerateOptions
            {
                Model = Model,
                FaceLimit = FaceLimit,
                Texture = Texture,
                Pbr = Pbr,
                TextureQuality = TextureQuality,
                AutoSize = AutoSize,
                EnableImageAutofix = EnableImageAutofix,
                PlaceInScene = PlaceInScene,
                ConvertToFbx = ConvertToFbx,
                RigInBlender = RigInBlender
            };
        }

        public static void SaveApiKeyToDisk()
        {
            try
            {
                var path = Path.GetFullPath(TripoPaths.SettingsFile);
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = "{\"apiKey\":\"" + Escape(ApiKey) + "\"}";
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Tripo3D] Could not save local settings: " + ex.Message);
            }
        }

        static void LoadApiKeyFromDiskIfEmpty()
        {
            if (!string.IsNullOrEmpty(ApiKey))
                return;

            var env = Environment.GetEnvironmentVariable("TRIPO_API_KEY");
            if (!string.IsNullOrEmpty(env))
            {
                ApiKey = env.Trim();
                return;
            }

            var path = Path.GetFullPath(TripoPaths.SettingsFile);
            if (!File.Exists(path))
                return;

            try
            {
                var json = File.ReadAllText(path);
                var key = ExtractJsonString(json, "apiKey");
                if (!string.IsNullOrEmpty(key))
                    ApiKey = key;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Tripo3D] Could not read local settings: " + ex.Message);
            }
        }

        static string ExtractJsonString(string json, string field)
        {
            var token = "\"" + field + "\"";
            var index = json.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
                return null;

            var colon = json.IndexOf(':', index + token.Length);
            if (colon < 0)
                return null;

            var start = json.IndexOf('"', colon + 1);
            if (start < 0)
                return null;

            var end = json.IndexOf('"', start + 1);
            if (end < 0)
                return null;

            return json.Substring(start + 1, end - start - 1);
        }

        static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
