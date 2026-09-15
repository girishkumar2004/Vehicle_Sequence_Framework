using System;

namespace TruckTyreReplacement.Core
{
    /// <summary>
    /// One request to synthesize a single piece of speech. Contains no
    /// training-module knowledge (no keys, no sequence/task awareness) -
    /// only the data an audio synthesis backend actually needs.
    /// </summary>
    public struct RuntimeTTSRequest
    {
        public string language;      // cache language name (e.g. "English"), matches LocalTTSCacheService conventions
        public string speechText;    // already-normalized text to synthesize
        public string expectedHash;  // SHA-256 the caller expects the resulting audio to be identified by
        public string outputPath;    // where the backend must write the resulting WAV
    }

    public struct RuntimeTTSResult
    {
        public bool success;
        public string outputPath;
        public string errorMessage;
    }

    /// <summary>
    /// Generic runtime audio-generation abstraction. An implementation knows
    /// only language/text/hash/output-path - nothing about welcome/step0/
    /// tyre/pressure/pipe or SequenceHandler. This is the seam a future
    /// on-device TTS backend (e.g. a mobile-capable ONNX/Sentis model) would
    /// implement to enable true standalone self-healing.
    /// </summary>
    public interface IRuntimeTTSService
    {
        bool IsAvailable(string language);
        bool IsGenerating { get; }
        /// <summary>0..1, only meaningful while IsGenerating is true. Must reflect real progress, never a fabricated value.</summary>
        float GenerationProgress { get; }
        string LastError { get; }

        /// <summary>
        /// Begins generation. onComplete is invoked exactly once. Implementations
        /// that do real work must not block the calling thread for longer than a
        /// single frame - use a coroutine/background task internally and invoke
        /// onComplete when finished.
        /// </summary>
        void GenerateAudio(RuntimeTTSRequest request, Action<RuntimeTTSResult> onComplete);

        void CancelGeneration();
    }

    /// <summary>
    /// Default, Quest/Android/IL2CPP-safe implementation. Always reports
    /// unavailable and never attempts generation - there is currently no
    /// on-device TTS synthesis backend in this project (the existing MMS-TTS
    /// pipeline is a desktop-only Editor tool that shells out to Python and
    /// cannot run in a Player build). This class contains no Python, no
    /// subprocess, no ONNX, and no network calls of any kind.
    ///
    /// Exists so the preflight pipeline always has a real, honest object to
    /// call rather than a null reference, and so a real backend can be
    /// dropped in later (via Manager.RuntimeTTSService) without touching the
    /// preflight/orchestration code at all.
    /// </summary>
    public class NullRuntimeTTSService : IRuntimeTTSService
    {
        public bool IsGenerating => false;
        public float GenerationProgress => 0f;
        public string LastError { get; private set; } = "No runtime TTS backend is installed in this build.";

        public bool IsAvailable(string language) => false;

        public void GenerateAudio(RuntimeTTSRequest request, Action<RuntimeTTSResult> onComplete)
        {
            LastError = "No runtime TTS backend is installed in this build.";
            onComplete?.Invoke(new RuntimeTTSResult
            {
                success = false,
                outputPath = request.outputPath,
                errorMessage = LastError
            });
        }

        public void CancelGeneration()
        {
            // Nothing to cancel - generation is never actually started.
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor Play Mode implementation of IRuntimeTTSService.
    /// Drives the desktop Python MMS-TTS generator (VedantaTTSGenerator) when
    /// playing in Unity Editor so preflight self-healing works seamlessly in Play Mode.
    /// Never compiled into Player builds.
    /// </summary>
    public class EditorRuntimeTTSService : IRuntimeTTSService
    {
        public bool IsGenerating { get; private set; }
        public float GenerationProgress => IsGenerating ? 0.5f : 0f;
        public string LastError { get; private set; }

        public bool IsAvailable(string language)
        {
            return language == "English" || language == "Hindi" || language == "Odia";
        }

        public void GenerateAudio(RuntimeTTSRequest request, Action<RuntimeTTSResult> onComplete)
        {
            IsGenerating = true;
            bool ok = false;
            string failureReason = null;

            try
            {
                var type = System.Type.GetType("TruckTyreReplacement.EditorTools.VedantaTTSGenerator, Assembly-CSharp-Editor");
                if (type == null)
                {
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = asm.GetType("TruckTyreReplacement.EditorTools.VedantaTTSGenerator");
                        if (type != null) break;
                    }
                }

                if (type != null)
                {
                    var method = type.GetMethod("RunGenerationWithResult", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (method != null)
                    {
                        object[] args = new object[] { true, true, null, null };
                        ok = (bool)method.Invoke(null, args);
                        failureReason = args[3] as string;
                    }
                    else
                    {
                        failureReason = "Method RunGenerationWithResult not found on VedantaTTSGenerator.";
                    }
                }
                else
                {
                    failureReason = "Type VedantaTTSGenerator not found.";
                }
            }
            catch (Exception ex)
            {
                failureReason = "Exception invoking TTS generator: " + ex.Message;
            }

            IsGenerating = false;
            if (!ok) LastError = failureReason;

            onComplete?.Invoke(new RuntimeTTSResult
            {
                success = ok,
                outputPath = request.outputPath,
                errorMessage = failureReason
            });
        }

        public void CancelGeneration() { }
    }
#endif
}
