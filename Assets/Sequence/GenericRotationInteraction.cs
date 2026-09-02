using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Generic thumbstick-driven rotation interaction for ANY Transform in VR.
/// Applies rotation around the target object's local axis when grabbed/selected in VR.
/// Completely generic and Inspector-driven.
///
/// USAGE:
///   1. Add to any rotatable GameObject (e.g., Whl HD FR).
///   2. Assign TargetTransform and LockPositionAtSpawn.
///   3. Assign ThumbstickInput (e.g. XRI Right/Thumbstick).
///   4. Set RequiredRotation (e.g. 180) and RotationAxis (e.g. Vector3.right for local X).
///   5. Wire OnCompleted -> SequenceHelperFunctions.CompleteCurrentTask().
///   6. Wire GenericRotationInteraction.Activate to Sequence EventsToFollow.
/// </summary>
[AddComponentMenu("Vedanta/Interactions/Generic Rotation Interaction")]
public class GenericRotationInteraction : MonoBehaviour
{
    [Header("TARGET")]
    [Tooltip("Transform to rotate. If null, uses this object's transform.")]
    public Transform targetTransform;
    [Tooltip("Lock target at this world position during rotation (prevents drift).")]
    public Transform lockPositionAtSpawn;

    [Header("INPUT")]
    [Tooltip("InputActionReference for thumbstick (e.g. XRI Right/Thumbstick).")]
    public InputActionReference thumbstickInput;
    [Tooltip("If true, fallback to searching for XRI Right/Thumbstick input action if thumbstickInput is unassigned.")]
    public bool enableFallbackInputLookup = true;

    [Header("ROTATION SETTINGS")]
    [Tooltip("Rotation speed in degrees per second.")]
    public float rotationSpeed = 90f;
    [Tooltip("Thumbstick input deadzone threshold (ignores values below this).")]
    public float deadzone = 0.15f;
    [Tooltip("Total accumulated rotation degrees required for completion.")]
    public float requiredRotation = 180f;
    [Tooltip("Local rotation axis. Vector3.right (1,0,0) = local X axis.")]
    public Vector3 rotationAxis = Vector3.right;
    [Tooltip("Invert rotation direction from thumbstick input.")]
    public bool invertRotation = false;

    [Header("GRAB / SELECTION REQUIREMENT")]
    [Tooltip("If true, rotation is applied ONLY while the object is actively grabbed/selected in VR.")]
    public bool requireGrabToRotate = true;

    [Header("STATE (Read-Only)")]
    [Range(0f, 1f)] public float currentProgress = 0f;
    public bool isActive = false;
    public bool isGrabbed = false;

    [Header("EVENTS")]
    [Tooltip("Fires every Update with progress 0 to 1.")]
    public UnityEvent<float> OnProgressChanged;
    [Tooltip("Fires once when requiredRotation is fully accumulated. Wire to SequenceHelperFunctions.CompleteCurrentTask().")]
    public UnityEvent OnCompleted;

    private bool isCompleted = false;
    private float accumulatedRotation = 0f;
    private float currentAxisValue = 0f;
    private Quaternion originalLocalRotation;
    private Vector3 lockedWorldPosition;

    private XRBaseInteractable baseInteractable;
    private InputAction fallbackAction;

    private void Awake()
    {
        enabled = false;
        isActive = false;
        isGrabbed = false;
    }

    private void Start()
    {
        if (targetTransform == null) targetTransform = transform;
        originalLocalRotation = targetTransform.localRotation;
        lockedWorldPosition = (lockPositionAtSpawn != null) ? lockPositionAtSpawn.position : targetTransform.position;

        BindInteractableEvents();
        EnableInputAction();
    }

    private void OnEnable()
    {
        BindInteractableEvents();
        EnableInputAction();
    }

    private void OnDisable()
    {
        UnbindInteractableEvents();
    }

    private void OnDestroy()
    {
        UnbindInteractableEvents();
    }

    private void BindInteractableEvents()
    {
        if (baseInteractable == null)
            baseInteractable = GetComponent<XRBaseInteractable>();

        if (baseInteractable != null)
        {
            baseInteractable.selectEntered.RemoveListener(OnSelectEntered);
            baseInteractable.selectExited.RemoveListener(OnSelectExited);
            baseInteractable.selectEntered.AddListener(OnSelectEntered);
            baseInteractable.selectExited.AddListener(OnSelectExited);
        }
    }

    private void UnbindInteractableEvents()
    {
        if (baseInteractable != null)
        {
            baseInteractable.selectEntered.RemoveListener(OnSelectEntered);
            baseInteractable.selectExited.RemoveListener(OnSelectExited);
        }
    }

    private void EnableInputAction()
    {
        if (thumbstickInput != null && thumbstickInput.action != null)
        {
            if (!thumbstickInput.action.enabled)
                thumbstickInput.action.Enable();
        }
        else if (enableFallbackInputLookup && fallbackAction == null)
        {
            try
            {
                fallbackAction = InputSystem.actions?.FindAction("XRI Right/Thumbstick");
                if (fallbackAction == null) fallbackAction = InputSystem.actions?.FindAction("RightHand/Thumbstick");
                if (fallbackAction == null) fallbackAction = InputSystem.actions?.FindAction("Turn");
                if (fallbackAction != null && !fallbackAction.enabled)
                    fallbackAction.Enable();
            }
            catch { }
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        string interactorName = args.interactorObject != null ? args.interactorObject.transform.name : "Unknown Interactor";
        Debug.Log($"[GenericRotationInteraction] GRAB ENTERED on '{gameObject.name}' by interactor '{interactorName}'.");

        if (isActive && !isCompleted && SequenceHelperFunctions.instance != null)
        {
            SequenceHelperFunctions.instance.RemoveSafeGhostHighlight(gameObject);
        }
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        isGrabbed = false;
        Debug.Log($"[GenericRotationInteraction] GRAB EXITED on '{gameObject.name}'.");

        if (isActive && !isCompleted && SequenceHelperFunctions.instance != null)
        {
            SequenceHelperFunctions.instance.ApplySafeGhostHighlight(gameObject);
        }
    }

    /// <summary>Call from Sequence EventsToFollow to begin this interaction.</summary>
    public void Activate()
    {
        if (targetTransform == null) targetTransform = transform;

        enabled = true;
        isActive = true;
        isCompleted = false;
        accumulatedRotation = 0f;
        currentAxisValue = 0f;
        currentProgress = 0f;
        originalLocalRotation = targetTransform.localRotation;
        lockedWorldPosition = (lockPositionAtSpawn != null) ? lockPositionAtSpawn.position : targetTransform.position;

        EnableInputAction();
        SetPanelProgress(0f, true);
        Debug.Log($"[GenericRotationInteraction] ACTIVATED on '{gameObject.name}'. Required={requiredRotation}deg Axis={rotationAxis}");
    }

    /// <summary>Stop this interaction. Called automatically on completion or from events.</summary>
    public void Deactivate()
    {
        enabled = false;
        isActive = false;
    }

    /// <summary>Reset all state so this component can be reused.</summary>
    public void ResetInteraction()
    {
        isActive = false;
        isCompleted = false;
        isGrabbed = false;
        accumulatedRotation = 0f;
        currentProgress = 0f;
        currentAxisValue = 0f;
        enabled = false;
        if (targetTransform != null) targetTransform.localRotation = originalLocalRotation;
    }

    public float GetProgress() => currentProgress;

    private float ReadThumbstickY()
    {
        float yVal = 0f;

        // 1. Try assigned InputActionReference
        if (thumbstickInput != null && thumbstickInput.action != null)
        {
            if (!thumbstickInput.action.enabled) thumbstickInput.action.Enable();
            Vector2 val = thumbstickInput.action.ReadValue<Vector2>();
            yVal = val.y;
            if (Mathf.Abs(yVal) < deadzone && Mathf.Abs(val.x) > deadzone)
                yVal = val.x; // Fallback if axis is mapped on X
        }

        // 2. Try fallback input action
        if (Mathf.Abs(yVal) < deadzone && enableFallbackInputLookup)
        {
            if (fallbackAction != null)
            {
                if (!fallbackAction.enabled) fallbackAction.Enable();
                Vector2 fval = fallbackAction.ReadValue<Vector2>();
                if (Mathf.Abs(fval.y) > deadzone) yVal = fval.y;
            }
        }

        return yVal;
    }

    private void Update()
    {
        if (!isActive || isCompleted) return;

        // Check if grab requirement is satisfied
        bool currentlySelected = isGrabbed || (baseInteractable != null && baseInteractable.isSelected);
        if (requireGrabToRotate && !currentlySelected)
        {
            return;
        }

        float inputValue = ReadThumbstickY();
        if (Mathf.Abs(inputValue) < deadzone) return;

        if (invertRotation) inputValue = -inputValue;

        float deltaRotation = inputValue * rotationSpeed * Time.deltaTime;
        currentAxisValue += deltaRotation;

        // Rotate target transform around local axis relative to its original rotation
        if (targetTransform != null)
        {
            targetTransform.localRotation = originalLocalRotation * Quaternion.AngleAxis(currentAxisValue, rotationAxis);
        }

        // Accumulate intentional thumbstick rotation
        accumulatedRotation += Mathf.Abs(deltaRotation);
        currentProgress = Mathf.Clamp01(accumulatedRotation / requiredRotation);

        OnProgressChanged?.Invoke(currentProgress);
        SetPanelProgress(currentProgress, true);

        Debug.Log($"[GenericRotationInteraction] '{gameObject.name}' Input={inputValue:F2} Delta={deltaRotation:F2} TotalAcc={accumulatedRotation:F1}/{requiredRotation} ({currentProgress * 100:F0}%)");

        if (currentProgress >= 1f && !isCompleted)
        {
            isCompleted = true;
            SetPanelProgress(1f, false);
            Deactivate();
            OnCompleted?.Invoke();
            Debug.Log($"[GenericRotationInteraction] COMPLETED on '{gameObject.name}'!");
        }
    }

    private void LateUpdate()
    {
        // Lock world position to prevent drift during rotation
        if (isActive && targetTransform != null)
        {
            targetTransform.position = lockedWorldPosition;
        }
    }

    private void SetPanelProgress(float progress, bool visible)
    {
        var panel = Object.FindFirstObjectByType<TruckTyreReplacement.UI.TrainingInstructionPanel>();
        if (panel != null)
        {
            panel.SetProgressVisible(visible);
            panel.SetProgress(progress);
        }
    }
}
