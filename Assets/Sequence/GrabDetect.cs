using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class GrabDetect : MonoBehaviour
{
    private XRGrabInteractable grab;
    private bool isActiveStep = false;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        grab.selectEntered.AddListener(OnGrabbed);
    }

    public void ActivateGrab()
    {
        isActiveStep = true;
        if (grab == null) grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.enabled = true;
            if (grab.interactionManager == null)
            {
                grab.interactionManager = Object.FindFirstObjectByType<XRInteractionManager>();
            }
            var col = GetComponent<Collider>();
            if (col != null && !grab.colliders.Contains(col))
                grab.colliders.Add(col);
        }
        var colObj = GetComponent<Collider>();
        if (colObj != null) colObj.enabled = true;
        Debug.Log("Grab Activated for: " + gameObject.name);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (!isActiveStep) return;

        isActiveStep = false;

        Debug.Log("[Tyre] Curves pipe ACTUALLY GRABBED");

        if (SequenceHelperFunctions.instance != null)
        {
            if (gameObject.name == "Curves pipe")
            {
                SequenceHelperFunctions.instance.OnPipeGrabbed();
            }
            else
            {
                SequenceHelperFunctions.instance.OnObjectGrabbed();
            }
        }
    }

    public void DeactivateGrab()
    {
        isActiveStep = false;
    }
}