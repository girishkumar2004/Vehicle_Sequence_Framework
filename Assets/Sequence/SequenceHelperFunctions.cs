using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Switch;
using TruckTyreReplacement.Core;

// ─────────────────────────────────────────────────────────────────────────────
// ObjectMovementMapping
// Maps a string key to a source GameObject + destination Transform.
// Used by MoveObjectToDestination(key) for Inspector-configurable object movement.
// ─────────────────────────────────────────────────────────────────────────────

[System.Serializable]
public class ObjectMovementMapping
{
    [Tooltip("Unique key used to call MoveObjectToDestination(key) from a UnityEvent.")]
    public string key;
    [Tooltip("The object to move.")]
    public GameObject sourceObject;
    [Tooltip("The destination transform.")]
    public Transform destination;
}

// ─────────────────────────────────────────────────────────────────────────────
// GrabSnapMapping
// ─────────────────────────────────────────────────────────────────────────────

[System.Serializable]
public class GrabSnapMapping
{
    public string Name;
    public GameObject GrabbableObject;
    public GameObject GhostTargetObject;

    [HideInInspector] public Vector3 InitialPosition;
    [HideInInspector] public Quaternion InitialRotation;
    [HideInInspector] public bool IsInsideTargetCollider;
}

// ─────────────────────────────────────────────────────────────────────────────
// SequenceAnimation
// ─────────────────────────────────────────────────────────────────────────────

[System.Serializable]
public class SequenceAnimation
{
    public string animationName = "Describe Animation Here";
    public Animator targetAnimator;
    public string triggerName;
    public float animationDuration = 2.0f;
}

// ─────────────────────────────────────────────────────────────────────────────
// SequenceHelperFunctions
// Generic reusable operations for the Sequence framework.
// ALL methods are Inspector-callable via UnityEvent.
// No module-specific logic (no tyre, wheel, AIRBUM, pipe references).
// ─────────────────────────────────────────────────────────────────────────────

[RequireComponent(typeof(SequenceHandler))]
public class SequenceHelperFunctions : MonoBehaviour
{
    public static SequenceHelperFunctions instance;
    [SerializeField] SequenceHandler handler;

    // ── AUDIO ─────────────────────────────────────────────────────────────────
    [Header("Audio Sources")]
    [SerializeField] AudioSource BG_Audio;
    [SerializeField] AudioSource Voice_Audio;
    [SerializeField] private Coroutine currentAudioCoroutine;

    // ── ANIMATION ─────────────────────────────────────────────────────────────
    [Header("Animations Configurations")]
    public List<SequenceAnimation> sequenceAnimations = new List<SequenceAnimation>();

    // ── FADE ──────────────────────────────────────────────────────────────────
    [Space(4)]
    [Header("Fade Controller")]
    [SerializeField] GameObject _fadeobj;
    [SerializeField] Material fadeMaterial;
    [SerializeField] float fadeDuration = 1f;

    // ── PLAYER ────────────────────────────────────────────────────────────────
    [Space(4)]
    [Header("Player (XR Origin)")]
    [Tooltip("Assign the XR Origin (VR) GameObject here for TeleportPlayer().")]
    [SerializeField] GameObject player;
    [SerializeField] private Transform _nextPlayerTransform;

    // ── UI ────────────────────────────────────────────────────────────────────
    [Space(4)]
    [Header("Canvas")]
    [SerializeField] GameObject UI_Canvas;
    [SerializeField] TextMeshProUGUI TitleText;
    [SerializeField] private string titleString;
    [SerializeField] TextMeshProUGUI DescriptionText;
    [SerializeField] private string desString;
    [SerializeField] private Transform _nextCanvasTransform;

    public Button nextButton, previousButton;

    // ── HIGHLIGHT ─────────────────────────────────────────────────────────────
    [SerializeField] Material transparentMaterial;
    public Material HighlightMaterial { get => transparentMaterial; set => transparentMaterial = value; }
    public bool _isTransparent;
    private Coroutine transparencyCoroutine;
    private Dictionary<Renderer, Material[]> originalMaterialsDict = new Dictionary<Renderer, Material[]>();

    private List<GameObject> activeHighlightedObjects = new List<GameObject>();
    private Coroutine blinkCoroutine;
    private bool highlightBlinkState = true;

    // ── OBJECT MOVEMENT ───────────────────────────────────────────────────────
    [Space(4)]
    [Header("Object Movement Mappings")]
    [Tooltip("Pre-configure named source->destination pairs. Call MoveObjectToDestination(key) from UnityEvent.")]
    public List<ObjectMovementMapping> movementMappings = new List<ObjectMovementMapping>();

    // ── CONTROLLER ────────────────────────────────────────────────────────────
    [Header("Controller")]
    public GameObject currentTarget;

    // ── PPE ───────────────────────────────────────────────────────────────────
    [Header("PPE Management")]
    private Dictionary<GameObject, Vector3> ppeInitialPositions = new Dictionary<GameObject, Vector3>();
    private Dictionary<GameObject, Quaternion> ppeInitialRotations = new Dictionary<GameObject, Quaternion>();
    private bool isInsidePPEZone = false;
    public GameObject ppeObjectToEnable;

    // ── MULTI GRAB ────────────────────────────────────────────────────────────
    [Header("Multi-Object Grab Logic")]
    public List<GrabSnapMapping> multiGrabMappings = new List<GrabSnapMapping>();

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        instance = this;
        if (activeHighlightedObjects == null) activeHighlightedObjects = new List<GameObject>();
        if (originalMaterialsDict == null) originalMaterialsDict = new Dictionary<Renderer, Material[]>();
        activeHighlightedObjects.Clear();
        originalMaterialsDict.Clear();
    }

    private void OnEnable()
    {
        instance = this;
        if (activeHighlightedObjects == null) activeHighlightedObjects = new List<GameObject>();
        if (originalMaterialsDict == null) originalMaterialsDict = new Dictionary<Renderer, Material[]>();
        activeHighlightedObjects.Clear();
        originalMaterialsDict.Clear();
    }

    private void Start()
    {
        handler = this.GetComponent<SequenceHandler>();
        if (BG_Audio != null) BG_Audio.Play();
        RegisterAllMultiGrabObjects();

        if (transparentMaterial == null)
        {
            transparentMaterial = Resources.Load<Material>("M_Highlight_FluorescentGreen");
#if UNITY_EDITOR
            if (transparentMaterial == null)
                transparentMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/TruckTyreReplacement/Materials/M_Highlight_FluorescentGreen.mat");
#endif
        }
    }

    private void OnDisable()
    {
        if (handler != null) handler.StopAllCoroutines();
        this.StopAllCoroutines();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SEQUENCE — Task Completion
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Complete the current task. Wire from OnCompleted, OnTargetReached, OnGrabbed, etc.</summary>
    public void CompleteCurrentTask()
    {
        if (handler == null) handler = SequenceHandler.instance != null ? SequenceHandler.instance : GetComponent<SequenceHandler>();
        if (handler != null)
        {
            Debug.Log("[SequenceHelperFunctions] CompleteCurrentTask() called.");
            handler.TaskCompleted();
        }
        else
        {
            Debug.LogError("[SequenceHelperFunctions] CompleteCurrentTask: SequenceHandler not found.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PLAYER — Teleportation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Teleport the XR Origin (player) to a destination transform.</summary>
    public void TeleportPlayer(Transform destination)
    {
        if (player == null)
        {
            var go = GameObject.Find("XR Origin (VR)");
            if (go == null) go = GameObject.Find("XR Origin");
            if (go != null) player = go;
        }
        if (player != null && destination != null)
        {
            player.transform.position = destination.position;
            player.transform.rotation = destination.rotation;
            Debug.Log($"[SequenceHelperFunctions] TeleportPlayer to '{destination.name}'");
        }
        else
        {
            Debug.LogWarning("[SequenceHelperFunctions] TeleportPlayer: player or destination is null.");
        }
    }

    // Legacy alias
    public void PlayerPosChange(Transform t)
    {
        if (player != null) { player.transform.localPosition = t.localPosition; player.transform.localRotation = t.localRotation; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GAMEOBJECT — SetActive, Move
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>SetActive(true) on a GameObject. Use from UnityEvent.</summary>
    public void SetActive_True(GameObject target) { if (target != null) target.SetActive(true); }

    /// <summary>SetActive(false) on a GameObject. Use from UnityEvent.</summary>
    public void SetActive_False(GameObject target) { if (target != null) target.SetActive(false); }

    /// <summary>Toggle active state of a GameObject.</summary>
    public void ToggleActive(GameObject target) { if (target != null) target.SetActive(!target.activeSelf); }

    /// <summary>
    /// Move a named object to its configured destination.
    /// Configure source+destination pairs in the 'Movement Mappings' Inspector field.
    /// Then call with the matching key from a UnityEvent.
    /// </summary>
    public void MoveObjectToDestination(string mappingKey)
    {
        var m = movementMappings.Find(x => x.key == mappingKey);
        if (m != null && m.sourceObject != null && m.destination != null)
        {
            m.sourceObject.transform.SetPositionAndRotation(m.destination.position, m.destination.rotation);
            Debug.Log($"[SequenceHelperFunctions] Moved '{m.sourceObject.name}' to '{m.destination.name}' via mapping '{mappingKey}'");
        }
        else
        {
            Debug.LogWarning($"[SequenceHelperFunctions] MoveObjectToDestination: mapping '{mappingKey}' not found or has null references.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HIGHLIGHT — Ghost Material Blinking System
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Apply blinking ghost highlight to a GameObject (preserves original materials).</summary>
    public void ApplySafeGhostHighlight(GameObject target)
    {
        if (target == null) return;
        if (transparentMaterial == null)
        {
            Debug.LogError("[SequenceHelperFunctions] HighlightObject: transparentMaterial is null. Assign M_Highlight_FluorescentGreen in Inspector.");
            return;
        }

        if (!activeHighlightedObjects.Contains(target)) activeHighlightedObjects.Add(target);

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        Material defMat = null;
        var airbum = UnityEngine.GameObject.Find("AIRBUM");
        if (airbum != null)
        {
            foreach (Transform child in airbum.transform)
            {
                var rnd = child.GetComponent<MeshRenderer>();
                if (rnd != null && rnd.sharedMaterial != null && !rnd.sharedMaterial.name.Contains("Highlight"))
                {
                    defMat = rnd.sharedMaterial;
                    break;
                }
            }
        }

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Material[] currentMats = r.sharedMaterials;
            Material[] cleanMats = new Material[currentMats.Length];

            for (int i = 0; i < currentMats.Length; i++)
            {
                if (currentMats[i] != null && currentMats[i].name.Contains("Highlight"))
                {
                    cleanMats[i] = defMat != null ? defMat : currentMats[i];
                }
                else
                {
                    cleanMats[i] = currentMats[i];
                }
            }

            if (!originalMaterialsDict.ContainsKey(r) || (originalMaterialsDict[r] != null && originalMaterialsDict[r].Length > 0 && originalMaterialsDict[r][0].name.Contains("Highlight")))
            {
                originalMaterialsDict[r] = cleanMats;
            }
        }

        ApplyGhostMaterials(target, true);

        if (blinkCoroutine == null)
            blinkCoroutine = StartCoroutine(BlinkHighlightRoutine());
    }

    /// <summary>Generic alias: highlight any object. Wire from Sequence EventsToFollow.</summary>
    public void HighlightObject(GameObject target) => ApplySafeGhostHighlight(target);

    /// <summary>Remove blinking ghost highlight, restoring original materials.</summary>
    public void RemoveSafeGhostHighlight(GameObject target)
    {
        if (target == null) return;
        activeHighlightedObjects.RemoveAll(x => x == null || x == target || x.name == target.name);

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (originalMaterialsDict.ContainsKey(r))
            {
                r.sharedMaterials = originalMaterialsDict[r];
                originalMaterialsDict.Remove(r);
            }
        }

        if (activeHighlightedObjects.Count == 0 && blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
    }

    /// <summary>Generic alias: remove highlight. Wire from Sequence EventsToFollow.</summary>
    public void RemoveHighlight(GameObject target) => RemoveSafeGhostHighlight(target);

    private void ApplyGhostMaterials(GameObject target, bool showGhost)
    {
        if (target == null) return;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (showGhost && originalMaterialsDict.ContainsKey(r))
            {
                Material[] ghostMats = new Material[originalMaterialsDict[r].Length];
                for (int i = 0; i < ghostMats.Length; i++) ghostMats[i] = transparentMaterial;
                r.sharedMaterials = ghostMats;
            }
            else if (!showGhost && originalMaterialsDict.ContainsKey(r))
            {
                r.sharedMaterials = originalMaterialsDict[r];
            }
        }
    }

    private IEnumerator BlinkHighlightRoutine()
    {
        while (activeHighlightedObjects.Count > 0)
        {
            yield return new WaitForSeconds(0.5f);
            highlightBlinkState = !highlightBlinkState;
            for (int i = activeHighlightedObjects.Count - 1; i >= 0; i--)
            {
                var obj = activeHighlightedObjects[i];
                if (obj != null && obj.activeInHierarchy)
                    ApplyGhostMaterials(obj, highlightBlinkState);
            }
        }
        blinkCoroutine = null;
    }

    private Material GetCleanDefaultMaterial()
    {
        var airbum = UnityEngine.GameObject.Find("AIRBUM");
        if (airbum != null)
        {
            var sel001 = airbum.transform.Find("select.001");
            if (sel001 != null)
            {
                var r = sel001.GetComponent<MeshRenderer>();
                if (r != null && r.sharedMaterial != null && !r.sharedMaterial.name.Contains("Highlight"))
                    return r.sharedMaterial;
            }
            var screen = airbum.transform.Find("screen");
            if (screen != null)
            {
                var r = screen.GetComponent<MeshRenderer>();
                if (r != null && r.sharedMaterial != null && !r.sharedMaterial.name.Contains("Highlight"))
                    return r.sharedMaterial;
            }
        }

        var nvc = UnityEngine.Object.FindFirstObjectByType<NumericVariableController>();
        if (nvc != null && nvc.defaultSelectMaterial != null && !nvc.defaultSelectMaterial.name.Contains("Highlight"))
            return nvc.defaultSelectMaterial;

        var allRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
        foreach (var r in allRenderers)
        {
            if (r != null && r.sharedMaterial != null && r.sharedMaterial.name.Contains("ChatGPT Image"))
                return r.sharedMaterial;
        }

        return null;
    }

    /// <summary>Stop all highlights and restore all original materials.</summary>
    public void ClearAllHighlights()
    {
        if (blinkCoroutine != null) { StopCoroutine(blinkCoroutine); blinkCoroutine = null; }
        var cleanMat = GetCleanDefaultMaterial();
        Debug.Log($"[ClearAllHighlights] cleanMat = '{(cleanMat != null ? cleanMat.name : "null")}', originalMaterialsDict count = {originalMaterialsDict.Count}");

        foreach (var kvp in originalMaterialsDict)
        {
            if (kvp.Key != null)
            {
                if (cleanMat != null)
                {
                    kvp.Key.sharedMaterials = new Material[] { cleanMat };
                    kvp.Key.materials = new Material[] { cleanMat };
                }
            }
        }
        originalMaterialsDict.Clear();
        activeHighlightedObjects.Clear();

        var airbum = UnityEngine.GameObject.Find("AIRBUM");
        if (airbum != null && cleanMat != null)
        {
            var plus = airbum.transform.Find("+_button")?.gameObject;
            var minus = airbum.transform.Find("-_button")?.gameObject;
            var select = airbum.transform.Find("select")?.gameObject;

            if (plus != null && plus.GetComponent<MeshRenderer>() != null)
            {
                plus.GetComponent<MeshRenderer>().sharedMaterials = new Material[] { cleanMat };
                plus.GetComponent<MeshRenderer>().materials = new Material[] { cleanMat };
            }
            if (minus != null && minus.GetComponent<MeshRenderer>() != null)
            {
                minus.GetComponent<MeshRenderer>().sharedMaterials = new Material[] { cleanMat };
                minus.GetComponent<MeshRenderer>().materials = new Material[] { cleanMat };
            }
            if (select != null && select.GetComponent<MeshRenderer>() != null)
            {
                select.GetComponent<MeshRenderer>().sharedMaterials = new Material[] { cleanMat };
                select.GetComponent<MeshRenderer>().materials = new Material[] { cleanMat };
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI — Canvas, Title, Description
    // ─────────────────────────────────────────────────────────────────────────

    public void UIPosChange(Transform t)
    {
        _nextCanvasTransform = t;
        if (UI_Canvas != null)
        {
            UI_Canvas.transform.localPosition = _nextCanvasTransform.localPosition;
            UI_Canvas.transform.localRotation = _nextCanvasTransform.localRotation;
        }
    }

    public void SetTitle(string title)
    {
        titleString = title;
        if (TitleText != null) TitleText.text = titleString;
    }

    public void SetDescription(string des)
    {
        desString = des;
        if (DescriptionText != null) DescriptionText.text = desString;
    }

    /// <summary>Look up display text for key in Manager's translation database, set on UI.</summary>
    public void SetLocalTitle(string key)
    {
        if (Manager.Instance != null)
            SetTitle(Manager.Instance.GetDisplayText(key));
        else
            SetTitle(key);
    }

    /// <summary>Look up display text for key in Manager's translation database, set on UI and hologram.</summary>
    public void SetLocalDescription(string key)
    {
        if (Manager.Instance != null)
        {
            string localizedDes = Manager.Instance.GetDisplayText(key);
            SetDescription(localizedDes);

            // Update hologram anchor panel if present
            var hologram = UnityEngine.Object.FindFirstObjectByType<TruckTyreReplacement.UI.TrainingHologramAnchor>();
            if (hologram != null) hologram.SetDescription(localizedDes);
        }
        else
        {
            SetDescription(key);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AUDIO — Locale-aware playback
    // ─────────────────────────────────────────────────────────────────────────

    public void BGAudioCall(AudioClip clip) { BG_Audio.clip = clip; BG_Audio.Play(); }
    public void BGAudioStop() => BG_Audio?.Stop();
    public void SetBGVolume(float vol) { if (BG_Audio != null) BG_Audio.volume = vol; }
    public void VoiceOverCall(AudioClip clip) { if (Voice_Audio != null) { Voice_Audio.clip = clip; Voice_Audio.Play(); } }

    public void PlayAudio_TriggerOnComplete(AudioClip clip) { if (clip != null) StartCoroutine(PlayAudioWhenReady(clip)); }
    private IEnumerator PlayAudioWhenReady(AudioClip clip)
    {
        Voice_Audio.clip = clip; yield return null; Voice_Audio.Play();
        yield return new WaitUntil(() => Voice_Audio.isPlaying);
        yield return new WaitForSeconds(Voice_Audio.clip.length + 1.5f);
        handler.TaskCompleted();
    }

    public void PlayAudio_WithoutNextTask(AudioClip clip) { if (clip != null) StartCoroutine(PlayAudioWhenReady_WithoutNextTask(clip)); }
    private IEnumerator PlayAudioWhenReady_WithoutNextTask(AudioClip clip)
    {
        Voice_Audio.clip = clip; yield return null; Voice_Audio.Play();
        yield return new WaitUntil(() => Voice_Audio.isPlaying);
        yield return new WaitForSeconds(Voice_Audio.clip.length + 1.5f);
        handler.CurrentTaskCompleted();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        var mgr = Manager.Instance != null ? Manager.Instance : UnityEngine.Object.FindFirstObjectByType<Manager>();
        if (mgr != null)
            mgr.PlaySFX(clip);
    }

    /// <summary>Play localized audio for key (fire-and-forget, no task completion).</summary>
    public void PlayLocaleAudio(string key)
    {
        var mgr = Manager.Instance != null ? Manager.Instance : UnityEngine.Object.FindFirstObjectByType<Manager>();
        if (mgr != null)
            mgr.Speak(key);
    }

    /// <summary>Play localized audio for key. Calls CompleteCurrentTask() when audio finishes.</summary>
    public void PlayLocaleAudio_TriggerOnComplete(string key)
    {
        StartCoroutine(PlayLocaleAudioRoutine(key, true));
    }

    /// <summary>Play localized audio for key. Marks task completed (no advance) when done.</summary>
    public void PlayLocaleAudio_WithoutNextTask(string key)
    {
        StartCoroutine(PlayLocaleAudioRoutine(key, false));
    }

    private IEnumerator PlayLocaleAudioRoutine(string key, bool triggerNextTask)
    {
        var mgr = Manager.Instance != null ? Manager.Instance : UnityEngine.Object.FindFirstObjectByType<Manager>();
        if (mgr != null)
        {
            Debug.Log($"[SequenceHelperFunctions][TTS] PlayLocaleAudio key='{key}' language={mgr.CurrentLanguage}");
            mgr.Speak(key);

            AudioSource source = mgr.GetVoiceAudioSource();
            Debug.Log($"[SequenceHelperFunctions][TTS] AudioSource: {(source != null ? source.gameObject.name : "NULL")} enabled={source?.enabled} volume={source?.volume}");

            if (source != null)
            {
                // Phase 1: wait up to 1s for Manager.IsSpeaking to become true (Speak() is async/queued)
                float waitStart = Time.realtimeSinceStartup;
                yield return new WaitUntil(() => mgr.IsSpeaking || (Time.realtimeSinceStartup - waitStart) > 1.0f);

                // Phase 2: wait up to 0.5s for AudioSource.isPlaying to become true
                waitStart = Time.realtimeSinceStartup;
                yield return new WaitUntil(() => source.isPlaying || (Time.realtimeSinceStartup - waitStart) > 0.5f);

                Debug.Log($"[SequenceHelperFunctions][TTS] isPlaying={source.isPlaying} clip={source.clip?.name} length={source.clip?.length}s");

                if (source.isPlaying)
                {
                    // Phase 3: wait for audio to finish
                    yield return new WaitUntil(() => !source.isPlaying && !mgr.IsSpeaking);
                    Debug.Log($"[SequenceHelperFunctions][TTS] Audio finished for key='{key}'");
                }
                else
                {
                    // Audio never started — missing clip or not preloaded
                    Debug.LogWarning($"[SequenceHelperFunctions][TTS] Audio did NOT start for key='{key}'. Check Manager translation database and preloaded audio cache.");
                    yield return new WaitForSeconds(1.0f);
                }
            }

            yield return new WaitForSeconds(0.3f);
        }
        else
        {
            Debug.LogWarning("[SequenceHelperFunctions][TTS] Manager instance unavailable — cannot play audio.");
            yield return new WaitForSeconds(1.0f);
        }

        if (triggerNextTask) handler.TaskCompleted();
        else handler.CurrentTaskCompleted();
    }

    /// <summary>
    /// Executes Task 04 final completion audio sequence:
    /// 1. localized "air_filling" TTS -> wait for finish
    /// 2. "Air Filling Sound" SFX -> wait for finish
    /// 3. localized "complete" TTS -> wait for finish
    /// 4. Complete training task.
    /// </summary>
    public void PlayCompletionSequence_TriggerOnComplete()
    {
        StartCoroutine(CompletionSequenceRoutine());
    }

    private IEnumerator CompletionSequenceRoutine()
    {
        var mgr = Manager.Instance != null ? Manager.Instance : UnityEngine.Object.FindFirstObjectByType<Manager>();
        if (mgr != null) mgr.StopSpeech();

        AudioSource voiceSource = mgr != null ? mgr.GetVoiceAudioSource() : null;
        AudioSource sfxSource   = mgr != null ? mgr.GetSFXAudioSource()   : null;

        // 1. Air Filling Sound SFX (Only play Air Filling Sound on pipe grab, no instruction speech!)
        Debug.Log("[AUDIO FLOW] Air Filling Sound START");
        AudioClip airFillClip = Resources.Load<AudioClip>("Audio/Air Filling Sound");
        if (airFillClip == null) airFillClip = Resources.Load<AudioClip>("Air Filling Sound");
        if (airFillClip == null)
        {
            var nvc = UnityEngine.Object.FindFirstObjectByType<NumericVariableController>(UnityEngine.FindObjectsInactive.Include);
            if (nvc != null && nvc.targetConfirmedSFX != null) airFillClip = nvc.targetConfirmedSFX;
        }

        if (airFillClip != null && mgr != null)
        {
            mgr.PlaySFX(airFillClip);
            if (sfxSource != null)
            {
                float start = Time.realtimeSinceStartup;
                yield return new WaitUntil(() => sfxSource.isPlaying || (Time.realtimeSinceStartup - start) > 0.5f);
                if (sfxSource.isPlaying)
                {
                    yield return new WaitUntil(() => !sfxSource.isPlaying);
                }
            }
            else
            {
                yield return new WaitForSeconds(1.5f);
            }
        }
        else
        {
            yield return new WaitForSeconds(1.0f);
        }
        Debug.Log("[AUDIO FLOW] Air Filling Sound COMPLETE");
        yield return new WaitForSeconds(0.3f);

        // 3. complete localized TTS
        Debug.Log("[AUDIO FLOW] complete TTS START");
        if (mgr != null)
        {
            string localizedComplete = mgr.GetDisplayText("complete");
            var anchor = UnityEngine.Object.FindFirstObjectByType<TruckTyreReplacement.UI.TrainingHologramAnchor>();
            if (anchor != null) anchor.SetDescription(localizedComplete);

            mgr.Speak("complete");
        }

        if (voiceSource != null && mgr != null)
        {
            float start = Time.realtimeSinceStartup;
            yield return new WaitUntil(() => mgr.IsSpeaking || (Time.realtimeSinceStartup - start) > 1.0f);
            yield return new WaitUntil(() => voiceSource.isPlaying || (Time.realtimeSinceStartup - start) > 1.5f);
            if (voiceSource.isPlaying)
            {
                yield return new WaitUntil(() => !voiceSource.isPlaying && !mgr.IsSpeaking);
            }
        }
        else
        {
            yield return new WaitForSeconds(2.0f);
        }
        Debug.Log("[AUDIO FLOW] complete TTS COMPLETE -> TRAINING COMPLETE");

        if (handler == null) handler = SequenceHandler.instance != null ? SequenceHandler.instance : GetComponent<SequenceHandler>();
        if (handler != null) handler.TaskCompleted();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INTERACTION — Enable/Disable XR interactables
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Enable XRBaseInteractable and Collider on a GameObject.</summary>
    public void EnableInteractable(GameObject target)
    {
        if (target == null) return;
        var interactable = target.GetComponent<XRBaseInteractable>();
        if (interactable != null) interactable.enabled = true;
        var col = target.GetComponent<Collider>();
        if (col != null) col.enabled = true;
        Debug.Log($"[SequenceHelperFunctions] EnableInteractable: '{target.name}'");
    }

    /// <summary>Disable XRBaseInteractable and Collider on a GameObject.</summary>
    public void DisableInteractable(GameObject target)
    {
        if (target == null) return;
        var interactable = target.GetComponent<XRBaseInteractable>();
        if (interactable != null) interactable.enabled = false;
        var col = target.GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    /// <summary>Activate a GrabDetect and XRGrabInteractable on a GameObject.</summary>
    public void EnableGrabObject(GameObject obj)
    {
        if (obj == null) return;

        var interactable = obj.GetComponent<XRBaseInteractable>();
        if (interactable != null) interactable.enabled = true;

        var grab = obj.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.enabled = true;
            if (grab.interactionManager == null)
            {
                grab.interactionManager = UnityEngine.Object.FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
            }
            var col = obj.GetComponent<Collider>();
            if (col != null && !grab.colliders.Contains(col))
                grab.colliders.Add(col);
        }

        var colObj = obj.GetComponent<Collider>();
        if (colObj != null) colObj.enabled = true;

        var nvc = UnityEngine.Object.FindFirstObjectByType<NumericVariableController>();
        if (nvc != null && (obj.name == "Curves pipe" || obj.name.Contains("Pipe")))
        {
            nvc.EnablePipeInteraction();
        }

        GrabDetect gd = obj.GetComponent<GrabDetect>();
        if (gd != null) gd.ActivateGrab();

        Debug.Log($"[SequenceHelperFunctions] EnableGrabObject: '{obj.name}' activated and enabled for XR grab!");
    }

    /// <summary>Deactivate a GrabDetect on a GameObject.</summary>
    public void DisableGrabObject(GameObject obj)
    {
        if (obj == null) return;
        GrabDetect gd = obj.GetComponent<GrabDetect>();
        if (gd != null) gd.DeactivateGrab();
    }

    /// <summary>Called by GrabDetect when an object is grabbed. Completes current task.</summary>
    public void OnObjectGrabbed()
    {
        if (handler == null) handler = SequenceHandler.instance != null ? SequenceHandler.instance : GetComponent<SequenceHandler>();
        if (handler != null)
        {
            Debug.Log("[SequenceHelperFunctions] OnObjectGrabbed -> CompleteCurrentTask");
            handler.TaskCompleted();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ANIMATION
    // ─────────────────────────────────────────────────────────────────────────

    public void PlayAnimation_TriggerOnComplete(int animIndex)
    {
        if (animIndex >= 0 && animIndex < sequenceAnimations.Count)
            StartCoroutine(AnimationRoutine(sequenceAnimations[animIndex], true));
    }

    public void PlayAnimation_WithoutNextTask(int animIndex)
    {
        if (animIndex >= 0 && animIndex < sequenceAnimations.Count)
            StartCoroutine(AnimationRoutine(sequenceAnimations[animIndex], false));
    }

    private IEnumerator AnimationRoutine(SequenceAnimation animData, bool completeTaskAndMoveNext)
    {
        if (animData.targetAnimator != null && !string.IsNullOrEmpty(animData.triggerName))
            animData.targetAnimator.SetTrigger(animData.triggerName);
        yield return new WaitForSeconds(animData.animationDuration);
        if (completeTaskAndMoveNext) handler.TaskCompleted();
        else handler.CurrentTaskCompleted();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FADE
    // ─────────────────────────────────────────────────────────────────────────

    public void FadeIn() => StartCoroutine(FadeTo(1f));
    public void FadeOut() => StartCoroutine(FadeTo(0f));
    public void TaskComplete_WithFade() => StartCoroutine(FadeTo(1f, () => handler.TaskCompleted()));
    public void NextSequence_WithFade() => StartCoroutine(FadeTo(1f, () => handler.NextSequence()));
    public void PreviousSequence_WithFade() => StartCoroutine(FadeTo(1f, () => handler.PreviousSequence()));
    public void ReloadSequence_WithFade() => StartCoroutine(FadeTo(1f, () => handler.ReloadSequence()));
    public void SelectSequence_WithFade(int n) => StartCoroutine(FadeTo(1f, () => handler.SequenceSelect(n)));

    private IEnumerator FadeTo(float targetAlpha, Action action = null)
    {
        if (fadeMaterial == null) { action?.Invoke(); yield break; }
        Color color = fadeMaterial.color;
        float startAlpha = color.a; float time = 0f;
        while (time < fadeDuration)
        {
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
            fadeMaterial.color = new Color(color.r, color.g, color.b, alpha);
            time += Time.deltaTime; yield return null;
        }
        fadeMaterial.color = new Color(color.r, color.g, color.b, targetAlpha);
        action?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PPE Logic
    // ─────────────────────────────────────────────────────────────────────────

    public void RegisterPPE(GameObject ppeObj)
    {
        if (!ppeInitialPositions.ContainsKey(ppeObj))
        {
            ppeInitialPositions.Add(ppeObj, ppeObj.transform.position);
            ppeInitialRotations.Add(ppeObj, ppeObj.transform.rotation);
        }
    }

    public void SetPPEZoneStatus(bool inside) => isInsidePPEZone = inside;

    public void HandlePPERelease(SelectExitEventArgs args)
    {
        GameObject ppeObj = args.interactableObject.transform.gameObject;
        if (isInsidePPEZone)
        {
            if (ppeObjectToEnable != null) ppeObjectToEnable.SetActive(true);
            StopTransparentEffect();
            handler.TaskCompleted();
            ppeObj.SetActive(false);
        }
        else
        {
            if (ppeInitialPositions.ContainsKey(ppeObj))
            {
                ppeObj.transform.position = ppeInitialPositions[ppeObj];
                ppeObj.transform.rotation = ppeInitialRotations[ppeObj];
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Multi-Object Grab & Snap Logic
    // ─────────────────────────────────────────────────────────────────────────

    public void RegisterAllMultiGrabObjects()
    {
        foreach (var mapping in multiGrabMappings)
        {
            if (mapping.GrabbableObject != null)
            {
                mapping.InitialPosition = mapping.GrabbableObject.transform.position;
                mapping.InitialRotation = mapping.GrabbableObject.transform.rotation;
                mapping.IsInsideTargetCollider = false;
            }
        }
    }

    public void OnMultiObjectGrabbed(SelectEnterEventArgs args)
    {
        GameObject grabbedObj = args.interactableObject.transform.gameObject;
        GrabSnapMapping mapping = multiGrabMappings.Find(m => m.GrabbableObject == grabbedObj);
        if (mapping != null && mapping.GhostTargetObject != null)
        {
            mapping.GhostTargetObject.SetActive(true);
            ApplySafeGhostHighlight(mapping.GhostTargetObject);
        }
    }

    public void SetMultiDropZoneStatus(GameObject grabbedObj, bool isInside)
    {
        GrabSnapMapping mapping = multiGrabMappings.Find(m => m.GrabbableObject == grabbedObj);
        if (mapping != null) mapping.IsInsideTargetCollider = isInside;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Transparent Effect (Legacy)
    // ─────────────────────────────────────────────────────────────────────────

    public void OnObjectPlaced()
    {
        handler.TaskCompleted();
        StopTransparentEffect();
    }

    public void TransParentEffect(GameObject targetObject)
    {
        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            if (transparencyCoroutine != null) StopCoroutine(transparencyCoroutine);
            transparencyCoroutine = StartCoroutine(DelayedTransparentEffect(renderers, 0.1f));
        }
    }

    private IEnumerator DelayedTransparentEffect(Renderer[] renderers, float delay)
    {
        yield return new WaitForSeconds(delay);
        _isTransparent = true;
        yield return StartCoroutine(ApplyEffect(renderers));
    }

    public void StopTransparentEffect() => _isTransparent = false;

    private IEnumerator ApplyEffect(Renderer[] renderers)
    {
        Material[][] originalMaterials = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].materials;
            Material[] ghostMaterials = new Material[originalMaterials[i].Length];
            for (int j = 0; j < ghostMaterials.Length; j++) ghostMaterials[j] = transparentMaterial;
            renderers[i].materials = ghostMaterials;
        }
        while (_isTransparent) yield return null;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].materials = originalMaterials[i];
        transparencyCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Button Utilities
    // ─────────────────────────────────────────────────────────────────────────

    public void ButtonOnIsSelected(Button button)
    {
        Color color;
        ColorUtility.TryParseHtmlString("#56BA1F", out color);
        button.image.color = color;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Switch Logic
    // ─────────────────────────────────────────────────────────────────────────

    public void EnableSwitch(GameObject switchObj) { }
    public void OnSwitchToggled(bool state) => handler.TaskCompleted();
    public void ExecuteSwitchToggle()
    {
        if (currentTarget != null)
        {
            SwitchController sc = currentTarget.GetComponent<SwitchController>();
            if (sc != null) sc.ToggleSwitch();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Universal Ray Interactions
    // ─────────────────────────────────────────────────────────────────────────

    public void SetCurrentTarget(HoverEnterEventArgs args) => currentTarget = args.interactableObject.transform.gameObject;
    public void ClearCurrentTarget(HoverExitEventArgs args) { if (currentTarget == args.interactableObject.transform.gameObject) currentTarget = null; }
    public void ProcessTriggerInteraction(SelectEnterEventArgs args)
    {
        GameObject hitObj = args.interactableObject.transform.gameObject;
        SwitchController switchObj = hitObj.GetComponent<SwitchController>();
        if (switchObj == null) switchObj = hitObj.GetComponentInParent<SwitchController>();
        if (switchObj != null) switchObj.ToggleSwitch();
    }

    public void ApplySequenceOnHover(GameObject obj) { if (UI_Canvas != null) UI_Canvas.SetActive(true); }

    // ─────────────────────────────────────────────────────────────────────────
    // Misc / Legacy
    // ─────────────────────────────────────────────────────────────────────────

    public void ExitApp() { PlayerPrefs.SetInt("Module", 0); Application.Quit(); }
    public void LoadScene(string a) => SceneManager.LoadScene(a);
    public void WaittoTaskComplete(float time) => StartCoroutine(TaskCompletetime(time));
    IEnumerator TaskCompletetime(float time) { yield return new WaitForSeconds(time); handler.TaskCompleted(); }
}


[RequireComponent(typeof(Collider))]
public class DropZoneTrigger : MonoBehaviour
{
    public GameObject ExpectedGrabbableObject;

    private void Start() { GetComponent<Collider>().isTrigger = true; }

    private void OnTriggerEnter(Collider other)
    {
        GameObject hitObj = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (hitObj == ExpectedGrabbableObject && SequenceHelperFunctions.instance != null)
        {
            SequenceHelperFunctions.instance.SetMultiDropZoneStatus(ExpectedGrabbableObject, true);
            XRGrabInteractable grab = ExpectedGrabbableObject.GetComponent<XRGrabInteractable>();
            if (grab != null && grab.isSelected)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        GameObject hitObj = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (hitObj == ExpectedGrabbableObject && SequenceHelperFunctions.instance != null)
            SequenceHelperFunctions.instance.SetMultiDropZoneStatus(ExpectedGrabbableObject, false);
    }
}
