using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    public sealed partial class BossSelectionPanel : MonoBehaviour
    {
        public Button[] slots;
        public TMP_Text[] slotLabels;
        public TMP_Text heading, detail, selectedName;
        public Button confirm;
        public Image[] frames;
        // Cinzel Black / Bold for the arcane look, set by Tools/Deepspace/BossFight/Bake Fantasy UI (null = keep scene fonts).
        public TMP_FontAsset fantasyTitle, fantasyBody;
        Action<int> choose;
        Action accept;
        bool wired;
        public void Bind(Action<int> onChoose, Action onAccept, bool floor)
        {
            choose = onChoose; accept = onAccept;
            // บาส 2026-10-02: wire once instead of RemoveAllListeners() on every Bind - that also wiped the
            // ButtonPressFeedback punch and BossFightAudio click added in Start. The lambdas read the
            // choose/accept fields, so rebinding just swaps those.
            if (!wired)
            {
                wired = true;
                for (int i = 0; i < slots.Length; i++)
                {
                    int index = i;
                    slots[i].onClick.AddListener(() => choose?.Invoke(index + 1));
                }
                confirm.onClick.AddListener(() => accept?.Invoke());
            }
            // บาส 2026-10-02: the CONFIRM / READY button was hidden. 2026-11-19: the floor's card look brings it back as
            // the SELECT button (ApplyFloorCards); pressing it starts the 3-2-1 (BossSlayerGameManager.StartBossCountdown).
            if (confirm != null) confirm.gameObject.SetActive(false);
            // 2026-11-20 (BossSlayer project): no LED floor here; the floor cards are picked by the players' cursors.
            LayoutFor(!floor);
        }

        public void LayoutFor(bool wall)
        {
            if(wall)ApplyWallLayout();
            else if (ApplyFloorCards()) return;   // the art team's card look (BossSelectionPanel.Cards.cs)
            else
            {
                ApplyFloorLayout();
                // บาส 2026-09-29: floor CONFIRM label was 40 in a 113-unit button (35% fill). Text only — the button rect
                // is also the LED stepping target (BossSelectionLedInput), so its size stays as is.
                var confirmLabel = confirm.GetComponentInChildren<TMP_Text>(true); if (confirmLabel != null) { Size(confirmLabel, 76, 40); confirmLabel.characterSpacing = 6; }
            }
            ApplyFantasy(wall);
            if (wall) ApplyWallLogo();
        }

        // บาส 2026-10-22: the wall no longer repeats the floor's 9 cards (players pick on the floor).
        // 2026-11-18: and it no longer shows anything during the boss select: no menu video, logo, round line or
        // countdown, just plain black. Everything the panel holds is hidden (the old logo objects are removed).
        // 2026-11-19: the black is now the boss hall picture (the 9 boss statues), cropped to fill. SelectBossBG_v7 is v6
        // with 600 px more hall on each side (FLUX outpaint, v6's own pixels in the middle), so the camera can centre the
        // Demon and Hydra too; the whole-hall view still shows about the same part as v6 did.
        const string WallBackgroundPath = "Art/UI/SelectBoss/SelectBossBG_v7";

        void ApplyWallLogo()
        {
            var root = (RectTransform)transform;
            var bg = root.Find("Wall Logo Background") as RectTransform ?? Rect(root, "Wall Logo Background", Vector2.zero, Vector2.one);
            if (!bg.TryGetComponent(out RawImage bgImage)) bgImage = bg.gameObject.AddComponent<RawImage>();
            var picture = GameArt.Load<Texture2D>(WallBackgroundPath);
            bgImage.texture = picture; bgImage.color = picture != null ? Color.white : Color.black; bgImage.raycastTarget = false;
            bgImage.uvRect = picture != null ? CoverUv(picture, root.rect.size) : new Rect(0, 0, 1, 1);
            SetWallHall(bgImage, bgImage.uvRect);   // the camera zooms on the picked boss (BossSelectionPanel.WallZoom.cs)
            bg.SetSiblingIndex(0);
            if (TryGetComponent(out Image backdrop)) backdrop.color = Color.black;
            foreach (var old in new[] { "RawImage == Menu Intro Video", "Wall Logo Text Plate", "Fantasy Border", "Wall Logo" })
                if (root.Find(old) is Transform leftover) RemoveChild(leftover.gameObject);
            // 2026-11-19: everything below lives in the hall picture, so it zooms and floats with the camera. (The rising
            // floor embers it also had were taken out: "เอา particle ตรงพื้นออก ไม่ได้ใช้แล้วหน้า select boss".)
            Rect HallView() => bgImage.uvRect;
            // Beaten bosses' statues break into rubble (BossStatueShatter.cs).
            wallShatter = BossStatueShatter.Attach(root, WallShatterName, HallView);
            wallShatter.transform.SetSiblingIndex(1);
            // The looked-at statue opens glowing eyes (BossStatueEyes.cs); broken statues have none.
            var shatter = wallShatter;
            wallEyes = BossStatueEyes.Attach(root, WallEyesName, HallView, shatter.IsBroken);
            wallEyes.transform.SetSiblingIndex(2);
            // ...and an under light and rising glow squares in its boss's colour (BossStatueAura.cs).
            wallAura = BossStatueAura.Attach(root, WallAuraName, HallView, shatter.IsBroken);
            wallAura.transform.SetSiblingIndex(3);
            // Stone chips and dust falling from the vaults (BossFallingDebris.cs).
            var debris = BossFallingDebris.Attach(root, WallDebrisName, HallView);
            debris.transform.SetSiblingIndex(4);
            // Anything else under the panel stays hidden (a scene saved earlier may still hold a "Wall Embers" layer).
            foreach (Transform child in root)
                child.gameObject.SetActive(child == bg || child == wallShatter.transform || child == wallEyes.transform
                    || child == wallAura.transform || child == debris.transform);
        }

        const string WallDebrisName = "Wall Falling Stones", WallShatterName = "Wall Statue Shatter";
        BossStatueShatter wallShatter;
        BossStatueEyes wallEyes;
        BossStatueAura wallAura;
        int wallPickedKind;   // the picked boss (Display), whose statue keeps its eyes and aura
        const string WallEyesName = "Wall Statue Eyes", WallAuraName = "Wall Statue Aura";

        // The part of the picture that fills an area of the given size without stretching (centre crop).
        static Rect CoverUv(Texture picture, Vector2 area)
        {
            if (area.x <= 0 || area.y <= 0) area = new Vector2(8192, 2160);
            float ratio = (area.x / area.y) / ((float)picture.width / picture.height);
            return ratio < 1 ? new Rect((1 - ratio) * .5f, 0, ratio, 1) : new Rect(0, (1 - 1 / ratio) * .5f, 1, 1 / ratio);
        }

        static void RemoveChild(GameObject child)
        {
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }

        // Arcane-codex skin over the existing layout: cut-corner stone cards with a gilded inner border and a name
        // plaque, carved-gold title between ornamental rules, Cinzel lettering. Colours of the card frame, portrait and
        // confirm bar are still driven by Display/ApplyTheme (per-boss accent), only their shape changes here.
        // Idempotent (children are found or created), so Bind can run it every time and the Editor bake can save it.
        void ApplyFantasy(bool wall)
        {
            var root = (RectTransform)transform;
            float h = Mathf.Max(1, root.rect.height), u = wall ? h / 2160f : h / (float)FloorDisplaySettings.Height;
            if (TryGetComponent(out Image backdrop)) backdrop.color = UiThemeKit.Alpha(BossFantasyTheme.Night, .97f);
            if (!wall) UiThemeKit.Ring(root, "Fantasy Border", UiThemeKit.Shape.Chamfer, UiThemeKit.Alpha(BossFantasyTheme.Gold, .55f), 5 * u, 22 * u).transform.SetAsFirstSibling();
            var title = heading.rectTransform;
            BossFantasyTheme.GoldText(heading, fantasyTitle, wall ? 18 : 12);
            float titleY = (title.anchorMin.y + title.anchorMax.y) * .5f;
            // Rules stop short of the title (it spans ~.38-.62 of the wall, ~.28-.72 of the floor).
            BossFantasyTheme.Rule(root, "Title Rule Left", wall ? .27f : .10f, wall ? .365f : .255f, titleY, 4 * u, true);
            BossFantasyTheme.Rule(root, "Title Rule Right", wall ? .635f : .745f, wall ? .73f : .90f, titleY, 4 * u, false);
            BossFantasyTheme.Letter(detail, fantasyBody, BossFantasyTheme.ParchmentDim, wall ? 8 : 4);
            BossFantasyTheme.GoldText(selectedName, fantasyTitle, wall ? 10 : 6, .1f);
            for (int i = 0; i < slots.Length; i++)
            {
                var frame = (RectTransform)slots[i].transform;
                float cut = Mathf.Min(frame.rect.width, frame.rect.height) * .12f;
                UiThemeKit.Skin(frames[i], UiThemeKit.Shape.Chamfer, frames[i].color, cut);
                frames[i].raycastTarget = true; // the card is the Button's target graphic (mouse picks in the Editor)
                var inside = frame.Find("Portrait Background") as RectTransform;
                if (inside == null) continue;
                if (inside.TryGetComponent(out Image portrait)) UiThemeKit.Skin(portrait, UiThemeKit.Shape.Chamfer, portrait.color, cut * .85f);
                UiThemeKit.Ring(inside, "Gilded Border", UiThemeKit.Shape.Chamfer, UiThemeKit.Alpha(BossFantasyTheme.Gold, .6f), (wall ? 5 : 2.5f) * u, (wall ? 18 : 7) * u).transform.SetSiblingIndex(0);
                // Name plaque: dark stone band with a gold edge under the name.
                var nameRect = slotLabels[i].rectTransform;
                var plaque = UiThemeKit.MakePlate(inside, "Name Plaque", UiThemeKit.Alpha(BossFantasyTheme.Gold, .7f), UiThemeKit.Alpha(BossFantasyTheme.Night, .85f),
                    (wall ? 26 : 10) * u, (wall ? 4 : 2) * u, false, default, false, UiThemeKit.Shape.Chamfer);
                plaque.rect.anchorMin = nameRect.anchorMin + new Vector2(0, .01f); plaque.rect.anchorMax = nameRect.anchorMax - new Vector2(0, wall ? .02f : 0);
                plaque.rect.offsetMin = plaque.rect.offsetMax = Vector2.zero;
                UiThemeKit.PutBehind(plaque.rect, nameRect);
                BossFantasyTheme.Letter(slotLabels[i], fantasyBody, BossFantasyTheme.Parchment, wall ? 2 : 1);
                slotLabels[i].margin = new Vector4(12, 0, 12, 0) * u;
                slotLabels[i].fontSizeMin = wall ? 30 : 16;
                // Wall emblem sits inside the gilded border with room to breathe.
                var face = inside.Find("Boss Face") as RectTransform;
                if (wall && face != null) { face.anchorMin = new Vector2(.12f, .32f); face.anchorMax = new Vector2(.88f, .93f); face.offsetMin = face.offsetMax = Vector2.zero; }
                // บาส 2026-10-02: floor cards got shorter for the walking lanes (ApplyFloorLayout), and the emblem circle
                // poked over the card's top edge - smaller, between the name plaque and the gilded border.
                else if (face != null) { face.anchorMin = new Vector2(.12f, .31f); face.anchorMax = new Vector2(.88f, .88f); face.offsetMin = face.offsetMax = Vector2.zero; }
            }
            var confirmRect = (RectTransform)confirm.transform;
            if (confirm.targetGraphic is Image bar) { UiThemeKit.Skin(bar, UiThemeKit.Shape.Chamfer, bar.color, confirmRect.rect.height * .3f); bar.raycastTarget = true; }
            UiThemeKit.Ring(confirmRect, "Gilded Border", UiThemeKit.Shape.Chamfer, UiThemeKit.Alpha(BossFantasyTheme.GoldLight, .85f), (wall ? 6 : 3) * u, (wall ? 14 : 6) * u).transform.SetSiblingIndex(0);
            var confirmLabel = confirm.GetComponentInChildren<TMP_Text>(true);
            BossFantasyTheme.Letter(confirmLabel, fantasyTitle, BossFantasyTheme.Parchment, wall ? 14 : 6);
        }
        // Countdown while a pick waits to be confirmed. บาส 2026-11-18: a big white number drops in each second
        // (.Countdown.cs); on the wall the boss name takes the round line's place above it.
        public void ShowAutoConfirm(int selected, int seconds)
        {
            if (selected <= 0) return;
            string name = BossNextBossCard.NameFor((BossPassiveState.Kind)selected);
            if (wallLayout && detail != null) detail.text = name;
            if (selectedName != null) selectedName.text = wallLayout ? "" : $"{name}  •  STARTS IN {seconds}";
            ShowCountdown(selected, seconds);
        }

        // บาส 2026-10-20: the 15-minute boss rush has no fixed boss count and smaller per-boss steps (BossSlayerGameManager.Rush.cs).
        public static bool OpenEndedRounds;
        public static float HpStepPercent = 25f, SpeedStepPercent = 12f;
        public static System.Func<int, int> HpPercentOfRound; // the rush reads its HP table instead of a fixed step
        // 2026-11-19: "ถ้า clear บอสตัวนั้นไปแล้วไปโดนปุ่ม ... ปิดการรับ input ไปเลย" - a cleared boss's card takes no input
        // at all: no bounce, no pick, no wall zoom (BossSelectionLedInput, BossSlayerGameManager.BossCardAt).
        readonly HashSet<int> clearedKinds = new HashSet<int>();
        public bool IsCleared(int kind) => clearedKinds.Contains(kind);

        public void Display(int round, int total, int selected, HashSet<int> available, HashSet<int> cleared)
        {
            clearedKinds.Clear(); clearedKinds.UnionWith(cleared);
            if (wallLayout && wallShatter != null) wallShatter.Show(cleared);
            wallPickedKind = selected;
            gameObject.SetActive(true); transform.SetAsLastSibling();
            heading.text = "CHOOSE YOUR FOE";
            detail.text = $"ROUND {round}{(OpenEndedRounds ? "" : $" / {total}")}     •     HP +{(HpPercentOfRound != null ? HpPercentOfRound(round) : Mathf.RoundToInt((round - 1) * HpStepPercent))}%     SPEED +{Mathf.RoundToInt((round - 1) * SpeedStepPercent)}%"; // บาส 2026-10-06: ATK no longer scales (B1)
            selectedName.text = selected > 0 ? BossNextBossCard.NameFor((BossPassiveState.Kind)selected) : "STEP ON A CARD";
            if (selected <= 0) HideCountdown();
            confirm.interactable = selected > 0 && available.Contains(selected) && !cleared.Contains(selected);
            if (cardLayout) { ShowCards(selected, available, cleared); return; }
            for (int i = 0; i < slots.Length; i++)
            {
                int kind = i + 1;
                bool done = cleared.Contains(kind), enabled = available.Contains(kind) && !done;
                slots[i].interactable = enabled;
                // CLEAR is now a badge across the card (ShowClearBadge), not a suffix on the name.
                slotLabels[i].text = BossNextBossCard.NameFor((BossPassiveState.Kind)kind) + (done ? "" : wallLayout ? (!enabled ? WallLocked : "") : !enabled ? "  /  LOCKED" : "");
                frames[i].color = done ? BossFantasyTheme.StoneLight : selected == kind ? BossFantasyTheme.Gold : BossFantasyTheme.GoldDeep;
            }
            ApplyTheme(selected, cleared);
            for (int i = 0; i < slots.Length; i++) ShowClearBadge(i, cleared.Contains(i + 1));
        }
        // บาส 2026-09-29 playtest: "CLEAR" was a 72%-size second line under the name (~32 units on the wall, ~15 on the
        // floor). A defeated boss now gets a dark veil over its card and a solid CLEAR band straight across the
        // emblem, sized to the card (wall band ~200 units tall, floor ~80), so it reads at a glance from the floor.
        // Runtime-only children of the slot, drawn last (above emblem + name), no raycast, so LED hits are unchanged.
        // Fantasy skin: a crimson "slain" band with a gold edge and parchment lettering.
        static readonly Color ClearBadgeColor = BossFantasyTheme.Blood, ClearInk = BossFantasyTheme.Parchment, ClearVeil = new Color(0f, 0f, 0f, .6f);
        GameObject[] clearBadges;
        void ShowClearBadge(int index, bool on)
        {
            if (clearBadges == null) clearBadges = new GameObject[slots.Length];
            if (!on) { if (clearBadges[index] != null && clearBadges[index].activeSelf) clearBadges[index].SetActive(false); return; }
            if (clearBadges[index] == null)
            {
                var frame = slots[index].transform;
                var root = Rect(frame, "Clear Badge", Vector2.zero, Vector2.one);
                var veil = root.gameObject.AddComponent<Image>(); veil.color = ClearVeil; veil.raycastTarget = false;
                // Band sits over the emblem, clear of the name line under it.
                var band = Rect(root, "Band", new Vector2(-.02f, wallLayout ? .45f : .38f), new Vector2(1.02f, wallLayout ? .61f : .66f));
                var fill = band.gameObject.AddComponent<Image>(); fill.color = ClearBadgeColor; fill.raycastTarget = false;
                var edge = band.gameObject.AddComponent<Outline>(); edge.effectColor = BossFantasyTheme.Gold; edge.effectDistance = wallLayout ? new Vector2(6, -6) : new Vector2(3, -3);
                var font = fantasyTitle != null ? fantasyTitle : slotLabels[index] != null ? slotLabels[index].font : null;
                var label = (TextMeshProUGUI)Label(band, "Label", font, "CLEAR", Vector2.zero, Vector2.one, wallLayout ? 190 : 72);
                label.color = ClearInk; label.characterSpacing = wallLayout ? 18 : 10; label.fontSizeMin = wallLayout ? 60 : 24;
                label.margin = new Vector4(8, 4, 8, 4); label.textWrappingMode = TextWrappingModes.NoWrap;
                clearBadges[index] = root.gameObject;
            }
            clearBadges[index].transform.SetAsLastSibling();
            if (!clearBadges[index].activeSelf) clearBadges[index].SetActive(true);
        }
        // บาส 2026-09-29: per-boss theme on the select screen. Each slot frame wears its own boss colour (dim),
        // the selected one goes bright; the name/confirm/backdrop cross-fade to the selected boss. 0 = old look.
        public float ThemeStrength;
        const float ThemeFadeSeconds = .4f;
        Graphic[] themed; Color[] themeOriginal, themeFrom, themeTo, themeLast; Tween themeTween;
        void ApplyTheme(int selected, HashSet<int> cleared)
        {
            if (themed == null)
            {
                themed = new Graphic[frames.Length * 2 + 3];
                for (int i = 0; i < frames.Length; i++) { themed[i] = frames[i]; var bg = frames[i].transform.Find("Portrait Background"); themed[frames.Length + i] = bg != null ? bg.GetComponent<Image>() : null; }
                themed[frames.Length * 2] = selectedName; themed[frames.Length * 2 + 1] = confirm != null ? confirm.targetGraphic : null; themed[frames.Length * 2 + 2] = GetComponent<Image>();
                themeOriginal = new Color[themed.Length]; themeFrom = new Color[themed.Length]; themeTo = new Color[themed.Length];
                for (int i = 0; i < themed.Length; i++) if (themed[i] != null) themeOriginal[i] = themed[i].color;
            }
            float strength = Mathf.Clamp01(ThemeStrength);
            var pick = BossThemePalette.UiTheme((BossPassiveState.Kind)Mathf.Max(0, selected));
            for (int i = 0; i < themed.Length; i++)
            {
                if (themed[i] == null) continue;
                Color look = themeOriginal[i];
                if (i < frames.Length)
                {
                    int kind = i + 1; var own = BossThemePalette.UiTheme((BossPassiveState.Kind)kind);
                    themeOriginal[i] = frames[i].color; // Display() just wrote the untinted state colour.
                    look = cleared.Contains(kind) ? themeOriginal[i] : selected == kind ? Color.Lerp(own.primary, Color.white, .3f) : own.primary * new Color(.42f,.42f,.42f,1);
                }
                else if (i < frames.Length * 2) look = Color.Lerp(themeOriginal[i], BossThemePalette.UiTheme((BossPassiveState.Kind)(i - frames.Length + 1)).panelDark, .75f);
                else if (i == frames.Length * 2) look = selected > 0 ? pick.textLight : themeOriginal[i];
                else if (i == frames.Length * 2 + 1) look = selected > 0 ? Color.Lerp(Color.Lerp(BossFantasyTheme.Blood, BossFantasyTheme.GoldDeep, .35f), pick.primary, .2f) : BossFantasyTheme.StoneLight;
                else if (selected > 0) look = Color.Lerp(themeOriginal[i], pick.panelDark, .7f);
                look.a = themeOriginal[i].a;
                themeTo[i] = Color.Lerp(themeOriginal[i], look, strength);
                themeFrom[i] = themeShown ? themeLast[i] : themeTo[i];
            }
            themeTween?.Kill(); themeShown = true;
            if (!gameObject.activeInHierarchy || strength <= 0) { BlendTheme(1); return; }
            BlendTheme(0); themeTween = DOVirtual.Float(0, 1, ThemeFadeSeconds, BlendTheme).SetEase(Ease.OutSine).SetTarget(this);
        }
        bool themeShown;
        void BlendTheme(float t)
        {
            if (themeLast == null) themeLast = new Color[themed.Length];
            for (int i = 0; i < themed.Length; i++) if (themed[i] != null) themed[i].color = themeLast[i] = Color.Lerp(themeFrom[i], themeTo[i], t);
        }
        // บาส 2026-09-29 playtest: on the 8192x2160 wall the floor-shaped 3x3 grid came out as 1800x400 strips with
        // ~300-unit emblems and 35-unit names — unreadable from the floor. The wall is 3.8:1, so a single row of 9
        // tall cards fits it: each card is ~815x1240 (57% of wall height) with a ~700-unit emblem, vs ~480 for a
        // scaled-up 3x3 (three rows must share the height). The row also keeps boss order left-to-right, which reads
        // the same as the floor grid row-by-row. Wall only; the floor keeps its scene layout. Runs on Bind, so the
        // scene still shows the floor-shaped defaults in Edit mode.
        // บาส 2026-10-02: walking lanes between the floor cards. The 3x3 grid had ~0.1 m between cards, so players
        // trod on a neighbour's card on the way to theirs. The grid now spreads wider (x .10-.90) and keeps a real lane
        // between cards; the cards themselves are the LED stepping targets (BossSelectionLedInput), so the lane is a
        // strip nobody selects anything in.
        [Header("Floor layout")]
        [Tooltip("Walking lane between floor cards, left-right, in metres.")]
        [SerializeField, Min(0f)] float floorLaneXMeters = .5f;
        [Tooltip("Walking lane between floor card rows, front-back, in metres.")]
        [SerializeField, Min(0f)] float floorLaneYMeters = .35f;

        void ApplyFloorLayout()
        {
            if (slots == null || slots.Length == 0) return;
            const float left = .10f, right = .90f, bottom = .19f, top = .79f; // between the title and "Selected Boss"
            const int columns = 3;
            int rows = Mathf.CeilToInt(slots.Length / (float)columns);
            float floorMeters = BossFloorPlayers.ActiveFloorWidthMeters;
            float pxPerMeter = FloorDisplaySettings.Width / floorMeters; // square floor pixels
            float gapX = Mathf.Min(floorLaneXMeters * pxPerMeter / FloorDisplaySettings.Width, (right - left) * .3f);
            float gapY = Mathf.Min(floorLaneYMeters * pxPerMeter / FloorDisplaySettings.Height, (top - bottom) * .3f);
            float cardW = (right - left - gapX * (columns - 1)) / columns;
            float cardH = (top - bottom - gapY * (rows - 1)) / rows;
            for (int i = 0; i < slots.Length; i++)
            {
                int col = i % columns, row = i / columns;
                float x = left + col * (cardW + gapX), y = top - (row + 1) * cardH - row * gapY;
                SetAnchors((RectTransform)slots[i].transform, new Vector2(x, y), new Vector2(x + cardW, y + cardH));
            }
        }

        bool wallLayout;
        const string WallLocked = "\n<size=72%>LOCKED</size>";
        void ApplyWallLayout()
        {
            wallLayout = true;
            SetAnchors(heading.rectTransform, new Vector2(.03f, .865f), new Vector2(.97f, .975f)); Size(heading, 200, 110);
            SetAnchors(detail.rectTransform, new Vector2(.03f, .795f), new Vector2(.97f, .865f)); Size(detail, 100, 60);
            const float left = .02f, width = .96f, gap = .008f, bottom = .245f, top = .775f;
            float slot = (width - gap * (slots.Length - 1)) / slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                float x = left + i * (slot + gap);
                var frame = (RectTransform)slots[i].transform;
                SetAnchors(frame, new Vector2(x, bottom), new Vector2(x + slot, top));
                var inside = frame.Find("Portrait Background") as RectTransform;
                if (inside != null) { SetAnchors(inside, Vector2.zero, Vector2.one); inside.offsetMin = new Vector2(16, 16); inside.offsetMax = new Vector2(-16, -16); }
                var face = inside != null ? inside.Find("Boss Face") as RectTransform : null;
                if (face != null) SetAnchors(face, new Vector2(.05f, .29f), new Vector2(.95f, .96f));
                SetAnchors(slotLabels[i].rectTransform, new Vector2(.03f, .025f), new Vector2(.97f, .27f));
                Size(slotLabels[i], 88, 44); slotLabels[i].characterSpacing = 4;
                slotLabels[i].textWrappingMode = TextWrappingModes.NoWrap; slotLabels[i].lineSpacing = -12;
            }
            // บาส 2026-09-29 playtest: CONFIRM / READY was an 84-max label in a 151-unit bar. Now a 3280x280 bar with a
            // 200-max label (wall is display-only, so this rect is not a touch target); cards give up 5% height for it.
            SetAnchors(selectedName.rectTransform, new Vector2(.2f, .158f), new Vector2(.8f, .238f)); Size(selectedName, 150, 80);
            SetAnchors((RectTransform)confirm.transform, new Vector2(.3f, .018f), new Vector2(.7f, .148f));
            var confirmLabel = confirm.GetComponentInChildren<TMP_Text>(true);
            if (confirmLabel != null) { Size(confirmLabel, 200, 90); confirmLabel.characterSpacing = 14; confirmLabel.margin = new Vector4(40, 0, 40, 0); }
        }
        static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static void Size(TMP_Text label, float max, float min) { label.enableAutoSizing = true; label.fontSizeMax = max; label.fontSizeMin = min; }
        void OnDestroy() { themeTween?.Kill(); wallZoomTween?.Kill(); }
        static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, string value, Vector2 min, Vector2 max, float size)
        {
            var label = Rect(parent,name,min,max).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = value; label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold; label.enableAutoSizing = true; label.fontSizeMin = 10; label.fontSizeMax = size;
            label.color = Color.white; label.raycastTarget = false; return label;
        }
        public static BossSelectionPanel Create(RectTransform parent, TMP_FontAsset font)
        {
            var root = Rect(parent,"Panel == Boss Selection",Vector2.zero,Vector2.one);
            root.gameObject.AddComponent<Image>().color = new Color(.015f,.025f,.075f,.99f);
            var panel = root.gameObject.AddComponent<BossSelectionPanel>();
            panel.heading = Label(root,"Title",font,"BOSS SELECT",new Vector2(.05f,.86f),new Vector2(.95f,.98f),100);
            panel.heading.color = new Color(.35f,.87f,1f);
            panel.detail = Label(root,"Round and Difficulty",font,"ROUND 1 / 3",new Vector2(.05f,.80f),new Vector2(.95f,.86f),38);
            panel.slots = new Button[9]; panel.slotLabels = new TMP_Text[9]; panel.frames = new Image[9];
            for (int i = 0; i < 9; i++)
            {
                float x = .16f + i % 3 * .23f, y = .19f + (2 - i / 3) * .20f;
                var frame = Rect(root,"Boss Slot " + (i+1),new Vector2(x,y),new Vector2(x+.22f,y+.185f));
                panel.frames[i] = frame.gameObject.AddComponent<Image>(); panel.frames[i].color = new Color(.12f,.5f,.78f);
                panel.slots[i] = frame.gameObject.AddComponent<Button>(); panel.slots[i].targetGraphic = panel.frames[i];
                var inside = Rect(frame,"Portrait Background",new Vector2(.015f,.025f),new Vector2(.985f,.975f));
                var bg = inside.gameObject.AddComponent<Image>(); bg.color = new Color(.025f,.06f,.14f); bg.raycastTarget = false;
                var iconObject = Rect(inside,"Boss Face",new Vector2(.12f,.23f),new Vector2(.88f,.98f)).gameObject; iconObject.AddComponent<CanvasRenderer>(); var icon = iconObject.AddComponent<BossEmblemGraphic>();
                icon.Kind = (BossPassiveState.Kind)(i+1); icon.raycastTarget = false;
                panel.slotLabels[i] = Label(inside,"Name",font,BossNextBossCard.NameFor(icon.Kind),new Vector2(.02f,.015f),new Vector2(.98f,.23f),35);
            }
            panel.selectedName = Label(root,"Selected Boss",font,"CHOOSE YOUR OPPONENT",new Vector2(.1f,.115f),new Vector2(.9f,.18f),42);
            var start = Rect(root,"Confirm Boss",new Vector2(.33f,.025f),new Vector2(.67f,.105f));
            var image = start.gameObject.AddComponent<Image>(); image.color = new Color(.08f,.46f,.7f);
            panel.confirm = start.gameObject.AddComponent<Button>(); panel.confirm.targetGraphic = image; panel.confirm.interactable = false;
            Label(start,"Label",font,"CONFIRM  /  READY",Vector2.zero,Vector2.one,40);
            panel.selectedName.gameObject.AddComponent<BossScorePulse>().amount = .06f;
            return panel;
        }
    }
}
