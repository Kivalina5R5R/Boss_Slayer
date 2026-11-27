using UnityEngine;

namespace BossSlayer
{
    // บาส 2026-11-18: "เปลี่ยน UI HUD หน้า Gameplay เป็นแบบรูปตัวอย่าง". The boss name, HP, STUN and ULTIMATE bars on the
    // wall are now the art team's (BossWallHud). The old bars keep running underneath (their fills, numbers, theme
    // colours and damage echo are still updated by the code that owns them) but are made invisible, so nothing that
    // reads or animates them breaks.
    public sealed partial class BossSlayerGameManager
    {
        BossWallHud wallHud;

        void UpdateBossWallHud()
        {
            if (cutsceneUiPanel == null) return;
            if (wallHud == null) wallHud = BossWallHud.Attach(cutsceneUiPanel);
            if (wallHud == null) return;
            HideOldBossBars();
            wallHud.Show(BossHudName(), bossHp, bossMaxHp, stunGauge, stunNeed, ultimateCharge, UltimateNeed);
            // Balance Note v3 §5.2: Quick Ultimate shortens the bar; Lightning shows its red-gem counter.
            wallHud.SetUltimateLength(UltimateNeed / (float)Mathf.Max(1, ultimateNeed));
            wallHud.ShowChain(IsRushActive && HasPerk("chain") ? RushChainNeed : 0, rushChainCount);
        }

        // "DEMON : RED BOY": the boss name and its epithet, as the old name plate showed them.
        string BossHudName()
        {
            string name = uiPlateLabel != null ? uiPlateLabel.text : "";
            string epithet = uiPlateEpithet != null ? uiPlateEpithet.text : "";
            if (string.IsNullOrEmpty(name)) name = bossModel != null ? BossNextBossCard.NameFor(BossPassiveState.Identify(bossModel.name)) : "";
            string text = string.IsNullOrEmpty(epithet) ? name : name + " : " + epithet;
            return text.ToUpperInvariant();
        }

        // The old bar roots (with their name plate) and the theme's frames and glow beside them.
        void HideOldBossBars()
        {
            foreach (var fill in new[] { bossHpFill, stunGaugeFill, ultimateGaugeFill })
                if (fill != null && fill.transform.parent != null) Invisible(fill.transform.parent);
            foreach (Transform child in cutsceneUiPanel)
                if (child.name.StartsWith("UI Theme ==")) Invisible(child);
        }

        static void Invisible(Transform part)
        {
            if (!part.TryGetComponent(out CanvasGroup group)) group = part.gameObject.AddComponent<CanvasGroup>();
            if (group.alpha != 0f) { group.alpha = 0f; group.blocksRaycasts = false; }
        }
    }
}
