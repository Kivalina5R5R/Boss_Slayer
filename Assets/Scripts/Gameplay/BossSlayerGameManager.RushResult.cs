using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // บาส 2026-10-20: the boss rush result screen, laid out like the web prototype and dressed in the Boss Fight
    // "arcane codex" look (BossFantasyTheme: chamfered night-stone plates, gilded borders and rules, carved-gold titles).
    // Wall: since 2026-11-26 the shared RAID COMPLETE / BOSS DEFEATED screen (BossRaidResultScreen). Floor: nothing since 2026-11-26 (only the result choices).
    // The floor's result panel stays (SKIP / Restart / Exit + ResultChoiceFlow); only its space backdrop and its other
    // children are hidden while the rush layout shows.
    public sealed partial class BossSlayerGameManager
    {
        int rushKills;
        readonly List<GameObject> rushHiddenResultParts = new();
        Image rushHiddenPanelBackdrop;
        Texture rushFloorBackgroundBefore;

        // ---------------------------------------------------------------- shared codex styling (perk vote, clock, results)
        TMP_FontAsset CodexTitleFont => floorBossSelection != null && floorBossSelection.fantasyTitle != null ? floorBossSelection.fantasyTitle : BossUiFont;

        // A chamfered night-stone plate with a gilded frame and inner trim; r is in the area's rect space, u = canvas unit.
        UiThemeKit.Plate CodexPlate(RectTransform parent, string name, Rect r, Rect area, float u, Color? accent = null, bool glow = false)
        {
            var trim = accent ?? BossFantasyTheme.GoldDeep;
            var plate = UiThemeKit.MakePlate(parent, name, UiThemeKit.Alpha(BossFantasyTheme.Gold, .85f), UiThemeKit.Alpha(BossFantasyTheme.Night, .97f),
                34f * u, 5f * u, true, UiThemeKit.Alpha(trim, .6f), glow, UiThemeKit.Shape.Chamfer);
            Place(plate.rect, r, area);
            if (glow && plate.glow != null) plate.glow.color = UiThemeKit.Alpha(accent ?? BossFantasyTheme.Gold, .4f);
            return plate;
        }

        // Latin headings in carved gold (Cinzel); Thai lines in the body font (Cinzel has no Thai glyphs).
        void CodexGold(TMP_Text t, bool latin, float spacing = 6) => BossFantasyTheme.GoldText(t, latin ? CodexTitleFont : BossUiFont, spacing, .12f);
        void CodexLetter(TMP_Text t, Color face, float spacing = 2) => BossFantasyTheme.Outlined(t, BossUiFont, face, .14f, spacing);

        // ---------------------------------------------------------------- result layout
        // 2026-11-26 "ลบ particle ตรงพื้นและ UI ตรงพื้นออกไปเลย มีแค่ตรงจอกำแพงพอ": the floor no longer shows a result
        // (no summary plate, texts or confetti). Every session hides the floor result panel's backdrop and its parts;
        // only the SKIP / Restart / Exit choices stay for ResultChoiceFlow. The rush also keeps the king's violet rift floor.
        void ShowRushResultLayout(PlayerState mvp)
        {
            HideRushResultLayout();
            if (gameplayResultPanel == null) return;
            if (gameplayResultPanel.TryGetComponent(out Image backdrop) && backdrop.enabled) { backdrop.enabled = false; rushHiddenPanelBackdrop = backdrop; }
            foreach (Transform child in gameplayResultPanel)
                if (child.gameObject.activeSelf && child.GetComponentsInChildren<Button>(true).Length == 0) { child.gameObject.SetActive(false); rushHiddenResultParts.Add(child.gameObject); }
            if (!IsRushActive || bossFloorBackgroundImage == null) return;
            rushFloorBackgroundBefore = bossFloorBackgroundImage.texture;
            var lava = LoadBackgroundTexture(FinalFloorArt);
            if (lava != null) { bossFloorBackgroundImage.texture = lava; PlayBossBackgroundMovie(bossFloorBackgroundImage, ref floorBackgroundVideo, lava); }
        }

        void HideRushResultLayout()
        {
            foreach (var part in rushHiddenResultParts) if (part != null) part.SetActive(true);
            rushHiddenResultParts.Clear();
            if (rushHiddenPanelBackdrop != null) { rushHiddenPanelBackdrop.enabled = true; rushHiddenPanelBackdrop = null; }
            if (rushFloorBackgroundBefore != null && bossFloorBackgroundImage != null) bossFloorBackgroundImage.texture = rushFloorBackgroundBefore;
            rushFloorBackgroundBefore = null;
            RefreshFireArena();
        }

        // ---------------------------------------------------------------- small layout helpers (rects are fractions of the area)
        static RectTransform NewLayer(RectTransform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            return rect;
        }
        static Rect Sub(Rect area, Rect f) => new Rect(area.xMin + area.width * f.x, area.yMin + area.height * f.y, area.width * f.width, area.height * f.height);
        static void PlaceIn(RectTransform rect, Rect area, Rect f) => Place(rect, Sub(area, f), area);

        TextMeshProUGUI Txt(RectTransform parent, Rect area, string name, Rect f, string text, Color color, FontStyles style)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false); PlaceIn(label.rectTransform, area, f);
            BossFantasyTheme.UseFont(label, BossUiFont);
            label.text = text; label.color = color; label.fontStyle = style; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.enableAutoSizing = true; label.fontSizeMin = 10; label.fontSizeMax = area.height * f.height * .8f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        TextMeshProUGUI RowTxt(RectTransform row, Rect local, Rect f, string text, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var label = Txt(row, local, "Text", f, text, Color.white, FontStyles.Normal);
            label.alignment = align;
            return label;
        }

    }
}
