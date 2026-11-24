using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Deepspace
{
    public sealed class BossFightReadyScreen:MonoBehaviour
    {
        public TMP_Text title,summary,roster,countdown,hint;
        // Cinzel Black / Bold for the arcane look, set by Tools/Deepspace/BossFight/Bake Fantasy UI (null = skip).
        public TMP_FontAsset fantasyTitle, fantasyBody;

        static readonly Color PanelColor = new Color(0.055f, 0.008f, 0.02f, 0.97f);
        static readonly Color TitleColor = new Color(1f, 0.22f, 0.32f, 1f);
        static readonly Color SummaryColor = new Color(1f, 0.82f, 0.38f, 1f);
        static readonly Color CountdownColor = new Color(0.35f, 0.9f, 1f, 1f);
        static readonly Color HintColor = new Color(0.68f, 0.72f, 1f, 1f);
        // Semantic ready colours (same hexes the old roster lines used): teal-green = in the area, orange = still waiting.
        static readonly Color ReadyColor = new Color32(0x78, 0xE5, 0xCC, 0xFF), WaitColor = new Color32(0xFF, 0xCE, 0x8A, 0xFF);

        void Awake()
        {
            var panel = GetComponent<Image>();
            if (panel != null) panel.color = PanelColor;
            if (title != null) title.color = TitleColor;
            if (summary != null) summary.color = SummaryColor;
            if (countdown != null) countdown.color = CountdownColor;
            if (hint != null) hint.color = HintColor;
        }

        // บาส 2026-09-29: the boss is already picked when READY shows, so wear its theme (panel, accent bar,
        // title, countdown). The roster's green/orange ready colours stay semantic. strength 0 = original look.
        // `kind`/`font` (optional) fill the boss name block and swap every label to the boss UI font.
        public void ApplyTheme(BossThemePalette.BossUiTheme theme, float strength, BossPassiveState.Kind kind = BossPassiveState.Kind.None, TMP_FontAsset font = null)
        {
            EnsureLayout();
            strength = Mathf.Clamp01(strength);
            // Swap font only if Thai still renders with it (own glyphs or a fallback such as Thai SDF).
            if (font != null && font != appliedFont && font.HasCharacter('ผ', true)) SetFont(font);
            var panel = GetComponent<Image>(); var accent = transform.Find("Accent");
            if (panel != null) { var c = Color.Lerp(PanelColor, theme.panelDark, strength); c.a = PanelColor.a; panel.color = c; }
            if (accent != null && accent.TryGetComponent<Image>(out var bar)) bar.color = Color.Lerp(AccentColor, theme.primary, strength);
            if (title != null) title.color = Color.Lerp(TitleColor, Color.Lerp(theme.primary, Color.white, .3f), strength);
            if (countdown != null) countdown.color = Color.Lerp(CountdownColor, Color.Lerp(theme.textLight, theme.glow, .35f), strength);
            if (hint != null) hint.color = Color.Lerp(HintColor, Color.Lerp(theme.secondary, Color.white, .6f), strength);
            bossKind = kind; bool known = kind != BossPassiveState.Kind.None;
            if (bossName != null)
            {
                bossName.text = known ? BossNextBossCard.NameFor(kind) : "";
                bossName.color = Color.Lerp(Color.white, theme.textLight, strength);
                SafeOutline(bossName, -1f, Color.Lerp(Color.black, theme.panelDark, .5f));
            }
            if (bossEpithet != null)
            {
                bossEpithet.text = known ? theme.epithet ?? "" : "";
                bossEpithet.color = Color.Lerp(SummaryColor, Color.Lerp(theme.secondary, Color.white, .45f), strength);
            }
            if (emblem != null) { emblem.Kind = kind; emblem.SetVerticesDirty(); emblem.gameObject.SetActive(known); }
            if (emblemRing != null) { emblemRing.color = Color.Lerp(new Color(.35f, .55f, .75f, .8f), theme.glow, strength); emblemRing.gameObject.SetActive(known); }
            // บาส 2026-10-02: the emblem now shows the boss model's picture, so draw it over the glow ring, not under it.
            if (emblem != null && emblem.transform.parent != null) emblem.transform.parent.SetAsLastSibling();
            ApplyFantasy(theme);
        }

        // Arcane-codex skin: dark stone tablet with cut corners and a gilded border, a gold rule instead of the accent
        // bar, carved-gold title and boss name in Cinzel. Ready/wait chip colours stay semantic.
        void ApplyFantasy(BossThemePalette.BossUiTheme theme)
        {
            if (fantasyTitle == null) return;
            var rect = (RectTransform)transform;
            float u = (wide ? panelHeight / 1814f : panelHeight / 1267f);
            if (TryGetComponent(out Image panel))
            {
                var c = Color.Lerp(BossFantasyTheme.Night, theme.panelDark, .35f); c.a = .97f;
                UiThemeKit.Skin(panel, UiThemeKit.Shape.Chamfer, c, 60 * u);
            }
            UiThemeKit.Ring(rect, "Gilded Border", UiThemeKit.Shape.Chamfer, UiThemeKit.Alpha(BossFantasyTheme.Gold, .7f), 5 * u, 18 * u).transform.SetSiblingIndex(0);
            if (transform.Find("Accent") is Transform accent && accent.TryGetComponent<Image>(out var bar)) bar.color = UiThemeKit.Alpha(BossFantasyTheme.Gold, .8f);
            BossFantasyTheme.GoldText(title, fantasyTitle, wide ? 20 : 12);
            BossFantasyTheme.GoldText(bossName, fantasyTitle, wide ? 8 : 4, .15f);
            if (bossEpithet != null) { BossFantasyTheme.UseFont(bossEpithet, fantasyBody); bossEpithet.color = Color.Lerp(BossFantasyTheme.ParchmentDim, theme.secondary, .35f); }
            if (countdown != null) BossFantasyTheme.GoldText(countdown, fantasyTitle, 0, .1f);
            // Soft round aura behind the emblem (the halo sprite read as a solid orange square at this size).
            if (emblemRing != null)
            {
                float size = Mathf.Max(1, Mathf.Min(emblemRing.rectTransform.rect.width, emblemRing.rectTransform.rect.height));
                emblemRing.sprite = UiThemeKit.SpriteFor(UiThemeKit.Shape.Rounded, UiThemeKit.Solid);
                emblemRing.type = Image.Type.Sliced; emblemRing.pixelsPerUnitMultiplier = 22f / (size * .5f);
                emblemRing.color = UiThemeKit.Alpha(Color.Lerp(theme.glow, BossFantasyTheme.Night, .35f), .35f);
            }
        }
        static readonly Color AccentColor = new Color(0.08f, 0.8f, 0.95f, 1f);

        public void Display(bool[] present,int required,int bosses,bool allPresent,int seconds)
        {
            gameObject.SetActive(true); // before EnsureLayout: TMP labels get their material on first enable
            EnsureLayout();
            if (countdown != null && countdown.GetComponent<DeepspaceScorePulse>() == null)
                countdown.gameObject.AddComponent<DeepspaceScorePulse>().amount = .09f;
            int ready=0;var lines=new StringBuilder();
            for(int i=0;i<required;i++){
                bool arrived=i<present.Length&&present[i];if(arrived)ready++;
                lines.Append("<color=").Append(arrived?"#78E5CC":"#FFCE8A").Append(">ผู้เล่น ").Append(i+1)
                    .Append(arrived?"   พร้อมแล้ว":"   รอเข้าพื้นที่").Append("</color>\n");
            }
            title.text="READY";
            summary.text=$"ผู้เล่นพร้อม {ready}/{required} คน   •   {bosses} บอส";
            roster.text=lines.ToString().TrimEnd(); // still written (editor verification reads it); the P1..Pn chips show it on screen
            countdown.text=allPresent?seconds.ToString():"รอผู้เล่น";
            // The number is the focal point (~70% of the wall panel height); the waiting line stays one readable line.
            if(laidOut){countdown.fontSizeMax=allPresent?panelHeight*(wide?.72f:.4f):panelHeight*(wide?.13f:.09f);
                countdown.fontSizeMin=countdown.fontSizeMax*.35f;}
            hint.text=allPresent?"พร้อมครบแล้ว เตรียมเริ่มเกม":"เดินเข้าพื้นที่ให้ครบทุกคนเพื่อเริ่มเกม";
            UpdateChips(present, required);
        }

        // ---------------------------------------------------------------- layout
        // บาส 2026-09-29 playtest: on the wall (8192x2160, read from metres away) READY was a 76%x76% box with a
        // 170-unit "READY", 85-100-unit Thai lines and a small number — mostly empty. Rebuilt at runtime (the chip
        // count follows the live 1-6 player count) as: boss emblem + name + epithet (theme colours) | giant countdown |
        // P1..Pn chips (semantic green/orange) | hint line. The floor gets the same pieces in a 2:1 arrangement.
        // All sizes are fractions of the panel height, so the same code serves both displays; scene objects keep
        // their serialized values (only their runtime rects/sizes change).
        bool laidOut, wide; float panelHeight, panelWidth; TMP_FontAsset appliedFont; BossPassiveState.Kind bossKind;
        TMP_Text bossName, bossEpithet; BossEmblemGraphic emblem; Image emblemRing; RectTransform chipArea;
        Chip[] chips = new Chip[0]; int chipCount = -1;
        sealed class Chip { public Image Back; public Outline Edge; public TMP_Text Label, State; public int Shown = -1; }

        void EnsureLayout()
        {
            // Only once the screen is active: setting outlineWidth on a never-enabled TMP label throws
            // (null shared material) — that NullReferenceException aborted this layout half-way in the
            // 2026-09-29 playtest, which is why READY still looked like the old screen.
            if (laidOut || title == null || !Application.isPlaying || !gameObject.activeInHierarchy) return;
            var rect = (RectTransform)transform; var parent = rect.parent as RectTransform;
            // Wall vs floor from the canvas's target display (Display 2 = wall), not from rect sizes: the first
            // call happens while the screen is still inactive, when rects can read as 0 — in the 2026-09-29
            // playtest that made the wall pick the floor layout with a ~100-unit panel (tiny text everywhere).
            var canvas = GetComponentInParent<Canvas>(true); var root = canvas != null ? canvas.rootCanvas : null;
            float parentHeight = parent != null ? parent.rect.height : 0f, parentWidth = parent != null ? parent.rect.width : 0f;
            wide = root != null ? root.targetDisplay == 1 : parentHeight > 1f && parentWidth / parentHeight > 3f;
            if (parentHeight <= 1f || parentWidth <= 1f) { parentHeight = wide ? 2160f : FloorDisplaySettings.Height; parentWidth = wide ? 8192f : FloorDisplaySettings.Width; } // display canvas sizes
            // Grow the box: the old 76% x 76% left a dead frame round it on both displays.
            Vector2 boxMin = new Vector2(.04f, wide ? .08f : .05f), boxMax = new Vector2(.96f, wide ? .92f : .95f);
            Anchor(rect, boxMin, boxMax);
            panelHeight = Mathf.Max(100f, parentHeight * (boxMax.y - boxMin.y));
            panelWidth = parentWidth * (boxMax.x - boxMin.x);
            float h = panelHeight;
            var font = title.font; appliedFont = font;
            if (roster != null) roster.gameObject.SetActive(false);
            if (wide)
            {
                Place(title, new Vector2(.03f, .80f), new Vector2(.46f, .93f), h * .09f, TextAlignmentOptions.Left, 24f);
                emblemRing = Ring(rect, new Vector2(.02f, .26f), new Vector2(.15f, .78f));
                emblem = Emblem(rect, new Vector2(.02f, .26f), new Vector2(.15f, .78f));
                bossName = New("Boss Name", font, new Vector2(.165f, .52f), new Vector2(.46f, .78f), h * .2f, TextAlignmentOptions.BottomLeft, 4f);
                bossEpithet = New("Boss Epithet", font, new Vector2(.165f, .40f), new Vector2(.46f, .52f), h * .085f, TextAlignmentOptions.TopLeft, 16f);
                Place(summary, new Vector2(.165f, .27f), new Vector2(.46f, .39f), h * .07f, TextAlignmentOptions.Left, 2f);
                Place(countdown, new Vector2(.47f, .16f), new Vector2(.66f, .95f), h * .72f, TextAlignmentOptions.Center, 0f);
                chipArea = Area(rect, "Ready Chips", new Vector2(.68f, .20f), new Vector2(.975f, .90f));
                Place(hint, new Vector2(.03f, .035f), new Vector2(.97f, .16f), h * .075f, TextAlignmentOptions.Center, 2f);
            }
            else
            {
                Place(title, new Vector2(.04f, .85f), new Vector2(.60f, .95f), h * .08f, TextAlignmentOptions.Left, 14f);
                emblemRing = Ring(rect, new Vector2(.03f, .47f), new Vector2(.19f, .83f));
                emblem = Emblem(rect, new Vector2(.03f, .47f), new Vector2(.19f, .83f));
                bossName = New("Boss Name", font, new Vector2(.21f, .64f), new Vector2(.62f, .83f), h * .14f, TextAlignmentOptions.BottomLeft, 2f);
                bossEpithet = New("Boss Epithet", font, new Vector2(.21f, .55f), new Vector2(.62f, .64f), h * .065f, TextAlignmentOptions.TopLeft, 10f);
                Place(summary, new Vector2(.21f, .45f), new Vector2(.62f, .54f), h * .055f, TextAlignmentOptions.Left, 1f);
                Place(countdown, new Vector2(.63f, .42f), new Vector2(.97f, .95f), h * .4f, TextAlignmentOptions.Center, 0f);
                chipArea = Area(rect, "Ready Chips", new Vector2(.04f, .17f), new Vector2(.96f, .40f));
                Place(hint, new Vector2(.04f, .03f), new Vector2(.96f, .14f), h * .06f, TextAlignmentOptions.Center, 1f);
            }
            SafeOutline(bossName, .2f, null);
            // Glow slot directly behind the emblem slot.
            emblemRing.transform.parent.SetSiblingIndex(emblem.transform.parent.GetSiblingIndex());
            laidOut = true; // last: if anything above throws, the next Display retries (children are reused by name)
        }
        // outlineWidth/outlineColor write into the label's material; skip while TMP hasn't created it yet.
        static void SafeOutline(TMP_Text label, float width, Color? color)
        {
            if (label == null || !label.isActiveAndEnabled || label.fontSharedMaterial == null) return;
            if (width >= 0f) label.outlineWidth = width;
            if (color.HasValue) label.outlineColor = color.Value;
        }
        void SetFont(TMP_FontAsset font)
        {
            appliedFont = font;
            foreach (var label in new[] { title, summary, roster, countdown, hint, bossName, bossEpithet }) BossFantasyTheme.UseFont(label, font);
            foreach (var chip in chips) if (chip != null) { BossFantasyTheme.UseFont(chip.Label, font); BossFantasyTheme.UseFont(chip.State, font); }
        }
        static RectTransform Area(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var existing = parent.Find(name) as RectTransform;
            var rect = existing != null ? existing : (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.layer = parent.gameObject.layer; rect.SetParent(parent, false); Anchor(rect, min, max); return rect;
        }
        static void Anchor(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static void Place(TMP_Text label, Vector2 min, Vector2 max, float size, TextAlignmentOptions align, float spacing)
        {
            if (label == null) return;
            Anchor(label.rectTransform, min, max);
            label.enableAutoSizing = true; label.fontSizeMax = size; label.fontSizeMin = size * .35f; label.fontSize = size;
            label.alignment = align; label.characterSpacing = spacing; label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow; label.margin = Vector4.zero;
            SafeOutline(label, .15f, new Color32(0, 0, 0, 200));
        }
        TMP_Text New(string name, TMP_FontAsset font, Vector2 min, Vector2 max, float size, TextAlignmentOptions align, float spacing)
        {
            var rect = Area(transform, name, min, max);
            var label = rect.GetComponent<TextMeshProUGUI>(); if (label == null) label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            BossFantasyTheme.UseFont(label, font);
            label.raycastTarget = false; label.text = "";
            Place(label, min, max, size, align, spacing); return label;
        }
        // FitInParent sizes a rect to its PARENT, so each graphic sits inside its own anchored slot; hung directly on
        // the panel it grew to a panel-high square (the big orange block over the READY text).
        static Image Ring(RectTransform parent, Vector2 min, Vector2 max)
        {
            var holder = Area(Area(parent, "Boss Emblem Glow Slot", min, max), "Boss Emblem Glow", Vector2.zero, Vector2.one);
            var fit = holder.GetComponent<AspectRatioFitter>(); if (fit == null) fit = holder.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1;
            var image = holder.GetComponent<Image>(); if (image == null) image = holder.gameObject.AddComponent<Image>();
            image.sprite = BossThemePalette.SoftHalo; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 31f / 90f; image.raycastTarget = false;
            holder.gameObject.SetActive(false);
            return image;
        }
        static BossEmblemGraphic Emblem(RectTransform parent, Vector2 min, Vector2 max)
        {
            var holder = Area(Area(parent, "Boss Emblem Slot", min, max), "Boss Emblem", Vector2.zero, Vector2.one);
            var fit = holder.GetComponent<AspectRatioFitter>(); if (fit == null) fit = holder.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1;
            var graphic = holder.GetComponent<BossEmblemGraphic>(); if (graphic == null) graphic = holder.gameObject.AddComponent<BossEmblemGraphic>();
            graphic.raycastTarget = false; holder.localScale = Vector3.one * .82f; holder.gameObject.SetActive(false);
            return graphic;
        }

        // ---------------------------------------------------------------- P1..Pn chips
        // Grid picked for the biggest chip that fits the live count (e.g. wall 6 -> 3x2, floor 6 -> 6x1).
        void BuildChips(int count)
        {
            chipCount = count;
            foreach (var chip in chips) if (chip != null && chip.Back != null) Destroy(chip.Back.gameObject);
            chips = new Chip[count];
            if (chipArea == null || count <= 0) return;
            var area = chipArea.rect; float gap = panelHeight * .025f;
            if (area.width <= 1f || area.height <= 1f) // not laid out yet: derive from the known panel size
                area = new Rect(0, 0, panelWidth * (chipArea.anchorMax.x - chipArea.anchorMin.x), panelHeight * (chipArea.anchorMax.y - chipArea.anchorMin.y));
            int cols = 1; float best = 0;
            for (int c = 1; c <= count; c++)
            {
                int r = Mathf.CeilToInt(count / (float)c);
                float w = (area.width - gap * (c - 1)) / c, hh = (area.height - gap * (r - 1)) / r;
                float s = Mathf.Min(w, hh * 1.4f); // chips a little wider than tall
                if (s > best + .01f) { best = s; cols = c; }
            }
            int rows = Mathf.CeilToInt(count / (float)cols);
            float cellH = (area.height - gap * (rows - 1)) / rows;
            float cw = Mathf.Min((area.width - gap * (cols - 1)) / cols, cellH * 1.4f), ch = Mathf.Min(cellH, cw / 1.1f);
            float totalH = rows * ch + (rows - 1) * gap;
            var font = appliedFont != null ? appliedFont : title.font;
            for (int i = 0; i < count; i++)
            {
                int row = i / cols, col = i % cols;
                int inRow = Mathf.Min(cols, count - row * cols); float rowW = inRow * cw + (inRow - 1) * gap; // centres a short last row
                var go = new GameObject("Ready Chip P" + (i + 1), typeof(RectTransform)); go.layer = chipArea.gameObject.layer;
                var rect = (RectTransform)go.transform; rect.SetParent(chipArea, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(cw, ch);
                rect.anchoredPosition = new Vector2(-rowW * .5f + cw * .5f + col * (cw + gap), totalH * .5f - ch * .5f - row * (ch + gap));
                var chip = chips[i] = new Chip();
                chip.Back = go.AddComponent<Image>(); chip.Back.raycastTarget = false;
                chip.Edge = go.AddComponent<Outline>(); float edge = Mathf.Max(3f, ch * .035f); chip.Edge.effectDistance = new Vector2(edge, -edge);
                chip.Label = ChipText(rect, "P", font, new Vector2(.04f, .36f), new Vector2(.96f, .97f), ch * .55f, 6f);
                chip.Label.text = "P" + (i + 1);
                chip.State = ChipText(rect, "State", font, new Vector2(.04f, .05f), new Vector2(.96f, .36f), ch * .22f, 2f);
            }
        }
        static TMP_Text ChipText(RectTransform parent, string name, TMP_FontAsset font, Vector2 min, Vector2 max, float size, float spacing)
        {
            var rect = Area(parent, name, min, max);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); if (font != null) label.font = font; label.raycastTarget = false;
            Place(label, min, max, size, TextAlignmentOptions.Center, spacing); return label;
        }
        void UpdateChips(bool[] present, int required)
        {
            if (!laidOut) return;
            if (required != chipCount) BuildChips(Mathf.Max(0, required));
            for (int i = 0; i < chips.Length; i++)
            {
                var chip = chips[i]; if (chip == null) continue;
                int arrived = i < present.Length && present[i] ? 1 : 0;
                if (arrived == chip.Shown) continue; // restyle only on change (Display runs every frame)
                chip.Shown = arrived;
                var c = arrived == 1 ? ReadyColor : WaitColor;
                chip.Back.color = new Color(c.r * .16f, c.g * .16f, c.b * .16f, .94f);
                chip.Edge.effectColor = c; chip.Label.color = c; chip.State.color = Color.Lerp(c, Color.white, .35f);
                chip.State.text = arrived == 1 ? "พร้อม" : "รอ";
            }
        }
    }
}
