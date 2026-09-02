using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TruckTyreReplacement.Core
{
    public enum LocalTTSLanguage
    {
        English = 0,
        Hindi = 1,
        Odia = 2
    }

    [System.Serializable]
    public class TranslationEntry
    {
        public string key;

        [TextArea(2, 5)]
        public string englishDisplay;

        [TextArea(2, 5)]
        public string englishSpeech;

        [TextArea(2, 5)]
        public string hindiDisplay;

        [TextArea(2, 5)]
        public string hindiSpeech;

        [TextArea(2, 5)]
        public string odiaDisplay;

        [TextArea(2, 5)]
        public string odiaSpeech;
    }

    /// <summary>
    /// Generic Manager for VR Training Modules.
    /// Handles VR Configuration, Multilingual Localization Database,
    /// Language Selection, and Preloaded Offline MMS-TTS Audio.
    /// </summary>
    [AddComponentMenu("Vedanta/Manager")]
    public class Manager : MonoBehaviour
    {
        public static Manager Instance { get; private set; }

        [Header("VR CONFIGURATION")]
        [Tooltip("[REQUIRED] The XR Origin (VR) GameObject in the scene")]
        [SerializeField] private GameObject xrOrigin;

        [Tooltip("[REQUIRED] The Main Camera inside the XR Origin")]
        [SerializeField] private Camera mainCamera;

        [Tooltip("[REQUIRED] Left Controller GameObject")]
        [SerializeField] private GameObject leftController;

        [Tooltip("[REQUIRED] Right Controller GameObject")]
        [SerializeField] private GameObject rightController;

        [Header("LANGUAGE CONFIGURATION")]
        [SerializeField] private LocalTTSLanguage currentLanguage = LocalTTSLanguage.English;
        [SerializeField] private bool alwaysAskLanguageOnStartup = true;
        [SerializeField] private bool speakOnStartup = false;

        [Header("AUDIO CONFIGURATION")]
        [SerializeField] private AudioSource voiceAudioSource;
        [SerializeField] private AudioSource sfxAudioSource;
        [Range(0f, 1f)]
        [SerializeField] private float voiceVolume = 1.0f;
        [SerializeField] private bool interruptCurrentSpeech = true;

        [Header("FONTS")]
        [SerializeField] private TMP_FontAsset englishFont;
        [SerializeField] private TMP_FontAsset devanagariFont;
        [SerializeField] private TMP_FontAsset odiaFont;

        [Header("TRANSLATION DATABASE")]
        [Tooltip("The multilingual translation entries editable directly in Inspector.")]
        [SerializeField] private List<TranslationEntry> translationDatabase = new List<TranslationEntry>();

        [Header("DEBUG")]
        [SerializeField] private bool debugMode = false;

        // ── Memory Audio Cache ───────────────────────────────
        private readonly Dictionary<string, AudioClip> audioClipMemoryCache = new Dictionary<string, AudioClip>();
        private readonly Queue<string> speechQueue = new Queue<string>();
        private Coroutine speechCoroutine;
        private bool isSpeaking = false;
        private string lastSpokenText = "";
        private string lastSpokenKey = "";
        public string LastSpokenKey => lastSpokenKey;

        // ── UI State ─────────────────────────────────────────
        private GameObject languageSelectionPanel;
        private bool sequenceStarted = false;
        public bool IsLanguageSelected { get; private set; } = false;
        public LocalTTSLanguage CurrentLanguage => currentLanguage;
        public bool IsSpeaking => isSpeaking;

        public event Action<LocalTTSLanguage> OnLanguageChanged;

        public static string GetLanguageCode(LocalTTSLanguage lang)
        {
            switch (lang)
            {
                case LocalTTSLanguage.Hindi: return "hi";
                case LocalTTSLanguage.Odia: return "or";
                default: return "en";
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeManager();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeManager()
        {
            // Setup voice AudioSource
            if (voiceAudioSource == null)
            {
                voiceAudioSource = GetComponent<AudioSource>();
                if (voiceAudioSource == null)
                {
                    voiceAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            voiceAudioSource.spatialBlend = 0.0f; // 2D speech
            voiceAudioSource.volume = voiceVolume;
            voiceAudioSource.playOnAwake = false;
            voiceAudioSource.loop = false;

            // Load fonts if not assigned
            LoadFontAssets();

            // Populate database from JSON if empty
            if (translationDatabase == null || translationDatabase.Count == 0)
            {
                ImportTrainingJson();
            }

            PrepopulateFonts();

            // Ensure player is at IntroductionPoint from the very start
            var introPoint = GameObject.Find("IntroductionPoint")?.transform;
            if (introPoint != null)
            {
                MovePlayerTo(introPoint);
            }
        }

        private void Start()
        {
            StartCoroutine(StartupLanguageRoutine());
        }

        private IEnumerator StartupLanguageRoutine()
        {
            // Ensure player is at IntroductionPoint while selecting language
            var introPoint = GameObject.Find("IntroductionPoint")?.transform;
            if (introPoint != null)
            {
                MovePlayerTo(introPoint);
            }

            bool hasLanguageKey = PlayerPrefs.HasKey("TrainingLanguage");
            int savedLangIndex = PlayerPrefs.GetInt("TrainingLanguage", (int)currentLanguage);

            if (alwaysAskLanguageOnStartup || !hasLanguageKey)
            {
                IsLanguageSelected = false;
                CreateLanguageSelectionPanel();
                if (languageSelectionPanel != null)
                {
                    languageSelectionPanel.SetActive(true);
                }
            }
            else
            {
                SetLanguage((LocalTTSLanguage)savedLangIndex);
            }
            yield return null;
        }

        // ─────────────────────────────────────────────────────
        // LANGUAGE & FONT MANAGEMENT
        // ─────────────────────────────────────────────────────

        public void SetLanguage(LocalTTSLanguage lang)
        {
            currentLanguage = lang;
            PlayerPrefs.SetInt("TrainingLanguage", (int)lang);
            PlayerPrefs.Save();
            IsLanguageSelected = true;

            if (debugMode) Debug.Log($"[Manager] Selected Language: {lang} (code: {GetLanguageCode(lang)})");

            ApplyFontForLanguage(lang);

            // Preload audio into memory cache for instant playback
            PreloadLanguageAudio(lang);

            if (languageSelectionPanel != null)
            {
                languageSelectionPanel.SetActive(false);
            }

            var canvas = FindTrainingInstructionCanvas();
            if (canvas != null)
            {
                var panelTrans = canvas.transform.Find("Panel");
                if (panelTrans != null)
                {
                    panelTrans.gameObject.SetActive(true);
                }
            }

            OnLanguageChanged?.Invoke(lang);

            // Start the sequence exactly once after language selection is finalized
            if (!sequenceStarted && SequenceHandler.instance != null)
            {
                sequenceStarted = true;
                SequenceHandler.instance.Init();
            }
        }

        private void LoadFontAssets()
        {
            if (englishFont == null)
            {
                englishFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
#if UNITY_EDITOR
                if (englishFont == null)
                {
                    englishFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                }
#endif
            }
            if (devanagariFont == null)
            {
#if UNITY_EDITOR
                devanagariFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LocalTTS/Fonts/NotoSansDevanagari SDF.asset");
#endif
            }
            if (odiaFont == null)
            {
#if UNITY_EDITOR
                odiaFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LocalTTS/Fonts/NotoSansOdia SDF.asset");
#endif
            }
        }

        private void PrepopulateFonts()
        {
            if (translationDatabase == null) return;

            var sb = new System.Text.StringBuilder(" SELECT LANGUAGE English हिन्दी ଓଡ଼ିଆ ");
            foreach (var entry in translationDatabase)
            {
                sb.Append(entry.hindiDisplay).Append(" ").Append(entry.hindiSpeech).Append(" ");
                sb.Append(entry.odiaDisplay).Append(" ").Append(entry.odiaSpeech).Append(" ");
            }

            string fullText = sb.ToString();

            if (devanagariFont != null)
            {
                devanagariFont.TryAddCharacters(fullText, true);
            }
            if (odiaFont != null)
            {
                odiaFont.TryAddCharacters(fullText, true);
            }
        }

        public void ApplyFontForLanguage(LocalTTSLanguage lang)
        {
            LoadFontAssets();

            TMP_FontAsset targetFont = englishFont;
            if (lang == LocalTTSLanguage.Hindi) targetFont = devanagariFont;
            else if (lang == LocalTTSLanguage.Odia) targetFont = odiaFont;

            if (targetFont == null) targetFont = englishFont;
            if (targetFont == null) return;

            var allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
            foreach (var txt in allTexts)
            {
                if (txt != null && txt.gameObject.scene.isLoaded)
                {
                    // Don't overwrite the language selection buttons' native fonts if panel is present
                    if (txt.transform.parent != null && 
                        (txt.transform.parent.name == "EnglishButton" || 
                         txt.transform.parent.name == "HindiButton" || 
                         txt.transform.parent.name == "OdiaButton" || 
                         txt.transform.parent.name == "LanguageSelectionPanel"))
                    {
                        continue;
                    }
                    txt.font = targetFont;
                }
            }
        }

        // ─────────────────────────────────────────────────────
        // TRANSLATION LOOKUP API
        // ─────────────────────────────────────────────────────

        public void GetTranslation(string key, out string displayText, out string speechText)
        {
            displayText = "";
            speechText = "";

            if (string.IsNullOrEmpty(key) || translationDatabase == null) return;
            if (string.Equals(key, "correct_pressed", StringComparison.OrdinalIgnoreCase)) key = "correct_pressure";

            var entry = translationDatabase.Find(e => string.Equals(e.key, key, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                switch (currentLanguage)
                {
                    case LocalTTSLanguage.Hindi:
                        displayText = !string.IsNullOrEmpty(entry.hindiDisplay) ? entry.hindiDisplay : entry.englishDisplay;
                        speechText = !string.IsNullOrEmpty(entry.hindiSpeech) ? entry.hindiSpeech : entry.englishSpeech;
                        break;
                    case LocalTTSLanguage.Odia:
                        displayText = !string.IsNullOrEmpty(entry.odiaDisplay) ? entry.odiaDisplay : entry.englishDisplay;
                        speechText = !string.IsNullOrEmpty(entry.odiaSpeech) ? entry.odiaSpeech : entry.englishSpeech;
                        break;
                    default:
                        displayText = entry.englishDisplay;
                        speechText = entry.englishSpeech;
                        break;
                }
            }
            else
            {
                displayText = key;
                speechText = key;
            }
        }

        public string GetDisplayText(string key)
        {
            GetTranslation(key, out string disp, out _);
            return disp;
        }

        public string GetSpeechText(string key)
        {
            GetTranslation(key, out _, out string speech);
            return speech;
        }

        // ─────────────────────────────────────────────────────
        // OFFLINE PRELOADED MMS-TTS AUDIO
        // ─────────────────────────────────────────────────────

        private string ComputeTextHash(string text)
        {
            using (var md5 = MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(text);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes, 0, 4).Replace("-", "");
            }
        }

        private string GetCacheKey(LocalTTSLanguage lang, string text)
        {
            string clean = text.Replace("\n", " ").Replace("\r", " ").Trim();
            return lang.ToString() + "_" + ComputeTextHash(clean);
        }

        private void PreloadLanguageAudio(LocalTTSLanguage lang)
        {
            if (translationDatabase == null) return;

            foreach (var entry in translationDatabase)
            {
                string speechText = "";
                switch (lang)
                {
                    case LocalTTSLanguage.Hindi: speechText = entry.hindiSpeech; break;
                    case LocalTTSLanguage.Odia: speechText = entry.odiaSpeech; break;
                    default: speechText = entry.englishSpeech; break;
                }

                if (string.IsNullOrEmpty(speechText)) continue;

                string cacheKey = GetCacheKey(lang, speechText);
                if (audioClipMemoryCache.ContainsKey(cacheKey)) continue;

                // Load from Resources Audio Cache
                string resourcePath = "Audio/" + lang.ToString() + "/" + cacheKey;
                AudioClip clip = Resources.Load<AudioClip>(resourcePath);

                if (clip == null)
                {
                    // Fallback to disk cache if available
                    string diskPath = Path.Combine(Application.persistentDataPath, "LocalTTSCache", lang.ToString(), cacheKey + ".wav");
                    if (File.Exists(diskPath))
                    {
                        try
                        {
                            byte[] wavBytes = File.ReadAllBytes(diskPath);
                            clip = LoadWavAsAudioClip(wavBytes, cacheKey);
                        }
                        catch { }
                    }
                }

                if (clip != null)
                {
                    audioClipMemoryCache[cacheKey] = clip;
                    if (debugMode) Debug.Log($"[Manager][AudioCache] Preloaded {lang}: {cacheKey}");
                }
            }
        }

        public void Speak(string textOrKey)
        {
            if (string.IsNullOrEmpty(textOrKey)) return;

            if (!IsLanguageSelected)
            {
                IsLanguageSelected = true;
                PreloadLanguageAudio(currentLanguage);
            }

            // Resolve key if text is a database key
            string textToSpeak = textOrKey;
            var entry = translationDatabase.Find(e => string.Equals(e.key, textOrKey, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                textToSpeak = GetSpeechText(textOrKey);
            }

            string cleanText = textToSpeak.Replace("\n", " ").Replace("\r", " ").Trim();
            if (string.IsNullOrEmpty(cleanText)) return;

            if (!string.IsNullOrEmpty(lastSpokenKey) && string.Equals(textOrKey, lastSpokenKey, StringComparison.OrdinalIgnoreCase) && cleanText == lastSpokenText && voiceAudioSource != null && voiceAudioSource.isPlaying)
                return;

            lastSpokenKey = textOrKey;
            lastSpokenText = cleanText;

            Debug.Log($"[AIR TTS]\nKey = {textOrKey}\nLanguage = {currentLanguage}\nSpeechText = {cleanText}\nAudioSource = {(voiceAudioSource != null ? voiceAudioSource.name : "null")}\nPlayRequested = TRUE");

            if (interruptCurrentSpeech)
            {
                StopSpeech();
            }

            speechQueue.Enqueue(cleanText);
            isSpeaking = true;

            if (speechCoroutine == null)
            {
                speechCoroutine = StartCoroutine(ProcessSpeechQueue());
            }
        }

        public void StopSpeech()
        {
            speechQueue.Clear();
            if (voiceAudioSource != null && voiceAudioSource.isPlaying)
            {
                voiceAudioSource.Stop();
            }
            isSpeaking = false;
            if (speechCoroutine != null)
            {
                StopCoroutine(speechCoroutine);
                speechCoroutine = null;
            }
        }

        public AudioSource GetVoiceAudioSource() => voiceAudioSource;

        public AudioSource GetSFXAudioSource()
        {
            if (sfxAudioSource == null)
            {
                sfxAudioSource = GetComponent<AudioSource>();
                if (sfxAudioSource == null) sfxAudioSource = gameObject.AddComponent<AudioSource>();
            }
            return sfxAudioSource;
        }

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null) return;
            var src = GetSFXAudioSource();
            if (src != null)
            {
                src.PlayOneShot(clip);
                Debug.Log($"[Manager][SFX] Playing SFX '{clip.name}' on {src.gameObject.name}");
            }
        }

        private IEnumerator ProcessSpeechQueue()
        {
            isSpeaking = true;

            while (speechQueue.Count > 0)
            {
                string text = speechQueue.Dequeue();
                string cacheKey = GetCacheKey(currentLanguage, text);

                AudioClip clipToPlay = null;

                // 1. Check memory cache
                if (audioClipMemoryCache.TryGetValue(cacheKey, out AudioClip memClip) && memClip != null)
                {
                    clipToPlay = memClip;
                }
                else
                {
                    // 2. Resources fallback
                    string resourcePath = "Audio/" + currentLanguage.ToString() + "/" + cacheKey;
                    clipToPlay = Resources.Load<AudioClip>(resourcePath);
                    if (clipToPlay != null)
                    {
                        audioClipMemoryCache[cacheKey] = clipToPlay;
                    }
                }

                if (clipToPlay != null && voiceAudioSource != null)
                {
                    voiceAudioSource.clip = clipToPlay;
                    voiceAudioSource.volume = voiceVolume;
                    voiceAudioSource.Play();

                    if (debugMode) Debug.Log($"[Manager][Audio] Playing {currentLanguage} clip: {clipToPlay.name} ({clipToPlay.length:F2}s)");

                    yield return new WaitForSeconds(clipToPlay.length + 0.1f);
                }
                else
                {
                    if (debugMode) Debug.LogWarning($"[Manager][Audio] No pre-generated audio found for key: {cacheKey}");
                    yield return new WaitForSeconds(1.0f);
                }
            }

            isSpeaking = false;
            speechCoroutine = null;
        }

        private AudioClip LoadWavAsAudioClip(byte[] wavBytes, string name)
        {
            if (wavBytes == null || wavBytes.Length < 44) return null;
            try
            {
                int channels = BitConverter.ToInt16(wavBytes, 22);
                int sampleRate = BitConverter.ToInt32(wavBytes, 24);
                int bitsPerSample = BitConverter.ToInt16(wavBytes, 34);
                int bytesPerSample = bitsPerSample / 8;

                int pos = 12;
                while (pos < wavBytes.Length - 8)
                {
                    if (wavBytes[pos] == 'd' && wavBytes[pos + 1] == 'a' && wavBytes[pos + 2] == 't' && wavBytes[pos + 3] == 'a')
                    { pos += 4; break; }
                    pos++;
                }

                int dataSize = BitConverter.ToInt32(wavBytes, pos);
                pos += 4;
                int totalSamples = dataSize / bytesPerSample;
                int samplesPerChannel = totalSamples / channels;

                float[] samples = new float[totalSamples];
                for (int i = 0; i < totalSamples; i++)
                {
                    if (bytesPerSample == 2)
                        samples[i] = BitConverter.ToInt16(wavBytes, pos + i * 2) / 32768f;
                    else if (bytesPerSample == 1)
                        samples[i] = (wavBytes[pos + i] - 128) / 128f;
                }

                AudioClip clip = AudioClip.Create(name, samplesPerChannel, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }
            catch
            {
                return null;
            }
        }

        // ─────────────────────────────────────────────────────
        // VR TELEPORTATION & MOVEMENT
        // ─────────────────────────────────────────────────────

        public void MovePlayerTo(Transform destination)
        {
            if (destination == null)
            {
                Debug.LogWarning("[Manager] MovePlayerTo: Destination transform is null!");
                return;
            }

            if (xrOrigin == null)
            {
                xrOrigin = GameObject.Find("XR Origin (VR)");
            }

            if (xrOrigin != null)
            {
                xrOrigin.transform.SetPositionAndRotation(destination.position, destination.rotation);
                if (debugMode) Debug.Log($"[Manager] Player moved to: {destination.name}");
            }
        }

        // ─────────────────────────────────────────────────────
        // VR LANGUAGE SELECTION PANEL
        // ─────────────────────────────────────────────────────

        private Canvas FindTrainingInstructionCanvas()
        {
            var canvasGo = GameObject.Find("TrainingInstructionCanvas");
            if (canvasGo != null)
            {
                var c = canvasGo.GetComponent<Canvas>();
                if (c != null) return c;
            }
            var anchor = UnityEngine.Object.FindFirstObjectByType<TruckTyreReplacement.UI.TrainingHologramAnchor>();
            if (anchor != null)
            {
                var c = anchor.GetComponentInChildren<Canvas>(true);
                if (c != null) return c;
            }
            return null;
        }

        private void CreateLanguageSelectionPanel()
        {
            var canvas = FindTrainingInstructionCanvas();
            if (canvas == null) return;

            var existing = canvas.transform.Find("LanguageSelectionPanel");
            if (existing != null)
            {
                languageSelectionPanel = existing.gameObject;
                languageSelectionPanel.SetActive(true);
                var p = canvas.transform.Find("Panel");
                if (p != null) p.gameObject.SetActive(false);
                return;
            }

            var panelTrans = canvas.transform.Find("Panel");
            if (panelTrans == null) return;
            panelTrans.gameObject.SetActive(false);

            languageSelectionPanel = new GameObject("LanguageSelectionPanel", typeof(RectTransform));
            languageSelectionPanel.transform.SetParent(canvas.transform, false);

            var rect = languageSelectionPanel.GetComponent<RectTransform>();
            var panelRect = panelTrans.GetComponent<RectTransform>();
            rect.anchorMin = panelRect.anchorMin;
            rect.anchorMax = panelRect.anchorMax;
            rect.pivot = panelRect.pivot;
            rect.sizeDelta = panelRect.sizeDelta;
            rect.anchoredPosition = panelRect.anchoredPosition;

            var panelImg = panelTrans.GetComponent<Image>();
            if (panelImg != null)
            {
                var img = languageSelectionPanel.AddComponent<Image>();
                img.sprite = panelImg.sprite;
                img.color = panelImg.color;
                img.type = panelImg.type;
            }

            // Title
            var titleGo = new GameObject("TitleText", typeof(RectTransform));
            titleGo.transform.SetParent(languageSelectionPanel.transform, false);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            if (devanagariFont != null) titleTmp.font = devanagariFont;
            titleTmp.text = "SELECT LANGUAGE\n<size=24>भाषा चुनें | ଭାଷା ଚୟନ କରନ୍ତୁ</size>";
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.fontSize = 28;
            titleTmp.color = Color.white;
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0, 160);
            titleRect.sizeDelta = new Vector2(480, 80);

            // Buttons with native fonts
            CreateLanguageBtn("EnglishButton", "ENGLISH", new Vector2(0, 50), LocalTTSLanguage.English, englishFont);
            CreateLanguageBtn("HindiButton", "हिन्दी", new Vector2(0, -40), LocalTTSLanguage.Hindi, devanagariFont);
            CreateLanguageBtn("OdiaButton", "ଓଡ଼ିଆ", new Vector2(0, -130), LocalTTSLanguage.Odia, odiaFont);
        }

        private void CreateLanguageBtn(string goName, string label, Vector2 pos, LocalTTSLanguage lang, TMP_FontAsset font)
        {
            var btnGo = new GameObject(goName, typeof(RectTransform));
            btnGo.transform.SetParent(languageSelectionPanel.transform, false);
            var rect = btnGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300, 65);
            rect.anchoredPosition = pos;

            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.12f, 0.45f, 0.75f, 1f);

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(btnGo.transform, false);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 22;
            tmp.color = Color.white;
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            btn.onClick.AddListener(() => SetLanguage(lang));
        }

        // ─────────────────────────────────────────────────────
        // JSON IMPORT UTILITY
        // ─────────────────────────────────────────────────────

        [ContextMenu("Import Training JSON")]
        public void ImportTrainingJson()
        {
            string jsonText = "";
            TextAsset jsonAsset = Resources.Load<TextAsset>("training");
            if (jsonAsset != null)
            {
                jsonText = jsonAsset.text;
            }
            else
            {
                string jsonPath = Path.Combine(Application.dataPath, "LocalTTS/TrainingData/training.json");
                if (!File.Exists(jsonPath))
                {
                    jsonPath = Path.Combine(Application.streamingAssetsPath, "LocalTTS/TrainingData/training.json");
                }
                if (File.Exists(jsonPath))
                {
                    jsonText = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                }
            }

            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogWarning("[Manager] training.json not found!");
                return;
            }

            try
            {
                var root = JsonUtility.FromJson<JsonRoot>(jsonText);
                if (root != null)
                {
                    translationDatabase.Clear();
                    AddJsonEntry("welcome", root.welcome);
                    AddJsonEntry("step0", root.step0);
                    AddJsonEntry("step1", root.step1);
                    AddJsonEntry("step2", root.step2);
                    AddJsonEntry("low_pressure", root.low_pressure);
                    AddJsonEntry("high_pressure", root.high_pressure);
                    AddJsonEntry("correct_pressure", root.correct_pressure);
                    AddJsonEntry("pipe_grab", root.pipe_grab);
                    AddJsonEntry("air_filling", root.air_filling);
                    AddJsonEntry("complete", root.complete);

                    Debug.Log($"[Manager] Successfully imported {translationDatabase.Count} entries from training.json.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Manager] Failed to parse training.json: {ex.Message}");
            }
        }

        private void AddJsonEntry(string key, JsonEntry raw)
        {
            if (raw == null) return;
            translationDatabase.Add(new TranslationEntry
            {
                key = key,
                englishDisplay = raw.display?.en ?? "",
                englishSpeech = raw.speech?.en ?? "",
                hindiDisplay = raw.display?.hi ?? "",
                hindiSpeech = raw.speech?.hi ?? "",
                odiaDisplay = raw.display?.or ?? "",
                odiaSpeech = raw.speech?.or ?? ""
            });
        }

        [Serializable] private class JsonTranslations { public string en; public string hi; public string or; }
        [Serializable] private class JsonEntry { public JsonTranslations display; public JsonTranslations speech; }
        [Serializable]
        private class JsonRoot
        {
            public JsonEntry welcome;
            public JsonEntry step0;
            public JsonEntry step1;
            public JsonEntry step2;
            public JsonEntry low_pressure;
            public JsonEntry high_pressure;
            public JsonEntry correct_pressure;
            public JsonEntry pipe_grab;
            public JsonEntry air_filling;
            public JsonEntry complete;
        }
    }
}
