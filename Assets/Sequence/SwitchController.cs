using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Switch
{
    [RequireComponent(typeof(XRBaseInteractable))]
    public class SwitchController : MonoBehaviour
    {
        public bool isOn = false; // Switch starts in the OFF state

        [Header("Animation Setup")]
        public Animator switchAnimator;
        public string animatorParameterName = "IsOn"; // Must match the Parameter in Animator

        private XRBaseInteractable interactable;

        private void Awake()
        {
            interactable = GetComponent<XRBaseInteractable>();
            // Listen for the trigger/select press from the Ray Interactor
            interactable.selectEntered.AddListener(OnSwitchSelected);
        }

        private void Start()
        {
            // Ensures the Animator knows we are starting in the OFF state
            if (switchAnimator != null)
            {
                switchAnimator.SetBool(animatorParameterName, isOn);
            }
        }

        private void OnDestroy()
        {
            if (interactable != null)
            {
                interactable.selectEntered.RemoveListener(OnSwitchSelected);
            }
        }

        private void OnSwitchSelected(SelectEnterEventArgs args)
        {
            ToggleSwitch();
        }

        public void ToggleSwitch()
        {
            if (isOn)
            {
                TurnOff();
            }
            else
            {
                TurnOn();
            }
        }

        public void TurnOn()
        {
            isOn = true;

            // Play the Turn ON Animation
            if (switchAnimator != null)
            {
                switchAnimator.SetBool(animatorParameterName, true);
            }

            Debug.Log("Switch Animated to ON position");

            // Tell your Sequence Task that the switch was turned on
            SequenceHelperFunctions.instance.OnSwitchToggled(isOn);
        }

        public void TurnOff()
        {
            isOn = false;

            // Play the Turn OFF Animation
            if (switchAnimator != null)
            {
                switchAnimator.SetBool(animatorParameterName, false);
            }

            Debug.Log("Switch Animated to OFF position");

            // Tell your Sequence Task that the switch was turned off
            SequenceHelperFunctions.instance.OnSwitchToggled(isOn);
        }
    }
}