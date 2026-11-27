using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // บาส 2026-09-29: per-boss UI theme ("ธีมตามบอส"). Chrome only — frames, plates, glows, titles, banners,
    // edge frames. Semantic colours (gems, stun/ultimate fill, boss/player HP fill, damage echo, damage numbers)
    // are never touched. Boss HP fill stays red on purpose: an accent fill would read as player-HP green on the
    // Hydra, as the blue ultimate gauge on the cyan bosses, and as an empty white bar on the Sentinel.
    // Everything here is runtime-created or a colour lerp from the scene's own colour, so strength 0 = old look.
    public sealed partial class BossSlayerGameManager
    {
        [Header("Boss UI Theme")]
        [SerializeField] bool enableBossUiTheme = true;
        [SerializeField, Range(0f, 1f)] float bossUiThemeStrength = 1f;
        [SerializeField, Min(0f)] float bossUiThemeFadeSeconds = .4f;
        [SerializeField, Min(0f)] float bossUiPhase2FadeSeconds = .8f;
        [SerializeField, Min(.2f)] float bossUiGlowPulseSeconds = 2.8f;
        [SerializeField, Range(0f, 1f)] float bossUiLowHpThreshold = .2f;
        [SerializeField, Range(0f, 1f)] float bossUiEdgeFrameAlpha = .4f;
        [SerializeField] bool bossUiShowNamePlate = true;

        BossThemePalette.BossUiTheme uiTheme = BossThemePalette.UiTheme(BossPassiveState.Kind.None), uiThemeFrom, uiThemeTo;
        BossPassiveState.Kind uiThemeKind; bool uiThemePhase2, uiThemeBuilt;
        float uiThemePhase, uiUrgency, uiUrgencyApplied = -1, uiKick, uiWeightApplied = -1; int uiBannerCount;
        Tween uiThemeTween;
        Image uiHpBack, uiStunBack, uiUltBack, uiHpGlow, uiHpFrame, uiStunFrame, uiUltFrame, uiPlateBack, uiPlateRule, uiPlateGlow, uiFloorEdge, uiWallEdge;
        Color uiHpBackBase, uiStunBackBase, uiUltBackBase;
        RectTransform uiPlate; TextMeshProUGUI uiPlateLabel, uiPlateEpithet;
        static readonly Color BannerBackBase = new Color(.08f, .015f, .04f, .88f), BannerTextBase = new Color(1, .88f, .25f);

        float UiThemeWeight => enableBossUiTheme ? Mathf.Clamp01(bossUiThemeStrength) : 0f;

        // Called by ResetFight once the next boss is picked (before the portal opens).
        void BeginBossUiTheme()
        {
            EnsureBossUiTheme();
            uiThemeKind = bossModel != null ? BossPassiveState.Identify(bossModel.name) : BossPassiveState.Kind.None;
            uiThemePhase2 = false; uiUrgency = 0; uiKick = 1;
            RetargetUiTheme(BossThemePalette.UiTheme(uiThemeKind), bossUiThemeFadeSeconds);
        }
        void RetargetUiTheme(BossThemePalette.BossUiTheme target, float seconds)
        {
            uiThemeTween?.Kill();
            uiThemeFrom = uiTheme; uiThemeTo = target; SetPlateText(target);
            if (seconds <= 0f || UiThemeWeight <= 0f) { BlendUiTheme(1); return; }
            BlendUiTheme(0);
            uiThemeTween = DOVirtual.Float(0, 1, seconds, BlendUiTheme).SetEase(Ease.InOutSine).SetLink(gameObject); // scaled time: pauses with timeScale
        }
        void BlendUiTheme(float t) { uiTheme = BossThemePalette.BossUiTheme.Lerp(uiThemeFrom, uiThemeTo, t); ApplyUiThemeStatic(); }

        void EnsureBossUiTheme()
        {
            if (uiThemeBuilt) return;
            uiThemeBuilt = true;
            var hpRoot = bossHpFill != null ? bossHpFill.transform.parent as RectTransform : null;
            uiHpBack = hpRoot != null ? hpRoot.GetComponent<Image>() : null; if (uiHpBack != null) uiHpBackBase = uiHpBack.color;
            uiStunBack = stunGaugeFill != null ? stunGaugeFill.transform.parent.GetComponent<Image>() : null; if (uiStunBack != null) uiStunBackBase = uiStunBack.color;
            uiUltBack = ultimateGaugeFill != null ? ultimateGaugeFill.transform.parent.GetComponent<Image>() : null; if (uiUltBack != null) uiUltBackBase = uiUltBack.color;
            if (hpRoot != null)
            {
                uiHpGlow = Surround(hpRoot, "UI Theme == Bar Glow", 90f, BossThemePalette.SoftHalo, 31f / 90f);
                uiHpFrame = Surround(hpRoot, "UI Theme == Bar Frame", 14f, uiHpBack != null ? uiHpBack.sprite : null, 1f);
                // บาส 2026-09-29 playtest fix: the plate used to sit in the 178-unit gap ABOVE the bar, which put it (and its
                // glow) against the wall's top edge and forced the epithet down to ~55% of a 118 max. It now sits BESIDE the
                // bar, right-aligned to the bar's left end and spanning the HP + stun rows (178..618 from the top edge), so:
                // no bar/gauge moves (the boss-overlap question stays the designer's), the glow stays >=110 units off the top
                // edge, and the name/epithet get two full stacked lines. Left of the bar is kept clear of the player cards
                // (WallHud: cards end at x=1010, plate starts at x=1071 on the 8192 wall).
                uiPlate = BossUiRect.Make(hpRoot, "UI Theme == Boss Name Plate");
                uiPlate.anchorMin = uiPlate.anchorMax = new Vector2(0, 1); uiPlate.pivot = new Vector2(1, 1);
                uiPlate.anchoredPosition = new Vector2(-PlateGap, 0); uiPlate.sizeDelta = new Vector2(PlateWidth, PlateHeight);
                uiPlateGlow = ThemeImage(uiPlate, "Plate Glow", BossThemePalette.SoftHalo, 31f / 60f, -60f);
                uiPlateBack = ThemeImage(uiPlate, "Plate Back", BossThemePalette.SoftHalo, 31f / 40f, 0f);
                uiPlateRule = ThemeImage(uiPlate, "Plate Rule", null, 1f, 0f);
                var rule = uiPlateRule.rectTransform; rule.anchorMin = new Vector2(.22f, .44f); rule.anchorMax = new Vector2(.97f, .44f); rule.pivot = new Vector2(1, .5f); rule.sizeDelta = new Vector2(0, 10); rule.anchoredPosition = Vector2.zero;
                var font = BossUiFont;
                // Name: bold, 180 max (the HP numbers are 156). Epithet: letter-spaced caps at 88 = 49% of the name.
                uiPlateLabel = PlateText(uiPlate, "Plate Name", font, new Vector2(.02f, .47f), new Vector2(.97f, .98f), PlateNameSize, 120f, 4f);
                uiPlateEpithet = PlateText(uiPlate, "Plate Epithet", font, new Vector2(.02f, .06f), new Vector2(.97f, .41f), PlateNameSize * .49f, PlateNameSize * .45f, 18f);
            }
            if (stunGaugeFill != null) uiStunFrame = Surround((RectTransform)stunGaugeFill.transform.parent, "UI Theme == Gauge Frame", 10f, uiStunBack != null ? uiStunBack.sprite : null, 1f);
            if (ultimateGaugeFill != null) uiUltFrame = Surround((RectTransform)ultimateGaugeFill.transform.parent, "UI Theme == Gauge Frame", 10f, uiUltBack != null ? uiUltBack.sprite : null, 1f);
            uiFloorEdge = EdgeFrame(playArea, bossFloorBackgroundImage, 80f);
            uiWallEdge = EdgeFrame(cutsceneArea, bossWallBackgroundImage, 150f);
            ApplyFantasyHud();
        }

        // Arcane-codex pass on the wall HUD: cut-corner bars and fills with a gilded rim, Cinzel numbers with a dark
        // outline, STUN / ULTIMATE captions (the gauges only showed "0 / 80"), and a stone name plate. Fill colours
        // stay as they are (they carry game meaning); only shapes, lettering and frames change.
        void ApplyFantasyHud()
        {
            var font = BossUiFont;
            FantasyBar(uiHpBack, bossHpFill, bossHpText, null, font);
            FantasyBar(uiStunBack, stunGaugeFill, stunGaugeText, "STUN", font);
            FantasyBar(uiUltBack, ultimateGaugeFill, ultimateGaugeText, "ULTIMATE", font);
            foreach (var frame in new[] { uiHpFrame, uiStunFrame, uiUltFrame })
                if (frame != null) UiThemeKit.Skin(frame, UiThemeKit.Shape.Chamfer, frame.color, frame.rectTransform.rect.height * .3f);
            if (uiPlate != null && uiPlateBack != null)
            {
                UiThemeKit.Skin(uiPlateBack, UiThemeKit.Shape.Chamfer, uiPlateBack.color, 70f);
                UiThemeKit.Ring(uiPlate, "Gilded Border", UiThemeKit.Shape.Chamfer, UiThemeKit.Alpha(BossFantasyTheme.Gold, .75f), 6f, 10f);
                BossFantasyTheme.UseFont(uiPlateLabel, font); BossFantasyTheme.UseFont(uiPlateEpithet, font);
            }
        }
        static void FantasyBar(Image back, Image fill, TMP_Text value, string caption, TMP_FontAsset font)
        {
            if (back == null) return;
            var rect = back.rectTransform;
            float cut = rect.rect.height * .32f;
            UiThemeKit.Skin(back, UiThemeKit.Shape.Chamfer, back.color, cut);
            if (fill != null) UiThemeKit.Skin(fill, UiThemeKit.Shape.Chamfer, fill.color, cut * .8f);
            var rim = UiThemeKit.Ring(rect, "Gilded Rim", UiThemeKit.Shape.Chamfer, UiThemeKit.Alpha(BossFantasyTheme.Gold, .8f), Mathf.Max(3f, rect.rect.height * .06f));
            rim.transform.SetSiblingIndex(fill != null ? fill.transform.GetSiblingIndex() + 1 : 0);
            if (value != null) { BossFantasyTheme.Outlined(value, font, BossFantasyTheme.Parchment, .22f, 2); value.fontStyle = FontStyles.Normal; }
            if (string.IsNullOrEmpty(caption)) return;
            var label = UiThemeKit.Text(rect, "Fantasy Caption", font);
            UiThemeKit.Place(label.rectTransform, .03f, .1f, .4f, .9f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            UiThemeKit.Size(label, rect.rect.height * .55f, .5f);
            BossFantasyTheme.Outlined(label, font, BossFantasyTheme.GoldLight, .2f, 6);
            label.text = caption;
        }
        // A sibling placed just behind `target`, the same rect grown by `pad` on every side.
        static Image Surround(RectTransform target, string name, float pad, Sprite sprite, float ppu)
        {
            var rect = BossUiRect.Make(target.parent, name);
            rect.SetSiblingIndex(target.GetSiblingIndex());
            rect.anchorMin = target.anchorMin; rect.anchorMax = target.anchorMax; rect.pivot = target.pivot;
            rect.sizeDelta = target.sizeDelta + Vector2.one * pad * 2f;
            rect.anchoredPosition = target.anchoredPosition + (target.pivot * 2f - Vector2.one) * pad;
            var image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false; image.color = Color.clear;
            if (sprite != null) { image.sprite = sprite; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = ppu; }
            return image;
        }
        static Image ThemeImage(RectTransform parent, string name, Sprite sprite, float ppu, float inset)
        {
            var rect = BossUiRect.Make(parent, name);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * inset; rect.offsetMax = -Vector2.one * inset;
            var image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false; image.color = Color.clear;
            if (sprite != null) { image.sprite = sprite; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = ppu; }
            return image;
        }
        // Soft rim glow round a whole display, drawn right above its background and under everything else.
        static Image EdgeFrame(RectTransform area, RawImage background, float width)
        {
            if (area == null) return null;
            var image = ThemeImage(area, "UI Theme == Edge Frame", BossThemePalette.SoftEdge, 31f / width, 0f);
            image.fillCenter = false;
            int index = background != null && background.transform.parent == area ? background.transform.GetSiblingIndex() + 1 : 0;
            image.transform.SetSiblingIndex(index); image.gameObject.SetActive(false);
            return image;
        }
        const float PlateGap = 90f, PlateWidth = 1600f, PlateHeight = 440f, PlateNameSize = 180f;
        static TextMeshProUGUI PlateText(RectTransform plate, string name, TMP_FontAsset font, Vector2 min, Vector2 max, float size, float minSize, float spacing)
        {
            var rect = BossUiRect.Make(plate, name);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            BossFantasyTheme.UseFont(label, font);
            label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.Right; label.characterSpacing = spacing;
            // Auto-size only as an overflow guard: the longest name/epithet fit at full size in 1600 units.
            label.enableAutoSizing = true; label.fontSizeMin = minSize; label.fontSizeMax = size; label.fontSize = size;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false; label.outlineWidth = .2f;
            return label;
        }
        // Same Outfit-Bold SDF the select/ready screens use (serialized on the floor select panel); the runtime-made
        // statusText only carries the TMP default font, which is why the old plate/victory text looked generic.
        // Fantasy pass: Cinzel Bold (baked onto the select panel, with Outfit + Thai fallbacks) for every Boss Fight label.
        TMP_FontAsset BossUiFont => floorBossSelection != null && floorBossSelection.fantasyBody != null ? floorBossSelection.fantasyBody
            : floorBossSelection != null && floorBossSelection.heading != null && floorBossSelection.heading.font != null
            ? floorBossSelection.heading.font : statusText != null ? statusText.font : null;
        void SetPlateText(BossThemePalette.BossUiTheme target)
        {
            if (uiPlateLabel == null) return;
            uiPlateLabel.text = uiThemeKind == BossPassiveState.Kind.None ? "" : BossNextBossCard.NameFor(uiThemeKind);
            if (uiPlateEpithet != null) uiPlateEpithet.text = target.epithet ?? "";
        }

        static Color Alpha(Color c, float a) { c.a = a; return c; }
        void ApplyUiThemeStatic()
        {
            if (!uiThemeBuilt) return;
            var t = uiTheme; float k = UiThemeWeight, u = uiUrgency;
            uiWeightApplied = k; uiUrgencyApplied = u;
            var back = Color.Lerp(t.panelDark, Color.white, .06f);
            if (uiHpBack != null) uiHpBack.color = Color.Lerp(uiHpBackBase, Alpha(back, uiHpBackBase.a), k);
            if (uiStunBack != null) uiStunBack.color = Color.Lerp(uiStunBackBase, Alpha(back, uiStunBackBase.a), k);
            if (uiUltBack != null) uiUltBack.color = Color.Lerp(uiUltBackBase, Alpha(back, uiUltBackBase.a), k);
            // Low HP: the frame heats toward white and the glow shifts toward the frame colour.
            if (uiHpFrame != null) uiHpFrame.color = Alpha(Color.Lerp(Color.Lerp(BossFantasyTheme.GoldDeep, t.primary, .35f), Color.white, .45f * u), k);
            if (uiHpGlow != null) uiHpGlow.color = Alpha(Color.Lerp(t.glow, t.primary, .5f * u), .6f * k);
            if (uiStunFrame != null) uiStunFrame.color = Alpha(Color.Lerp(BossFantasyTheme.GoldDeep, t.secondary, .3f), .8f * k);
            if (uiUltFrame != null) uiUltFrame.color = Alpha(Color.Lerp(BossFantasyTheme.GoldDeep, t.secondary, .3f), .8f * k);
            if (uiPlate != null)
            {
                bool plate = bossUiShowNamePlate && k > 0f && uiThemeKind != BossPassiveState.Kind.None;
                if (uiPlate.gameObject.activeSelf != plate) uiPlate.gameObject.SetActive(plate);
                uiPlateBack.color = Alpha(t.panelDark, .85f * k); uiPlateRule.color = Alpha(t.secondary, k); uiPlateGlow.color = Alpha(t.glow, .45f * k);
                uiPlateLabel.color = Alpha(t.textLight, Mathf.Clamp01(k * 2f)); uiPlateLabel.outlineColor = Color.Lerp(Color.black, t.panelDark, .5f);
                if (uiPlateEpithet != null) { uiPlateEpithet.color = Alpha(Color.Lerp(t.secondary, Color.white, .45f), Mathf.Clamp01(k * 2f)); uiPlateEpithet.outlineColor = uiPlateLabel.outlineColor; }
            }
            ThemePlayerCards(t, k);
            if (uiFloorEdge != null) uiFloorEdge.color = Alpha(t.primary, bossUiEdgeFrameAlpha * k);
            if (uiWallEdge != null) uiWallEdge.color = Alpha(t.glow, bossUiEdgeFrameAlpha * .8f * k);
            uiBannerCount = BannerCount(); ThemeBanner(floorSignatureBanner, t, k); ThemeBanner(wallSignatureBanner, t, k);
        }
        static void ThemeBanner(TextMeshProUGUI label, BossThemePalette.BossUiTheme t, float k)
        {
            if (label == null) return;
            label.color = Color.Lerp(BannerTextBase, Color.Lerp(t.textLight, Color.white, .25f), k);
            label.outlineColor = Color.Lerp(Color.black, t.panelDark, k);
            var bg = label.transform.parent.GetChild(0).GetComponent<Image>(); if (bg == null) return;
            bg.color = Color.Lerp(BannerBackBase, Alpha(t.panelDark, BannerBackBase.a), k);
            if (!bg.TryGetComponent<Outline>(out var edge)) edge = bg.gameObject.AddComponent<Outline>();
            edge.effectDistance = new Vector2(4, -4); edge.effectColor = Alpha(t.primary, k);
        }

        // Every frame from LateUpdate: pulses only touch CanvasRenderer alpha (no mesh rebuild, no allocations).
        void TickBossUiTheme(float dt)
        {
            if (!uiThemeBuilt) return;
            float k = UiThemeWeight;
            bool fightUi = cutsceneUiPanel == null || cutsceneUiPanel.gameObject.activeInHierarchy;
            // 2026-11-24 "ลบขอบจอที่เป็นสีต่างๆตอนสู้บอสออก ทุกตัว": the coloured screen-edge frames stay off for every boss.
            SetEdgeActive(uiFloorEdge, false); SetEdgeActive(uiWallEdge, false);
            if (passiveFeedback != null) { passiveFeedback.ThemePanel = uiTheme.panelDark; passiveFeedback.ThemeEdge = uiTheme.secondary; passiveFeedback.ThemeWeight = k; }
            if (!fightUi || dt <= 0f) return; // timeScale 0: hold the current frame
            // Phase 2 (Demon/Dragon/Golem): same flags that fire the "PHASE 2: ..." notice, so the UI turns with it.
            bool phase2 = BossThemePalette.HasPhase2Theme(uiThemeKind) &&
                (uiThemeKind == BossPassiveState.Kind.Demon ? passives.DemonAdult : uiThemeKind == BossPassiveState.Kind.Dragon ? passives.DragonHeated
                    : uiThemeKind == BossPassiveState.Kind.AbyssalKing ? passives.KingAwakened : golemRedPhase);
            if (phase2 != uiThemePhase2) { uiThemePhase2 = phase2; uiKick = 1; RetargetUiTheme(BossThemePalette.UiTheme(uiThemeKind, phase2), bossUiPhase2FadeSeconds); }
            float ratio = bossHp / (float)Mathf.Max(1, bossMaxHp);
            bool urgent = gameStarted && !resultShown && bossHp > 0 && ratio < bossUiLowHpThreshold;
            uiUrgency = Mathf.MoveTowards(uiUrgency, urgent ? 1f : 0f, dt * 3f);
            uiKick = Mathf.MoveTowards(uiKick, 0f, dt * 1.25f);
            if (Mathf.Abs(k - uiWeightApplied) > .001f || Mathf.Abs(uiUrgency - uiUrgencyApplied) > .02f || (uiUrgency != uiUrgencyApplied && (uiUrgency == 0 || uiUrgency == 1)) || BannerCount() != uiBannerCount) ApplyUiThemeStatic();
            float period = Mathf.Lerp(bossUiGlowPulseSeconds, .6f, uiUrgency);
            uiThemePhase = Mathf.Repeat(uiThemePhase + dt * Mathf.PI * 2f / period, Mathf.PI * 2f);
            float p = .5f + .5f * Mathf.Sin(uiThemePhase);
            float glow = Mathf.Max(uiKick, Mathf.Lerp(Mathf.Lerp(.45f, 1f, p), Mathf.Lerp(.25f, 1f, p), uiUrgency));
            SetPulse(uiHpGlow, glow); SetPulse(uiPlateGlow, glow);
            SetPulse(uiHpFrame, Mathf.Max(uiKick, Mathf.Lerp(Mathf.Lerp(.85f, 1f, p), Mathf.Lerp(.55f, 1f, p), uiUrgency)));
            float edge = Mathf.Lerp(.65f, 1f, p); SetPulse(uiFloorEdge, edge); SetPulse(uiWallEdge, edge);
        }
        int BannerCount() => (floorSignatureBanner != null ? 1 : 0) + (wallSignatureBanner != null ? 1 : 0);
        static void SetPulse(Graphic graphic, float alpha) { if (graphic != null && graphic.isActiveAndEnabled) graphic.canvasRenderer.SetAlpha(alpha); }
        static void SetEdgeActive(Image edge, bool on) { if (edge != null && edge.gameObject.activeSelf != on) edge.gameObject.SetActive(on); }

        // Screens outside the fight read the same palette.
        void ThemeBossSelection()
        {
            if (floorBossSelection != null) floorBossSelection.ThemeStrength = UiThemeWeight;
            if (wallBossSelection != null) wallBossSelection.ThemeStrength = UiThemeWeight;
        }
        void ThemeBossVictory(BossVictoryPanel panel)
        {
            if (panel == null) return;
            panel.ThemeStrength = UiThemeWeight; panel.ThemePhase2 = uiThemePhase2;
        }
    }
}
