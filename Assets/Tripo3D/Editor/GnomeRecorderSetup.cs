using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace Tripo3D.Editor
{
    public static class GnomeRecorderSetup
    {
        const string DumpPath = "Temp/gnome-recorder-dump.txt";

        [InitializeOnLoadMethod]
        static void AutoApplyIfFourK()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                Apply(false);
            };
        }

        [MenuItem("Tripo 3D/Apply X MP4 Recorder Settings")]
        public static void Execute()
        {
            Apply(true);
        }

        static void Apply(bool force)
        {
            var controller = RecorderControllerSettings.GetGlobalSettings();
            if (controller == null)
                return;

            var dump = new StringBuilder();
            dump.AppendLine("Unity Recorder — before");
            MovieRecorderSettings movie = null;
            foreach (var rec in controller.RecorderSettings)
            {
                dump.AppendLine(Describe(rec));
                movie = rec as MovieRecorderSettings ?? movie;
            }

            if (movie == null)
            {
                movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movie.name = "Movie";
                controller.AddRecorderSettings(movie);
            }

            var height = movie.ImageInputSettings != null ? movie.ImageInputSettings.OutputHeight : 0;
            if (!force && height > 0 && height <= 1080)
            {
                dump.AppendLine("Already 1080p or lower; left as-is.");
                WriteDump(dump.ToString());
                return;
            }

            controller.FrameRate = 30f;
            controller.FrameRatePlayback = FrameRatePlayback.Constant;
            controller.CapFrameRate = true;
            controller.SetRecordModeToManual();
            controller.ExitPlayMode = true;

            movie.Enabled = true;
            movie.CaptureAudio = false;
            movie.CaptureAlpha = false;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Custom,
                EncodingProfile = CoreEncoderSettings.H264EncodingProfile.High,
                TargetBitRate = 8f,
                GopSize = 30,
                NumConsecutiveBFrames = 2
            };
            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = 1920,
                OutputHeight = 1080
            };
            movie.FileNameGenerator.FileName = "GnomePreview_<Take>";

            controller.Save();

            var window = EditorWindow.GetWindow<RecorderWindow>(false, "Recorder");
            window.SetRecorderControllerSettings(controller);
            window.Focus();

            dump.AppendLine();
            dump.AppendLine("Unity Recorder — after (X / short MP4)");
            dump.AppendLine(Describe(movie));
            dump.AppendLine("Expect ~20 MB for a ~22s clip at 1080p30 / 8 Mbps H.264 High.");
            WriteDump(dump.ToString());
            Debug.Log("[Recorder] X MP4: 1920x1080, 30 fps, H.264 High, 8 Mbps, no audio. " + DumpPath);
        }

        static string Describe(RecorderSettings rec)
        {
            var sb = new StringBuilder();
            sb.AppendLine("name=" + rec.name + " enabled=" + rec.Enabled + " type=" + rec.GetType().Name);
            var movie = rec as MovieRecorderSettings;
            if (movie == null)
                return sb.ToString();
            var input = movie.ImageInputSettings;
            sb.AppendLine("  input=" + (input != null ? input.GetType().Name : "null")
                          + " " + (input != null ? input.OutputWidth + "x" + input.OutputHeight : ""));
            sb.AppendLine("  audio=" + movie.CaptureAudio + " alpha=" + movie.CaptureAlpha);
            var core = movie.EncoderSettings as CoreEncoderSettings;
            if (core != null)
            {
                sb.AppendLine("  codec=" + core.Codec
                              + " quality=" + core.EncodingQuality
                              + " profile=" + core.EncodingProfile
                              + " bitrateMbps=" + core.TargetBitRate
                              + " gop=" + core.GopSize);
            }

            return sb.ToString();
        }

        static void WriteDump(string text)
        {
            try
            {
                Directory.CreateDirectory("Temp");
                File.WriteAllText(DumpPath, text);
            }
            catch
            {
            }
        }
    }
}
