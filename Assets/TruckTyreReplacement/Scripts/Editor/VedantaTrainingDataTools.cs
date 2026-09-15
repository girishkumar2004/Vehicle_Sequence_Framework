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
            if (!File.Exists(DevJsonSourcePath))
            {
                Debug.LogError($"[VEDANTA DEPLOY] Source training.json not found at {DevJsonSourcePath}");
                return;
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
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VEDANTA DEPLOY] Failed to deploy training.json.\nException = {ex.Message}");
            }
        }

        [MenuItem("Vedanta Training Data/Migrate Legacy Audio Cache")]
        public static void MigrateLegacyAudioCache()
        {
            string destJsonPath = Path.Combine(Application.persistentDataPath, "TrainingData", "training.json");
            if (!File.Exists(destJsonPath))
            {
                Debug.LogWarning("[VEDANTA MIGRATE] No deployed training.json found at persistentDataPath. Run 'Deploy Training JSON' first.");
                return;
            }

            string jsonText;
            try
            {
                jsonText = File.ReadAllText(destJsonPath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VEDANTA MIGRATE] Failed to read deployed training.json.\nException = {ex.Message}");
                return;
            }

            JsonRootV2 root;
            try
            {
                root = JsonUtility.FromJson<JsonRootV2>(jsonText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VEDANTA MIGRATE] Failed to parse deployed training.json.\nException = {ex.Message}");
                return;
            }

            if (root == null || root.entries == null)
            {
                Debug.LogError("[VEDANTA MIGRATE] Deployed training.json has no entries.");
                return;
            }

            string cacheRoot = Path.Combine(Application.persistentDataPath, "LocalTTSCache");
            var languages = new[] { "English", "Hindi", "Odia" };
            int migrated = 0, skipped = 0;
            var manifestEntries = new List<ManifestEntryForWrite>();

            foreach (var entry in root.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key)) continue;

                foreach (var lang in languages)
                {
                    string speechText = GetSpeech(entry, lang);
                    if (string.IsNullOrEmpty(speechText)) continue;

                    // Locate the legacy WAV using the ORIGINAL 4-byte MD5 cache-key
                    // algorithm the pre-refactor Manager used, so already-generated
                    // audio is preserved instead of left behind.
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
            var manager = UnityEngine.Object.FindFirstObjectByType<Manager>();
            if (manager == null)
            {
                Debug.LogWarning("[VEDANTA VALIDATE] No Manager instance found in the open scene.");
                return;
            }
            Debug.Log(manager.BuildFrameworkValidationReport());
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
