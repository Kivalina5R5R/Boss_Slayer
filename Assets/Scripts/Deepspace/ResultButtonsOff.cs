using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // 2026-11-25 "เอาปุ่ม restart ตรงจอพื้นหลังจากจบเกมออกไปเลย ให้ขึ้นหน้า result เฉยๆ": Boss Slayer's floor result
    // panel shows the results only. Its scene buttons (Restart, Exit, and a SKIP any older flow may have made) are kept
    // hidden whenever the panel is up; the next round starts from the launcher.
    [DisallowMultipleComponent]
    public sealed class ResultButtonsOff : MonoBehaviour
    {
        static readonly string[] Hidden = { "Restart", "Exit", "Skip" };

        public static void Install(RectTransform panel)
        {
            if (!Application.isPlaying || panel == null) return;
            if (panel.TryGetComponent(out ResultChoiceFlow flow)) Destroy(flow);
            if (!panel.TryGetComponent(out ResultButtonsOff off)) panel.gameObject.AddComponent<ResultButtonsOff>();
        }

        void OnEnable() => HideButtons();

        // Every frame too: other code (theme, language, a press bridge) may switch a button back on.
        void LateUpdate() => HideButtons();

        void HideButtons()
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
                if (System.Array.IndexOf(Hidden, button.name) >= 0 && button.gameObject.activeSelf)
                    button.gameObject.SetActive(false);
        }
    }
}
