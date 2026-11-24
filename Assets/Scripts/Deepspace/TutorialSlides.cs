using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // บาส 2026-10-29: the "how to play" tutorial — four themed slides over the whole wall (targetDisplay 1), started from
    // the operator screen's TUTORIAL button. Pages turn by themselves every PageSeconds; the operator can skip. It closes
    // itself when a session starts (players stepped on START meanwhile), so it never covers real play.
    // 2026-10-30 ("ทำเป็น Obj. จริง"): the slides are baked into each scene as a real, hidden object (Tools/Deepspace/
    // Bake Operator Display); TUTORIAL shows it and closing hides it again. A scene without the baked object still gets
    // one built at runtime and destroyed on close, as before.
    public sealed class TutorialSlides : MonoBehaviour
    {
        public const float PageSeconds = 6f;
        const float Fade = .35f;

        GameShowcase showcase;
        [SerializeField] bool baked;
        [SerializeField] Canvas canvas;
        [SerializeField] CanvasGroup group;
        [SerializeField] RawImage picture;
        [SerializeField] AspectRatioFitter pictureFit;
        [SerializeField] TMP_Text kicker, heading, headingThai, body, pageLabel;
        [SerializeField] Image progress;
        [SerializeField] Image[] dots;
        int page;
        float pageAge, alpha;
        bool closing;

        public static TutorialSlides Active { get; private set; }
        public int Page => page;
        public int PageCount => showcase.pages.Length;
        public event Action Closed;

        public static TutorialSlides Open(GameShowcase showcase)
        {
            if (Active != null) return Active;
            TutorialSlides slides = null;
            foreach (var s in FindObjectsByType<TutorialSlides>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s.baked && s.gameObject.scene.IsValid()) { slides = s; break; }   // the baked one, hidden in the scene
            if (slides == null)
            {
                slides = new GameObject(ObjectName).AddComponent<TutorialSlides>();
                slides.showcase = showcase;
                slides.Build();
            }
            slides.showcase = showcase;
            // these labels are already written in the chosen language (the English heading stays English on purpose)
            foreach (var label in new[] { slides.kicker, slides.heading, slides.headingThai, slides.body, slides.pageLabel })
                if (label != null) label.textPreprocessor = LangUntranslated.Instance;
            slides.closing = false; slides.alpha = 0; slides.group.alpha = 0;
            slides.Show(0);
            Active = slides;
            slides.gameObject.SetActive(true);
            return slides;
        }

        public const string ObjectName = "Tutorial Slides (Wall)";

        /// <summary>Editor bake: the slides as a real object in the scene, hidden until TUTORIAL.</summary>
        public static TutorialSlides Bake(GameShowcase showcase)
        {
            var slides = new GameObject(ObjectName).AddComponent<TutorialSlides>();
            slides.showcase = showcase;
            slides.Build();
            slides.baked = true;
            slides.group.alpha = 0;
            slides.gameObject.SetActive(false);
            return slides;
        }

        // TH/EN pressed while the slides run: same page, new language
        void OnEnable() => Lang.Changed += Relabel;
        void OnDisable() => Lang.Changed -= Relabel;

        public void Close() => closing = true;

        void Update()
        {
            if (showcase == null) { gameObject.SetActive(false); return; }   // switched on by hand (not by Open)
            if (!closing && showcase.tutorialAllowed != null && !showcase.tutorialAllowed()) closing = true;   // a game started
            alpha = Mathf.MoveTowards(alpha, closing ? 0 : 1, Time.unscaledDeltaTime / Fade);
            group.alpha = alpha;
            if (closing)
            {
                if (alpha <= 0) Finish();
                return;
            }
            pageAge += Time.unscaledDeltaTime;
            progress.fillAmount = Mathf.Clamp01(pageAge / PageSeconds);
            if (pageAge < PageSeconds) return;
            if (page + 1 < showcase.pages.Length) Show(page + 1);
            else closing = true;
        }

        // บาส 2026-11-02 bug hunt: Show() restarts the page timer; a language swap only re-renders
        // the text, so keep the page's age (and its progress bar) where it was
        void Relabel()
        {
            if (showcase == null) return;
            float age = pageAge;
            Show(page);
            pageAge = age;
        }

        // closed: the baked slides go back into hiding, a runtime-built set is destroyed
        void Finish()
        {
            if (Active == this) Active = null;
            var closed = Closed;
            Closed = null;
            closed?.Invoke();
            if (baked) gameObject.SetActive(false);
            else Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Active == this) { Active = null; Closed?.Invoke(); }
        }

        void Show(int index)
        {
            page = index; pageAge = 0;
            var p = showcase.pages[index];
            // Thai: the English heading + its Thai line under it (the 2026-10-29 look) and Thai body;
            // English: the heading alone and the English body (Lang, 2026-10-30)
            kicker.text = Lang.T("HOW TO PLAY", "วิธีเล่น");
            heading.text = p.heading;
            headingThai.text = Lang.Thai ? p.headingThai : "";
            var lines = Lang.Thai || p.linesEn == null ? p.lines : p.linesEn;
            body.text = "• " + string.Join("\n• ", lines);
            pageLabel.text = $"{index + 1} / {showcase.pages.Length}";
            var tex = showcase.Load(p.image);
            picture.texture = tex;
            picture.enabled = tex != null;
            if (tex != null) pictureFit.aspectRatio = tex.width / (float)Mathf.Max(1, tex.height);
            for (int i = 0; i < dots.Length; i++)
                dots[i].color = i == index ? showcase.accent : UiThemeKit.Alpha(showcase.muted, i < index ? .7f : .3f);
        }

        void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = 1;
            canvas.sortingOrder = 30000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(4096, 1080);   // the wall is 8192 x 2160 (3.8 : 1)
            scaler.matchWidthOrHeight = 1;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
            var root = (RectTransform)transform;
            var s = showcase;
            var headFont = s.Headline();
            var thai = GameShowcase.Thai();

            var ground = UiThemeKit.Child(root, "Ground");
            UiThemeKit.Stretch(ground, 0);
            ground.gameObject.AddComponent<Image>().color = UiThemeKit.Alpha(s.ground, .94f);

            // the game's own backdrop, faint, so the slides read as part of the game
            var back = s.Load(s.backdrop);
            if (back != null)
            {
                var b = UiThemeKit.Child(root, "Backdrop");
                UiThemeKit.Stretch(b, 0);
                var raw = b.gameObject.AddComponent<RawImage>();
                raw.texture = back; raw.color = new Color(1, 1, 1, .18f); raw.raycastTarget = false;
                Cover(raw, back, 4096f / 1080f);
                var shade = UiThemeKit.Child(root, "Shade");
                UiThemeKit.Stretch(shade, 0);
                shade.gameObject.AddComponent<Image>().color = UiThemeKit.Alpha(s.ground, .55f);
            }

            // left: the picture in a themed frame
            var card = UiThemeKit.MakePlate(root, "Picture Card", s.frame, s.panel, 28, 10, true, UiThemeKit.Alpha(s.frame, .5f), true, s.shape);
            UiThemeKit.Place(card.rect, .035f, .1f, .5f, .9f);
            if (card.glow != null) card.glow.color = UiThemeKit.Alpha(s.accent, .35f);
            var frameArea = UiThemeKit.Child(card.fill.rectTransform, "Picture Area");
            UiThemeKit.Stretch(frameArea, 14);
            frameArea.gameObject.AddComponent<RectMask2D>();
            var pic = UiThemeKit.Child(frameArea, "Picture");
            UiThemeKit.Place(pic, 0, 0, 1, 1);
            picture = pic.gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;
            pictureFit = pic.gameObject.AddComponent<AspectRatioFitter>();
            pictureFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;   // fill the frame, crop the overflow

            // right: header strip, page title, the rules
            var logoTex = s.Load(s.logo);
            if (logoTex != null)
            {
                // fitted by an AspectRatioFitter: the overlay canvas has no size yet here, so UiThemeKit.Logo's maths is off
                var box = UiThemeKit.Child(root, "Logo Box");
                UiThemeKit.Place(box, .8f, .7f, .975f, .97f);
                var l = UiThemeKit.Child(box, "Logo");
                UiThemeKit.Place(l, 0, 0, 1, 1);
                var raw = l.gameObject.AddComponent<RawImage>();
                raw.texture = logoTex; raw.raycastTarget = false;
                var fit = l.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = logoTex.width / (float)Mathf.Max(1, logoTex.height);
            }
            var highlight = Highlight(s);
            kicker = UiThemeKit.Text(root, "Kicker", headFont);
            UiThemeKit.Place((RectTransform)kicker.transform, .54f, .86f, .78f, .94f);
            kicker.text = "HOW TO PLAY";
            Style(kicker, 46, highlight, TextAlignmentOptions.MidlineLeft);
            kicker.characterSpacing = 6;

            heading = UiThemeKit.Text(root, "Heading", headFont);
            UiThemeKit.Place((RectTransform)heading.transform, .54f, .69f, .79f, .85f);
            Style(heading, 120, Color.white, TextAlignmentOptions.MidlineLeft);
            heading.enableAutoSizing = true; heading.fontSizeMin = 60; heading.fontSizeMax = 120;
            if (s.ink != Color.white) UiThemeKit.ApplyOutline(heading, .18f, s.ground);

            headingThai = UiThemeKit.Text(root, "Heading Thai", thai);
            UiThemeKit.Place((RectTransform)headingThai.transform, .54f, .565f, .97f, .69f);
            Style(headingThai, 72, highlight, TextAlignmentOptions.MidlineLeft);
            // Thai's tall line box does not fit an ellipsis check at this height and TMP then hides the whole line
            headingThai.overflowMode = TextOverflowModes.Overflow;

            var rule = UiThemeKit.Child(root, "Rule");
            UiThemeKit.Place(rule, .54f, .55f, .97f, .554f);
            rule.gameObject.AddComponent<Image>().color = UiThemeKit.Alpha(s.frame, .6f);

            body = UiThemeKit.Text(root, "Body", thai);
            UiThemeKit.Place((RectTransform)body.transform, .54f, .16f, .97f, .53f);
            Style(body, 64, Color.white, TextAlignmentOptions.TopLeft);
            body.lineSpacing = 20;
            body.enableAutoSizing = true; body.fontSizeMin = 40; body.fontSizeMax = 64;
            body.overflowMode = TextOverflowModes.Overflow;

            // bottom: page dots, number, the 6 s progress bar
            dots = new Image[s.pages.Length];
            for (int i = 0; i < dots.Length; i++)
            {
                var d = UiThemeKit.Child(root, "Dot " + (i + 1));
                float x = .54f + i * .022f;
                UiThemeKit.Place(d, x, .085f, x + .012f, .13f);
                dots[i] = UiThemeKit.Skin(d.gameObject.AddComponent<Image>(), s.shape, Color.white, 10);
            }
            pageLabel = UiThemeKit.Text(root, "Page", headFont);
            UiThemeKit.Place((RectTransform)pageLabel.transform, .64f, .07f, .78f, .145f);
            Style(pageLabel, 40, s.muted, TextAlignmentOptions.MidlineLeft);

            var track = UiThemeKit.Child(root, "Progress Track");
            UiThemeKit.Place(track, .035f, .035f, .965f, .05f);
            track.gameObject.AddComponent<Image>().color = UiThemeKit.Alpha(s.muted, .25f);
            var bar = UiThemeKit.Child(track, "Progress");
            UiThemeKit.Stretch(bar, 0);
            progress = bar.gameObject.AddComponent<Image>();
            progress.sprite = UiThemeKit.SpriteFor(UiThemeKit.Shape.Rounded, 0);
            progress.type = Image.Type.Filled; progress.fillMethod = Image.FillMethod.Horizontal;
            progress.color = s.accent;
        }

        // The theme colour that stands out most from the slide ground: Memory Farm's barn red and Volley's palm green
        // both vanish on their own grounds, so those fall back to the light theme colour.
        static Color Highlight(GameShowcase s)
        {
            float ground = UiThemeKit.Luminance(s.ground);
            float Contrast(Color c) { float l = UiThemeKit.Luminance(c); return (Mathf.Max(l, ground) + .05f) / (Mathf.Min(l, ground) + .05f); }
            return Contrast(s.accent) >= 3 ? s.accent : Contrast(s.muted) >= 3 ? s.muted : Color.white;
        }

        static void Style(TMP_Text t, float size, Color color, TextAlignmentOptions align)
        {
            t.fontSize = size; t.color = color; t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Ellipsis;
        }

        // A RawImage that covers a frame of this aspect, centred, cropping what does not fit.
        public static void Cover(RawImage image, Texture tex, float frameAspect)
        {
            float texAspect = tex.width / (float)Mathf.Max(1, tex.height);
            image.uvRect = texAspect > frameAspect
                ? new Rect((1 - frameAspect / texAspect) / 2, 0, frameAspect / texAspect, 1)
                : new Rect(0, (1 - texAspect / frameAspect) / 2, 1, texAspect / frameAspect);
        }
    }
}
