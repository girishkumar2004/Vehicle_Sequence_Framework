using UnityEngine;
using UnityEngine.Events;

namespace TruckTyreReplacement.Core
{
    [AddComponentMenu("Vedanta/Training Teleport Point")]
    public class TrainingTeleportPoint : MonoBehaviour
    {
        [Header("TELEPORT CONFIGURATION")]
        [Tooltip("Unique ID for this teleport point")]
        public string pointId;

        [Tooltip("Description of where this teleport point is and when it is used")]
        public string description;

        [Header("VISUAL CONTROLS")]
        [SerializeField] private GameObject visualRoot;

        [Header("GIZMO SETTINGS")]
        public Color gizmoColor = Color.cyan;
        public float gizmoRadius = 0.3f;

        [Header("EVENTS")]
        public UnityEvent OnPlayerArrived;

        private bool hasArrived = false;

        private void OnTriggerEnter(Collider other)
        {
            if (hasArrived) return;

            // Detect if the collider belongs to the XR Origin player
            bool isPlayer = other.CompareTag("Player") || 
                            other.gameObject.name == "XR Origin (VR)" || 
                            other.CompareTag("MainCamera") ||
                            other.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null;

            if (isPlayer)
            {
                hasArrived = true;
                OnPlayerArrived?.Invoke();
            }
        }

        public void ShowVisual()
        {
            if (visualRoot != null)
            {
                visualRoot.SetActive(true);
            }
            
            var visualComp = GetComponent<TrainingTeleportPointVisual>();
            if (visualComp != null)
            {
                visualComp.Show();
            }
        }

        public void HideVisual()
        {
            if (visualRoot != null)
            {
                visualRoot.SetActive(false);
            }
            
            var visualComp = GetComponent<TrainingTeleportPointVisual>();
            if (visualComp != null)
            {
                visualComp.Hide();
            }
        }

        public void ResetArrival()
        {
            hasArrived = false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, gizmoRadius);
            
            // Draw a line indicating forward orientation
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 0.6f);
        }
    }
}
