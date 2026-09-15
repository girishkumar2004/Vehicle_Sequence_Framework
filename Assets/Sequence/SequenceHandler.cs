using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SequenceHandler : MonoBehaviour
{
    public static SequenceHandler instance;
    public bool autostart = false;

    [Tooltip("The sequence list is referred from the scriptable objects here.")]
    public List<Sequence> sequenceList = new List<Sequence>();
    [SerializeField]
    public int currentSequence, currentTask;
    public bool WaitForTrigger;
    public bool isSequenceMode;
    public bool lockSequence = false;

    public void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }
    public void Start()
    {
        if (autostart)
            Init();
    }
    public void DelayedStart(float s)
    {
        Invoke(nameof(Init), s);
    }

    public void Init()
    {
        currentSequence = 0;
        currentTask = 0;
        //if (isSequenceMode)
            NextTask();
    }

    public void TaskCompleted()
    {
        if (sequenceList == null || currentSequence < 0 || currentSequence >= sequenceList.Count)
        {
            Debug.Log("[SequenceHandler] All sequences completed or sequence index out of bounds.");
            return;
        }

        if (sequenceList[currentSequence].TaskList == null || currentTask < 0 || currentTask >= sequenceList[currentSequence].TaskList.Count)
            return;

        if (sequenceList[currentSequence].TaskList[currentTask].TaskCompleted)
        {
            Debug.LogWarning($"[SequenceHandler] Task {currentTask} is already completed. Rejects duplicate completion call.");
            return;
        }

        sequenceList[currentSequence].TaskList[currentTask].TaskCompleted = true;
        Debug.Log("Current task num : " + currentTask + " completed by " + gameObject.name);
        currentTask++;
        NextTask();
    }

    public void CurrentTaskCompleted()
    {
        if (sequenceList == null || currentSequence < 0 || currentSequence >= sequenceList.Count) return;
        if (sequenceList[currentSequence].TaskList == null || currentTask < 0 || currentTask >= sequenceList[currentSequence].TaskList.Count) return;

        sequenceList[currentSequence].TaskList[currentTask].TaskCompleted = true;
        currentTask++;
    }

    /*public void SkipSequence()
    {
        for (int i = 0; i < sequenceList[currentSequence].TaskList.Count; i++)
        {
            sequenceList[currentSequence].TaskList[i].TaskCompleted = true;
        }
        currentTask = sequenceList[currentSequence].TaskList.Count;
        lockSequence = true;
        this.GetComponent<AudioSource>().Stop();
    }*/

    public void SkipSequence()
    {
        // Complete all remaining tasks in current sequence
        for (int i = 0; i < sequenceList[currentSequence].TaskList.Count; i++)
        {
            sequenceList[currentSequence].TaskList[i].TaskCompleted = true;
            sequenceList[currentSequence].TaskList[i].TriggerCompleted = true;
        }

        // Stop audio if available
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        Debug.Log("Skipping Sequence : " + currentSequence);

        // Move directly to next sequence
        currentSequence++;

        if (currentSequence >= sequenceList.Count)
        {
            Debug.Log("All sequences completed.");
            return;
        }

        currentTask = 0;

        // Start next sequence
        NextTask();
    }

    public void SequenceSelect(int n)
    {
        currentSequence = n;
        currentTask = 0;
        NextTask();
    }

    public void NextTask()
    {
        Debug.Log(currentSequence.CompareTo(currentTask));
        if (currentTask >= sequenceList[currentSequence].TaskList.Count)
        {
            if (!lockSequence)
            {
                NextSequence();
            }
            else
            {
                Debug.Log("Sequence is locked. Not moving to next sequence");
            }
            return;
        }

        Task activeTask = sequenceList[currentSequence].TaskList[currentTask];
        activeTask.TaskCompleted = false;

        Debug.Log($"[SEQUENCE START]\nSequence = {currentSequence}\nTask Index = {currentTask}\nTask Name = {activeTask.TaskName}");

        ExecuteTaskInstructionAndTTS(activeTask);

        switch (activeTask.typeOfInteraction)
        {
            case Task.TypeOfInteraction.None:
                WaitForTrigger = false;
                break;
            default:
                break;
        }

        if (!WaitForTrigger)
        {
            activeTask.EventsToFollow.Invoke();
        }
    }

    private void PlayCurrentTask()
    {
        if (currentSequence >= 0 && currentSequence < sequenceList.Count &&
            currentTask >= 0 && currentTask < sequenceList[currentSequence].TaskList.Count)
        {
            Task activeTask = sequenceList[currentSequence].TaskList[currentTask];
            activeTask.TaskCompleted = false;

            Debug.Log($"Playing task {currentTask} in sequence {currentSequence}");

            ExecuteTaskInstructionAndTTS(activeTask);

            switch (activeTask.typeOfInteraction)
            {
                case Task.TypeOfInteraction.None:
                    WaitForTrigger = false;
                    break;
                default:
                    break;
            }

            if (!WaitForTrigger)
            {
                activeTask.EventsToFollow.Invoke();
            }
        }
        else
        {
            Debug.LogWarning($"Attempted to play invalid task: Sequence {currentSequence}, Task {currentTask}");
        }
    }

    private void ExecuteTaskInstructionAndTTS(Task task)
    {
        if (task == null) return;

        string displayTextKey = !string.IsNullOrEmpty(task.instructionText) ? task.instructionText : task.TaskName;
        string speechKeyOrText = !string.IsNullOrEmpty(task.ttsTextOrKey) ? task.ttsTextOrKey : task.instructionText;

        var manager = TruckTyreReplacement.Core.Manager.Instance;
        if (manager == null) manager = UnityEngine.Object.FindFirstObjectByType<TruckTyreReplacement.Core.Manager>();

        // 1. Display Instruction Text on TrainingHologramAnchor / Instruction Panel
        if (!string.IsNullOrEmpty(displayTextKey))
        {
            string resolvedDisplay = displayTextKey;
            if (manager != null)
            {
                string localized = manager.GetDisplayText(displayTextKey);
                if (!string.IsNullOrEmpty(localized)) resolvedDisplay = localized;
            }

            var anchor = UnityEngine.Object.FindFirstObjectByType<TruckTyreReplacement.UI.TrainingHologramAnchor>();
            if (anchor != null)
            {
                anchor.SetDescription(resolvedDisplay);
            }
            else
            {
                var panel = UnityEngine.Object.FindFirstObjectByType<TruckTyreReplacement.UI.TrainingInstructionPanel>();
                if (panel != null) panel.SetInstruction(resolvedDisplay);
            }
        }

        // 2. Play LocalTTS Audio
        if (task.useTTS)
        {
            if (task.audioClipOverride != null)
            {
                if (SequenceHelperFunctions.instance != null)
                {
                    if (task.completionMode == CompletionMode.AudioComplete)
                        SequenceHelperFunctions.instance.PlayAudio_TriggerOnComplete(task.audioClipOverride);
                    else
                        SequenceHelperFunctions.instance.VoiceOverCall(task.audioClipOverride);
                }
            }
            else if (!string.IsNullOrEmpty(speechKeyOrText))
            {
                Debug.Log($"[SequenceHandler][TTS] Task '{task.TaskName}' (Mode: {task.completionMode}) speaking key: '{speechKeyOrText}'");
                if (SequenceHelperFunctions.instance != null)
                {
                    if (task.completionMode == CompletionMode.AudioComplete)
                    {
                        if (speechKeyOrText == "complete")
                            SequenceHelperFunctions.instance.PlayCompletionSequence_TriggerOnComplete();
                        else
                            SequenceHelperFunctions.instance.PlayLocaleAudio_TriggerOnComplete(speechKeyOrText);
                    }
                    else
                    {
                        SequenceHelperFunctions.instance.PlayLocaleAudio(speechKeyOrText);
                    }
                }
                else if (manager != null)
                {
                    manager.Speak(speechKeyOrText);
                }
                else
                {
                    Debug.LogWarning($"[SequenceHandler][TTS] LocalTTS / Manager unavailable for task '{task.TaskName}'. Continuing without audio.");
                }
            }
        }
    }

    public void NextSequence()
    {
        currentSequence++;
        if (currentSequence >= sequenceList.Count)
        {
            return;
        }
        currentTask = 0;
        NextTask();
    }

    public void PreviousSequence()
    {
        currentSequence--;
        if (currentSequence < 0)
        {
            return;
        }
        currentTask = 0;
        NextTask();
    }

    public void ReloadSequence()
    {
        if (currentSequence >= 0 && currentSequence < sequenceList.Count)
        {
            currentTask = 0;
            Debug.Log("Reload Sequence" + currentSequence);
            PlayCurrentTask();
        }
    }

    public void TriggerForTaskDone()
    {
        switch (sequenceList[currentSequence].TaskList[currentTask].typeOfInteraction)
        {
            case Task.TypeOfInteraction.None:
                WaitForTrigger = false;
                break;
            default:
                break;

        }
        sequenceList[currentSequence].TaskList[currentTask].TriggerCompleted = true;

        sequenceList[currentSequence].TaskList[currentTask].EventsToFollow.Invoke();

        WaitForTrigger = false;
    }

    public void LoadScene(string name)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(name);
    }

    public void ApplicationExit()
    {
        Application.Quit();
    }
}