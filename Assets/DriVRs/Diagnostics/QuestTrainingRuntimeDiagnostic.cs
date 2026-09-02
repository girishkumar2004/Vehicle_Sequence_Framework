using UnityEngine;
using TMPro;
using TruckTyreReplacement.UI;
using TruckTyreReplacement.Core;

namespace TruckTyreReplacement.Diagnostics
{
    public class QuestTrainingRuntimeDiagnostic : MonoBehaviour
    {
        private void Start()
        {
            StartCoroutine(DiagnosticRoutine());
        }

        private System.Collections.IEnumerator DiagnosticRoutine()
        {
            int checks = 0;
            while (checks < 10)
            {
                yield return new WaitForSeconds(3f);
                checks++;

                string log = "[QUEST TRAINING] Diagnostic Check #" + checks + "\n";

                // 1. Check TrainingHologramAnchor
                var anchor = Object.FindFirstObjectByType<TrainingHologramAnchor>();
                if (anchor != null)
                {
                    log += $"- Anchor: Found, Active={anchor.gameObject.activeInHierarchy}, Parent={(anchor.transform.parent != null ? anchor.transform.parent.name : "None")}\n";
                }
                else
                {
                    log += "- Anchor: NOT FOUND in scene\n";
                }

                // 2. Check Camera
                var cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
                if (cam != null)
                {
                    log += $"- Main Camera: Found, Active={cam.gameObject.activeInHierarchy}, WorldPos={cam.transform.position}\n";
                }

                // 3. Check Manager
                var mgr = Manager.Instance ?? Object.FindFirstObjectByType<Manager>();
                if (mgr != null)
                {
                    log += $"- Manager: Found, Language={mgr.CurrentLanguage}, IsSpeaking={mgr.IsSpeaking}\n";
                }
                else
                {
                    log += "- Manager: NOT FOUND\n";
                }

                Debug.Log(log);
            }
        }
    }
}
