using UnityEngine;
using System.Collections.Generic;

namespace TruckTyreReplacement.Interactions
{
    public class ObjectiveHighlight : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("The root target GameObject to highlight")]
        [SerializeField] private GameObject targetObject;

        [Tooltip("List of renderers on the target object to apply highlighting to")]
        [SerializeField] private Renderer[] targetRenderers;

        [Tooltip("If true, automatically queries all renderers in children on target object")]
        [SerializeField] private bool includeChildren = true;

        [Header("Material Settings")]
        [Tooltip("The shared fluorescent green highlight material")]
        [SerializeField] private Material highlightMaterial;

        [Header("Blinking Settings")]
        [SerializeField] private bool blink = false;
        [SerializeField] private float blinkInterval = 0.5f;

        // Runtime caching states
        private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        private bool hasCachedOriginalMaterials = false;
        private bool isHighlighted = false;
        
        private float blinkTimer = 0f;
        private bool blinkState = true;

        public bool IsHighlighted => isHighlighted;
        
        public Material HighlightMaterial 
        { 
            get => highlightMaterial; 
            set => highlightMaterial = value; 
        }

        public bool IncludeChildren 
        { 
            get => includeChildren; 
            set => includeChildren = value; 
        }

        public bool Blink
        {
            get => blink;
            set => blink = value;
        }

        private void Awake()
        {
            if (targetObject == null)
            {
                targetObject = gameObject;
            }
            InitializeRenderers();
        }

        /// <summary>
        /// Collects renderers from the target object if not manually assigned
        /// </summary>
        public void InitializeRenderers()
        {
            if (targetObject == null)
            {
                targetObject = gameObject;
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                if (includeChildren)
                {
                    targetRenderers = targetObject.GetComponentsInChildren<Renderer>(true);
                }
                else
                {
                    var r = targetObject.GetComponent<Renderer>();
                    targetRenderers = r != null ? new Renderer[] { r } : new Renderer[0];
                }
            }
        }

        private void Update()
        {
            if (!isHighlighted || !blink) return;

            blinkTimer += Time.deltaTime;
            if (blinkTimer >= blinkInterval)
            {
                blinkTimer = 0f;
                blinkState = !blinkState;
                ApplyBlinkState(blinkState);
            }
        }

        private void ApplyBlinkState(bool showHighlight)
        {
            if (targetRenderers == null) return;

            foreach (var renderer in targetRenderers)
            {
                if (renderer == null) continue;

                if (showHighlight)
                {
                    int materialCount = renderer.sharedMaterials.Length;
                    Material[] highlightMats = new Material[materialCount];
                    for (int i = 0; i < materialCount; i++)
                    {
                        highlightMats[i] = highlightMaterial;
                    }
                    renderer.sharedMaterials = highlightMats;
                }
                else
                {
                    if (originalMaterials.TryGetValue(renderer, out Material[] origMats))
                    {
                        renderer.sharedMaterials = origMats;
                    }
                }
            }
        }

        /// <summary>
        /// Configures the material and children settings dynamically
        /// </summary>
        public void Configure(Material material, bool children)
        {
            if (isHighlighted)
            {
                DisableHighlight();
            }
            highlightMaterial = material;
            includeChildren = children;
            targetRenderers = null; // Reset renderers to force query with new includeChildren value
            InitializeRenderers();
        }

        /// <summary>
        /// Updates the target object dynamically and updates renderer references
        /// </summary>
        public void SetTarget(GameObject target)
        {
            if (isHighlighted)
            {
                DisableHighlight();
            }

            targetObject = target;
            targetRenderers = null; // Forces re-query
            hasCachedOriginalMaterials = false;
            originalMaterials.Clear();
            InitializeRenderers();
        }

        /// <summary>
        /// Temporarily replaces all materials on the target renderers with the highlight material
        /// </summary>
        public void EnableHighlight()
        {
            if (isHighlighted) return;
            if (highlightMaterial == null)
            {
                Debug.LogWarning("[ObjectiveHighlight] Highlight material is not assigned.");
                return;
            }

            InitializeRenderers();

            if (targetRenderers == null || targetRenderers.Length == 0) return;

            // 1. Cache original materials if they haven't been cached already
            if (!hasCachedOriginalMaterials)
            {
                originalMaterials.Clear();
                foreach (var renderer in targetRenderers)
                {
                    if (renderer == null) continue;
                    originalMaterials[renderer] = renderer.sharedMaterials;
                }
                hasCachedOriginalMaterials = true;
            }

            // 2. Apply highlight materials
            foreach (var renderer in targetRenderers)
            {
                if (renderer == null) continue;

                int materialCount = renderer.sharedMaterials.Length;
                Material[] highlightMats = new Material[materialCount];
                for (int i = 0; i < materialCount; i++)
                {
                    highlightMats[i] = highlightMaterial;
                }
                renderer.sharedMaterials = highlightMats;
            }

            isHighlighted = true;
            blinkTimer = 0f;
            blinkState = true;
        }

        /// <summary>
        /// Restores original materials on all target renderers
        /// </summary>
        public void DisableHighlight()
        {
            if (!isHighlighted) return;

            if (hasCachedOriginalMaterials)
            {
                foreach (var renderer in targetRenderers)
                {
                    if (renderer == null) continue;
                    
                    if (originalMaterials.TryGetValue(renderer, out Material[] origMats))
                    {
                        renderer.sharedMaterials = origMats;
                    }
                }
            }

            isHighlighted = false;
            blinkState = true;
        }
    }
}
