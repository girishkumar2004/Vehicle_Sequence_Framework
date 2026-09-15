using UnityEngine;

namespace TruckTyreReplacement.Core
{
    public class TrainingTeleportPointVisual : MonoBehaviour
    {
        [Header("Visual Materials")]
        [SerializeField] private Material ringMaterial;
        [SerializeField] private Material beamMaterial;

        [Header("Animation Settings")]
        [SerializeField] private float rotationSpeed = 25f;
        [SerializeField] private float pulseSpeed = 2.5f;
        [SerializeField] private float pulseAmount = 0.06f;

        private GameObject visualRoot;
        private Transform ringTransform;
        private Transform beamTransform;
        
        private Vector3 originalRingScale = new Vector3(1.4f, 1.4f, 1f);
        private Vector3 originalBeamScale = new Vector3(1.0f, 1.5f, 1.0f);
        
        private bool isVisible = true;
        private bool isInteractable = true;

        public bool IsVisible => isVisible;

        private void Start()
        {
            // Auto-load materials if not assigned in Inspector
#if UNITY_EDITOR
            if (ringMaterial == null)
            {
                ringMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/TruckTyreReplacement/Materials/M_Teleport_Ring.mat");
            }
            if (beamMaterial == null)
            {
                beamMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/TruckTyreReplacement/Materials/M_Teleport_Beam.mat");
            }
#endif

            BuildVisuals();
        }

        private void BuildVisuals()
        {
            // Create root container for all visuals
            visualRoot = new GameObject("TeleportVisualRoot");
            visualRoot.transform.SetParent(transform, false);
            visualRoot.layer = 2; // Ignore Raycast layer

            // 1. Build Ring (Floor Marker Quad)
            GameObject ringGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ringGo.name = "Ring";
            ringGo.transform.SetParent(visualRoot.transform, false);
            // Slightly above floor to prevent z-fighting
            ringGo.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            // Rotate to lie flat on the floor
            ringGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ringGo.transform.localScale = originalRingScale;
            ringGo.layer = 2; // Ignore Raycast layer

            // Remove Collider
            Collider ringCol = ringGo.GetComponent<Collider>();
            if (ringCol != null) Destroy(ringCol);

            // Set Material
            MeshRenderer ringRenderer = ringGo.GetComponent<MeshRenderer>();
            if (ringRenderer != null && ringMaterial != null)
            {
                ringRenderer.sharedMaterial = ringMaterial;
            }
            ringTransform = ringGo.transform;

            // 2. Build Beam (Volumetric Holographic Cylinder)
            GameObject beamGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beamGo.name = "Beam";
            beamGo.transform.SetParent(visualRoot.transform, false);
            // Center is Y = Height / 2. Height is 2 units in Unity Cylinder.
            beamGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            beamGo.transform.localScale = originalBeamScale;
            beamGo.layer = 2; // Ignore Raycast layer

            // Remove Collider
            Collider beamCol = beamGo.GetComponent<Collider>();
            if (beamCol != null) Destroy(beamCol);

            // Set Material
            MeshRenderer beamRenderer = beamGo.GetComponent<MeshRenderer>();
            if (beamRenderer != null && beamMaterial != null)
            {
                beamRenderer.sharedMaterial = beamMaterial;
            }
            beamTransform = beamGo.transform;

            // Sync visual active state
            visualRoot.SetActive(isVisible);
        }

        private void Update()
        {
            if (!isVisible || visualRoot == null) return;

            // Rotate ring and beam in opposite directions
            if (ringTransform != null)
            {
                ringTransform.Rotate(Vector3.forward * (rotationSpeed * Time.deltaTime));
                
                // Pulse scale
                float ringPulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
                ringTransform.localScale = originalRingScale * ringPulse;
            }

            if (beamTransform != null)
            {
                beamTransform.Rotate(Vector3.up * (-rotationSpeed * 0.5f * Time.deltaTime));
                
                // Pulse beam width slightly
                float beamPulse = 1f + Mathf.Sin(Time.time * pulseSpeed * 0.8f) * (pulseAmount * 0.5f);
                beamTransform.localScale = new Vector3(originalBeamScale.x * beamPulse, originalBeamScale.y, originalBeamScale.z * beamPulse);
            }
        }

        public void Show()
        {
            isVisible = true;
            if (visualRoot != null)
            {
                visualRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            isVisible = false;
            if (visualRoot != null)
            {
                visualRoot.SetActive(false);
            }
        }

        public void SetInteractable(bool value)
        {
            isInteractable = value;
            // Optionally update material colors or alphas if needed to represent inactive state
            if (visualRoot != null)
            {
                var renderers = visualRoot.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var r in renderers)
                {
                    if (r != null && r.material != null)
                    {
                        Color col = r.material.color;
                        col.a = value ? 1.0f : 0.25f;
                        r.material.color = col;
                    }
                }
            }
        }
    }
}
