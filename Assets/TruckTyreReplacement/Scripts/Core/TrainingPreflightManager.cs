using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TruckTyreReplacement.Core
{
    public enum TrainingPreflightState
    {
        Idle,
        Preparing,
        Ready,
        Failed
    }

    /// <summary>
    /// One progress snapshot emitted while a preflight check runs. Generic and
    /// project-agnostic - contains only counts and labels, no module-specific
    /// keys or content.
    /// </summary>
    public readonly struct TrainingPreflightUpdate
    {
        public readonly TrainingPreflightState state;
        public readonly string phaseLabel;
        public readonly int checkedCount;
        public readonly int totalCount;
        public readonly int validCount;
        public readonly int outdatedCount;
        public readonly int missingCount;
        /// <summary>Distinct (language, hash) physical assets, after content-addressed dedup.</summary>
        public readonly int physicalAssetsTotal;
        public readonly int physicalAssetsReady;
        public readonly string failureReason;

        public TrainingPreflightUpdate(TrainingPreflightState state, string phaseLabel, int checkedCount, int totalCount, int validCount, int outdatedCount, int missingCount, int physicalAssetsTotal, int physicalAssetsReady, string failureReason)
        {
            this.state = state;
            this.phaseLabel = phaseLabel;
            this.checkedCount = checkedCount;
            this.totalCount = totalCount;
            this.validCount = validCount;
            this.outdatedCount = outdatedCount;
            this.missingCount = missingCount;
            this.physicalAssetsTotal = physicalAssetsTotal;
            this.physicalAssetsReady = physicalAssetsReady;
            this.failureReason = failureReason;
        }
    }

    /// <summary>
    /// Generic training preparation ("preflight") pipeline: CHECK -> REPAIR ->
    /// VALIDATE -> READY. Validates that training data and its cached audio
    /// are ready before a training sequence is allowed to begin, and attempts
    /// to repair missing/outdated/invalid audio through the generic
    /// IRuntimeTTSService abstraction (Manager.RuntimeTTSService) before
    /// failing. Contains no sequence/task progression logic and no UI code.
    ///
    /// IMPORTANT: repair is only as capable as the assigned IRuntimeTTSService.
    /// The default (Manager's NullRuntimeTTSService) is always unavailable -
    /// there is no on-device TTS synthesis backend in this project - so on a
    /// standalone build this pipeline currently behaves as VALIDATION ONLY and
    /// reports Failed for anything that isn't already Valid, with a clear
    /// reason. It does not fabricate success.
    /// </summary>
    public class TrainingPreflightManager : MonoBehaviour
    {
        [Tooltip("Minimum time the preparation UI stays visible, even if validation finishes instantly. Avoids an unprofessional single-frame flash.")]
        [SerializeField] private float minimumDisplaySeconds = 1.0f;

        [Tooltip("How many items to check per frame before yielding, to keep the UI responsive during preparation.")]
        [SerializeField] private int itemsPerFrame = 4;

        [Tooltip("Max seconds to wait for a single audio generation before treating it as failed.")]
        [SerializeField] private float generationTimeoutSeconds = 30f;

        public event Action<TrainingPreflightUpdate> OnPreflightUpdated;

        public TrainingPreflightState State { get; private set; } = TrainingPreflightState.Idle;

        /// <summary>
        /// Runs the CHECK -> REPAIR -> VALIDATE -> READY pipeline against the
        /// given Manager. Calls onReady only if every item resolves VALID;
        /// otherwise leaves State as Failed and never calls onReady. Safe to
        /// call again after a failure (e.g. once content/cache has been fixed
        /// or a real TTS backend becomes available) - re-running while already
        /// Preparing is ignored.
        /// </summary>
        public void RunPreflight(Manager manager, Action onReady)
        {
            if (State == TrainingPreflightState.Preparing) return;
            StartCoroutine(PreflightRoutine(manager, onReady));
        }

        private IEnumerator PreflightRoutine(Manager manager, Action onReady)
        {
            State = TrainingPreflightState.Preparing;
            float startTime = Time.realtimeSinceStartup;
            Debug.Log("[PREFLIGHT START]");

            Emit(TrainingPreflightState.Preparing, "Checking training data...", 0, 0, 0, 0, 0, 0, 0, null);

            if (manager == null)
            {
                FailAndEmit("Training manager unavailable.");
                yield break;
            }

            string jsonPath = manager.GetTrainingJsonPath();
            if (!File.Exists(jsonPath))
            {
                FailAndEmit($"Training data not found at {jsonPath}");
                yield break;
            }

            yield return null;

            // ── CHECK ────────────────────────────────────────
            Emit(TrainingPreflightState.Preparing, "Checking audio cache...", 0, 0, 0, 0, 0, 0, 0, null);

            List<Manager.TrainingAudioCheckItem> items = manager.GetTrainingAudioCheckItems();
            int total = items.Count;

            if (total == 0)
            {
                FailAndEmit("Training data contains no localized speech entries to validate.");
                yield break;
            }

            var needsRepair = items.Where(i => i.status != TTSCacheStatus.Valid).ToList();
            var repairGroups = needsRepair
                .GroupBy(i => i.language + "|" + i.hash)
                .Select(g => g.ToList())
                .ToList();

            int validCount = items.Count(i => i.status == TTSCacheStatus.Valid);
            int outdatedCount = items.Count(i => i.status == TTSCacheStatus.Outdated);
            int missingCount = items.Count(i => i.status == TTSCacheStatus.Missing);
            int physicalAssetsTotal = repairGroups.Count;

            Debug.Log($"[PREFLIGHT CHECK]\nLogicalEntries = {total}\nUniqueAudioAssetsNeedingRepair = {physicalAssetsTotal}");

            for (int i = 0; i < items.Count; i++)
            {
                Emit(TrainingPreflightState.Preparing, "Checking audio cache...", i + 1, total, validCount, outdatedCount, missingCount, physicalAssetsTotal, 0, null);
                if (itemsPerFrame > 0 && i % itemsPerFrame == 0) yield return null;
            }

            // ── REPAIR ───────────────────────────────────────
            int physicalAssetsReady = 0;
            var repairService = manager.RuntimeTTSService;

            for (int g = 0; g < repairGroups.Count; g++)
            {
                var group = repairGroups[g];
                var first = group[0];

                Debug.Log($"[TTS REPAIR REQUIRED]\nLanguage = {first.language}\nHash = {first.hash}\nReason = {first.status.ToString().ToUpperInvariant()}");

                string preparingLabel = repairGroups.Count == 1
                    ? "Preparing 1 audio asset..."
                    : $"Preparing required audio... ({g + 1}/{repairGroups.Count})";
                Emit(TrainingPreflightState.Preparing, preparingLabel, total, total, validCount, outdatedCount, missingCount, physicalAssetsTotal, physicalAssetsReady, null);

                if (!repairService.IsAvailable(first.language))
                {
                    Debug.LogWarning($"[TTS GENERATION FAILED]\nLanguage = {first.language}\nHash = {first.hash}\nReason = No runtime TTS backend available for this language.");
                    continue;
                }

                string tempDir = manager.GetTempGenerationDirectory();
                string tempPath = Path.Combine(tempDir, $"{first.language}_{first.hash}_{Guid.NewGuid():N}.wav");

                Debug.Log($"[TTS GENERATION START]\nLanguage = {first.language}\nHash = {first.hash}");

                bool done = false;
                RuntimeTTSResult result = default;
                repairService.GenerateAudio(new RuntimeTTSRequest
                {
                    language = first.language,
                    speechText = first.normalizedSpeech,
                    expectedHash = first.hash,
                    outputPath = tempPath
                }, r => { result = r; done = true; });

                float waitStart = Time.realtimeSinceStartup;
                while (!done && (Time.realtimeSinceStartup - waitStart) < generationTimeoutSeconds)
                {
                    yield return null;
                }

                if (!done)
                {
                    Debug.LogWarning($"[TTS GENERATION FAILED]\nLanguage = {first.language}\nHash = {first.hash}\nReason = Generation timed out after {generationTimeoutSeconds}s");
                    continue;
                }

                if (!result.success)
                {
                    Debug.LogWarning($"[TTS GENERATION FAILED]\nLanguage = {first.language}\nHash = {first.hash}\nReason = {result.errorMessage}");
                    continue;
                }

                var keysSharingHash = group.Select(i => i.key).ToList();
                bool committed = manager.TryCommitGeneratedAudio(keysSharingHash, first.language, first.hash, tempPath, first.normalizedSpeech, out string commitFailure);

                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* best-effort cleanup */ }

                if (committed)
                {
                    physicalAssetsReady++;
                    Debug.Log($"[TTS GENERATION COMPLETE]\nLanguage = {first.language}\nHash = {first.hash}\nPath = {manager.GetTTSCachePath()}");
                }
                else
                {
                    Debug.LogWarning($"[TTS GENERATION FAILED]\nLanguage = {first.language}\nHash = {first.hash}\nReason = {commitFailure}");
                }

                yield return null;
            }

            // ── VALIDATE ─────────────────────────────────────
            var finalItems = manager.GetTrainingAudioCheckItems();
            int finalValid = finalItems.Count(i => i.status == TTSCacheStatus.Valid);
            int finalOutdated = finalItems.Count(i => i.status == TTSCacheStatus.Outdated);
            int finalMissing = finalItems.Count(i => i.status == TTSCacheStatus.Missing);
            int finalTotal = finalItems.Count;

            Debug.Log($"[PREFLIGHT PROGRESS]\nReady = {finalValid}/{finalTotal}\nPhysicalAssetsReady = {physicalAssetsReady}/{physicalAssetsTotal}");

            Emit(TrainingPreflightState.Preparing, "Validating audio cache...", finalTotal, finalTotal, finalValid, finalOutdated, finalMissing, physicalAssetsTotal, physicalAssetsReady, null);

            float elapsed = Time.realtimeSinceStartup - startTime;
            if (elapsed < minimumDisplaySeconds)
            {
                yield return new WaitForSeconds(minimumDisplaySeconds - elapsed);
            }

            if (finalMissing > 0 || finalOutdated > 0)
            {
                string reason = $"{finalMissing} missing and {finalOutdated} outdated audio file(s) out of {finalTotal}.";
                if (physicalAssetsTotal > 0 && physicalAssetsReady == 0 && !repairService.IsAvailable(items[0].language))
                {
                    reason += " No runtime TTS backend is available to repair audio on this device.";
                }
                FailAndEmit(reason, finalValid, finalOutdated, finalMissing, finalTotal, physicalAssetsTotal, physicalAssetsReady);
                yield break;
            }

            // ── READY ────────────────────────────────────────
            State = TrainingPreflightState.Ready;
            Emit(TrainingPreflightState.Ready, "Ready", finalTotal, finalTotal, finalValid, finalOutdated, finalMissing, physicalAssetsTotal, physicalAssetsReady, null);
            Debug.Log($"[PREFLIGHT READY]\nValid = {finalValid}/{finalTotal}");

            onReady?.Invoke();
        }

        private void FailAndEmit(string reason, int valid = 0, int outdated = 0, int missing = 0, int total = 0, int physicalTotal = 0, int physicalReady = 0)
        {
            State = TrainingPreflightState.Failed;
            Emit(TrainingPreflightState.Failed, "Preparation failed", total, total, valid, outdated, missing, physicalTotal, physicalReady, reason);
            Debug.LogError($"[PREFLIGHT FAILED]\nReason = {reason}");
        }

        private void Emit(TrainingPreflightState state, string phaseLabel, int checkedCount, int totalCount, int validCount, int outdatedCount, int missingCount, int physicalAssetsTotal, int physicalAssetsReady, string failureReason)
        {
            OnPreflightUpdated?.Invoke(new TrainingPreflightUpdate(state, phaseLabel, checkedCount, totalCount, validCount, outdatedCount, missingCount, physicalAssetsTotal, physicalAssetsReady, failureReason));
        }
    }
}
