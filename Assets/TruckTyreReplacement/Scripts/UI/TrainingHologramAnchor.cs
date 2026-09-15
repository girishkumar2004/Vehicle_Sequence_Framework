using UnityEngine;

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

        private void Awake()
        {
            if (instructionPanel == null)
            {
                instructionPanel = GetComponentInChildren<TrainingInstructionPanel>(true);
            }
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
    }
}
