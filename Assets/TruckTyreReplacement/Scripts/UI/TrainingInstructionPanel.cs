using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TruckTyreReplacement.UI
{
    /// <summary>
    /// UI Presentation script managing title, instructions, progress bar, and buttons.
    /// </summary>
    public class TrainingInstructionPanel : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Button nextButton;
        
        [Header("Optional Elements")]
        [SerializeField] private Image clientLogo;
        [SerializeField] private GameObject progressContainer;
        [SerializeField] private TMP_Text progressText;

        [Header("UI Presentation Events")]
        public UnityEngine.Events.UnityEvent OnNextButtonClickedEvent;

        private void Awake()
        {
            SetProgressVisible(false);
            if (nextButton == null)
            {
                var btnTrans = transform.Find("Footer/NextButton");
                if (btnTrans != null) nextButton = btnTrans.GetComponent<Button>();
            }
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(true);
                nextButton.interactable = true;
            }
        }

        private void Start()
        {
            SetProgressVisible(false);

            if (nextButton == null)
            {
                var btnTrans = transform.Find("Footer/NextButton");
                if (btnTrans != null) nextButton = btnTrans.GetComponent<Button>();
            }

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(true);
                nextButton.interactable = true;
                nextButton.onClick.RemoveListener(OnNextButtonClicked);
                nextButton.onClick.AddListener(OnNextButtonClicked);
            }

            // Ensure Panel is raycastTarget so the controller ray hits the holographic anchor
            var panelImg = GetComponent<UnityEngine.UI.Image>();
            if (panelImg != null)
            {
                panelImg.raycastTarget = true;
            }

            // [NEXT BUTTON INIT] Runtime Diagnostics
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var canvas = GetComponentInParent<Canvas>();
            Debug.Log($"[NEXT BUTTON INIT]\n" +
                      $"Button: {(nextButton != null ? nextButton.name : "null")}\n" +
                      $"GameObject: {(nextButton != null ? nextButton.gameObject.name : "null")}\n" +
                      $"ActiveSelf: {(nextButton != null ? nextButton.gameObject.activeSelf.ToString() : "false")}\n" +
                      $"ActiveInHierarchy: {(nextButton != null ? nextButton.gameObject.activeInHierarchy.ToString() : "false")}\n" +
                      $"Interactable: {(nextButton != null ? nextButton.interactable.ToString() : "false")}\n" +
                      $"Canvas: {(canvas != null ? canvas.name : "null")}\n" +
                      $"Canvas Active: {(canvas != null ? canvas.gameObject.activeInHierarchy.ToString() : "false")}\n" +
                      $"TrainingInstructionPanel: {this.name}\n" +
                      $"EventSystem: {(eventSystem != null ? eventSystem.name : "null")}\n" +
                      $"Listener Count: {(nextButton != null ? nextButton.onClick.GetPersistentEventCount() : 0)}\n" +
                      $"Current Task: {(SequenceHandler.instance != null ? SequenceHandler.instance.currentTask.ToString() : "null")}");
        }

        private float lastClickTime = -1f;

        public void OnNextButtonClicked()
        {
            if (Time.unscaledTime - lastClickTime < 0.35f) return;
            lastClickTime = Time.unscaledTime;

            var seq = SequenceHandler.instance != null ? SequenceHandler.instance : UnityEngine.Object.FindFirstObjectByType<SequenceHandler>();
            int currentTask = seq != null ? seq.currentTask : -1;
            int currentSeq  = seq != null ? seq.currentSequence : -1;

            string taskName = "None";
            CompletionMode mode = CompletionMode.Manual;
            bool taskValid = seq != null && seq.sequenceList.Count > currentSeq && currentSeq >= 0
                          && currentTask >= 0 && currentTask < seq.sequenceList[currentSeq].TaskList.Count;
            if (taskValid)
            {
                taskName = seq.sequenceList[currentSeq].TaskList[currentTask].TaskName;
                mode = seq.sequenceList[currentSeq].TaskList[currentTask].completionMode;
            }

            Debug.Log($"[NEXT BUTTON CLICK] Seq={currentSeq} Task={currentTask} Name='{taskName}' Mode={mode}");
            OnNextButtonClickedEvent?.Invoke();

            if (seq != null && taskValid)
            {
                var mgr = Core.Manager.Instance;
                AudioSource voiceSrc = mgr != null ? mgr.GetVoiceAudioSource() : null;
                bool isSpeaking = (mgr != null && mgr.IsSpeaking) || (voiceSrc != null && voiceSrc.isPlaying);

                // Next button only completes Manual or Automatic tasks.
                // Interaction, VariableCondition, and active AudioComplete tasks must complete via their own completion paths.
                bool nextCanComplete = (mode == CompletionMode.Manual || mode == CompletionMode.Automatic)
                                    || (mode == CompletionMode.AudioComplete && !isSpeaking);

                if (nextCanComplete)
                {
                    Debug.Log($"[TrainingInstructionPanel] Next button advancing task: Seq={currentSeq} Task={currentTask} ({mode})");
                    seq.TaskCompleted();
                }
                else
                {
                    Debug.Log($"[TrainingInstructionPanel] Next button blocked — task mode is {mode} (isSpeaking={isSpeaking}). Task completes via its own completion path.");
                }
            }
        }

        public void SetPanelVisible(bool visible)
        {
            if (Core.Manager.Instance != null && !Core.Manager.Instance.IsLanguageSelected)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(visible);
        }

        public void SetTitle(string title)
        {
            if (titleText != null)
            {
                titleText.text = title.ToUpper();
            }
        }

        public void SetInstruction(string text)
        {
            if (instructionText != null)
            {
                instructionText.text = text;
            }
        }

        public void SetProgress(float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            if (progressSlider != null)
            {
                progressSlider.value = clamped;
            }
            if (progressText != null)
            {
                int percentage = Mathf.RoundToInt(clamped * 100f);
                progressText.text = percentage + "%";
            }
        }

        public void SetNextVisible(bool visible)
        {
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(visible);
            }
        }

        public void SetNextInteractable(bool interactable)
        {
            if (nextButton != null)
            {
                nextButton.interactable = interactable;
            }
        }

        public void SetLogoVisible(bool visible)
        {
            if (clientLogo != null) clientLogo.gameObject.SetActive(visible);
        }

        public void SetTitleVisible(bool visible)
        {
            if (titleText != null) titleText.gameObject.SetActive(visible);
        }

        public void SetInstructionVisible(bool visible)
        {
            if (instructionText != null) instructionText.gameObject.SetActive(visible);
        }

        public void SetProgressVisible(bool visible)
        {
            // Show progress slider only for tasks with CompletionMode.Interaction (e.g. wheel rotation)
            bool isInteractionTask = false;
            if (SequenceHandler.instance != null)
            {
                int seq  = SequenceHandler.instance.currentSequence;
                int task = SequenceHandler.instance.currentTask;
                var seqList = SequenceHandler.instance.sequenceList;
                if (seq >= 0 && seq < seqList.Count && task >= 0 && task < seqList[seq].TaskList.Count)
                    isInteractionTask = seqList[seq].TaskList[task].completionMode == CompletionMode.Interaction;
            }
            bool shouldShow = visible && isInteractionTask;

            if (progressSlider != null) progressSlider.gameObject.SetActive(shouldShow);
            if (progressContainer != null) progressContainer.SetActive(shouldShow);
            if (progressText != null) progressText.gameObject.SetActive(shouldShow);
        }

        public void SetProgressTextVisible(bool visible)
        {
            bool isInteractionTask = false;
            if (SequenceHandler.instance != null)
            {
                int seq  = SequenceHandler.instance.currentSequence;
                int task = SequenceHandler.instance.currentTask;
                var seqList = SequenceHandler.instance.sequenceList;
                if (seq >= 0 && seq < seqList.Count && task >= 0 && task < seqList[seq].TaskList.Count)
                    isInteractionTask = seqList[seq].TaskList[task].completionMode == CompletionMode.Interaction;
            }
            bool shouldShow = visible && isInteractionTask;
            if (progressText != null) progressText.gameObject.SetActive(shouldShow);
        }

        public void ResetPanel()
        {
            SetProgress(0f);
            SetProgressVisible(false);
            SetNextVisible(true);
            SetNextInteractable(true);
        }

        private void LateUpdate()
        {
            // Failsafe: hide progress slider if current task is not an Interaction task
            if (SequenceHandler.instance != null)
            {
                int seq  = SequenceHandler.instance.currentSequence;
                int task = SequenceHandler.instance.currentTask;
                var seqList = SequenceHandler.instance.sequenceList;
                bool isInteractionTask = seq >= 0 && seq < seqList.Count && task >= 0 && task < seqList[seq].TaskList.Count
                    && seqList[seq].TaskList[task].completionMode == CompletionMode.Interaction;

                if (!isInteractionTask)
                {
                    if (progressContainer != null && progressContainer.activeSelf) progressContainer.SetActive(false);
                    if (progressSlider    != null && progressSlider.gameObject.activeSelf) progressSlider.gameObject.SetActive(false);
                    if (progressText      != null && progressText.gameObject.activeSelf)   progressText.gameObject.SetActive(false);
                }
            }
        }
    }
}
