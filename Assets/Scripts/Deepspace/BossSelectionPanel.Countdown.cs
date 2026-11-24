using TMPro;
using UnityEngine;

namespace Deepspace
{
    // บาส 2026-11-18: the 5-4-3-2-1 after a boss is picked, as a big white Jersey 15 number that drops in each second
    // (BossCountdownDrop). On the floor it sits in the middle of the cards; on the wall under the logo, with the boss
    // name on the line above it.
    public sealed partial class BossSelectionPanel
    {
        // บาส 2026-11-18: "ตรงพื้นไม่ต้องมีนับเลข จะนับตรงจอกำแพงกลางจอ ตัวใหญ่ๆ" - no number on the floor (it covered
        // the cards); on the wall it is big in the middle (was a strip at the bottom).
        static readonly Vector4 WallCountdownBox = new(.3f, .1f, .7f, .9f);

        BossCountdownDrop countdown;

        void ShowCountdown(int selected, int seconds)
        {
            if (!wallLayout) { HideCountdown(); return; }
            var drop = Countdown();
            if (drop == null) return;
            drop.transform.SetAsLastSibling();
            drop.Show(seconds, selected);
        }

        void HideCountdown()
        {
            if (countdown != null) countdown.Hide();
        }

        BossCountdownDrop Countdown()
        {
            if (countdown != null) return countdown;
            var root = (RectTransform)transform;
            var rect = root.Find("Countdown Drop") as RectTransform;
            var box = WallCountdownBox;
            var label = rect != null && rect.TryGetComponent(out TMP_Text existing) ? existing
                : Label(root, "Countdown Drop", null, "5", new Vector2(box.x, box.y), new Vector2(box.z, box.w), 1000);
            SetAnchors(label.rectTransform, new Vector2(box.x, box.y), new Vector2(box.z, box.w));   // a baked one keeps the old box
            var font = BossPlayerCards.Jersey();
            if (font != null) { label.font = font; label.fontSharedMaterial = font.material; }
            label.color = Color.white;
            label.fontStyle = FontStyles.Normal; label.enableVertexGradient = false;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableAutoSizing = true; label.fontSizeMin = 8; label.fontSizeMax = 2000;
            label.raycastTarget = false;
            if (!label.TryGetComponent(out countdown)) countdown = label.gameObject.AddComponent<BossCountdownDrop>();
            countdown.Hide();
            return countdown;
        }
    }
}
