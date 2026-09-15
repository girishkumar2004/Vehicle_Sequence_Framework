using UnityEngine;
using UnityEngine.UI;

namespace TruckTyreReplacement.Core
{
    public enum TrainingStepType
    {
        Instruction,
        InspectRotation,
        ButtonPress,
        Grab,
        Place,
        Move,
        Inflate,
        Custom
    }

    [System.Serializable]
    public class TrainingStep
    {
        [Tooltip("Unique ID for this step")]
        public string stepId;

        [Tooltip("Title of the step shown on the UI panel")]
        public string stepTitle;

        [TextArea(3, 10)]
        [Tooltip("Detailed instructions for the user")]
        public string instructionText;

        [Tooltip("Audio clip for voice instructions")]
        public AudioClip audioClip;

        [Tooltip("The step type of this training step")]
        public TrainingStepType stepType;

        [Tooltip("The position/transform where the instruction panel should be placed for this step")]
        public Transform instructionPanelTransform;

        [Tooltip("The Next button associated with the UI panel")]
        public Button nextButton;

        [Tooltip("The target GameObject for interaction in this step")]
        public GameObject targetGameObject;

        [Tooltip("The target Transform to move/align the targetGameObject to")]
        public Transform targetTransform;

        [Tooltip("The progress slider to represent interaction progress")]
        public Slider progressSlider;

        [Tooltip("The teleport point where the user should stand")]
        public Transform teleportPoint;

        [Tooltip("The required value to complete this interaction (e.g. 360 degrees for rotation)")]
        public float requiredCompletionValue = 360f;

        [Header("UI Visibility")]
        public bool showPanel = true;
        public bool showClientLogo = true;
        public bool showTitle = true;
        public bool showInstruction = true;
        public bool showProgress = false;
        public bool showProgressText = false;
        public bool showNextButton = false;

        [Header("Button")]
        public bool nextButtonInteractable = false;

        [Header("Objective")]
        public bool enableObjectiveHighlight = false;
        public GameObject objectiveTarget;

        [Header("Objective Behavior Options")]
        public bool highlightOnStepStart = true;
        public bool disableHighlightOnInteraction = true;
        public bool requireTargetSelection = false;
        public Material customHighlightMaterial;
    }
}
