using UnityEngine;

namespace TruckTyreReplacement.Audio
{
    /// <summary>
    /// Forwards TTS requests to Manager.Instance.
    /// </summary>
    public class TrainingHologramTTS : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool speakOnStepChange = true;

        [Header("References")]
        [SerializeField] private TruckTyreReplacement.UI.TrainingInstructionPanel instructionPanel;

        public bool SpeakOnStepChange
        {
            get => speakOnStepChange;
            set => speakOnStepChange = value;
        }

        private void Start()
        {
            if (instructionPanel == null)
            {
                instructionPanel = Object.FindFirstObjectByType<TruckTyreReplacement.UI.TrainingInstructionPanel>();
            }
        }

        public void SpeakText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (TruckTyreReplacement.Core.Manager.Instance != null)
            {
                TruckTyreReplacement.Core.Manager.Instance.Speak(text);
            }
        }

        public void StopSpeech()
        {
            if (TruckTyreReplacement.Core.Manager.Instance != null)
            {
                TruckTyreReplacement.Core.Manager.Instance.StopSpeech();
            }
        }
    }
}
