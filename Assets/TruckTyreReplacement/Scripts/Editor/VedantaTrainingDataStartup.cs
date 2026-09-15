#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TruckTyreReplacement.EditorTools
{
    /// <summary>
    /// Automatic Editor Startup Orchestrator for the Vedanta Training Data Pipeline.
    /// Runs the 5-stage Editor preparation workflow automatically upon Editor project startup:
    ///   1. Generate Missing TTS
    ///   2. Deploy Training TTS
    ///   3. Migrate Legacy Audio Cache
    ///   4. Validate TTS
    ///   5. Validate Framework
    ///
    /// SESSION & BUILD SAFETY:
    ///   - Protected by SessionState so it executes ONCE per Editor startup session (not on domain reloads, script recompiles, or Play Mode transitions).
    ///   - Protected by #if UNITY_EDITOR - never compiled into Android/Quest runtime builds.
    /// </summary>
    [InitializeOnLoad]
    public static class VedantaTrainingDataStartup
    {
        private const string SessionStateKey = "VedantaTrainingDataStartupExecuted";

        static VedantaTrainingDataStartup()
        {
            EditorApplication.delayCall += TryAutoStartPipeline;
        }

        private static void TryAutoStartPipeline()
        {
            if (SessionState.GetBool(SessionStateKey, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryAutoStartPipeline;
                return;
            }

            SessionState.SetBool(SessionStateKey, true);
            RunPipeline(isManual: false);
        }

        [MenuItem("Tools/Vedanta/Prepare Training Data", false, 10)]
        [MenuItem("Vedanta Training Data/Prepare Training Data (Full Pipeline)", false, 0)]
        public static void RunPipelineManual()
        {
            RunPipeline(isManual: true);
        }

        public static bool RunPipeline(bool isManual)
        {
            Debug.Log("[VEDANTA STARTUP] Preparation started" + (isManual ? " (Manual Request)" : " (Auto Startup)"));

            // -- STEP 1/5: Generate Missing TTS --------------------------------------
            Debug.Log("[VEDANTA STARTUP] Step 1/5 Generate Missing TTS");
            bool step1Ok = VedantaTTSGenerator.RunGenerationWithResult(includeOutdated: true, includeMissing: true, out string genSummary, out string genFailure);
            if (!step1Ok)
            {
                ReportFailure("Generate Missing TTS", genFailure ?? "TTS generation encountered errors.");
                return false;
            }
            Debug.Log("[VEDANTA STARTUP] Step 1/5 COMPLETE");

            // -- STEP 2/5: Deploy Training TTS ----------------------------------------
            Debug.Log("[VEDANTA STARTUP] Step 2/5 Deploy Training TTS");
            bool step2Ok = VedantaTrainingDataTools.DeployTrainingJsonWithResult(out string deployFailure);
            if (!step2Ok)
            {
                ReportFailure("Deploy Training TTS", deployFailure ?? "Failed to deploy training JSON.");
                return false;
            }
            Debug.Log("[VEDANTA STARTUP] Step 2/5 COMPLETE");

            // -- STEP 3/5: Migrate Legacy Audio Cache --------------------------------
            Debug.Log("[VEDANTA STARTUP] Step 3/5 Migrate Legacy Audio Cache");
            bool step3Ok = VedantaTrainingDataTools.MigrateLegacyAudioCacheWithResult(out int migrated, out int skipped, out string migrateFailure);
            if (!step3Ok)
            {
                ReportFailure("Migrate Legacy Audio Cache", migrateFailure ?? "Legacy audio migration failed.");
                return false;
            }
            Debug.Log("[VEDANTA STARTUP] Step 3/5 COMPLETE");

            // -- STEP 4/5: Validate TTS ----------------------------------------------
            Debug.Log("[VEDANTA STARTUP] Step 4/5 Validate TTS");
            bool step4Ok = VedantaTTSGenerator.ValidateTTSCacheWithResult(out int validCount, out int outdatedCount, out int missingCount, out int totalCount, out string valReport);
            if (!step4Ok || validCount < totalCount)
            {
                string valReason = $"TTS cache validation failed: {validCount}/{totalCount} valid, {outdatedCount} outdated, {missingCount} missing.";
                ReportFailure("Validate TTS", valReason);
                return false;
            }
            Debug.Log("[VEDANTA STARTUP] Step 4/5 COMPLETE");

            // -- STEP 5/5: Validate Framework ----------------------------------------
            Debug.Log("[VEDANTA STARTUP] Step 5/5 Validate Framework");
            bool step5Ok = VedantaTrainingDataTools.ValidateFrameworkWithResult(out string fwReport, out string fwFailure);
            if (!step5Ok)
            {
                ReportFailure("Validate Framework", fwFailure ?? "Framework validation failed.");
                return false;
            }
            Debug.Log("[VEDANTA STARTUP] Step 5/5 COMPLETE");

            // -- FINAL SUCCESS REPORT ------------------------------------------------
            var sb = new StringBuilder();
            sb.AppendLine("============================================================");
            sb.AppendLine("VEDANTA TRAINING DATA READY");
            sb.AppendLine("============================================================");
            sb.AppendLine("v Training data generated");
            sb.AppendLine("v Training TTS deployed");
            sb.AppendLine("v Legacy audio cache migrated");
            sb.AppendLine("v TTS validated");
            sb.AppendLine("v Framework validated");
            sb.AppendLine();
            sb.AppendLine($"TTS:\n{validCount}/{totalCount} valid");
            sb.AppendLine();
            sb.AppendLine("Framework:\nVALID");
            sb.AppendLine();
            sb.AppendLine("Status:\nREADY");
            sb.AppendLine("============================================================");

            Debug.Log(sb.ToString());
            Debug.Log("[VEDANTA STARTUP] PREPARATION READY");
            return true;
        }

        private static void ReportFailure(string stage, string reason)
        {
            var sb = new StringBuilder();
            sb.AppendLine("============================================================");
            sb.AppendLine("VEDANTA TRAINING DATA -- PREPARATION FAILED");
            sb.AppendLine("============================================================");
            sb.AppendLine($"Stage:\n{stage}");
            sb.AppendLine();
            sb.AppendLine("Status:\nFAILED");
            sb.AppendLine();
            sb.AppendLine($"Reason:\n{reason}");
            sb.AppendLine("============================================================");

            Debug.LogError(sb.ToString());
            Debug.LogError($"[VEDANTA STARTUP] FAILED\nStage = {stage}\nReason = {reason}");
        }
    }
}
#endif

