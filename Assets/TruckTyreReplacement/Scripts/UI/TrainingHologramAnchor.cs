using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TruckTyreReplacement.Core;

namespace TruckTyreReplacement.UI
{
    /// <summary>
    /// Presentation component attached to the player's controller that displays
    /// holographic training instructions.
    /// Localization is resolved by Manager or SequenceHelperFunctions.
    /// </summary>
    [AddComponentMenu("Vedanta/UI/Training Hologram Anchor")]
    public class TrainingHologramAnchor : MonoBehaviour
    {
        [Header("CONTROLLER ATTACHMENT")]
        [Tooltip("[RUNTIME] The hand controller transform this hologram attaches to.")]
        [SerializeField] private Transform controller;

        [Header("PLACEMENT OFFSETS")]
        [Tooltip("Local position offset relative to the controller.")]
        [SerializeField] private Vector3 localPosition = new Vector3(0f, 0.10f, 0.20f);
        
        [Tooltip("Local rotation offset relative to the controller.")]
        [SerializeField] private Vector3 localRotation = new Vector3(35f, 0f, 0f);
        
        [Tooltip("Local scale offset.")]
        [SerializeField] private Vector3 localScale = new Vector3(0.4f, 0.4f, 0.4f);

        [Header("PANEL REFERENCE")]
        [Tooltip("[OPTIONAL] The TrainingInstructionPanel component.")]
        [SerializeField] private TrainingInstructionPanel instructionPanel;

        [Header("TRAINING PREPARATION UI")]
        [Tooltip("[OPTIONAL] Logo Image pulsed while a preflight/preparation check is running. Defaults to a child named 'ClientLogo' if not set.")]
        [SerializeField] private Image logoImage;
        [Tooltip("Seconds for one full pulse cycle (100% -> ~40% -> 100%).")]
        [SerializeField] private float logoBlinkDuration = 1.2f;
        [Tooltip("Lowest alpha the logo pulses down to.")]
        [SerializeField, Range(0f, 1f)] private float logoMinAlpha = 0.4f;

        private Coroutine logoPulseCoroutine;
        private Color logoBaseColor = Color.white;

        private void Awake()
        {
            if (instructionPanel == null)
            {
                instructionPanel = GetComponentInChildren<TrainingInstructionPanel>(true);
            }
            if (logoImage == null)
            {
                var logoTrans = transform.Find("Panel/Header/ClientLogo");
                if (logoTrans == null && instructionPanel != null)
                {
                    logoTrans = instructionPanel.transform.Find("Header/ClientLogo");
                }
                if (logoTrans != null) logoImage = logoTrans.GetComponent<Image>();
            }
            if (logoImage != null) logoBaseColor = logoImage.color;
        }

        private void Start()
        {
            AttachToController();
        }

        public void AttachToController()
        {
            if (controller == null)
            {
                var leftCtrlGo = GameObject.Find("XR Origin (VR)/Camera Offset/Left Controller");
                if (leftCtrlGo != null)
                {
                    controller = leftCtrlGo.transform;
                }
            }

            if (controller != null)
            {
                transform.SetParent(controller, false);
                transform.localPosition = localPosition;
                transform.localRotation = Quaternion.Euler(localRotation);
                transform.localScale = localScale;
            }
        }

        // ─────────────────────────────────────────────────────
        // PRESENTATION COMMANDS
        // ─────────────────────────────────────────────────────

        public void SetTitle(string text)
        {
            if (instructionPanel != null)
            {
                instructionPanel.SetTitle(text);
            }
        }

        public void SetDescription(string text)
        {
            if (instructionPanel != null)
            {
                instructionPanel.SetInstruction(text);
            }
        }

        public void ShowInstruction(string text)
        {
            SetDescription(text);
        }

        public void ClearInstruction()
        {
            if (instructionPanel != null)
            {
                instructionPanel.SetInstruction("");
            }
        }

        public void SetProgress(float progress)
        {
            if (instructionPanel != null)
            {
                instructionPanel.SetProgress(progress);
            }
        }

        public void SetProgressVisible(bool visible)
        {
            if (instructionPanel != null)
            {
                instructionPanel.SetProgressVisible(visible);
            }
        }

        public void SetPanelVisible(bool visible)
        {
            if (instructionPanel != null)
            {
                instructionPanel.SetPanelVisible(visible);
            }
        }

        // ─────────────────────────────────────────────────────
        // TRAINING PREPARATION (PREFLIGHT) PRESENTATION
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Shown once a language is selected but before the trainee has pressed
        /// Next to begin training preparation. Generic text - no module-specific
        /// wording.
        /// </summary>
        public void ShowPrestartPrompt()
        {
            if (instructionPanel == null) return;
            SetLogoPulsing(false);
            instructionPanel.SetPreflightMode(false);
            instructionPanel.SetTitle("TRAINING");
            instructionPanel.SetInstruction("Press Next to prepare training.");
            instructionPanel.SetProgressVisible(false);
            instructionPanel.SetNextVisible(true);
            instructionPanel.SetNextInteractable(true);
        }

        /// <summary>
        /// Renders one preflight/preparation update. Bound to
        /// TrainingPreflightManager.OnPreflightUpdated by Manager. Presentation
        /// only - never touches SequenceHandler.
        /// </summary>
        public void ShowPreflightState(TrainingPreflightUpdate update)
        {
            if (instructionPanel == null) return;

            switch (update.state)
            {
                case TrainingPreflightState.Preparing:
                {
                    SetLogoPulsing(true);
                    instructionPanel.SetPreflightMode(true);
                    instructionPanel.SetNextVisible(false);
                    instructionPanel.SetTitle("PREPARING TRAINING");
                    instructionPanel.SetInstruction(BuildPreparingText(update));
                    instructionPanel.SetProgressVisible(true);
                    float fraction = update.totalCount > 0 ? (float)update.checkedCount / update.totalCount : 0f;
                    instructionPanel.SetProgress(fraction);
                    instructionPanel.SetProgressTextRaw($"{update.checkedCount}/{update.totalCount} audio files ready");
                    break;
                }
                case TrainingPreflightState.Ready:
                {
                    SetLogoPulsing(false);
                    instructionPanel.SetTitle("READY");
                    instructionPanel.SetInstruction(BuildReadyText(update));
                    instructionPanel.SetProgress(1f);
                    instructionPanel.SetProgressTextRaw($"{update.validCount}/{update.totalCount} audio files ready");
                    instructionPanel.SetPreflightMode(false);
                    // Restore the Next button before SequenceHandler.Init() (triggered by
                    // the same Ready callback) drives the first task's own instruction
                    // state - otherwise it would stay hidden from the Preparing phase with
                    // no manual fallback for the trainee.
                    instructionPanel.SetNextVisible(true);
                    instructionPanel.SetNextInteractable(true);
                    break;
                }
                case TrainingPreflightState.Failed:
                {
                    SetLogoPulsing(false);
                    instructionPanel.SetPreflightMode(true);
                    instructionPanel.SetTitle("PREPARATION FAILED");
                    instructionPanel.SetInstruction($"Training preparation failed.\n{update.failureReason}\nPress Next to retry once fixed.");
                    instructionPanel.SetProgressVisible(update.totalCount > 0);
                    if (update.totalCount > 0)
                    {
                        float fraction = (float)update.validCount / update.totalCount;
                        instructionPanel.SetProgress(fraction);
                        instructionPanel.SetProgressTextRaw($"{update.validCount}/{update.totalCount} audio files ready");
                    }
                    instructionPanel.SetNextVisible(true);
                    instructionPanel.SetNextInteractable(true);
                    break;
                }
            }
        }

        private static string BuildPreparingText(TrainingPreflightUpdate update)
        {
            bool checkingDone = update.checkedCount >= update.totalCount && update.totalCount > 0;
            bool repairing = update.phaseLabel != null && update.phaseLabel.StartsWith("Preparing required", StringComparison.Ordinal)
                || update.phaseLabel == "Preparing 1 audio asset...";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Preparing training audio...");
            if (checkingDone) sb.AppendLine("[OK] Training data checked");
            if (repairing)
            {
                sb.AppendLine("[OK] Audio cache checked");
                sb.Append("[UPDATING] ").Append(update.phaseLabel);
            }
            else
            {
                sb.Append(update.phaseLabel);
            }
            return sb.ToString();
        }

        private static string BuildReadyText(TrainingPreflightUpdate update)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[OK] Training data checked");
            sb.AppendLine("[OK] Audio cache checked");
            sb.Append($"[OK] {update.validCount}/{update.totalCount} audio files ready");
            return sb.ToString();
        }

        private void SetLogoPulsing(bool pulsing)
        {
            if (pulsing)
            {
                if (logoPulseCoroutine == null && logoImage != null)
                {
                    logoPulseCoroutine = StartCoroutine(LogoPulseRoutine());
                }
            }
            else
            {
                if (logoPulseCoroutine != null)
                {
                    StopCoroutine(logoPulseCoroutine);
                    logoPulseCoroutine = null;
                }
                if (logoImage != null)
                {
                    var c = logoBaseColor;
                    c.a = 1f;
                    logoImage.color = c;
                }
            }
        }

        private IEnumerator LogoPulseRoutine()
        {
            float t = 0f;
            float duration = Mathf.Max(0.1f, logoBlinkDuration);
            while (true)
            {
                t += Time.deltaTime;
                // Smooth sine-based pulse: 1.0 -> logoMinAlpha -> 1.0, never abrupt.
                float phase = (Mathf.Sin((t / duration) * Mathf.PI * 2f) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(logoMinAlpha, 1f, phase);
                var c = logoBaseColor;
                c.a = alpha;
                logoImage.color = c;
                yield return null;
            }
        }
    }
}
