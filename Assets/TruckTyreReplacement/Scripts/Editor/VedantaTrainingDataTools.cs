#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using TruckTyreReplacement.Core;

namespace TruckTyreReplacement.EditorTools
{
    /// <summary>
    /// Editor-only development/deployment utilities for the generic training
    /// framework. Never referenced by runtime code - runtime reads exclusively
    /// from Application.persistentDataPath (see Manager.TrainingJsonPath /
    /// GetTTSCachePath).
    /// </summary>
    public static class VedantaTrainingDataTools
    {
        private const string DevJsonSourcePath = "Assets/LocalTTS/TrainingData/training.json";
        private const string LegacyAudioRoot = "Assets/LocalTTS/Resources/Audio";

        [MenuItem("Vedanta Training Data/Deploy Training JSON")]
        public static void DeployTrainingJson()
        {
            DeployTrainingJsonWithResult(out _);
        }

        public static bool DeployTrainingJsonWithResult(out string failureReason)
        {
            failureReason = null;
            if (!File.Exists(DevJsonSourcePath))
            {
                failureReason = $"Source training.json not found at {DevJsonSourcePath}";
                Debug.LogError($"[VEDANTA DEPLOY] {failureReason}");
                return false;
            }

            string destPath = Path.Combine(Application.persistentDataPath, "TrainingData", "training.json");
            string destDir = Path.GetDirectoryName(destPath);

            try
            {
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(DevJsonSourcePath, destPath, overwrite: true);
                Debug.Log($"[VEDANTA DEPLOY]\nSource = {DevJsonSourcePath}\nDestination = {destPath}");
                return true;
            }
            catch (Exception ex)
            {
                failureReason = $"Failed to deploy training.json: {ex.Message}";
                Debug.LogError($"[VEDANTA DEPLOY] {failureReason}");
                return false;
            }
        }

        [MenuItem("Vedanta Training Data/Migrate Legacy Audio Cache")]
        public static void MigrateLegacyAudioCache()
        {
            MigrateLegacyAudioCacheWithResult(out _, out _, out _);
        }

        public static bool MigrateLegacyAudioCacheWithResult(out int migrated, out int skipped, out string failureReason)
        {
            migrated = 0; skipped = 0; failureReason = null;
            string destJsonPath = Path.Combine(Application.persistentDataPath, "TrainingData", "training.json");
            if (!File.Exists(destJsonPath))
            {
                failureReason = "No deployed training.json found at persistentDataPath. Run 'Deploy Training JSON' first.";
                Debug.LogWarning($"[VEDANTA MIGRATE] {failureReason}");
                return false;
            }

            string jsonText;
            try
            {
                jsonText = File.ReadAllText(destJsonPath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                failureReason = $"Failed to read deployed training.json: {ex.Message}";
                Debug.LogError($"[VEDANTA MIGRATE] {failureReason}");
                return false;
            }

            JsonRootV2 root;
            try
            {
                root = JsonUtility.FromJson<JsonRootV2>(jsonText);
            }
            catch (Exception ex)
            {
                failureReason = $"Failed to parse deployed training.json: {ex.Message}";
                Debug.LogError($"[VEDANTA MIGRATE] {failureReason}");
                return false;
            }

            if (root == null || root.entries == null)
            {
                failureReason = "Deployed training.json has no entries.";
                Debug.LogError($"[VEDANTA MIGRATE] {failureReason}");
                return false;
            }

            string cacheRoot = Path.Combine(Application.persistentDataPath, "LocalTTSCache");
            var languages = new[] { "English", "Hindi", "Odia" };
            var manifestEntries = new List<ManifestEntryForWrite>();

            foreach (var entry in root.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key)) continue;

                foreach (var lang in languages)
                {
                    string speechText = GetSpeech(entry, lang);
                    if (string.IsNullOrEmpty(speechText)) continue;

                    string legacyNormalized = NormalizeLegacy(speechText);
                    string legacyHash = LegacyMD5Hash(legacyNormalized);
                    string legacyFileName = $"{lang}_{legacyHash}.wav";
                    string legacySourcePath = Path.Combine(LegacyAudioRoot, lang, legacyFileName);

                    if (!File.Exists(legacySourcePath))
                    {
                        skipped++;
                        continue;
                    }

                    string newNormalized = LocalTTSCacheService.NormalizeSpeechText(speechText);
                    string newHash = LocalTTSCacheService.ComputeSpeechHash(lang, newNormalized);
                    string newFileName = $"{lang}_{newHash}.wav";
                    string destDir = Path.Combine(cacheRoot, lang);
                    string destPath = Path.Combine(destDir, newFileName);

                    try
                    {
                        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                        File.Copy(legacySourcePath, destPath, overwrite: true);
                        migrated++;
                        manifestEntries.Add(new ManifestEntryForWrite
                        {
                            key = entry.key,
                            language = lang,
                            textHash = newHash,
                            fileName = newFileName,
                            speechText = speechText
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[VEDANTA MIGRATE] Failed to copy legacy audio.\nKey = {entry.key}\nLanguage = {lang}\nException = {ex.Message}");
                    }
                }
            }

            WriteManifest(cacheRoot, manifestEntries);

            Debug.Log($"[VEDANTA MIGRATE]\nMigrated = {migrated}\nSkipped (no legacy audio match) = {skipped}\nCache root = {cacheRoot}");
            return true;
        }

        [MenuItem("Vedanta Training Data/Open Persistent Data Folder")]
        public static void OpenPersistentDataFolder()
        {
            string path = Application.persistentDataPath;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Vedanta Training Data/Validate Framework")]
        public static void ValidateFramework()
        {
            ValidateFrameworkWithResult(out _, out _);
        }

        public static bool ValidateFrameworkWithResult(out string report, out string failureReason)
        {
            report = ""; failureReason = null;
            var manager = UnityEngine.Object.FindFirstObjectByType<Manager>();
            if (manager == null)
            {
                failureReason = "No Manager instance found in the open scene.";
                Debug.LogWarning($"[VEDANTA VALIDATE] {failureReason}");
                return false;
            }
            report = manager.BuildFrameworkValidationReport();
            Debug.Log(report);
            return true;
        }

        // ─────────────────────────────────────────────────────
        // helpers
        // ─────────────────────────────────────────────────────

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

        private static string NormalizeLegacy(string text)
        {
            // Mirrors the EXACT legacy normalization the pre-refactor Manager
            // used (GetCacheKey), so this tool can locate old audio files by
            // their original filename hash.
            return text.Replace("\n", " ").Replace("\r", " ").Trim();
        }

        private static string LegacyMD5Hash(string text)
        {
            using (var md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(text);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes, 0, 4).Replace("-", "");
            }
        }

        private static void WriteManifest(string cacheRoot, List<ManifestEntryForWrite> newEntries)
        {
            try
            {
                if (!Directory.Exists(cacheRoot)) Directory.CreateDirectory(cacheRoot);
                string manifestPath = Path.Combine(cacheRoot, "cache_manifest.json");

                var manifest = new TTSCacheManifest();
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var existing = JsonUtility.FromJson<TTSCacheManifest>(File.ReadAllText(manifestPath, Encoding.UTF8));
                        if (existing != null && existing.entries != null) manifest = existing;
                    }
                    catch
                    {
                        // Unreadable existing manifest - start fresh rather than fail the migration.
                    }
                }

                foreach (var e in newEntries)
                {
                    var existingEntry = manifest.entries.Find(m => m.key == e.key && m.language == e.language);
                    if (existingEntry == null)
                    {
                        existingEntry = new TTSCacheManifestEntry();
                        manifest.entries.Add(existingEntry);
                    }
                    existingEntry.key = e.key;
                    existingEntry.language = e.language;
                    existingEntry.textHash = e.textHash;
                    existingEntry.fileName = e.fileName;
                    existingEntry.speechText = e.speechText;
                    existingEntry.generatedAt = DateTime.UtcNow.ToString("o");
                }

                File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VEDANTA MIGRATE] Failed to write cache manifest.\nException = {ex.Message}");
            }
        }

        private struct ManifestEntryForWrite
        {
            public string key;
            public string language;
            public string textHash;
            public string fileName;
            public string speechText;
        }

        [Serializable] private class JsonLangText { public string en; public string hi; public string or; }
        [Serializable] private class JsonEntryRaw { public string key; public JsonLangText display; public JsonLangText speech; }
        [Serializable] private class JsonRootV2 { public List<JsonEntryRaw> entries; }
    }
}
#endif
