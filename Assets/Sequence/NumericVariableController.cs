using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Inspector-configurable condition evaluated against a NumericVariableController value.
/// </summary>
[System.Serializable]
public class VariableCondition
{
    [Tooltip("Label for this condition (Inspector readability only).")]
    public string conditionName = "Condition";
    public VariableConditionOperator op = VariableConditionOperator.LessThan;
    public float threshold = 0f;

    [Header("Highlight Actions (when condition becomes true)")]
    public List<GameObject> highlightOnTrue = new List<GameObject>();
    public List<GameObject> removeHighlightOnTrue = new List<GameObject>();

    [Header("SetActive Actions (when condition becomes true)")]
    public List<GameObject> activateOnTrue = new List<GameObject>();
    public List<GameObject> deactivateOnTrue = new List<GameObject>();

    [Header("Events")]
    public UnityEvent OnConditionMet;
    public UnityEvent OnConditionLost;

    [HideInInspector] public bool wasTrueLastFrame = false;
}

public enum VariableConditionOperator
{
    LessThan,
    LessOrEqual,
    Equal,
    GreaterOrEqual,
    GreaterThan,
    NotEqual
}

public enum InflationState
{
    PRESSURE_CHECK,
    LOW_PRESSURE,
    HIGH_PRESSURE,
    CORRECT_PRESSURE,
    WAITING_FOR_SELECT,
    SELECT_PRESSED,
    WAITING_FOR_PIPE_GRAB,
    PIPE_GRABBED,
    COMPLETE
}

/// <summary>
/// Generic numeric variable controller for Inspector-driven VR training.
/// Implements explicit InflationState state machine and warning card visibility controls.
/// </summary>
[AddComponentMenu("Vedanta/Interactions/Numeric Variable Controller")]
public class NumericVariableController : MonoBehaviour
{
    [Header("VARIABLE CONFIGURATION")]
    public string variableName = "Variable";
    public float initialValue  = 0f;
    public float currentValue  = 0f;
    [SerializeField] private float targetPressure = 110f;
    public float targetValue   = 110f;
    public float pressureTolerance = 0.5f;
    public float minimumValue  = 0f;
    public float maximumValue  = 200f;
    public float incrementStep = 10f;
    public float decrementStep = 10f;

    [Header("DISPLAY")]
    public TextMeshProUGUI displayText;
    public string displayFormat = "{0} PSI";

    [Header("OBJECT LOCKING (Optional)")]
    public GameObject objectToLock;
    public Transform  lockAtTransform;

    [Header("AUDIO SFX & VOICE")]
    public AudioClip incrementSFX;
    public AudioClip decrementSFX;
    public AudioClip confirmSFX;
    public AudioClip highWarningSFX;
    public AudioClip lowWarningSFX;
    public AudioClip targetConfirmedSFX;

    [Header("UI WARNING CARDS & SELECT BUTTON & PIPE")]
    public GameObject lowWarningCard;
    public GameObject highWarningCard;
    public GameObject successCard;
    public GameObject selectButtonObject;
    public GameObject curvesPipeObject;
    public GameObject plusButtonObject;
    public GameObject minusButtonObject;

    [Header("MATERIALS & HIGHLIGHTS")]
    public Material defaultSelectMaterial;
    public Material highlightSelectMaterial;
    public Material defaultPipeMaterial;
    public Material highlightPipeMaterial;
    public Material defaultPlusMaterial;
    public Material defaultMinusMaterial;

    private MeshRenderer selectRenderer;
    private MeshRenderer pipeRenderer;
    private MeshRenderer plusRenderer;
    private MeshRenderer minusRenderer;

    [Header("STATE MACHINE (Read-Only)")]
    public InflationState currentState = InflationState.PRESSURE_CHECK;
    public bool isSelectReady = false;

    [Header("TOP-LEVEL EVENTS")]
    public UnityEvent<float> OnValueChanged;
    public UnityEvent OnTooLow;
    public UnityEvent OnTooHigh;
    public UnityEvent OnTargetReached;
    public UnityEvent OnConfirmed;
    public UnityEvent OnConfirmFailed;

    [Header("ADVANCED CONDITIONS (Optional)")]
    public List<VariableCondition> conditions = new List<VariableCondition>();

    private bool isInitialized = false;

    private void Awake() { enabled = false; }

    public void Initialize()
    {
        enabled = true;
        isInitialized = true;
        currentState = InflationState.PRESSURE_CHECK;
        targetValue = targetPressure;

        InitMaterials();

        var defMat = GetDefaultButtonMaterial();
        if (plusButtonObject != null)
        {
            var rnd = plusButtonObject.GetComponent<MeshRenderer>();
            if (rnd != null && defMat != null) rnd.sharedMaterials = new Material[] { defMat };
        }
        if (minusButtonObject != null)
        {
            var rnd = minusButtonObject.GetComponent<MeshRenderer>();
            if (rnd != null && defMat != null) rnd.sharedMaterials = new Material[] { defMat };
        }
        if (selectButtonObject != null)
        {
            var rnd = selectButtonObject.GetComponent<MeshRenderer>();
            if (rnd != null && defMat != null) rnd.sharedMaterials = new Material[] { defMat };
        }

        if (SequenceHelperFunctions.instance != null)
        {
            SequenceHelperFunctions.instance.ClearAllHighlights();
        }

        // Ensure pipe 2 is inactive on startup / initialization
        var allGOs = UnityEngine.Object.FindObjectsByType<UnityEngine.GameObject>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
        foreach (var g in allGOs)
        {
            if (g.name == "pipe 2")
            {
                g.SetActive(false);
                break;
            }
        }

        // Move player to AirfillPoint if not already there
        var airfillPoint = UnityEngine.GameObject.Find("AirfillPoint");
        if (airfillPoint != null)
        {
            var mgr = GetManager();
            if (mgr != null) mgr.MovePlayerTo(airfillPoint.transform);
        }

        // Position cards explicitly at target transform Vector3(9.38000011, -13.323, 6.13700008)
        UnityEngine.Vector3 targetCardPos = new UnityEngine.Vector3(9.38000011f, -13.323f, 6.13700008f);
        if (lowWarningCard != null) lowWarningCard.transform.position = targetCardPos;
        if (highWarningCard != null) highWarningCard.transform.position = targetCardPos;
        if (successCard != null) successCard.transform.position = targetCardPos;

        DisablePlusHighlight();
        DisableMinusHighlight();
        DisableSelectInteraction();
        DisablePipeInteraction();
        HideAllCards();

        foreach (var c in conditions) c.wasTrueLastFrame = false;
        currentValue = initialValue;
        UpdateDisplay();
        EvaluateState();
        EvaluateAdvancedConditions();
        Debug.Log($"[Tyre] Current PSI: {currentValue}");
        Debug.Log($"[Tyre] Target PSI: {targetPressure}");
        Debug.Log($"[Tyre] Pressure State: {currentState}");
    }

    private void InitMaterials()
    {
        var airbum = UnityEngine.GameObject.Find("AIRBUM");
        if (airbum != null)
        {
            if (plusButtonObject == null) plusButtonObject = airbum.transform.Find("+_button")?.gameObject;
            if (minusButtonObject == null) minusButtonObject = airbum.transform.Find("-_button")?.gameObject;
            if (selectButtonObject == null) selectButtonObject = airbum.transform.Find("select")?.gameObject;
        }

        if (selectButtonObject != null && selectRenderer == null)
            selectRenderer = selectButtonObject.GetComponent<MeshRenderer>();

        if (selectButtonObject != null)
        {
            var sel001 = selectButtonObject.transform.parent != null ? selectButtonObject.transform.parent.Find("select.001") : null;
            var sel001Renderer = sel001 != null ? sel001.GetComponent<MeshRenderer>() : null;

            if (sel001Renderer != null && sel001Renderer.sharedMaterial != null)
            {
                defaultSelectMaterial = sel001Renderer.sharedMaterial;
            }
            else if (selectRenderer != null && selectRenderer.sharedMaterial != null && !selectRenderer.sharedMaterial.name.Contains("Highlight"))
            {
                defaultSelectMaterial = selectRenderer.sharedMaterial;
            }
        }

        if (defaultSelectMaterial != null)
        {
            defaultPlusMaterial = defaultSelectMaterial;
            defaultMinusMaterial = defaultSelectMaterial;
        }

        var pipe = curvesPipeObject != null ? curvesPipeObject : UnityEngine.GameObject.Find("Curves pipe");
        if (pipe != null)
        {
            curvesPipeObject = pipe;
            if (pipeRenderer == null) pipeRenderer = pipe.GetComponent<MeshRenderer>();
            if (pipeRenderer != null && (defaultPipeMaterial == null || defaultPipeMaterial.name.Contains("Highlight")))
            {
                if (pipeRenderer.sharedMaterial != null && !pipeRenderer.sharedMaterial.name.Contains("Highlight"))
                    defaultPipeMaterial = pipeRenderer.sharedMaterial;
            }
        }

        if (selectButtonObject != null && selectRenderer == null)
            selectRenderer = selectButtonObject.GetComponent<MeshRenderer>();

        if (selectButtonObject != null)
        {
            var sel001 = selectButtonObject.transform.parent != null ? selectButtonObject.transform.parent.Find("select.001") : null;
            var sel001Renderer = sel001 != null ? sel001.GetComponent<MeshRenderer>() : null;

            if (sel001Renderer != null && sel001Renderer.sharedMaterial != null)
            {
                defaultSelectMaterial = sel001Renderer.sharedMaterial;
            }
            else if (selectRenderer != null && selectRenderer.sharedMaterial != null && !selectRenderer.sharedMaterial.name.Contains("Highlight"))
            {
                defaultSelectMaterial = selectRenderer.sharedMaterial;
            }
        }

#if UNITY_EDITOR
        if (highlightSelectMaterial == null)
            highlightSelectMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/TruckTyreReplacement/Materials/M_Highlight_FluorescentGreen.mat");
        if (highlightPipeMaterial == null)
            highlightPipeMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/TruckTyreReplacement/Materials/M_Highlight_FluorescentGreen.mat");
#endif
    }

    public void Increment()
    {
        if (!isInitialized || currentState == InflationState.SELECT_PRESSED || currentState == InflationState.WAITING_FOR_PIPE_GRAB || currentState == InflationState.COMPLETE) return;
        PlaySFX(incrementSFX);
        float before = currentValue;
        SetValue(currentValue + incrementStep);
        Debug.Log($"[Tyre] Current PSI: {currentValue}");
        Debug.Log($"[Tyre] Pressure State: {currentState}");
    }

    public void Decrement()
    {
        if (!isInitialized || currentState == InflationState.SELECT_PRESSED || currentState == InflationState.WAITING_FOR_PIPE_GRAB || currentState == InflationState.COMPLETE) return;
        PlaySFX(decrementSFX);
        float before = currentValue;
        SetValue(currentValue - decrementStep);
        Debug.Log($"[Tyre] Current PSI: {currentValue}");
        Debug.Log($"[Tyre] Pressure State: {currentState}");
    }

    public void SetValue(float value)
    {
        currentValue = Mathf.Clamp(value, minimumValue, maximumValue);
        OnValueChanged?.Invoke(currentValue);
        UpdateDisplay();
        EvaluateState();
        EvaluateAdvancedConditions();
    }

    public void Confirm()
    {
        if (!isInitialized) return;
        if (currentState != InflationState.WAITING_FOR_SELECT && currentState != InflationState.CORRECT_PRESSURE)
        {
            Debug.Log($"[AIR PRESSURE SELECT] Blocked — state={currentState}, value={currentValue} != {targetPressure}");
            return;
        }

        if (Mathf.Abs(currentValue - targetPressure) <= pressureTolerance)
        {
            OnSelectPressed();
        }
        else
        {
            PlaySFX(confirmSFX);
            OnConfirmFailed?.Invoke();
            Debug.Log($"[AIR PRESSURE SELECT] FAILED — value={currentValue} != target={targetPressure}");
        }
    }

    private System.Collections.IEnumerator SelectPressedRoutine()
    {
        var mgr = TruckTyreReplacement.Core.Manager.Instance;
        AudioSource voiceSrc = mgr != null ? mgr.GetVoiceAudioSource() : null;

        Debug.Log("[AUDIO FLOW] SELECT PRESSED");
        Debug.Log("Key = select");
        if (mgr != null)
        {
            var anchor = UnityEngine.Object.FindFirstObjectByType<TruckTyreReplacement.UI.TrainingHologramAnchor>();
            if (anchor != null) anchor.SetDescription(mgr.GetDisplayText("select"));

            mgr.Speak("select");
        }

        if (voiceSrc != null && mgr != null)
        {
            float start = Time.realtimeSinceStartup;
            yield return new WaitUntil(() => mgr.IsSpeaking || (Time.realtimeSinceStartup - start) > 1.0f);
            if (mgr.IsSpeaking || voiceSrc.isPlaying)
            {
                yield return new WaitUntil(() => !voiceSrc.isPlaying && !mgr.IsSpeaking);
            }
        }
        else
        {
            yield return new WaitForSeconds(1.0f);
        }

        Debug.Log("[AUDIO FLOW] SELECT AUDIO COMPLETE");
        HideAllCards();
        currentState = InflationState.WAITING_FOR_PIPE_GRAB;

        OnConfirmed?.Invoke();
    }

    public void OnPipeGrabbedSelectEntered(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args) => OnPipeGrabbed();

    public void OnPipeGrabbed()
    {
        if (currentState != InflationState.WAITING_FOR_PIPE_GRAB)
        {
            var sh = SequenceHandler.instance != null ? SequenceHandler.instance : UnityEngine.Object.FindFirstObjectByType<SequenceHandler>();
            if (sh == null || (sh.currentTask != 2 && sh.currentTask != 3))
            {
                Debug.Log($"[Tyre] OnPipeGrabbed BLOCKED — state={currentState}");
                return;
            }
        }

        currentState = InflationState.PIPE_GRABBED;
        Debug.Log("[Tyre] Curves pipe ACTUALLY GRABBED");

        DisablePipeInteraction();

        // Enable pipe 2 on successful grab
        var allGOs = UnityEngine.Object.FindObjectsByType<UnityEngine.GameObject>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
        foreach (var g in allGOs)
        {
            if (g.name == "pipe 2")
            {
                g.SetActive(true);
                Debug.Log("[Tyre] Activated pipe 2 upon Curves pipe grab.");
                break;
            }
        }

        // Disable Curves pipe GameObject so it hides after being grabbed
        var pipe = curvesPipeObject != null ? curvesPipeObject : UnityEngine.GameObject.Find("Curves pipe");
        if (pipe != null)
        {
            pipe.SetActive(false);
            Debug.Log("[Tyre] Disabled Curves pipe GameObject upon grab.");
        }

        StartCoroutine(AirFillingSoundAndCompleteRoutine());
    }

    private System.Collections.IEnumerator AirFillingSoundAndCompleteRoutine()
    {
        var mgr = GetManager();

        // Stop any currently playing TTS speech (like pipe_grab instruction) immediately on grab
        if (mgr != null)
        {
            mgr.StopSpeech();
        }

        AudioSource sfxSrc = mgr != null ? mgr.GetSFXAudioSource() : null;

        Debug.Log("[AUDIO FLOW] Air Filling Sound START");
        AudioClip airFillClip = Resources.Load<AudioClip>("Audio/Air Filling Sound");
        if (airFillClip == null) airFillClip = Resources.Load<AudioClip>("Air Filling Sound");
        if (airFillClip == null && targetConfirmedSFX != null) airFillClip = targetConfirmedSFX;

        if (airFillClip != null && mgr != null)
        {
            mgr.PlaySFX(airFillClip);
            if (sfxSrc != null)
            {
                float start = Time.realtimeSinceStartup;
                yield return new WaitUntil(() => sfxSrc.isPlaying || (Time.realtimeSinceStartup - start) > 0.5f);
                if (sfxSrc.isPlaying)
                {
                    yield return new WaitUntil(() => !sfxSrc.isPlaying);
                }
            }
            else
            {
                yield return new WaitForSeconds(airFillClip.length);
            }
        }
        else
        {
            yield return new WaitForSeconds(2.0f);
        }
        Debug.Log("[AUDIO FLOW] Air Filling Sound ENDS");
        yield return new WaitForSeconds(0.3f);

        currentState = InflationState.COMPLETE;

        if (SequenceHelperFunctions.instance != null)
            SequenceHelperFunctions.instance.OnObjectGrabbed();
    }

    public void ConnectCurvesPipeToWhlHdFr()
    {
        var pipe = curvesPipeObject != null ? curvesPipeObject : UnityEngine.GameObject.Find("Curves pipe");
        var whl = UnityEngine.GameObject.Find("Whl HD FR");

        if (pipe != null && whl != null)
        {
            Debug.Log("[Tyre] Connecting Curves pipe → Whl HD FR");
            pipe.transform.SetParent(whl.transform, true);
            pipe.transform.localPosition = new UnityEngine.Vector3(0f, 0.1f, 0.2f);
            pipe.transform.localRotation = UnityEngine.Quaternion.identity;
        }
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        if (TruckTyreReplacement.Core.Manager.Instance != null)
            TruckTyreReplacement.Core.Manager.Instance.PlaySFX(clip);
        else if (SequenceHelperFunctions.instance != null)
            SequenceHelperFunctions.instance.PlaySFX(clip);
    }

    public void ResetValue()
    {
        currentState = InflationState.PRESSURE_CHECK;
        DisableSelectInteraction();
        DisablePipeInteraction();
        HideAllCards();
        SetValue(initialValue);
    }

    public void Deactivate()
    {
        isInitialized = false;
        DisableSelectInteraction();
        DisablePipeInteraction();
        HideAllCards();
        enabled = false;
    }

    public bool  IsAtTarget()      => Mathf.Abs(currentValue - targetPressure) <= pressureTolerance;
    public float GetCurrentValue() => currentValue;
    public float GetTargetValue()  => targetPressure;

    private void LateUpdate()
    {
        if (isInitialized && objectToLock != null && lockAtTransform != null)
            objectToLock.transform.SetPositionAndRotation(lockAtTransform.position, lockAtTransform.rotation);
    }

    private void UpdateDisplay()
    {
        if (displayText != null)
            displayText.text = string.Format(displayFormat, currentValue);
    }

    private TruckTyreReplacement.Core.Manager GetManager()
    {
        return TruckTyreReplacement.Core.Manager.Instance != null
            ? TruckTyreReplacement.Core.Manager.Instance
            : UnityEngine.Object.FindFirstObjectByType<TruckTyreReplacement.Core.Manager>();
    }

    private void EvaluateState()
    {
        if (currentState == InflationState.SELECT_PRESSED || currentState == InflationState.WAITING_FOR_PIPE_GRAB || currentState == InflationState.PIPE_GRABBED || currentState == InflationState.COMPLETE)
        {
            return;
        }

        bool isLow     = (currentValue < targetPressure) && (Mathf.Abs(currentValue - targetPressure) > pressureTolerance);
        bool isHigh    = (currentValue > targetPressure) && (Mathf.Abs(currentValue - targetPressure) > pressureTolerance);
        bool isCorrect = Mathf.Abs(currentValue - targetPressure) <= pressureTolerance;

        var mgr = GetManager();

        if (isLow)
        {
            if (currentState != InflationState.LOW_PRESSURE)
            {
                currentState = InflationState.LOW_PRESSURE;
                EnablePlusHighlight();
                DisableMinusHighlight();
                DisableSelectInteraction();
                DisablePipeInteraction();

                ShowCard(lowWarningCard);
                HideCard(highWarningCard);
                HideCard(successCard);

                Debug.Log("Playing voice key: low_pressure");
                if (mgr != null) mgr.Speak("low_pressure");

                OnTooLow?.Invoke();
            }
        }
        else if (isHigh)
        {
            if (currentState != InflationState.HIGH_PRESSURE)
            {
                currentState = InflationState.HIGH_PRESSURE;
                DisablePlusHighlight();
                EnableMinusHighlight();
                DisableSelectInteraction();
                DisablePipeInteraction();

                ShowCard(highWarningCard);
                HideCard(lowWarningCard);
                HideCard(successCard);

                Debug.Log("Playing voice key: high_pressure");
                if (mgr != null) mgr.Speak("high_pressure");

                OnTooHigh?.Invoke();
            }
        }
        else if (isCorrect)
        {
            if (currentState != InflationState.CORRECT_PRESSURE && currentState != InflationState.WAITING_FOR_SELECT)
            {
                currentState = InflationState.CORRECT_PRESSURE;
                Debug.Log("[Tyre] CORRECT PRESSURE — 110 PSI");
                DisablePlusHighlight();
                DisableMinusHighlight();
                DisablePipeInteraction();

                ShowCard(successCard);
                HideCard(lowWarningCard);
                HideCard(highWarningCard);

                Debug.Log("Playing voice key: correct_pressure");
                if (mgr != null) mgr.Speak("correct_pressure");

                currentState = InflationState.WAITING_FOR_SELECT;
                EnableSelectInteraction();
                OnTargetReached?.Invoke();
            }
        }
    }

    private Material GetDefaultButtonMaterial()
    {
        if (defaultSelectMaterial != null)
        {
            try
            {
                if (!string.IsNullOrEmpty(defaultSelectMaterial.name) && !defaultSelectMaterial.name.Contains("Highlight"))
                    return defaultSelectMaterial;
            }
            catch { }
        }

        var airbum = UnityEngine.GameObject.Find("AIRBUM");
        if (airbum != null)
        {
            foreach (Transform child in airbum.transform)
            {
                var r = child.GetComponent<MeshRenderer>();
                if (r != null && r.sharedMaterial != null && !string.IsNullOrEmpty(r.sharedMaterial.name) && !r.sharedMaterial.name.Contains("Highlight"))
                {
                    defaultSelectMaterial = r.sharedMaterial;
                    return r.sharedMaterial;
                }
            }
        }

        var allRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
        foreach (var r in allRenderers)
        {
            if (r != null && r.sharedMaterial != null && !string.IsNullOrEmpty(r.sharedMaterial.name) && r.sharedMaterial.name.Contains("ChatGPT Image"))
            {
                defaultSelectMaterial = r.sharedMaterial;
                return r.sharedMaterial;
            }
        }

        return defaultSelectMaterial;
    }

    public void EnablePlusHighlight()
    {
        var airbum = UnityEngine.GameObject.Find("AIRBUM");
        if (plusButtonObject == null && airbum != null) plusButtonObject = airbum.transform.Find("+_button")?.gameObject;
        if (plusButtonObject != null)
        {
            var rnd = plusButtonObject.GetComponent<MeshRenderer>();
            if (rnd != null && highlightSelectMaterial != null)
            {
                rnd.sharedMaterial = highlightSelectMaterial;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(rnd);
#endif
            }

            Debug.Log("[Tyre] Plus Button Highlight ON");
        }
    }

    public void DisablePlusHighlight()
    {
        var airbum = UnityEngine.GameObject.Find("AIRBUM");
        if (plusButtonObject == null && airbum != null) plusButtonObject = airbum.transform.Find("+_button")?.gameObject;
        if (plusButtonObject != null)
        {
            if (SequenceHelperFunctions.instance != null)
                SequenceHelperFunctions.instance.RemoveSafeGhostHighlight(plusButtonObject);

            var rnd = plusButtonObject.GetComponent<MeshRenderer>();
            var defMat = GetDefaultButtonMaterial();
            if (rnd != null && defMat != null)
            {
                rnd.sharedMaterials = new Material[] { defMat };
                rnd.materials = new Material[] { defMat };
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(rnd);
#endif
            }

            Debug.Log("[Tyre] Plus Button Highlight OFF");
        }
    }

    public void EnableMinusHighlight()
    {
        var airbum = UnityEngine.GameObject.Find("AIRBUM");
        if (minusButtonObject == null && airbum != null) minusButtonObject = airbum.transform.Find("-_button")?.gameObject;
        if (minusButtonObject != null)
        {
            var rnd = minusButtonObject.GetComponent<MeshRenderer>();
            if (rnd != null && highlightSelectMaterial != null)
            {
                rnd.sharedMaterial = highlightSelectMaterial;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(rnd);
#endif
            }

            Debug.Log("[Tyre] Minus Button Highlight ON");
        }
    }

    public void DisableMinusHighlight()
    {
        var airbum = UnityEngine.GameObject.Find("AIRBUM");
        if (minusButtonObject == null && airbum != null) minusButtonObject = airbum.transform.Find("-_button")?.gameObject;
        if (minusButtonObject != null)
        {
            if (SequenceHelperFunctions.instance != null)
                SequenceHelperFunctions.instance.RemoveSafeGhostHighlight(minusButtonObject);

            var rnd = minusButtonObject.GetComponent<MeshRenderer>();
            var defMat = GetDefaultButtonMaterial();
            if (rnd != null && defMat != null)
            {
                rnd.sharedMaterials = new Material[] { defMat };
                rnd.materials = new Material[] { defMat };
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(rnd);
#endif
            }

            Debug.Log("[Tyre] Minus Button Highlight OFF");
        }
    }

    public void EnableSelectInteraction()
    {
        isSelectReady = true;
        if (selectButtonObject != null)
        {
            selectButtonObject.SetActive(true);
            var interactable = selectButtonObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
            if (interactable != null) interactable.enabled = true;
            var col = selectButtonObject.GetComponent<UnityEngine.Collider>();
            if (col != null) col.enabled = true;

            if (selectRenderer == null) selectRenderer = selectButtonObject.GetComponent<MeshRenderer>();
            if (selectRenderer != null && highlightSelectMaterial != null)
                selectRenderer.sharedMaterial = highlightSelectMaterial;

            if (SequenceHelperFunctions.instance != null)
                SequenceHelperFunctions.instance.ApplySafeGhostHighlight(selectButtonObject);

            Debug.Log("[Tyre] Select Highlight ON");
        }
    }

    public void DisableSelectInteraction()
    {
        isSelectReady = false;
        if (selectButtonObject != null)
        {
            var interactable = selectButtonObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
            if (interactable != null) interactable.enabled = false;

            if (SequenceHelperFunctions.instance != null)
                SequenceHelperFunctions.instance.RemoveSafeGhostHighlight(selectButtonObject);

            if (selectRenderer == null) selectRenderer = selectButtonObject.GetComponent<MeshRenderer>();
            if (selectRenderer != null && defaultSelectMaterial != null)
                selectRenderer.sharedMaterial = defaultSelectMaterial;

            Debug.Log("[Tyre] Select Highlight OFF");
        }
    }

    public void EnablePipeInteraction()
    {
        currentState = InflationState.WAITING_FOR_PIPE_GRAB;
        var pipe = curvesPipeObject != null ? curvesPipeObject : UnityEngine.GameObject.Find("Curves pipe");
        if (pipe == null)
        {
            var allGOs = UnityEngine.Object.FindObjectsByType<UnityEngine.GameObject>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            foreach (var g in allGOs) if (g.name == "Curves pipe") { pipe = g; break; }
        }

        if (pipe != null)
        {
            curvesPipeObject = pipe;
            pipe.SetActive(true);

            // Ensure player is positioned at AirfillPoint facing AIRBUM and Curves pipe
            var airfillPoint = UnityEngine.GameObject.Find("AirfillPoint");
            if (airfillPoint != null)
            {
                var mgr = GetManager();
                if (mgr != null) mgr.MovePlayerTo(airfillPoint.transform);
            }

            var grab = pipe.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab == null) grab = pipe.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

            if (grab != null)
            {
                grab.enabled = true;
                grab.interactionLayers = -1; // Allow all interaction layers
                grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Instantaneous;
                grab.trackPosition = true;
                grab.trackRotation = true;
                grab.throwOnDetach = false;

                if (grab.interactionManager == null)
                {
                    grab.interactionManager = UnityEngine.Object.FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
                }

                var col = pipe.GetComponent<UnityEngine.Collider>();
                if (col != null)
                {
                    col.enabled = true;
                    if (!grab.colliders.Contains(col))
                        grab.colliders.Add(col);
                }

                grab.selectEntered.RemoveListener(OnPipeGrabbedSelectEntered);
                grab.selectEntered.AddListener(OnPipeGrabbedSelectEntered);

#if UNITY_EDITOR
                var so = new UnityEditor.SerializedObject(grab);
                var farProp = so.FindProperty("m_FarAttachMode");
                if (farProp != null)
                {
                    farProp.enumValueIndex = 2; // Far attach mode (allows raycast grab)
                    so.ApplyModifiedProperties();
                }
#endif
            }

            var colBox = pipe.GetComponent<UnityEngine.BoxCollider>();
            if (colBox != null)
            {
                colBox.enabled = true;
                colBox.isTrigger = false;
                colBox.size = new Vector3(0.005f, 0.015f, 0.01f);
                colBox.center = Vector3.zero;
            }

            var rb = pipe.GetComponent<UnityEngine.Rigidbody>();
            if (rb == null) rb = pipe.AddComponent<UnityEngine.Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            var gd = pipe.GetComponent<GrabDetect>();
            if (gd != null) gd.ActivateGrab();

            if (pipeRenderer == null) pipeRenderer = pipe.GetComponent<MeshRenderer>();
            if (pipeRenderer != null && highlightPipeMaterial != null)
                pipeRenderer.sharedMaterial = highlightPipeMaterial;

            if (SequenceHelperFunctions.instance != null)
                SequenceHelperFunctions.instance.ApplySafeGhostHighlight(pipe);

            Debug.Log("[Tyre] Curves Pipe Highlight ON (Player Moved to AirfillPoint & Far Raycast Enabled)");
        }
    }

    public void DisablePipeInteraction()
    {
        var pipe = curvesPipeObject != null ? curvesPipeObject : UnityEngine.GameObject.Find("Curves pipe");
        if (pipe == null)
        {
            var allGOs = UnityEngine.Object.FindObjectsByType<UnityEngine.GameObject>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
            foreach (var g in allGOs) if (g.name == "Curves pipe") { pipe = g; break; }
        }

        if (pipe != null)
        {
            curvesPipeObject = pipe;
            var grab = pipe.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null) grab.enabled = false;

            var gd = pipe.GetComponent<GrabDetect>();
            if (gd != null) gd.DeactivateGrab();

            if (SequenceHelperFunctions.instance != null)
                SequenceHelperFunctions.instance.RemoveSafeGhostHighlight(pipe);

            if (pipeRenderer == null) pipeRenderer = pipe.GetComponent<MeshRenderer>();
            if (pipeRenderer != null && defaultPipeMaterial != null)
                pipeRenderer.sharedMaterial = defaultPipeMaterial;

            Debug.Log("[Tyre] Curves Pipe Highlight OFF");
        }
    }

    public void ShowSelectButton() => EnableSelectInteraction();
    public void HideSelectButton() => DisableSelectInteraction();

    public void OnSelectPressed()
    {
        if (currentState == InflationState.SELECT_PRESSED || currentState == InflationState.WAITING_FOR_PIPE_GRAB || currentState == InflationState.PIPE_GRABBED || currentState == InflationState.COMPLETE) return;

        Debug.Log("[Tyre] Select PRESSED");
        currentState = InflationState.SELECT_PRESSED;
        DisablePlusHighlight();
        DisableMinusHighlight();
        DisableSelectInteraction();
        StartCoroutine(SelectPressedRoutine());
    }

    private void ShowCard(GameObject card)
    {
        if (card == null) return;
        card.transform.position = new UnityEngine.Vector3(9.38000011f, -13.323f, 6.13700008f);
        card.SetActive(true);
        card.transform.localScale = new UnityEngine.Vector3(0.5f, 0.5f, 0.5f);
        var cg = card.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }

    private void HideCard(GameObject card)
    {
        if (card == null) return;
        card.SetActive(false);
        var cg = card.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 0f;
        }
    }

    public void HideAllCards()
    {
        HideCard(lowWarningCard);
        HideCard(highWarningCard);
        HideCard(successCard);
    }

    private void EvaluateAdvancedConditions()
    {
        foreach (var cond in conditions)
        {
            bool isNowTrue = EvalOp(cond);
            if (isNowTrue && !cond.wasTrueLastFrame)
            {
                ApplyHighlights(cond.highlightOnTrue, true);
                ApplyHighlights(cond.removeHighlightOnTrue, false);
                ApplyActive(cond.activateOnTrue, true);
                ApplyActive(cond.deactivateOnTrue, false);
                cond.OnConditionMet?.Invoke();
            }
            else if (!isNowTrue && cond.wasTrueLastFrame)
            {
                cond.OnConditionLost?.Invoke();
            }
            cond.wasTrueLastFrame = isNowTrue;
        }
    }

    private bool EvalOp(VariableCondition cond)
    {
        switch (cond.op)
        {
            case VariableConditionOperator.LessThan:      return currentValue < cond.threshold;
            case VariableConditionOperator.LessOrEqual:   return currentValue <= cond.threshold;
            case VariableConditionOperator.Equal:         return Mathf.Approximately(currentValue, cond.threshold);
            case VariableConditionOperator.GreaterOrEqual:return currentValue >= cond.threshold;
            case VariableConditionOperator.GreaterThan:   return currentValue > cond.threshold;
            case VariableConditionOperator.NotEqual:      return !Mathf.Approximately(currentValue, cond.threshold);
            default: return false;
        }
    }

    private void ApplyHighlights(List<GameObject> targets, bool highlight)
    {
        if (SequenceHelperFunctions.instance == null) return;
        foreach (var t in targets)
        {
            if (t == null) continue;
            if (highlight) SequenceHelperFunctions.instance.ApplySafeGhostHighlight(t);
            else           SequenceHelperFunctions.instance.RemoveSafeGhostHighlight(t);
        }
    }

    private void ApplyActive(List<GameObject> targets, bool active)
    {
        foreach (var t in targets) { if (t != null) t.SetActive(active); }
    }
}
