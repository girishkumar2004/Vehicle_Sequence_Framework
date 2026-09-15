using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace TruckTyreReplacement.Core
{
    public enum TTSCacheStatus
    {
        Valid,
        Outdated,
        Missing
    }

    [Serializable]
    public class TTSCacheManifestEntry
    {
        public string key;
        public string language;
        public string textHash;
        public string fileName;
        public string speechText;
        public string generatedAt;
    }

    [Serializable]
    public class TTSCacheManifest
    {
        public List<TTSCacheManifestEntry> entries = new List<TTSCacheManifestEntry>();
    }

    /// <summary>
    /// Generic, project-agnostic TTS audio cache service.
    /// Resolves cached WAV files under Application.persistentDataPath/LocalTTSCache,
    /// keyed by SHA-256(language + "|" + normalizedSpeechText). Contains no
    /// training-module-specific knowledge.
    /// </summary>
    public class LocalTTSCacheService
    {
        private const string CacheRootFolder = "LocalTTSCache";
        private const string ManifestFileName = "cache_manifest.json";

        private readonly Dictionary<string, AudioClip> memoryCache = new Dictionary<string, AudioClip>();
        private TTSCacheManifest manifest;

        public string CacheRootPath => Path.Combine(Application.persistentDataPath, CacheRootFolder);
        public string ManifestPath => Path.Combine(CacheRootPath, ManifestFileName);

        public static string NormalizeSpeechText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
        }

        public static string ComputeSpeechHash(string language, string normalizedSpeechText)
        {
            using (var sha = SHA256.Create())
            {
                byte[] input = Encoding.UTF8.GetBytes(language + "|" + normalizedSpeechText);
                byte[] hash = sha.ComputeHash(input);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("X2"));
                return sb.ToString();
            }
        }

        public string GetCacheFileName(string language, string speechHash) => $"{language}_{speechHash}.wav";

        public string GetCacheFilePath(string language, string speechHash)
        {
            return Path.Combine(CacheRootPath, language, GetCacheFileName(language, speechHash));
        }

        public string GetMemoryCacheKey(string language, string speechHash) => language + "|" + speechHash;

        // ─────────────────────────────────────────────────────
        // MANIFEST
        // ─────────────────────────────────────────────────────

        public void EnsureCacheDirectories()
        {
            try
            {
                if (!Directory.Exists(CacheRootPath))
                {
                    Directory.CreateDirectory(CacheRootPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TTS CACHE ERROR]\nReason = Failed to create cache root directory\nPath = {CacheRootPath}\nException = {ex.Message}");
            }
        }

        public TTSCacheManifest LoadManifest(bool forceReload = false)
        {
            if (manifest != null && !forceReload) return manifest;

            manifest = new TTSCacheManifest();
            try
            {
                if (File.Exists(ManifestPath))
                {
                    string json = File.ReadAllText(ManifestPath, Encoding.UTF8);
                    var loaded = JsonUtility.FromJson<TTSCacheManifest>(json);
                    if (loaded != null && loaded.entries != null)
                    {
                        manifest = loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TTS CACHE ERROR]\nReason = Failed to read cache manifest\nPath = {ManifestPath}\nException = {ex.Message}");
            }
            return manifest;
        }

        public void SaveManifest()
        {
            if (manifest == null) return;
            try
            {
                EnsureCacheDirectories();
                string json = JsonUtility.ToJson(manifest, true);
                File.WriteAllText(ManifestPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TTS CACHE ERROR]\nReason = Failed to write cache manifest\nPath = {ManifestPath}\nException = {ex.Message}");
            }
        }

        public void UpsertManifestEntry(string key, string language, string speechHash, string fileName, string speechText)
        {
            var m = LoadManifest();
            var existing = m.entries.Find(e => e.key == key && e.language == language);
            if (existing == null)
            {
                existing = new TTSCacheManifestEntry();
                m.entries.Add(existing);
            }
            existing.key = key;
            existing.language = language;
            existing.textHash = speechHash;
            existing.fileName = fileName;
            existing.speechText = speechText;
            existing.generatedAt = DateTime.UtcNow.ToString("o");
        }

        // ─────────────────────────────────────────────────────
        // CACHE STATUS
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Resolves cache validity from BOTH the manifest and disk. File existence
        /// alone is not sufficient, and manifest existence alone is not sufficient -
        /// both must agree on the current speech hash and the file must be present.
        /// </summary>
        public TTSCacheStatus GetStatus(string key, string language, string currentSpeechHash, out string expectedFilePath)
        {
            expectedFilePath = GetCacheFilePath(language, currentSpeechHash);
            var m = LoadManifest();
            var manifestEntry = m.entries.Find(e => e.key == key && e.language == language);

            bool fileExists = File.Exists(expectedFilePath);

            if (manifestEntry == null)
            {
                return fileExists ? TTSCacheStatus.Valid : TTSCacheStatus.Missing;
            }

            if (manifestEntry.textHash != currentSpeechHash)
            {
                return TTSCacheStatus.Outdated;
            }

            return fileExists ? TTSCacheStatus.Valid : TTSCacheStatus.Missing;
        }

        // ─────────────────────────────────────────────────────
        // MEMORY CACHE
        // ─────────────────────────────────────────────────────

        public bool TryGetMemoryClip(string language, string speechHash, out AudioClip clip)
        {
            return memoryCache.TryGetValue(GetMemoryCacheKey(language, speechHash), out clip);
        }

        public void SetMemoryClip(string language, string speechHash, AudioClip clip)
        {
            memoryCache[GetMemoryCacheKey(language, speechHash)] = clip;
        }

        public void InvalidateMemoryClip(string language, string speechHash)
        {
            memoryCache.Remove(GetMemoryCacheKey(language, speechHash));
        }

        public void ClearMemoryCache()
        {
            memoryCache.Clear();
        }

        // ─────────────────────────────────────────────────────
        // DISK LOAD
        // ─────────────────────────────────────────────────────

        public AudioClip LoadClipFromDisk(string language, string speechHash, string clipName)
        {
            string path = GetCacheFilePath(language, speechHash);
            if (!File.Exists(path))
            {
                return null;
            }

            byte[] wavBytes;
            try
            {
                wavBytes = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TTS CACHE ERROR]\nKey = {clipName}\nLanguage = {language}\nPath = {path}\nReason = Failed to read file\nException = {ex.Message}");
                return null;
            }

            var clip = WavToAudioClip(wavBytes, clipName, out string failureReason);
            if (clip == null)
            {
                Debug.LogError($"[TTS CACHE ERROR]\nKey = {clipName}\nLanguage = {language}\nPath = {path}\nReason = {failureReason}");
            }
            return clip;
        }

        /// <summary>
        /// Minimal but validated PCM WAV parser. Returns null with a diagnostic
        /// reason (via out param) instead of silently swallowing failures.
        /// </summary>
        public AudioClip WavToAudioClip(byte[] wavBytes, string clipName, out string failureReason)
        {
            failureReason = null;

            if (wavBytes == null || wavBytes.Length < 44)
            {
                failureReason = "File too small to be a valid WAV (< 44 byte header)";
                return null;
            }

            if (wavBytes[0] != 'R' || wavBytes[1] != 'I' || wavBytes[2] != 'F' || wavBytes[3] != 'F' ||
                wavBytes[8] != 'W' || wavBytes[9] != 'A' || wavBytes[10] != 'V' || wavBytes[11] != 'E')
            {
                failureReason = "Invalid WAV header (missing RIFF/WAVE markers)";
                return null;
            }

            try
            {
                int channels = BitConverter.ToInt16(wavBytes, 22);
                int sampleRate = BitConverter.ToInt32(wavBytes, 24);
                int bitsPerSample = BitConverter.ToInt16(wavBytes, 34);
                int bytesPerSample = bitsPerSample / 8;

                if (channels <= 0 || sampleRate <= 0 || (bytesPerSample != 1 && bytesPerSample != 2))
                {
                    failureReason = $"Unsupported WAV format (channels={channels}, sampleRate={sampleRate}, bitsPerSample={bitsPerSample})";
                    return null;
                }

                int pos = 12;
                bool foundData = false;
                while (pos < wavBytes.Length - 8)
                {
                    if (wavBytes[pos] == 'd' && wavBytes[pos + 1] == 'a' && wavBytes[pos + 2] == 't' && wavBytes[pos + 3] == 'a')
                    {
                        pos += 4;
                        foundData = true;
                        break;
                    }
                    pos++;
                }

                if (!foundData)
                {
                    failureReason = "Invalid WAV structure (no 'data' chunk found)";
                    return null;
                }

                int dataSize = BitConverter.ToInt32(wavBytes, pos);
                pos += 4;

                if (dataSize <= 0 || pos + dataSize > wavBytes.Length)
                {
                    failureReason = $"Invalid WAV data chunk size ({dataSize} bytes, buffer has {wavBytes.Length - pos} remaining)";
                    return null;
                }

                int totalSamples = dataSize / bytesPerSample;
                int samplesPerChannel = totalSamples / channels;

                if (samplesPerChannel <= 0)
                {
                    failureReason = "WAV data chunk contains no samples";
                    return null;
                }

                float[] samples = new float[totalSamples];
                for (int i = 0; i < totalSamples; i++)
                {
                    if (bytesPerSample == 2)
                        samples[i] = BitConverter.ToInt16(wavBytes, pos + i * 2) / 32768f;
                    else
                        samples[i] = (wavBytes[pos + i] - 128) / 128f;
                }

                AudioClip clip = AudioClip.Create(clipName, samplesPerChannel, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }
            catch (Exception ex)
            {
                failureReason = $"Exception while parsing WAV: {ex.Message}";
                return null;
            }
        }

        public void WriteClipBytes(string language, string speechHash, byte[] wavBytes)
        {
            EnsureCacheDirectories();
            string dir = Path.Combine(CacheRootPath, language);
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string path = GetCacheFilePath(language, speechHash);
                File.WriteAllBytes(path, wavBytes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TTS CACHE ERROR]\nLanguage = {language}\nHash = {speechHash}\nReason = Failed to write cache file\nException = {ex.Message}");
            }
        }
    }
}
