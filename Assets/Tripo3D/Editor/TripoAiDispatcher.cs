using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Tripo3D.Editor
{
    internal sealed class TripoAiDispatchResult
    {
        public bool ok;
        public string method;
        public string sessionId;
        public string sessionName;
        public string detail;
        public string error;

        public string Summary
        {
            get
            {
                var provider = TripoSettings.AiProviderLabel;
                if (!string.IsNullOrEmpty(error) && !ok)
                    return provider + " dispatch failed: " + error;
                if (!string.IsNullOrEmpty(detail))
                    return detail;
                return "Queued for " + provider + ".";
            }
        }
    }

    internal static class TripoAiDispatcher
    {
        static readonly List<Process> Running = new List<Process>();

        public static string LastSummary { get; private set; } = string.Empty;

        public static TripoAiDispatchResult Dispatch(string jobJsonPath, string promptPath)
        {
            var result = TripoSettings.AiProvider == TripoAiProvider.ChatGPT
                ? DispatchChatGpt(jobJsonPath, promptPath)
                : DispatchGrok(jobJsonPath, promptPath);

            LastSummary = result.Summary;
            try
            {
                EditorGUIUtility.systemCopyBuffer = BuildShortPrompt(jobJsonPath, promptPath);
            }
            catch
            {
            }

            UnityEngine.Debug.Log("[Tripo3D:AI] " + result.Summary);
            return result;
        }

        public static string DescribeEnvironment()
        {
            var grok = FindGrok();
            var grokLive = TryFindLiveGrokSession(out var grokId);
            var grokLine = string.IsNullOrEmpty(grok)
                ? "Grok: CLI not found"
                : grokLive
                    ? "Grok: " + grok + "  ·  open session " + ShortId(grokId)
                    : "Grok: " + grok + "  ·  no open session in this project";

            var codex = FindCodex();
            var chatLive = TryFindLiveCodexSession(out var chatId, out var chatName);
            string chatLine;
            if (string.IsNullOrEmpty(codex))
                chatLine = "ChatGPT: Codex CLI not found";
            else if (chatLive)
                chatLine = "ChatGPT: " + codex + "  ·  open session " + (string.IsNullOrEmpty(chatName) ? ShortId(chatId) : chatName);
            else
                chatLine = "ChatGPT: " + codex + "  ·  no open Codex session";

            return grokLine + "\n" + chatLine;
        }

        static TripoAiDispatchResult DispatchChatGpt(string jobJsonPath, string promptPath)
        {
            var result = new TripoAiDispatchResult();
            var exe = FindCodex();
            if (string.IsNullOrEmpty(exe))
            {
                result.method = "file-only";
                result.error = "Codex CLI not found. Install ChatGPT Codex desktop, or set Codex.exe in Settings.";
                result.detail = "Job written for ChatGPT, but Codex CLI was not found. Prompt is on the clipboard.";
                return result;
            }

            var message = BuildShortPrompt(jobJsonPath, promptPath);
            if (TryFindLiveCodexSession(out var liveId, out var liveName))
            {
                var queued = RunCapture(exe, "queue --thread " + Quote(liveId) + " --message " + Quote(message), 20000);
                if (queued.exitCode == 0)
                {
                    result.ok = true;
                    result.method = "queued-open-session";
                    result.sessionId = liveId;
                    result.sessionName = liveName;
                    result.detail = "Queued into your open ChatGPT (Codex) session"
                        + (string.IsNullOrEmpty(liveName) ? "." : ": " + liveName + ".");
                    return result;
                }

                result.error = Trim(queued.output, 400);
            }

            var last = FindLatestCodexSession();
            if (!string.IsNullOrEmpty(last.id))
            {
                var args = "exec resume " + last.id
                    + " --skip-git-repo-check --cd " + Quote(TripoBlenderRunner.ProjectRoot)
                    + " " + Quote(message);
                if (Spawn(exe, args, promptPath))
                {
                    result.ok = true;
                    result.method = "spawned-resume";
                    result.sessionId = last.id;
                    result.sessionName = last.name;
                    result.detail = "No live ChatGPT window accepted the queue, so Codex resumed session"
                        + (string.IsNullOrEmpty(last.name) ? " " + ShortId(last.id) : " \"" + last.name + "\"")
                        + " in the background.";
                    return result;
                }
            }

            var fresh = "exec --skip-git-repo-check --cd " + Quote(TripoBlenderRunner.ProjectRoot) + " " + Quote(message);
            if (Spawn(exe, fresh, promptPath))
            {
                result.ok = true;
                result.method = "spawned-new";
                result.detail = "Started a new ChatGPT (Codex) run in the background.";
                return result;
            }

            result.method = "file-only";
            result.detail = "Job written for ChatGPT. Could not start Codex. Prompt is on the clipboard.";
            if (string.IsNullOrEmpty(result.error))
                result.error = "Failed to start Codex.";
            return result;
        }

        static TripoAiDispatchResult DispatchGrok(string jobJsonPath, string promptPath)
        {
            var result = new TripoAiDispatchResult();
            var exe = FindGrok();
            var live = TryFindLiveGrokSession(out var liveId);
            if (live)
            {
                result.ok = true;
                result.method = "open-session-file";
                result.sessionId = liveId;
                result.detail = "A Grok session is already open in this project (" + ShortId(liveId)
                    + "). Keep it open — it will see Temp/tripo-ai-jobs/. Prompt copied to clipboard.";
                return result;
            }

            if (string.IsNullOrEmpty(exe))
            {
                result.method = "file-only";
                result.error = "Grok CLI not found. Keep a Grok session open, or set Grok.exe in Settings.";
                result.detail = "Job written for Grok, but grok.exe was not found. Prompt is on the clipboard.";
                return result;
            }

            var promptArg = "--prompt-file " + Quote(promptPath)
                + " --cwd " + Quote(TripoBlenderRunner.ProjectRoot)
                + " --yolo --verbatim";

            if (HasGrokHistory() && Spawn(exe, "-c " + promptArg, promptPath))
            {
                result.ok = true;
                result.method = "spawned-resume";
                result.detail = "No open Grok window, so Unity continued the last Grok session for this project.";
                return result;
            }

            if (Spawn(exe, promptArg, promptPath))
            {
                result.ok = true;
                result.method = "spawned-new";
                result.detail = "Started a new Grok run in the background.";
                return result;
            }

            result.method = "file-only";
            result.detail = "Job written for Grok. Could not start grok.exe. Prompt is on the clipboard.";
            result.error = "Failed to start Grok.";
            return result;
        }

        static string BuildShortPrompt(string jobJsonPath, string promptPath)
        {
            return
                "Unity Tripo Studio queued a biped_humanoid_v1 rig job. Do this now.\n\n"
                + "Read and follow:\n"
                + "- " + promptPath + "\n"
                + "- " + jobJsonPath + "\n\n"
                + "Drive Blender (MCP execute_blender_code and/or headless blender.exe) through inventory, skeleton, bone weights, then author Idle / Run / Jump / SwordSlash, then validate and Unity FBX export with those clips. Specs are listed in the prompt file.";
        }

        public static string FindGrok()
        {
            if (FileOk(TripoSettings.GrokPath))
                return Path.GetFullPath(TripoSettings.GrokPath);

            var env = Environment.GetEnvironmentVariable("GROK_PATH");
            if (FileOk(env))
                return Path.GetFullPath(env);

            var home = Environment.GetEnvironmentVariable("GROK_HOME");
            if (string.IsNullOrEmpty(home))
                home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grok");
            var bundled = Path.Combine(home, "bin", "grok.exe");
            if (FileOk(bundled))
                return bundled;

            return FindOnPath("grok.exe") ?? FindOnPath("grok");
        }

        public static string FindCodex()
        {
            if (FileOk(TripoSettings.CodexPath))
                return Path.GetFullPath(TripoSettings.CodexPath);

            var env = Environment.GetEnvironmentVariable("CODEX_CLI_PATH");
            if (FileOk(env))
                return Path.GetFullPath(env);

            var onPath = FindOnPath("codex.exe") ?? FindOnPath("codex");
            if (FileOk(onPath))
                return onPath;

            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var newest = NewestExe(Path.Combine(local, "OpenAI", "Codex", "bin"), "codex.exe");
            if (FileOk(newest))
                return newest;

            newest = NewestExe(Path.Combine(local, "Programs", "Codex"), "codex.exe");
            return FileOk(newest) ? newest : null;
        }

        static bool TryFindLiveCodexSession(out string id, out string name)
        {
            id = null;
            name = null;
            var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            var locksDir = Path.Combine(home, "thread-writer-locks");
            if (!Directory.Exists(locksDir))
                return false;

            var index = LoadCodexIndex(Path.Combine(home, "session_index.jsonl"));
            string bestId = null;
            DateTime bestTime = DateTime.MinValue;
            string[] locks;
            try
            {
                locks = Directory.GetFiles(locksDir, "*.lock");
            }
            catch
            {
                return false;
            }

            for (var i = 0; i < locks.Length; i++)
            {
                if (!IsFileLocked(locks[i]))
                    continue;
                var lockId = Path.GetFileNameWithoutExtension(locks[i]);
                var updated = File.GetLastWriteTimeUtc(locks[i]);
                if (index.TryGetValue(lockId, out var session) && session.updated > updated)
                    updated = session.updated;
                if (bestId == null || updated > bestTime)
                {
                    bestId = lockId;
                    bestTime = updated;
                }
            }

            if (string.IsNullOrEmpty(bestId))
                return false;

            id = bestId;
            if (index.TryGetValue(bestId, out var found))
                name = found.name;
            return true;
        }

        static (string id, string name) FindLatestCodexSession()
        {
            var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            var index = LoadCodexIndex(Path.Combine(home, "session_index.jsonl"));
            string bestId = null;
            string bestName = null;
            var bestTime = DateTime.MinValue;
            foreach (var pair in index)
            {
                if (bestId == null || pair.Value.updated > bestTime)
                {
                    bestId = pair.Key;
                    bestName = pair.Value.name;
                    bestTime = pair.Value.updated;
                }
            }

            return (bestId, bestName);
        }

        static Dictionary<string, (string name, DateTime updated)> LoadCodexIndex(string path)
        {
            var map = new Dictionary<string, (string name, DateTime updated)>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
                return map;

            try
            {
                var lines = File.ReadAllLines(path);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var id = ExtractJsonString(line, "id");
                    if (string.IsNullOrEmpty(id))
                        continue;
                    var name = ExtractJsonString(line, "thread_name");
                    var updatedRaw = ExtractJsonString(line, "updated_at");
                    DateTime updated;
                    if (!DateTime.TryParse(updatedRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out updated))
                        updated = DateTime.MinValue;
                    map[id] = (name, updated);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[Tripo3D] Could not read Codex session index: " + ex.Message);
            }

            return map;
        }

        static bool TryFindLiveGrokSession(out string id)
        {
            id = null;
            var home = Environment.GetEnvironmentVariable("GROK_HOME");
            if (string.IsNullOrEmpty(home))
                home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grok");
            var root = TripoBlenderRunner.ProjectRoot;
            var group = Path.Combine(home, "sessions", Uri.EscapeDataString(root));
            if (!Directory.Exists(group))
                group = Path.Combine(home, "sessions", Uri.EscapeDataString(root.Replace('\\', '/')));
            if (!Directory.Exists(group))
                group = Path.Combine(home, "sessions", Uri.EscapeDataString(root.Replace('/', '\\')));
            if (!Directory.Exists(group))
                return false;

            string bestId = null;
            var bestTime = DateTime.MinValue;
            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(group);
            }
            catch
            {
                return false;
            }

            for (var i = 0; i < dirs.Length; i++)
            {
                var sessionId = Path.GetFileName(dirs[i]);
                string[] locks;
                try
                {
                    locks = Directory.GetFiles(dirs[i], "*.lock", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    continue;
                }

                var locked = false;
                var newest = DateTime.MinValue;
                for (var j = 0; j < locks.Length; j++)
                {
                    if (!IsFileLocked(locks[j]))
                        continue;
                    locked = true;
                    var write = File.GetLastWriteTimeUtc(locks[j]);
                    if (write > newest)
                        newest = write;
                }

                if (!locked)
                    continue;
                if (bestId == null || newest > bestTime)
                {
                    bestId = sessionId;
                    bestTime = newest;
                }
            }

            id = bestId;
            return !string.IsNullOrEmpty(bestId);
        }

        static bool HasGrokHistory()
        {
            var home = Environment.GetEnvironmentVariable("GROK_HOME");
            if (string.IsNullOrEmpty(home))
                home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grok");
            var root = TripoBlenderRunner.ProjectRoot;
            var groups = new[]
            {
                Path.Combine(home, "sessions", Uri.EscapeDataString(root)),
                Path.Combine(home, "sessions", Uri.EscapeDataString(root.Replace('\\', '/'))),
                Path.Combine(home, "sessions", Uri.EscapeDataString(root.Replace('/', '\\')))
            };
            for (var i = 0; i < groups.Length; i++)
            {
                if (!Directory.Exists(groups[i]))
                    continue;
                try
                {
                    if (Directory.GetDirectories(groups[i]).Length > 0)
                        return true;
                }
                catch
                {
                }
            }

            return false;
        }

        static bool Spawn(string exe, string arguments, string promptPath)
        {
            try
            {
                PruneRunning();
                var logDir = Path.GetDirectoryName(promptPath);
                if (string.IsNullOrEmpty(logDir))
                    logDir = Path.Combine(TripoBlenderRunner.ProjectRoot, "Temp", "tripo-ai-jobs");
                Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, Path.GetFileNameWithoutExtension(promptPath) + ".dispatch.log");

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = TripoBlenderRunner.ProjectRoot
                };

                var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                var log = new StringBuilder();
                proc.OutputDataReceived += (s, e) =>
                {
                    if (e.Data == null)
                        return;
                    lock (log)
                        log.AppendLine(e.Data);
                };
                proc.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data == null)
                        return;
                    lock (log)
                        log.AppendLine(e.Data);
                };
                proc.Exited += (s, e) =>
                {
                    try
                    {
                        lock (log)
                            File.WriteAllText(logPath, log.ToString());
                    }
                    catch
                    {
                    }
                };

                if (!proc.Start())
                {
                    proc.Dispose();
                    return false;
                }

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                Running.Add(proc);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[Tripo3D] Failed to start " + exe + ": " + ex.Message);
                return false;
            }
        }

        static (int exitCode, string output) RunCapture(string exe, string arguments, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = TripoBlenderRunner.ProjectRoot
                };
                using (var proc = new Process { StartInfo = psi })
                {
                    var log = new StringBuilder();
                    proc.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                            log.AppendLine(e.Data);
                    };
                    proc.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                            log.AppendLine(e.Data);
                    };
                    if (!proc.Start())
                        return (-1, "failed to start");
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    if (!proc.WaitForExit(timeoutMs))
                    {
                        try { proc.Kill(); } catch { }
                        return (-1, "timed out");
                    }

                    return (proc.ExitCode, log.ToString());
                }
            }
            catch (Exception ex)
            {
                return (-1, ex.Message);
            }
        }

        static void PruneRunning()
        {
            for (var i = Running.Count - 1; i >= 0; i--)
            {
                var proc = Running[i];
                try
                {
                    if (proc == null || proc.HasExited)
                    {
                        if (proc != null)
                            proc.Dispose();
                        Running.RemoveAt(i);
                    }
                }
                catch
                {
                    Running.RemoveAt(i);
                }
            }
        }

        static bool IsFileLocked(string path)
        {
            if (!File.Exists(path))
                return false;
            try
            {
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        static string FindOnPath(string fileName)
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
                return null;
            var parts = path.Split(Path.PathSeparator);
            for (var i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;
                try
                {
                    var candidate = Path.Combine(parts[i].Trim().Trim('"'), fileName);
                    if (FileOk(candidate))
                        return Path.GetFullPath(candidate);
                }
                catch
                {
                }
            }

            return null;
        }

        static string NewestExe(string directory, string fileName)
        {
            if (!Directory.Exists(directory))
                return null;
            string newest = null;
            var best = DateTime.MinValue;
            try
            {
                var files = Directory.GetFiles(directory, fileName, SearchOption.AllDirectories);
                for (var i = 0; i < files.Length; i++)
                {
                    var write = File.GetLastWriteTimeUtc(files[i]);
                    if (newest == null || write > best)
                    {
                        newest = files[i];
                        best = write;
                    }
                }
            }
            catch
            {
                return null;
            }

            return newest;
        }

        static bool FileOk(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "(unknown)";
            return id.Length <= 12 ? id : id.Substring(0, 8);
        }

        static string Trim(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var cleaned = text.Trim();
            return cleaned.Length <= max ? cleaned : cleaned.Substring(0, max) + "...";
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
    }
}
