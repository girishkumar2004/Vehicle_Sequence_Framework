using UnityEngine;
using UnityEngine.EventSystems;

namespace TruckTyreReplacement.Core
{
    [AddComponentMenu("Vedanta/Training Verification")]
    public class TrainingVerification : MonoBehaviour
    {
        [ContextMenu("Run Training Verification")]
        public bool RunVerification()
        {
            Debug.Log("[Verification] Starting Framework & Scene Verification...");
            bool allPassed = true;

            // 1. Verify Exactly One XR Origin exists
            int originCount = 0;
            foreach (var go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None)) {
                if (go.name == "XR Origin (VR)") originCount++;
            }
            if (originCount == 1)
            {
                Debug.Log("[Verification] PASS: Exactly one XR Origin (VR) exists in the scene.");
            }
            else
            {
                Debug.LogError($"[Verification] FAIL: Found {originCount} GameObjects named 'XR Origin (VR)'. Expected exactly 1.");
                allPassed = false;
            }

            // 2. Verify Zero LocalTTS GameObjects exist
            var oldTTS = GameObject.Find("LocalTTS");
            if (oldTTS == null)
            {
                Debug.Log("[Verification] PASS: 0 duplicate LocalTTS GameObjects in scene.");
            }
            else
            {
                Debug.LogError("[Verification] FAIL: Dedicated LocalTTS GameObject still exists!");
                allPassed = false;
            }

            // 3. Verify Exactly One Manager exists
            var managers = Object.FindObjectsByType<Manager>(FindObjectsSortMode.None);
            if (managers.Length == 1)
            {
                Debug.Log("[Verification] PASS: Exactly 1 generic Manager component active.");
            }
            else
            {
                Debug.LogError($"[Verification] FAIL: Found {managers.Length} Manager components. Expected exactly 1.");
                allPassed = false;
            }

            // 4. Verify Exactly One SequenceHandler exists
            var handlers = Object.FindObjectsByType<SequenceHandler>(FindObjectsSortMode.None);
            if (handlers.Length == 1)
            {
                Debug.Log("[Verification] PASS: Exactly 1 SequenceHandler active.");
            }
            else
            {
                Debug.LogError($"[Verification] FAIL: Found {handlers.Length} SequenceHandler components. Expected exactly 1.");
                allPassed = false;
            }

            // 5. Verify Manager translation database
            var mgr = Manager.Instance ?? Object.FindFirstObjectByType<Manager>();
            if (mgr != null)
            {
                string disp = mgr.GetDisplayText("welcome");
                if (!string.IsNullOrEmpty(disp) && disp != "welcome")
                {
                    Debug.Log($"[Verification] PASS: Translation database loaded. 'welcome' -> {disp.Substring(0, Mathf.Min(30, disp.Length))}...");
                }
                else
                {
                    Debug.LogError("[Verification] FAIL: Translation database failed to resolve 'welcome'.");
                    allPassed = false;
                }
            }

            // 6. Verify AIRBUM and Wheel references
            var wheel = GameObject.Find("Whl HD FR");
            if (wheel != null)
            {
                Debug.Log("[Verification] PASS: Whl HD FR exists in scene.");
            }
            else
            {
                Debug.LogError("[Verification] FAIL: Whl HD FR missing in scene.");
                allPassed = false;
            }

            var airbum = GameObject.Find("AIRBUM");
            if (airbum != null)
            {
                Debug.Log("[Verification] PASS: AIRBUM exists in scene.");
            }
            else
            {
                Debug.LogError("[Verification] FAIL: AIRBUM missing in scene.");
                allPassed = false;
            }

            if (allPassed)
            {
                Debug.Log("[Verification] ALL VERIFICATION CHECKS PASSED!");
            }
            else
            {
                Debug.LogError("[Verification] VERIFICATION COMPLETED WITH FAILURES.");
            }

            return allPassed;
        }
    }
}
