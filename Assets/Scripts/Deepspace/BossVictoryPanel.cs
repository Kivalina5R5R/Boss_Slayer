using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Deepspace
{
    // 2026-11-24 "เปลี่ยนหน้า victory เป็นแบบรูปตัวอย่าง ใช้กับทุกบอส พื้นหลังเอาฉากเดิมที่ตีบอสตัวนั้นอยู่": the wall's
    // VICTORY screen follows the art team's mockup (Downloads/Victory): no backdrop of its own, so the arena the boss was
    // fought in stays behind it; a soft gold glow band across the middle (BgLight), the gold ring with its rays (Ring)
    // in the centre, the pixel VICTORY title (Title) between two gold rules with red gems (Line), a dark band with a
    // red gem under it (BgLine) carrying "ROUND 1/3 CLEARED", "BOSS SELECT IN 5S" in the top-right corner, and gold
    // squares (Particle) drifting up. Pixel lettering in Jersey 15, like the rest of the HUD.
    // The old card (boss emblem medallion, "BOSS DEFEATED", boss name, voxel celebration, near-black backdrop) is gone.
    // Art: Resources/BossSlayer/Victory, drawn 1:1 for the 8192x2160 wall: laid out in those pixels and scaled to the
    // wall canvas (imported at full size: Non Power of 2 = None, Max Size 8192).
    // Intro: the screen fades in with the ring still in place, the rules slide out from the middle and
    // VICTORY pops in (past its size, then back). A boss that escaped (rush) shows "BOSS ESCAPED" in place of VICTORY.
    public sealed class BossVictoryPanel : MonoBehaviour
    {
        const string Folder = "BossSlayer/Victory/";
        const float DesignW = 8192f, DesignH = 2160f;
        public const float Duration = 6f;

        // Layout (wall pixels, measured on the mockup): everything centred on x = 4096.
        const float CenterX = DesignW / 2f, RingCenterY = 1080f, TitleCenterY = 1090f, LineCenterY = 1075f, BandTop = 1415f;
        // 2026-11-24 "เพิ่มขนาดตัวหนังสือ ตัวตรงกลางจัดให้อยู่ตรงกลาง": the round line is taller (125 -> 200) and centred on
        // the band's own middle (BandTop + 414/2), not just under its gem; the corner countdown is taller too (115 -> 170).
        // 2026-11-24 "ขยับตัวหนังสือลงมาอีกนิด มันชนกับขอบบน": RoundDrop moves it that many wall pixels below the band's
        // middle, clear of the gold rule and gem along the band's top edge.
        const float BandHeight = 414f, RoundHeight = 200f, CountdownHeight = 170f, RoundDrop = 55f;
        static readonly Rect RoundBox = new(CenterX - 1800f, BandTop + BandHeight / 2f - RoundHeight / 2f + RoundDrop, 3600f, RoundHeight);
        static readonly Rect CountdownBox = new(DesignW - 230f - 3000f, 140f, 3000f, CountdownHeight); // right-aligned, top-right corner
        static readonly Color Ink = Color.white;

        // Intro timing (seconds).
        const float TitleDelay = .25f, TitleGrowSeconds = .28f, TitleSettleSeconds = .2f;
        // The other parts come in with VICTORY (from TitleDelay), staggered: glow + ring + rules with it, then the band,
        // the round line (with a small pop) and the corner countdown. PartInSeconds = how long each fade takes.
        const float PartInSeconds = .35f, GlowInDelay = TitleDelay, GlowInSeconds = .6f, RingInDelay = TitleDelay;
        const float LineDelay = TitleDelay + .05f, LineSeconds = .45f;
        const float BandInDelay = TitleDelay + .25f, RoundInDelay = TitleDelay + .4f, CountdownInDelay = TitleDelay + .55f, RoundOvershoot = 1.12f;
        const float TitleOvershoot = 1.15f, GlowBreatheSeconds = 2.4f;
        // The gold glow band: how strong it is at most (1 = the art as drawn), and how much of its height fades out at
        // the top and at the bottom.
        const float GlowAlpha = .5f, GlowBreatheDepth = .12f, GlowEdgeFade = .45f;

        // Gold squares drifting up the lower half (wall pixels; the picture is 199x230 with a soft glow round the square).
        const int ParticleCount = 30;
        const float ParticleSizeMin = 70f, ParticleSizeMax = 160f, ParticleRiseMin = 160f, ParticleRiseMax = 340f, ParticleTop = 1350f;

        public BossPassiveState.Kind BossKind { get; private set; }
        public string BossName { get; private set; }
        // Kept for BossSlayerGameManager.ThemeBossVictory; the new look uses the art's own gold for every boss.
        public float ThemeStrength; public bool ThemePhase2;

        CanvasGroup group;
        RawImage glow, ring, line, title, band;
        TMP_Text escapedTitle, progress, countdown;
        RectTransform particleLayer;
        struct Mote { public RectTransform Rect; public RawImage Image; public float X, Y, Speed, Size, Sway, Phase, Bright; }
        Mote[] motes;
        float age; bool next, escaped;

        public static BossVictoryPanel Create(RectTransform parent, TMP_FontAsset font, bool floor)
        {
            var root = Area(parent, "Panel == Boss Victory");
            var panel = root.gameObject.AddComponent<BossVictoryPanel>();
            panel.group = root.gameObject.AddComponent<CanvasGroup>();
            panel.group.blocksRaycasts = false; panel.group.interactable = false;
            panel.Build(root, BossPlayerCards.Jersey() ?? font);
            root.gameObject.SetActive(false);
            return panel;
        }

        void Build(RectTransform root, TMP_FontAsset font)
        {
            glow = Picture(root, "Gold Glow", "BgLight", CenterX, RingCenterY);
            // 2026-11-24 "ทำ Bg Light ให้จางๆ เหมือน Red alpha": fainter (GlowAlpha) and melting away at its top and bottom
            // edges (BossVerticalFade, the same soft curve as the fire wall's Red Alpha), so it reads as a haze, not a band.
            glow.gameObject.AddComponent<BossVerticalFade>().SetFade(GlowEdgeFade, GlowEdgeFade);
            particleLayer = Area(root, "Victory Particles");
            ring = Picture(root, "Victory Ring", "Ring", CenterX, RingCenterY);
            band = Picture(root, "Round Band", "BgLine", CenterX, 0f, BandTop);
            line = Picture(root, "Victory Rules", "Line", CenterX, LineCenterY);
            title = Picture(root, "Victory Title", "Title", CenterX, TitleCenterY);

            escapedTitle = Label(root, "Escaped Title", font, new Rect(CenterX - 1500f, TitleCenterY - 230f, 3000f, 460f), TextAlignmentOptions.Center);
            escapedTitle.text = "BOSS ESCAPED"; escapedTitle.color = new Color32(255, 214, 84, 255);
            progress = Label(root, "Session Progress", font, RoundBox, TextAlignmentOptions.Center);
            countdown = Label(root, "Next Screen Countdown", font, CountdownBox, TextAlignmentOptions.Right);

            var square = Resources.Load<Texture2D>(Folder + "Particle");
            motes = new Mote[ParticleCount];
            for (int i = 0; i < ParticleCount; i++)
            {
                var rect = Area(particleLayer, "Victory Particle " + i);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                var image = rect.gameObject.AddComponent<RawImage>();
                image.texture = square; image.raycastTarget = false;
                motes[i] = new Mote { Rect = rect, Image = image };
            }
        }

        public void Show(string modelName, int round, int total, bool hasNext, Color accent, bool escaped = false)
        {
            BossKind = BossPassiveState.Identify(modelName);
            BossName = BossNextBossCard.NameFor(BossKind);
            next = hasNext; this.escaped = escaped;
            title.gameObject.SetActive(!escaped);
            escapedTitle.gameObject.SetActive(escaped);
            // total 0 = the rush's Abyssal King
            progress.text = total <= 0 ? "FINAL BOSS CLEARED" : "ROUND " + Mathf.Max(1, round) + "/" + total + (escaped ? " ESCAPED" : " CLEARED");
            for (int i = 0; i < motes.Length; i++) Respawn(ref motes[i], Random.value);
            age = 0; gameObject.SetActive(true); transform.SetAsLastSibling(); Sample(0);
        }

        public void Sample(float elapsed)
        {
            float dt = Mathf.Max(0f, elapsed - age);
            age = elapsed;
            group.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .35f)) * Mathf.Clamp01((Duration - age) / .3f);
            countdown.text = (next ? "BOSS SELECT IN " : "RESULTS IN ") + Mathf.CeilToInt(Mathf.Max(0, Duration - age)) + "S";

            // 2026-11-24 "UI Victory ตัวอื่นๆ pop up มาพร้อมกับคำว่า Victory ด้วย Fade": the other parts are not there from
            // the start any more; they come in with VICTORY (TitleDelay), one after another.
            // the glow fades in with VICTORY, then breathes a little
            float breathe = 1f - GlowBreatheDepth * (.5f + .5f * Mathf.Sin(age * Mathf.PI * 2f / GlowBreatheSeconds));
            glow.color = new Color(1f, 1f, 1f, GlowAlpha * breathe * FadeIn(GlowInDelay, GlowInSeconds));
            // "Victory pic ไม่ต้องหมุน อยู่ที่เดิม": the ring stays still at its own size and place; it only fades in
            ring.rectTransform.localScale = Vector3.one;
            ring.rectTransform.localEulerAngles = Vector3.zero;
            ring.color = new Color(1f, 1f, 1f, FadeIn(RingInDelay, PartInSeconds));
            // the rules fade in as they slide out from the middle
            float l = Mathf.Clamp01((age - LineDelay) / LineSeconds);
            line.rectTransform.localScale = new Vector3(1f - (1f - l) * (1f - l) * (1f - l), 1f, 1f);
            line.color = new Color(1f, 1f, 1f, FadeIn(LineDelay, PartInSeconds));
            // the band fades in, then its round line fades in with a small pop, then the corner countdown fades in
            band.color = new Color(1f, 1f, 1f, FadeIn(BandInDelay, PartInSeconds));
            progress.alpha = FadeIn(RoundInDelay, PartInSeconds);
            progress.rectTransform.localScale = Vector3.one * Pop(age - RoundInDelay, RoundOvershoot);
            countdown.alpha = FadeIn(CountdownInDelay, PartInSeconds);
            // VICTORY pops in: grows past its size, then settles
            var popped = escaped ? escapedTitle.rectTransform : title.rectTransform;
            popped.localScale = Vector3.one * Pop(age - TitleDelay, TitleOvershoot);

            StepMotes(dt);
        }

        // 0 before `delay`, then eases up to 1 over `seconds`.
        float FadeIn(float delay, float seconds) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - delay) / Mathf.Max(.01f, seconds)));

        // Pop-in scale t seconds after it starts: 0 -> overshoot (TitleGrowSeconds) -> 1 (TitleSettleSeconds).
        static float Pop(float t, float overshoot)
        {
            if (t <= 0f) return 0f;
            if (t < TitleGrowSeconds) { float u = 1f - t / TitleGrowSeconds; return Mathf.Lerp(0f, overshoot, 1f - u * u); }
            return Mathf.Lerp(overshoot, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - TitleGrowSeconds) / TitleSettleSeconds)));
        }

        public void Hide() { gameObject.SetActive(false); }

        // ---------------------------------------------------------------- particles
        static void Respawn(ref Mote m, float progress)
        {
            m.X = Random.Range(.03f, .97f);
            m.Speed = Random.Range(ParticleRiseMin, ParticleRiseMax);
            m.Size = Mathf.Lerp(ParticleSizeMin, ParticleSizeMax, Random.value * Random.value);
            m.Sway = Random.Range(10f, 40f);
            m.Phase = Random.Range(0f, Mathf.PI * 2f);
            m.Bright = Random.Range(.55f, 1f);
            m.Y = -m.Size + progress * (ParticleTop + m.Size);   // wall pixels from the bottom
        }

        void StepMotes(float dt)
        {
            if (motes == null) return;
            var area = ((RectTransform)transform).rect;
            float unit = area.height / DesignH;
            for (int i = 0; i < motes.Length; i++)
            {
                ref var m = ref motes[i];
                m.Y += m.Speed * dt;
                if (m.Y > ParticleTop) Respawn(ref m, 0f);
                float u = Mathf.Clamp01((m.Y + m.Size) / (ParticleTop + m.Size));
                float x = m.X * area.width + m.Sway * Mathf.Sin(age * 1.3f + m.Phase) * unit;
                m.Rect.anchoredPosition = new Vector2(x, m.Y * unit);
                m.Rect.sizeDelta = new Vector2(m.Size * 199f / 230f, m.Size) * unit;
                m.Image.color = new Color(1f, 1f, 1f, Mathf.Min(1f, u / .15f) * (1f - u) * m.Bright);
            }
        }

        // ---------------------------------------------------------------- building blocks
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

        // A picture at its own pixel size, centred on (centerX, centerY) in wall pixels from the top-left; with top >= 0
        // it hangs from that top edge instead. Anchored as fractions of the wall, so it scales with the wall canvas.
        static RawImage Picture(RectTransform root, string name, string art, float centerX, float centerY, float top = -1f)
        {
            var texture = Resources.Load<Texture2D>(Folder + art);
            float w = texture != null ? texture.width : 100f, h = texture != null ? texture.height : 100f;
            if (texture == null) Debug.LogWarning($"[Boss victory] Missing Resources/{Folder}{art}.png");
            float y0 = top >= 0f ? top : centerY - h / 2f;
            var rect = Area(root, name);
            rect.anchorMin = new Vector2((centerX - w / 2f) / DesignW, 1f - (y0 + h) / DesignH);
            rect.anchorMax = new Vector2((centerX + w / 2f) / DesignW, 1f - y0 / DesignH);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
            return image;
        }

        // Pixel-font line sized to its box (box in wall pixels from the top-left).
        static TMP_Text Label(RectTransform root, string name, TMP_FontAsset font, Rect box, TextAlignmentOptions align)
        {
            var rect = Area(root, name);
            rect.anchorMin = new Vector2(box.xMin / DesignW, 1f - box.yMax / DesignH);
            rect.anchorMax = new Vector2(box.xMax / DesignW, 1f - box.yMin / DesignH);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) { text.font = font; text.fontSharedMaterial = font.material; }
            text.color = Ink; text.alignment = align; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true; text.fontSizeMin = 8f; text.fontSizeMax = 1000f;
            text.characterSpacing = 2f;
            return text;
        }
    }
}
