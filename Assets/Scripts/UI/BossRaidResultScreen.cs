using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-26 "Result เอาไปทำหน้าจอเกมใหม่หน่อยลบ UI อันเก่าทิ้งเลย ... ตัว EX2 จะขึ้นมาก่อนค่อยเป็น result ex": the wall's
    // end-of-session result follows the art team's mockups (Downloads/Result), replacing the old MVP plate + player plates
    // and the rush's codex result on the wall. No backdrop of its own: the arena stays behind, a little dimmed, with the
    // gold edge light (EdgeLight) over it.
    // 1. RAID COMPLETE (Result EX2): gold glow band, the gold rings (RingCycle), the pixel title (RaidCompleteText) and the
    //    red-gem rule under it (GemLine), gold dots and squares drifting up.
    // 2. BOSS DEFEATED (Result EX): the dark gold-framed panel (Panel) with BOSS DEFEATED, TEAM DAMAGE and the team total
    //    counting up, then one row per player (PlayerRow + Point), ranked by damage, the top one in gold.
    // A rush that ended with the king still alive skips step 1 and titles the panel RAID OVER.
    // Art: Art/UI/Result, drawn for an 8256x2048 mockup and laid out here in the 8192x2160 wall's pixels at its own size.
    // 2026-11-26 "เอา UI หน้านี้ไว้บนสุด แล้วก็ตอนขึ้น raid complete ใส่พื้นหลัง Alpha สีดำให้เหมือนรูปต้นฉบับ": the screen has
    // its own overlay canvas on the wall's display (sorting 250, above the juice, banners, ultimate titles and the king's
    // intro), and RAID COMPLETE plays over a black alpha layer (IntroBlackAlpha) like Result EX2.
    // "ทำไมเลขซ้อนกัน": on its own canvas the screen was built before that canvas had its size, so every text came out
    // too big; text sizes now follow the screen's height every frame, and the screen keeps the wall's shape
    // (AspectRatioFitter) so the art never stretches.
    // "ตัว UI คะแนนต้องอยู่ด้านหน้า Alpha ด้วย อยู่หน้าทุกอย่าง มี animation ลอยขึ้นลง ... แก้สีของ Alpha จุดๆ เป็นสีม่วง":
    // the black alpha stays up behind the score panel too (it no longer goes with RAID COMPLETE), the panel is the
    // last thing drawn and floats gently up and down. (A purple-grain panel was tried and dropped for the art team's own.)
    // "particle ที่ลอยขึ้นมาต้องอยู่หน้าสุด": the drifting dots are drawn in front of the glow, rings and title.
    // (A Linear-colour-space correction of the glows and the black was tried and dropped: "แบบก่อนล่าสุดยังดีกว่า".)
    // "ตัวสีเหลืองๆ ลดความกว้างและความฟุ้งของแต่ละอันลง มี 2 อัน ตรงกลางกับขอบบนล่าง": the middle band keeps its full size
    // (GlowScale; it was tried smaller), and the top/bottom light uses EdgeLightTight (the same light reaching a third as far in).
    public sealed class BossRaidResultScreen : MonoBehaviour
    {
        const string Folder = "Art/UI/Result/";
        const float DesignW = 8192f, DesignH = 2160f, CenterX = DesignW / 2f;
        static readonly Color Gold = new Color32(232, 160, 56, 255);
        static readonly Color Ink = Color.white;

        // RAID COMPLETE layout (wall pixels from the top-left, measured on Result EX2).
        const float GlowY = 970f, RingX = 4067f, RingY = 1026f, TitleX = 4184f, TitleY = 983f, GemLineX = 4136f, GemLineY = 1245f;
        // Panel layout (measured on Result EX, relative to the panel's top edge and centre line).
        const float PanelY = 1131f, PanelTopFromCenter = 865f;
        const float HeadingY = 176f, HeadingCap = 115f, TeamLabelY = 313f, TeamLabelCap = 58f, TeamTotalY = 440f, TeamTotalCap = 132f;
        const float FirstRowY = 654f, RowStep = 183f, RowCap = 58f, PointX = -922f, NameLeft = -670f, ScoreRight = 709f;
        // Jersey 15's capitals are about this much of its font size.
        const float CapPerEm = .62f;

        // Timeline (seconds). The panel starts at PanelIn (0 when there's no RAID COMPLETE intro).
        const float IntroSeconds = 3.6f, IntroOutSeconds = .45f, FadeSeconds = .4f, TitleDelay = .2f;
        const float PopGrow = .28f, PopSettle = .2f, TitleOvershoot = 1.15f;
        const float RowFirstDelay = .55f, RowStagger = .18f, RowSlideSeconds = .4f, RowSlide = 260f, CountSeconds = 1.2f;
        const float GlowAlpha = .4f, EdgeLightAlpha = .35f, DimAlpha = .3f, IntroBlackAlpha = .9f;   // 2026-11-26 "มืด 90% โชว์ข้างหลังแค่ 10%"
        const int SortingOrder = 250;
        // The middle gold band's size against its picture: width, height (its soft edge shrinks with it).
        static readonly Vector2 GlowScale = new Vector2(1f, 1f);   // "ขยายตรงกลางเท่าเดิม": back to full size
        // The score panel's float: how far up and down (wall pixels) and how fast (radians per second).
        const float FloatPixels = 22f, FloatSpeed = 1.6f;

        const int MoteCount = 36;

        public readonly struct Row
        {
            public readonly string Name; public readonly int Damage;
            public Row(string name, int damage) { Name = name; Damage = damage; }
        }

        struct Mote { public RectTransform Rect; public RawImage Image; public float X, Y, Speed, Size, Sway, Phase, Bright; }
        struct RowView { public RectTransform Root; public CanvasGroup Group; public TMP_Text Score; public int Damage; }

        RectTransform root, intro, panel, moteLayer;
        CanvasGroup group, introGroup, panelGroup;
        Image dim;
        RawImage edgeLight, glow, ring, title, gemLine;
        TMP_Text heading, teamLabel, teamTotal;
        TMP_FontAsset font;
        Mote[] motes;
        readonly List<RowView> rows = new List<RowView>();
        // every label with its font size in wall pixels, sized to the screen in Sample
        readonly List<(TMP_Text text, float size)> labels = new List<(TMP_Text, float)>();
        float age, panelIn; int teamDamage; bool playing;

        // wall: any rect on the wall's canvas (its display is used); owner: where the overlay canvas is kept.
        public static BossRaidResultScreen Create(RectTransform wall, Transform owner, TMP_FontAsset fallbackFont)
        {
            var source = wall != null ? wall.GetComponentInParent<Canvas>()?.rootCanvas : null;
            var go = new GameObject("Canvas == Raid Result", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(owner, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (source != null) canvas.targetDisplay = source.targetDisplay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var rect = Area(go.transform, "Panel == Raid Result");
            var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = DesignW / DesignH;
            var screen = rect.gameObject.AddComponent<BossRaidResultScreen>();
            screen.Build(rect, BossPlayerCards.Jersey() ?? fallbackFont);
            rect.gameObject.SetActive(false);
            return screen;
        }

        void Build(RectTransform rect, TMP_FontAsset textFont)
        {
            root = rect; font = textFont;
            group = rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;

            dim = Area(root, "Dim").gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, DimAlpha); dim.raycastTarget = false;
            var black = Area(root, "Black Alpha").gameObject.AddComponent<Image>();
            black.color = new Color(0f, 0f, 0f, IntroBlackAlpha); black.raycastTarget = false;
            edgeLight = Picture(root, "Edge Light", "EdgeLightTight", CenterX, DesignH / 2f);
            Stretch(edgeLight.rectTransform);

            intro = Area(root, "Raid Complete");
            introGroup = intro.gameObject.AddComponent<CanvasGroup>();
            glow = Picture(intro, "Gold Glow", "RaidGlow", CenterX, GlowY);
            glow.rectTransform.localScale = new Vector3(GlowScale.x, GlowScale.y, 1f);
            moteLayer = Area(intro, "Particles");
            ring = Picture(intro, "Rings", "RingCycle", RingX, RingY);
            gemLine = Picture(intro, "Gem Line", "GemLine", GemLineX, GemLineY);
            title = Picture(intro, "Title", "RaidCompleteText", TitleX, TitleY);
            BuildMotes();
            moteLayer.SetAsLastSibling();   // the drifting dots go in front of the glow, rings and title

            // a full-wall layer holding the panel picture and everything on it (all laid out in wall pixels)
            panel = Area(root, "Result Panel");
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            Picture(panel, "Panel Back", "Panel", CenterX, PanelY);   // "ต้องตามรูปที่ให้ไป": the art team's panel as drawn
            float top = PanelY - PanelTopFromCenter;
            heading = Label(panel, "Heading", CenterX - 1200f, top + HeadingY, 2400f, HeadingCap, TextAlignmentOptions.Center);
            teamLabel = Label(panel, "Team Damage Label", CenterX - 1200f, top + TeamLabelY, 2400f, TeamLabelCap, TextAlignmentOptions.Center);
            teamLabel.text = "TEAM DAMAGE";
            teamTotal = Label(panel, "Team Damage", CenterX - 1200f, top + TeamTotalY, 2400f, TeamTotalCap, TextAlignmentOptions.Center);
        }

        // Shows the result: with raidComplete the RAID COMPLETE screen plays first, then the panel; otherwise the panel
        // comes straight in titled RAID OVER. Rows are expected best first; the first one is drawn in gold.
        public void Show(bool raidComplete, IReadOnlyList<Row> ranked)
        {
            ClearRows();
            teamDamage = 0;
            for (int i = 0; i < ranked.Count; i++) { teamDamage += ranked[i].Damage; AddRow(i, ranked[i]); }
            heading.text = raidComplete ? "BOSS DEFEATED" : "RAID OVER";
            intro.gameObject.SetActive(raidComplete);
            panelIn = raidComplete ? IntroSeconds : 0f;
            for (int i = 0; i < motes.Length; i++) Respawn(ref motes[i], Random.value);
            age = 0f; playing = true;
            gameObject.SetActive(true); root.SetAsLastSibling();
            Sample();
        }

        public void Hide()
        {
            playing = false;
            if (this != null) gameObject.SetActive(false);
        }

        void Update()
        {
            if (!playing) return;
            float dt = Time.unscaledDeltaTime;
            age += dt;
            StepMotes(dt);
            Sample();
        }

        void Sample()
        {
            float unit = Unit;
            for (int i = 0; i < labels.Count; i++)
                if (labels[i].text != null) labels[i].text.fontSize = labels[i].size * unit;
            group.alpha = Ease(age / FadeSeconds);
            dim.color = new Color(0f, 0f, 0f, DimAlpha);
            edgeLight.color = new Color(1f, 1f, 1f, EdgeLightAlpha);   // 2026-11-26 "แสงสีทองลด Alpha ลงให้เหมือนรูปตัวอย่าง" (was 1, glow was .7)

            if (intro.gameObject.activeSelf)
            {
                introGroup.alpha = 1f - Ease((age - (panelIn - IntroOutSeconds)) / IntroOutSeconds);
                glow.color = new Color(1f, 1f, 1f, GlowAlpha * Ease((age - TitleDelay) / .6f));
                ring.color = new Color(1f, 1f, 1f, Ease((age - TitleDelay) / .5f));
                ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, Ease((age - TitleDelay) / .8f));
                float l = Mathf.Clamp01((age - TitleDelay - .05f) / .45f);
                gemLine.rectTransform.localScale = new Vector3(1f - (1f - l) * (1f - l) * (1f - l), 1f, 1f);
                gemLine.color = new Color(1f, 1f, 1f, Ease((age - TitleDelay) / .35f));
                title.rectTransform.localScale = Vector3.one * Pop(age - TitleDelay, TitleOvershoot);
                if (introGroup.alpha <= 0f) intro.gameObject.SetActive(false);
            }

            float t = age - panelIn;
            panelGroup.alpha = Ease(t / FadeSeconds);
            panel.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, Ease(t / .35f));
            panel.anchoredPosition = new Vector2(0f, Mathf.Sin(Mathf.Max(0f, t) * FloatSpeed) * FloatPixels * Unit);
            heading.rectTransform.localScale = Vector3.one * Pop(t - .1f, 1.1f);
            float count = Ease((t - .3f) / CountSeconds);
            teamTotal.text = Mathf.RoundToInt(teamDamage * count).ToString();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                float rt = t - RowFirstDelay - i * RowStagger;
                float slide = Ease(rt / RowSlideSeconds);
                row.Group.alpha = slide;
                row.Root.anchoredPosition = new Vector2(-RowSlide * (1f - slide) * Unit, 0f);
                row.Score.text = Mathf.RoundToInt(row.Damage * Ease((rt - .2f) / CountSeconds)).ToString();
            }
        }

        // ---------------------------------------------------------------- rows
        void AddRow(int rank, Row data)
        {
            float panelTop = PanelY - PanelTopFromCenter, y = panelTop + FirstRowY + rank * RowStep;
            var rowRoot = Area(panel, "Row " + (rank + 1));
            var rowGroup = rowRoot.gameObject.AddComponent<CanvasGroup>();
            Picture(rowRoot, "Row Back", "PlayerRow", CenterX, y);
            Picture(rowRoot, "Point", "Point", CenterX + PointX, y);
            bool top = rank == 0;
            var name = Label(rowRoot, "Name", CenterX + NameLeft, y, 1300f, RowCap, TextAlignmentOptions.Left);
            name.text = data.Name.ToUpperInvariant(); name.color = top ? Gold : Ink;
            var score = Label(rowRoot, "Score", CenterX + ScoreRight - 900f, y, 900f, RowCap, TextAlignmentOptions.Right);
            score.color = top ? Gold : Ink; score.text = "0";
            rows.Add(new RowView { Root = rowRoot, Group = rowGroup, Score = score, Damage = data.Damage });
        }

        void ClearRows()
        {
            foreach (var row in rows) if (row.Root != null) Destroy(row.Root.gameObject);
            rows.Clear();
            labels.RemoveAll(l => l.text == null || l.text.transform.IsChildOf(panel) && l.text.transform.parent != panel);
        }

        // ---------------------------------------------------------------- particles (gold dots and squares drifting up)
        void BuildMotes()
        {
            var dot = GameArt.Load<Texture2D>(Folder + "ParticleDot");
            var square = GameArt.Load<Texture2D>(Folder + "ParticleSquare");
            motes = new Mote[MoteCount];
            for (int i = 0; i < MoteCount; i++)
            {
                var rect = Area(moteLayer, "Particle " + i);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                var image = rect.gameObject.AddComponent<RawImage>();
                image.texture = i % 3 == 0 ? square : dot; image.raycastTarget = false;
                motes[i] = new Mote { Rect = rect, Image = image };
            }
        }

        static void Respawn(ref Mote m, float progress)
        {
            // mostly around the middle third of the wall, like the mockup
            float spread = Random.value * Random.value * Mathf.Sign(Random.value - .5f);
            m.X = Mathf.Clamp01(.5f + spread * .32f);
            m.Speed = Random.Range(60f, 160f);
            m.Size = Mathf.Lerp(90f, 330f, Random.value * Random.value);
            m.Sway = Random.Range(10f, 40f);
            m.Phase = Random.Range(0f, Mathf.PI * 2f);
            m.Bright = Random.Range(.6f, 1f);
            m.Y = -m.Size + progress * (DesignH + m.Size); // wall pixels from the bottom
        }

        void StepMotes(float dt)
        {
            if (motes == null || !intro.gameObject.activeSelf) return;
            var area = root.rect;
            for (int i = 0; i < motes.Length; i++)
            {
                ref var m = ref motes[i];
                m.Y += m.Speed * dt;
                if (m.Y > DesignH) Respawn(ref m, 0f);
                float u = Mathf.Clamp01((m.Y + m.Size) / (DesignH + m.Size));
                float twinkle = .75f + .25f * Mathf.Sin(age * 2.3f + m.Phase);
                m.Rect.anchoredPosition = new Vector2(m.X * area.width + m.Sway * Mathf.Sin(age * 1.1f + m.Phase) * Unit, m.Y * Unit);
                m.Rect.sizeDelta = Vector2.one * m.Size * Unit;
                m.Image.color = new Color(1f, 1f, 1f, Mathf.Min(1f, u / .1f) * Mathf.Min(1f, (1f - u) / .2f) * m.Bright * twinkle);
            }
        }

        // ---------------------------------------------------------------- building blocks
        float Unit => root.rect.height / DesignH;

        static float Ease(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        // 0 before it starts, grows past full size, then settles at 1.
        static float Pop(float t, float overshoot)
        {
            if (t <= 0f) return 0f;
            if (t < PopGrow) { float u = 1f - t / PopGrow; return Mathf.Lerp(0f, overshoot, 1f - u * u); }
            return Mathf.Lerp(overshoot, 1f, Ease((t - PopGrow) / PopSettle));
        }

        static RectTransform Area(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // A picture at its own pixel size centred on (centerX, centerY) in wall pixels from the top-left. Anchored as
        // fractions of the wall (parent must span the whole wall), so it scales with the wall canvas.
        static RawImage Picture(RectTransform parent, string name, string art, float centerX, float centerY)
        {
            var texture = GameArt.Load<Texture2D>(Folder + art);
            if (texture == null) Debug.LogWarning("[Raid result] Missing " + Folder + art + ".png");
            float w = texture != null ? texture.width : 100f, h = texture != null ? texture.height : 100f;
            var rect = Area(parent, name);
            SetBox(rect, new Rect(centerX - w / 2f, centerY - h / 2f, w, h));
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
            return image;
        }

        // A line of pixel text whose capitals are `cap` wall pixels tall, centred on centreY (wall pixels from the top).
        TMP_Text Label(RectTransform parent, string name, float left, float centreY, float width, float cap, TextAlignmentOptions align)
        {
            float size = cap / CapPerEm;
            var rect = Area(parent, name);
            SetBox(rect, new Rect(left, centreY - size * .75f, width, size * 1.5f));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) { text.font = font; text.fontSharedMaterial = font.material; }
            text.color = Ink; text.raycastTarget = false;
            text.alignment = align == TextAlignmentOptions.Left ? TextAlignmentOptions.MidlineLeft
                : align == TextAlignmentOptions.Right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.Midline;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = false; text.fontSize = size * Unit; text.characterSpacing = 2f;
            labels.Add((text, size));
            return text;
        }

        // box in wall pixels from the top-left, set as anchors (fractions of the wall).
        static void SetBox(RectTransform rect, Rect box)
        {
            rect.anchorMin = new Vector2(box.xMin / DesignW, 1f - box.yMax / DesignH);
            rect.anchorMax = new Vector2(box.xMax / DesignW, 1f - box.yMin / DesignH);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
