using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// ─────────────────────────────────────────────────────────────────────────────
// CompletionMode
// Documents how a Task completes in the Inspector.
// Does NOT gate code logic — actual completion is always triggered by
// the configured events/components calling SequenceHelperFunctions.CompleteCurrentTask().
// ─────────────────────────────────────────────────────────────────────────────

public enum CompletionMode
{
    Manual,            // Developer calls CompleteCurrentTask() manually
    Automatic,         // Completes immediately when EventsToFollow fires
    Interaction,       // Completed by a user interaction (grab, rotation, click)
    Event,             // Completed by a UnityEvent wired to CompleteCurrentTask()
    AudioComplete,     // PlayLocaleAudio_TriggerOnComplete detects audio end
    VariableCondition  // NumericVariableController.OnTargetReached
}

// ─────────────────────────────────────────────────────────────────────────────
// HandInteractionObject (legacy — kept for backward compatibility)
// ─────────────────────────────────────────────────────────────────────────────

public class HandInteractionObject
{
    [System.Serializable]
    public enum Types { Interactable, Non_interactable }

    public Types Type;
    [Tooltip("Events to invoke on interaction.")]
    public UnityEvent EventsToFollow;
    [Tooltip("Animation name when dropped.")]
    public string AnimToDropObject;
    public bool ifGrabbed;
    public bool droppedAndLocked;
}

// ─────────────────────────────────────────────────────────────────────────────
// Task
// Core data unit consumed by SequenceHandler. Every field is Inspector-visible.
// ─────────────────────────────────────────────────────────────────────────────

[System.Serializable]
public class Task
{
    // ── IDENTITY ──────────────────────────────────────────────────────────────
    [Header("Identity")]
    [Tooltip("Human-readable name. Used only in Inspector and debug logs. Never used in logic.")]
    public string TaskName;

    [Tooltip("Developer notes for this task. Not used at runtime.")]
    [TextArea(1, 3)]
    public string Notes;

    // ── COMPLETION ────────────────────────────────────────────────────────────
    [Header("Completion")]
    [Tooltip("Documents intended completion type. Actual completion is always event-driven.")]
    public CompletionMode completionMode;

    // ── STATE ─────────────────────────────────────────────────────────────────
    [Header("State (Runtime)")]
    [Tooltip("Automatically set true when handler.TaskCompleted() is called.")]
    public bool TaskCompleted;

    [Tooltip("Set by TriggerForTaskDone() for interaction-gated tasks.")]
    public bool TriggerCompleted;

    // ── INSTRUCTION & LOCAL TTS / AUDIO ──────────────────────────────────────
    [Header("Instruction & LocalTTS / Audio")]
    [Tooltip("UI Instruction text or translation database key to display on TrainingHologramAnchor.")]
    [TextArea(2, 4)]
    public string instructionText;

    [Tooltip("Enable LocalTTS audio playback for this task.")]
    public bool useTTS = true;

    [Tooltip("Speech text or database key passed to LocalTTS / Manager. If empty, instructionText will be used.")]
    [TextArea(2, 4)]
    public string ttsTextOrKey;

    [Tooltip("Optional direct AudioClip override (used if not speaking via translation key/LocalTTS).")]
    public AudioClip audioClipOverride;

    // ── EVENTS TO FOLLOW ──────────────────────────────────────────────────────
    [Header("Events To Follow")]
    [Tooltip("All actions fired when this task starts. Wire to SequenceHelperFunctions, GenericRotationInteraction.Activate(), NumericVariableController.Initialize(), etc.")]
    public UnityEvent EventsToFollow;

    // ── INTERACTION TYPE ──────────────────────────────────────────────────────
    [Header("Interaction Type (Advanced)")]
    [Tooltip("Controls WaitForTrigger in SequenceHandler. Leave None for standard event-driven tasks.")]
    public TypeOfInteraction typeOfInteraction;

    public enum TypeOfInteraction
    {
        None
        // Extend here for future interaction-gated task types
    }

    // ── LEGACY FIELDS ─────────────────────────────────────────────────────────
    // Kept for backward compatibility only. Not used by the generic framework.
    [HideInInspector] public Transform InteractableObject;
    [HideInInspector] public Transform DestinationObject;
    [HideInInspector] public Transform InitialParent;
    [HideInInspector] public Vector3 InitialLocalPosition;
    [HideInInspector] public Quaternion InitialLocalRotation;
    [HideInInspector] public string AnimationToBePlayed;
    [HideInInspector] public string CallOutText;
    [HideInInspector] public Animator Objectanimator;
    [HideInInspector] public string CurrentTaskName; // Legacy: use TaskName
    [HideInInspector] public string Debug;           // Legacy: use Notes
}
