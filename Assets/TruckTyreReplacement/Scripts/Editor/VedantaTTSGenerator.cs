#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using TruckTyreReplacement.Core;
using Debug = UnityEngine.Debug;

namespace TruckTyreReplacement.EditorTools
{
    /// <summary>
    /// Editor-only TTS regeneration workflow. Drives the existing offline
    /// MMS-TTS Python backend (Assets/LocalTTS/PythonBackend/tts_backend.py)
    /// to (re)generate WAVs for training.json entries whose speech text
    /// changed or whose cached audio is missing, using the SAME hashing
    /// (LocalTTSCacheService.ComputeSpeechHash/NormalizeSpeechText) and the
    /// SAME manifest format the runtime already uses. Never invoked by
    /// runtime code - runtime only ever reads the WAV/manifest this produces.
    /// </summary>
    public static class VedantaTTSGenerator
    {
        private static readonly string[] Languages = { "English", "Hindi", "Odia" };

        private struct CacheEntryInfo
        {
            public string key;
            public string language;
            public string speechText;
            public string normalizedText;
            public string hash;
            public TTSCacheStatus status;
        }

        [Serializable] private class JsonLangText { public string en; public string hi; public string or; }
        [Serializable] private class JsonEntryRaw { public string key; public JsonLangText display; public JsonLangText speech; }
        [Serializable] private class JsonRootV2 { public List<JsonEntryRaw> entries; }

        [MenuItem("Vedanta Training Data/Validate TTS Cache")]
        public static void ValidateTTSCache()
        {
            ValidateTTSCacheWithResult(out _, out _, out _, out _, out _);
        }

        public static bool ValidateTTSCacheWithResult(out int valid, out int outdated, out int missing, out int total, out string report)
        {
            valid = 0; outdated = 0; missing = 0; total = 0; report = "";
            var entries = LoadEntriesOrNull();
            if (entries == null)
            {
                report = "Failed to load training.json entries.";
                return false;
            }

            var cache = new LocalTTSCacheService();
            cache.LoadManifest(forceReload: true);

            var results = BuildCacheEntryInfos(entries, cache);
            total = results.Count;

            var sb = new StringBuilder();
            sb.AppendLine("[VEDANTA TTS CACHE VALIDATION]");
            foreach (var r in results)
            {
                sb.AppendLine($"{r.key} | {r.language} | {r.status.ToString().ToUpperInvariant()}");
                if (r.status == TTSCacheStatus.Valid) valid++;
                else if (r.status == TTSCacheStatus.Outdated) outdated++;
                else missing++;
            }
            sb.AppendLine();
            sb.AppendLine($"VALID={valid} OUTDATED={outdated} MISSING={missing} TOTAL={total}");
            report = sb.ToString();
            Debug.Log(report);

            return (valid == total && outdated == 0 && missing == 0 && total > 0);
        }

        [MenuItem("Vedanta Training Data/Generate Missing TTS")]
        public static void GenerateMissingTTS()
        {
            RunGenerationWithResult(includeOutdated: true, includeMissing: true, out _, out _);
        }

        [MenuItem("Vedanta Training Data/Regenerate Changed TTS")]
        public static void RegenerateChangedTTS()
        {
            RunGenerationWithResult(includeOutdated: true, includeMissing: false, out _, out _);
        }

        public static bool RunGenerationWithResult(bool includeOutdated, bool includeMissing, out string summaryReport, out string failureReason)
        {
            summaryReport = "";
            failureReason = null;

            var entries = LoadEntriesOrNull();
            if (entries == null)
            {
                failureReason = "training.json not found or invalid.";
                return false;
            }

            string venvPython = GetVenvPythonPath();
            string backendScript = GetTtsBackendScriptPath();
            if (!File.Exists(venvPython))
            {
                failureReason = $"Python venv not found at expected path: {venvPython}. Run setup_tts.bat first.";
                Debug.LogError($"[TTS GENERATION FAILED]\nReason = Python venv not found\nExpected = {venvPython}");
                return false;
            }
            if (!File.Exists(backendScript))
            {
                failureReason = $"tts_backend.py not found at expected path: {backendScript}";
                Debug.LogError($"[TTS GENERATION FAILED]\nReason = tts_backend.py not found\nExpected = {backendScript}");
                return false;
            }

            var cache = new LocalTTSCacheService();
            cache.LoadManifest(forceReload: true);

            var all = BuildCacheEntryInfos(entries, cache);
            var targets = new List<CacheEntryInfo>();
            foreach (var e in all)
            {
                if (e.status == TTSCacheStatus.Outdated && includeOutdated) targets.Add(e);
                else if (e.status == TTSCacheStatus.Missing && includeMissing) targets.Add(e);
            }

            int generated = 0, failed = 0;
            var failures = new List<string>();

            foreach (var target in targets)
            {
                string reason;
                bool ok = GenerateOne(target, cache, venvPython, backendScript, out reason);
                if (ok)
                {
                    generated++;
                }
                else
                {
                    failed++;
                    failures.Add($"key={target.key} language={target.language} reason={reason}");
                }
            }

            var final = BuildCacheEntryInfos(entries, cache);
            int validCount = 0, outdatedCount = 0, missingCount = 0;
            foreach (var e in final)
            {
                if (e.status == TTSCacheStatus.Valid) validCount++;
                else if (e.status == TTSCacheStatus.Outdated) outdatedCount++;
                else missingCount++;
            }

            var sb = new StringBuilder();
            sb.AppendLine("TTS CACHE REGENERATION COMPLETE");
            sb.AppendLine();
            sb.AppendLine($"VALID: {validCount}");
            sb.AppendLine($"GENERATED: {generated}");
            sb.AppendLine($"OUTDATED: {outdatedCount}");
            sb.AppendLine($"MISSING: {missingCount}");
            sb.AppendLine($"FAILED: {failed}");
            if (failures.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Failures:");
                foreach (var f in failures) sb.AppendLine(" - " + f);
            }
            summaryReport = sb.ToString();
            Debug.Log(summaryReport);

            if (failed > 0)
            {
                failureReason = $"{failed} audio generation task(s) failed: " + string.Join("; ", failures);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Generates a replacement WAV for one key/language using the safe
        /// transaction order: generate to a temp file, validate it, only then
        /// copy it into the final hash path and update the manifest, and only
        /// delete the previous WAV for this key/language AFTER the new one is
        /// confirmed valid. On any failure the old WAV/manifest entry are left
        /// completely untouched.
        /// </summary>
        private static bool GenerateOne(CacheEntryInfo target, LocalTTSCacheService cache, string venvPython, string backendScript, out string failureReason)
        {
            failureReason = null;

            if (!Enum.TryParse<LocalTTSLanguage>(target.language, out var langEnum))
            {
                failureReason = $"Unrecognized language '{target.language}'";
                return false;
            }
            string langCode = Manager.GetLanguageCode(langEnum);

            // Remember the previous manifest entry (if any) so we know what to
            // clean up afterward, and never touch it before success.
            var manifest = cache.LoadManifest();
            var previousEntry = manifest.entries.Find(e => e.key == target.key && e.language == target.language);
            string previousFileName = previousEntry?.fileName;

            string tempDir = Path.Combine(cache.CacheRootPath, "_generating");
            Directory.CreateDirectory(tempDir);
            string tempWavPath = Path.Combine(tempDir, $"{target.language}_{target.hash}_{Guid.NewGuid():N}.wav");
            string requestPath = Path.Combine(tempDir, $"request_{Guid.NewGuid():N}.json");

            try
            {
                string requestJson = BuildRequestJson(target.speechText, langCode, tempWavPath);
                File.WriteAllText(requestPath, requestJson, new UTF8Encoding(false));

                string pythonDir = Path.GetDirectoryName(backendScript);
                var psi = new ProcessStartInfo
                {
                    FileName = venvPython,
                    Arguments = $"\"{backendScript}\" \"{requestPath}\"",
                    WorkingDirectory = pythonDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                Debug.Log($"[TTS GENERATE] Starting generation\nKey = {target.key}\nLanguage = {target.language}\nHash = {target.hash}");

                using (var process = Process.Start(psi))
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    bool exited = process.WaitForExit(180000); // 180s - CPU VITS inference can be slow on first model load

                    if (!exited)
                    {
                        try { process.Kill(); } catch { /* best-effort */ }
                        failureReason = "Generation process timed out after 180s";
                        Debug.LogError($"[TTS GENERATION FAILED]\nKey = {target.key}\nLanguage = {target.language}\nReason = {failureReason}\nOutputPath (requested) = {tempWavPath}");
                        return false;
                    }

                    if (process.ExitCode != 0)
                    {
                        failureReason = $"Python exited with code {process.ExitCode}";
                        Debug.LogError($"[TTS GENERATION FAILED]\nKey = {target.key}\nLanguage = {target.language}\nReason = {failureReason}\nExitCode = {process.ExitCode}\nStdErr = {stderr}\nStdOut = {stdout}");
                        return false;
                    }
                }

                if (!File.Exists(tempWavPath))
                {
                    failureReason = "Generator reported success but no WAV file was produced";
                    Debug.LogError($"[TTS GENERATION FAILED]\nKey = {target.key}\nLanguage = {target.language}\nReason = {failureReason}\nExpectedPath = {tempWavPath}");
                    return false;
                }

                // Validate the generated WAV using the SAME loader runtime uses.
                byte[] wavBytes = File.ReadAllBytes(tempWavPath);
                var clip = cache.WavToAudioClip(wavBytes, $"{target.language}_{target.hash}", out string wavFailure);
                if (clip == null)
                {
                    failureReason = "Generated WAV failed validation: " + wavFailure;
                    Debug.LogError($"[TTS GENERATION FAILED]\nKey = {target.key}\nLanguage = {target.language}\nReason = {failureReason}");
                    return false;
                }
                if (clip.length <= 0f)
                {
                    failureReason = "Generated WAV validated but has zero length";
                    Debug.LogError($"[TTS GENERATION FAILED]\nKey = {target.key}\nLanguage = {target.language}\nReason = {failureReason}");
                    return false;
                }

                // Validated - now commit: write final file, then manifest.
                cache.WriteClipBytes(target.language, target.hash, wavBytes);
                string newFileName = cache.GetCacheFileName(target.language, target.hash);
                cache.UpsertManifestEntry(target.key, target.language, target.hash, newFileName, target.speechText);
                cache.SaveManifest();

                // Verify the new entry actually reports VALID before touching the old file.
                var verifyStatus = cache.GetStatus(target.key, target.language, target.hash, out _);
                if (verifyStatus != TTSCacheStatus.Valid)
                {
                    failureReason = $"Post-generation verification reported {verifyStatus} instead of Valid";
                    Debug.LogError($"[TTS GENERATION FAILED]\nKey = {target.key}\nLanguage = {target.language}\nReason = {failureReason}");
                    return false;
                }

                Debug.Log($"[TTS GENERATE] SUCCESS\nKey = {target.key}\nLanguage = {target.language}\nHash = {target.hash}\nFile = {newFileName}\nDuration = {clip.length:F2}s");

                // Only now, after the new WAV is confirmed valid and the manifest
                // points to it, remove the old (now-stale) file if it differs.
                if (!string.IsNullOrEmpty(previousFileName) && previousFileName != newFileName)
                {
                    string oldPath = Path.Combine(cache.CacheRootPath, target.language, previousFileName);
                    if (File.Exists(oldPath))
                    {
                        File.Delete(oldPath);
                        Debug.Log($"[TTS GENERATE] Removed stale WAV\nKey = {target.key}\nLanguage = {target.language}\nFile = {previousFileName}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                failureReason = "Exception: " + ex.Message;
                Debug.LogError($"[TTS GENERATION FAILED]\nKey = {target.key}\nLanguage = {target.language}\nReason = {failureReason}");
                return false;
            }
            finally
            {
                try { if (File.Exists(requestPath)) File.Delete(requestPath); } catch { /* best-effort cleanup */ }
                try { if (File.Exists(tempWavPath)) File.Delete(tempWavPath); } catch { /* best-effort cleanup */ }
            }
        }

        private static string BuildRequestJson(string text, string langCode, string outputPath)
        {
            // Minimal, safe JSON string escaping for a flat {text, language, output_path} object.
            string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
            string outputPathJson = outputPath.Replace("\\", "/");
            return "{\"text\":\"" + Escape(text) + "\",\"language\":\"" + langCode + "\",\"output_path\":\"" + Escape(outputPathJson) + "\"}";
        }

        // ─────────────────────────────────────────────────────
        // Status scanning (shared by Validate/Generate/Regenerate)
        // ─────────────────────────────────────────────────────

        private static List<CacheEntryInfo> BuildCacheEntryInfos(List<JsonEntryRaw> entries, LocalTTSCacheService cache)
        {
            var results = new List<CacheEntryInfo>();
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key)) continue;
                foreach (var lang in Languages)
                {
                    string speech = GetSpeech(entry, lang);
                    if (string.IsNullOrEmpty(speech)) continue;

                    string normalized = LocalTTSCacheService.NormalizeSpeechText(speech);
                    string hash = LocalTTSCacheService.ComputeSpeechHash(lang, normalized);
                    var status = cache.GetStatus(entry.key, lang, hash, out _);

                    results.Add(new CacheEntryInfo
                    {
                        key = entry.key,
                        language = lang,
                        speechText = speech,
                        normalizedText = normalized,
                        hash = hash,
                        status = status
                    });
                }
            }
            return results;
        }

        private static void PrintStatusReport(List<CacheEntryInfo> results)
        {
            int valid = 0, outdated = 0, missing = 0;
            var sb = new StringBuilder();
            sb.AppendLine("[VEDANTA TTS CACHE VALIDATION]");
            foreach (var r in results)
            {
                sb.AppendLine($"{r.key} | {r.language} | {r.status.ToString().ToUpperInvariant()}");
                if (r.status == TTSCacheStatus.Valid) valid++;
                else if (r.status == TTSCacheStatus.Outdated) outdated++;
                else missing++;
            }
            sb.AppendLine();
            sb.AppendLine($"VALID={valid} OUTDATED={outdated} MISSING={missing} TOTAL={results.Count}");
            Debug.Log(sb.ToString());
        }

        private static string GetSpeech(JsonEntryRaw entry, string lang)
        {
            if (entry?.speech == null) return null;
            switch (lang)
            {
                case "Hindi": return entry.speech.hi;
                case "Odia": return entry.speech.or;
                default: return entry.speech.en;
            }
        }

        // ─────────────────────────────────────────────────────
        // Paths (no machine-specific usernames; project-relative and
        // Application.persistentDataPath only)
        // ─────────────────────────────────────────────────────

        private static List<JsonEntryRaw> LoadEntriesOrNull()
        {
            string jsonPath = Path.Combine(Application.persistentDataPath, "TrainingData", "training.json");
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[VEDANTA TTS] training.json not found at {jsonPath}. Run 'Deploy Training JSON' first.");
                return null;
            }

            string jsonText;
            try
            {
                jsonText = File.ReadAllText(jsonPath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VEDANTA TTS] Failed to read training.json: {ex.Message}");
                return null;
            }

            JsonRootV2 root;
            try
            {
                root = JsonUtility.FromJson<JsonRootV2>(jsonText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VEDANTA TTS] Failed to parse training.json: {ex.Message}");
                return null;
            }

            if (root?.entries == null)
            {
                Debug.LogError("[VEDANTA TTS] training.json has no entries.");
                return null;
            }

            return root.entries;
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string GetVenvPythonPath()
        {
            return Path.Combine(GetProjectRoot(), "Assets", "LocalTTS", "PythonBackend", ".venv", "Scripts", "python.exe");
        }

        private static string GetTtsBackendScriptPath()
        {
            return Path.Combine(GetProjectRoot(), "Assets", "LocalTTS", "PythonBackend", "tts_backend.py");
        }
    }
}
#endif
