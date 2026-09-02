using UnityEngine;
using UnityEngine.EventSystems;

namespace TruckTyreReplacement.XR
{
    [ExecuteInEditMode]
    public class TrainingXRUI : MonoBehaviour
    {
        [Tooltip("Optional reference to the Training Instruction Canvas to validate")]
        [SerializeField] private Canvas targetCanvas;

        private void Awake()
        {
            ValidateConfiguration();
        }

        [ContextMenu("Validate XR UI Configuration")]
        public void ValidateConfiguration()
        {
            Debug.Log("[TrainingXRUI] Starting XR UI validation...");

            // 1. Validate EventSystem
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                Debug.LogWarning("[TrainingXRUI] No EventSystem found. Creating one...");
                GameObject esObj = new GameObject("EventSystem");
                eventSystem = esObj.AddComponent<EventSystem>();
            }

            // 2. Validate XR UI Input Module
            BaseInputModule inputModule = eventSystem.GetComponent<BaseInputModule>();
            var xrInputModuleType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");
            if (xrInputModuleType == null)
            {
                xrInputModuleType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule");
            }

            if (xrInputModuleType != null)
            {
                Component xrModule = eventSystem.GetComponent(xrInputModuleType);
                if (xrModule == null)
                {
                    Debug.Log("[TrainingXRUI] Adding XRUIInputModule to EventSystem...");
                    if (inputModule != null)
                    {
                        if (Application.isPlaying)
                        {
                            Destroy(inputModule);
                        }
                        else
                        {
                            DestroyImmediate(inputModule);
                        }
                    }
                    eventSystem.gameObject.AddComponent(xrInputModuleType);
                }
            }
            else
            {
                var isModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (isModuleType != null && eventSystem.GetComponent(isModuleType) == null)
                {
                    Debug.LogWarning("[TrainingXRUI] XRUIInputModule type not found. Adding InputSystemUIInputModule as fallback...");
                    if (inputModule != null)
                    {
                        if (Application.isPlaying) Destroy(inputModule);
                        else DestroyImmediate(inputModule);
                    }
                    eventSystem.gameObject.AddComponent(isModuleType);
                }
            }

            // 3. Validate Canvas Raycaster
            if (targetCanvas != null)
            {
                if (targetCanvas.renderMode != RenderMode.WorldSpace)
                {
                    Debug.LogWarning("[TrainingXRUI] Target Canvas render mode is not World Space. Changing to World Space...");
                    targetCanvas.renderMode = RenderMode.WorldSpace;
                }

                var raycasterType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
                if (raycasterType == null)
                {
                    raycasterType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster");
                }

                if (raycasterType != null)
                {
                    Component raycaster = targetCanvas.GetComponent(raycasterType);
                    if (raycaster == null)
                    {
                        Debug.Log("[TrainingXRUI] Adding TrackedDeviceGraphicRaycaster to target Canvas...");
                        var normalRaycaster = targetCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                        if (normalRaycaster != null)
                        {
                            if (Application.isPlaying) Destroy(normalRaycaster);
                            else DestroyImmediate(normalRaycaster);
                        }
                        targetCanvas.gameObject.AddComponent(raycasterType);
                    }
                }
                else
                {
                    Debug.LogError("[TrainingXRUI] TrackedDeviceGraphicRaycaster type not found! UI raycast will not work in VR.");
                }
            }

            Debug.Log("[TrainingXRUI] XR UI validation complete.");
        }
    }
}
