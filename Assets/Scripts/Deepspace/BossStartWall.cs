using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // 2026-11-18: Boss Slayer's wall while the players pick their cards on the floor, from the art team's "Start"
    // mockup: the codex hall, the Boss Slayer logo floating gently with a white sword-slash glint sweeping across it,
    // embers and shards rising from the bottom, and the two text bands (STAND ON GEMS / INFORMATION).
    // Art: Resources/BossSlayer/Start. BG is the hall with the art team's Alpha.png shade already blended in (as Photoshop
    // blends it; a separate shade layer reads too strong in this linear-colour project). บาส 2026-11-18: the shade covers the
    // lower part at its full strength (~45%) and fades in smoothly from 30% to 75% of the height from the top (no visible
    // split line), so the top stays clean. The older BossSlayerWallBackground is a
    // framed backdrop and is not used here.
    // Everything is laid out in fractions of the 8192x2160 mockup, so it fits whatever size the wall canvas has.
    public sealed class BossStartWall : MonoBehaviour
    {
        public const string PanelName = "Panel == Start Wall";
        const string Folder = "BossSlayer/Start/";
        const float DesignW = 8192f, DesignH = 2160f;

        // Logo: where the mockup puts it, how far it floats and how often.
        static readonly Rect LogoRect = new(2628, 500, 2578, 996);
        const float FloatHeight = 22f, FloatSeconds = 4f;

        // Glint: a white band slanted like a sword cut, sweeping left to right once every SlashEvery seconds.
        const float SlashEvery = 5f, SlashSeconds = 1.3f, SlashWidth = 900f, SlashAngle = -28f;

        RectTransform root, logo, shine;
        TMP_Text standText, infoText;
        BossRisingParticles particles;   // red squares and orange shards rising from the bottom (BossRisingParticles.cs)
        float clock, laidOutHeight = -1f;

        // Finds or builds the panel under the wall area; it starts hidden.
        public static BossStartWall Attach(RectTransform wall, TMP_FontAsset font)
        {
            if (wall == null) return null;
            var panel = wall.Find(PanelName) as RectTransform;
            if (panel == null)
            {
                panel = NewRect(wall, PanelName);
                panel.gameObject.SetActive(false);
            }
            if (!panel.TryGetComponent(out BossStartWall view)) view = panel.gameObject.AddComponent<BossStartWall>();
            view.Build(font);
            return view;
        }

        public void Show(bool show)
        {
            if (gameObject.activeSelf == show) return;
            gameObject.SetActive(show);
            if (!show) return;
            transform.SetAsLastSibling();
            clock = 0f;
            if (particles != null) particles.Scatter();
        }

        void Build(TMP_FontAsset font)
        {
            root = (RectTransform)transform;
            // บาส 2026-11-18: the wall text is in Jersey 15 like the floor cards; the given font is only the fallback.
            font = BossPlayerCards.Jersey() ?? font;
            Picture(root, "Background", Folder + "BG", new Rect(0, 0, DesignW, DesignH));
            Picture(root, "Band Top", Folder + "LineBG1", new Rect((DesignW - 6316) / 2, 1580, 6316, 306));
            Picture(root, "Band Bottom", Folder + "LineBG2", new Rect((DesignW - 4348) / 2, 1945, 4348, 169));
            standText = Label(root, "Stand On Gems", "STAND ON GEMS", font, new Rect(1500, 1668, DesignW - 3000, 140));
            infoText = Label(root, "Information", "INFORMATION", font, new Rect(1500, 1990, DesignW - 3000, 100));

            particles = BossRisingParticles.Attach(root);

            logo = Picture(root, "Logo", Folder + "Logo", LogoRect).rectTransform;
            if (!logo.TryGetComponent(out Mask mask)) mask = logo.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var glint = Picture(logo, "Shine", null, new Rect(0, 0, 0, 0));
            glint.texture = ShineTexture();
            shine = glint.rectTransform;
            shine.anchorMin = shine.anchorMax = new Vector2(0f, .5f);
            shine.localEulerAngles = new Vector3(0, 0, SlashAngle);
            glint.enabled = false;
            laidOutHeight = -1f;
        }

        void Update()
        {
            // Not built yet (the wall is built by its owner, which may come after this first Update).
            if (root == null) return;
            float dt = Time.unscaledDeltaTime;
            clock += dt;
            if (!Mathf.Approximately(root.rect.height, laidOutHeight)) Layout();
            FloatLogo();
            SweepShine();
        }

        // Font sizes follow the wall's real height (the mockup's text is ~100 and ~64 px tall at 2160).
        void Layout()
        {
            laidOutHeight = root.rect.height;
            float unit = laidOutHeight / DesignH;
            if (standText != null) { standText.fontSize = 92f * unit; standText.characterSpacing = 4f; }
            if (infoText != null) { infoText.fontSize = 60f * unit; infoText.characterSpacing = 85f; }
            if (shine != null) shine.sizeDelta = new Vector2(SlashWidth * unit, LogoRect.height * 2.6f * unit);
        }

        void FloatLogo()
        {
            // Smooth up-and-down drift, eased at both ends.
            float lift = FloatHeight * (.5f - .5f * Mathf.Cos(clock * Mathf.PI * 2f / FloatSeconds));
            Place(logo, new Rect(LogoRect.x, LogoRect.y - lift, LogoRect.width, LogoRect.height));
        }

        void SweepShine()
        {
            var image = shine.GetComponent<RawImage>();
            float k = ((clock - 1f) % SlashEvery) / SlashSeconds;
            bool on = clock >= 1f && k <= 1f;
            image.enabled = on;
            if (!on) return;
            float eased = k * k * (3f - 2f * k);
            float width = logo.rect.width, reach = shine.rect.width * 1.6f;
            shine.anchoredPosition = new Vector2(Mathf.Lerp(-reach, width + reach, eased), 0f);
            image.color = new Color(1f, 1f, 1f, Mathf.Sin(k * Mathf.PI) * .95f);
        }

        static Texture2D shineTexture;

        static Texture2D ShineTexture()
        {
            if (shineTexture != null) return shineTexture;
            const int size = 128;
            shineTexture = new Texture2D(size, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Boss Start Shine" };
            for (int x = 0; x < size; x++)
            {
                float d = (x - (size - 1) / 2f) / (size / 2f);
                float a = Mathf.Exp(-d * d / .02f) + .55f * Mathf.Exp(-d * d / .18f);
                shineTexture.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
            shineTexture.Apply();
            return shineTexture;
        }

        RawImage Picture(RectTransform parent, string name, string texture, Rect design)
        {
            var rect = parent.Find(name) as RectTransform ?? NewRect(parent, name);
            if (!rect.TryGetComponent(out RawImage image)) image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            if (texture != null) image.texture = Resources.Load<Texture2D>(texture);
            if (parent == root) Place(rect, design);
            return image;
        }

        TMP_Text Label(RectTransform parent, string name, string text, TMP_FontAsset font, Rect design)
        {
            var rect = parent.Find(name) as RectTransform ?? NewRect(parent, name);
            if (!rect.TryGetComponent(out TextMeshProUGUI label)) label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) { label.font = font; label.fontSharedMaterial = font.material; }
            label.fontStyle = FontStyles.Normal;
            label.text = text;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            Place(rect, design);
            return label;
        }

        // design is in mockup pixels measured from the top-left corner.
        static void Place(RectTransform rect, Rect design)
        {
            rect.anchorMin = new Vector2(design.x / DesignW, 1f - (design.y + design.height) / DesignH);
            rect.anchorMax = new Vector2((design.x + design.width) / DesignW, 1f - design.y / DesignH);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
