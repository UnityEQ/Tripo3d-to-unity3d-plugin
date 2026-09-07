using System;
using UnityEngine;

namespace Tripo3D.Editor
{
    [Serializable]
    internal class TripoResponse
    {
        public int code;
        public string status;
        public string message;
        public string suggestion;
        public TripoData data;
    }

    [Serializable]
    internal class TripoData
    {
        public string task_id;
        public string file_token;
        public string type;
        public string status;
        public int progress;
        public int error_code;
        public string error_message;
        public float credits_consumed;
        public float balance;
        public float frozen;
        public bool riggable;
        public string rig_type;
        public TripoOutput output;
    }

    [Serializable]
    internal class TripoOutput
    {
        public string model_url;
        public string pbr_model_url;
        public string base_model_url;
        public string model;
        public string pbr_model;
        public string base_model;
        public string rendered_image_url;
        public string rendered_image;
        public string front_view_url;
        public string left_view_url;
        public string back_view_url;
        public string right_view_url;

        public string BestModelUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(model_url))
                    return model_url;
                if (!string.IsNullOrEmpty(model))
                    return model;
                if (!string.IsNullOrEmpty(pbr_model_url))
                    return pbr_model_url;
                if (!string.IsNullOrEmpty(pbr_model))
                    return pbr_model;
                if (!string.IsNullOrEmpty(base_model_url))
                    return base_model_url;
                return base_model;
            }
        }

        public string PreviewUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(rendered_image_url))
                    return rendered_image_url;
                return rendered_image;
            }
        }

        public string[] AllModelUrls()
        {
            var list = new System.Collections.Generic.List<string>();
            AddUrl(list, model_url);
            AddUrl(list, model);
            AddUrl(list, pbr_model_url);
            AddUrl(list, pbr_model);
            AddUrl(list, base_model_url);
            AddUrl(list, base_model);
            return list.ToArray();
        }

        static void AddUrl(System.Collections.Generic.List<string> list, string url)
        {
            if (string.IsNullOrEmpty(url))
                return;
            for (var i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], url, StringComparison.Ordinal))
                    return;
            }
            list.Add(url);
        }
    }

    internal sealed class TripoPropPart
    {
        public string name;
        public string[] sourceNames;
        public bool selected = true;
        public string assetPath;
        public Texture2D preview;
    }

    internal enum TripoAiProvider
    {
        Grok,
        ChatGPT
    }

    internal enum TripoJobKind
    {
        ImageToModel,
        TextToModel,
        ImageToMultiview,
        MultiviewToModel,
        ConvertFbx,
        BlenderRig,
        PropDetect,
        PropExtract
    }

    internal enum TripoJobState
    {
        Idle,
        Uploading,
        Creating,
        Queued,
        Running,
        Downloading,
        Rigging,
        Importing,
        Success,
        Failed,
        Cancelled
    }

    [Serializable]
    internal class TripoJobRecord
    {
        public string id;
        public string name;
        public string kind;
        public string status;
        public string taskId;
        public string assetPath;
        public string previewPath;
        public string model;
        public string error;
        public string message;
        public string createdAt;
        public int progress;
    }

    internal sealed class TripoGenerateOptions
    {
        public string Model = TripoSettings.DefaultModel;
        public int FaceLimit = 5000;
        public bool Texture = true;
        public bool Pbr;
        public string TextureQuality = "detailed";
        public bool AutoSize = true;
        public bool EnableImageAutofix = true;
        public bool Quad;
        public bool PlaceInScene = true;
        public bool ConvertToFbx;
        public bool RigInBlender;
        public string Prompt;
        public string OutputName;
    }

    internal sealed class TripoException : Exception
    {
        public int Code { get; }
        public string Suggestion { get; }

        public TripoException(string message, int code = 0, string suggestion = null)
            : base(message)
        {
            Code = code;
            Suggestion = suggestion;
        }
    }

    internal static class TripoPaths
    {
        public const string OutputRoot = "Assets/TripoModels";
        public const string SettingsFile = "UserSettings/Tripo3D.settings.json";
        public const string StyleSheet = "Assets/Tripo3D/Editor/TripoStudio.uss";
        public const string BlenderScript = "Tools/Blender/biped_humanoid_v1/run.py";
        public const string BipedSchema = "Biped Humanoid Rig v1 - Reference.json";

        public static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "tripo_model";

            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    chars[i] = '_';
            }

            var cleaned = new string(chars).Trim('_');
            return string.IsNullOrEmpty(cleaned) ? "tripo_model" : cleaned;
        }
    }
}
