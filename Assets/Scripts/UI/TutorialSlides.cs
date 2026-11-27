using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BossSlayer.TutorialArt;

namespace BossSlayer
{
    // บาส 2026-10-29: the "how to play" tutorial - slides over the whole wall (targetDisplay 1), opened by the launcher's
    // TUTORIAL (or T, BossArenaRemoteHooks). Pages turn by themselves every PageSeconds until the launcher's BACK / NEXT
    // take over (GoToPage). It closes itself when a session starts, so it never covers real play.
    // 2026-11-27 "มันรกเกินไป ทำให้อ่านง่ายด้วย ซ้ายเป็นรูปเป็น animation ขวาเป็น text": two halves on a quiet dark
    // ground. The left half is a framed moving picture made from the game's own art (TutorialScenes); the right half
    // is only words: HOW TO PLAY and the page number, the heading (Thai under it), three rule lines, and page bars.
    // It is always built fresh when opened: the copy baked into older scenes (with its empty picture frame) stays hidden.
    public sealed class TutorialSlides : MonoBehaviour
    {
        public const float PageSeconds = 7f;
        public const string ObjectName = "Tutorial Slides (Wall)";
        const float Fade = .35f, PageIn = .45f;
        const int MaxLines = 3;

        // Each page takes its gem's colour (page 1 red, 2 green, 3 yellow, 4 blue).
        static readonly Color[] PageColors = { UiThemeKit.Rgb(0xFF5B4A), UiThemeKit.Rgb(0x5BE36A), UiThemeKit.Rgb(0xFFD23F), UiThemeKit.Rgb(0x4FB8FF) };

        GameShowcase showcase;
        CanvasGroup group;
        RectTransform stage, column;
        TMP_Text kicker, step, heading, headingThai;
        readonly TMP_Text[] lines = new TMP_Text[MaxLines];
        readonly Image[] bullets = new Image[MaxLines];
        // One bar per page under the rules: done pages full, the current one fills with its timer.
        Image[] pageBars;
        TutorialScene scene;
        RectTransform sceneRoot;
        CanvasGroup sceneFade;
        int page;
        float pageAge, alpha;
        bool closing, heldByLauncher;

        public static TutorialSlides Active { get; private set; }
        public int Page => page;
        public int PageCount => showcase.pages.Length;
        public event Action Closed;

        public static TutorialSlides Open(GameShowcase showcase)
        {
            if (Active != null) return Active;
            var slides = new GameObject(ObjectName).AddComponent<TutorialSlides>();
            slides.showcase = showcase;
            slides.Build();
            slides.Show(0);
            Active = slides;
            return slides;
        }

        // TH/EN pressed while the slides run: same page, new language
        void OnEnable() => Lang.Changed += Relabel;
        void OnDisable() => Lang.Changed -= Relabel;

        public void Close() => closing = true;

        // The launcher's BACK / NEXT (2026-11-27, as in Memory Farm): pageNumber starts at 1; out-of-range numbers are clamped.
        public void GoToPage(int pageNumber)
        {
            if (showcase == null || closing) return;
            heldByLauncher = true;
            Show(Mathf.Clamp(pageNumber - 1, 0, showcase.pages.Length - 1));
        }

        void Update()
        {
            if (showcase == null) { Destroy(gameObject); return; }
            if (!closing && showcase.tutorialAllowed != null && !showcase.tutorialAllowed()) closing = true;   // a game started
            float dt = Time.unscaledDeltaTime;
            alpha = Mathf.MoveTowards(alpha, closing ? 0 : 1, dt / Fade);
            group.alpha = alpha;
            if (closing)
            {
                if (alpha <= 0) Finish();
                return;
            }

            pageAge += dt;
            scene?.Tick(pageAge);
            AnimateFrame();
            if (heldByLauncher || pageAge < PageSeconds) return;
            if (page + 1 < showcase.pages.Length) Show(page + 1);
            else closing = true;
        }

        // The page bars follow the timer; a new page slides its words in and fades its picture up.
        void AnimateFrame()
        {
            float timer = heldByLauncher ? 1f : Mathf.Clamp01(pageAge / PageSeconds);
            for (int i = 0; i < pageBars.Length; i++)
                pageBars[i].fillAmount = i < page ? 1f : i == page ? timer : 0f;

            float enter = Ease(0, PageIn, pageAge);
            column.anchoredPosition = new Vector2((1 - enter) * 80f, 0);
            float stageScale = Mathf.Lerp(.97f, 1f, enter);
            sceneRoot.localScale = new Vector3(stageScale, stageScale, 1);
            sceneFade.alpha = enter;
        }

        // Show() restarts the page timer; a language swap only re-renders the page, so keep its age where it was
        void Relabel()
        {
            if (showcase == null) return;
            float age = pageAge;
            Show(page);
            pageAge = Mathf.Max(age, PageIn);
        }

        void Finish()
        {
            if (Active == this) Active = null;
            var closed = Closed;
            Closed = null;
            closed?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (Active == this) { Active = null; Closed?.Invoke(); }
        }

        void Show(int index)
        {
            page = index;
            pageAge = 0;
            var p = showcase.pages[index];
            var color = PageColors[index % PageColors.Length];

            kicker.text = Lang.Thai ? "วิธีเล่น" : "HOW TO PLAY";
            kicker.font = Lang.Thai ? GameShowcase.Thai() : PixelFont();
            step.text = $"{index + 1} / {showcase.pages.Length}";
            heading.text = p.heading;
            headingThai.text = Lang.Thai ? p.headingThai : "";

            var text = Lang.Thai || p.linesEn == null ? p.lines : p.linesEn;
            for (int i = 0; i < MaxLines; i++)
            {
                bool shown = i < text.Length;
                lines[i].text = shown ? text[i] : "";
                lines[i].font = Lang.Thai ? GameShowcase.Thai() : PixelFont();
                bullets[i].enabled = shown;
                bullets[i].color = color;
            }
            for (int i = 0; i < pageBars.Length; i++) pageBars[i].color = i == index ? color : Gold;

            BuildScene(p.art);
        }

        // The page's moving picture, made fresh in the stage (the old one is thrown away).
        void BuildScene(TutorialScene.Kind kind)
        {
            if (sceneRoot != null) Destroy(sceneRoot.gameObject);
            var go = new GameObject("Scene " + kind, typeof(RectTransform), typeof(CanvasGroup));
            go.layer = gameObject.layer;
            sceneRoot = (RectTransform)go.transform;
            sceneRoot.SetParent(stage, false);
            UiThemeKit.Stretch(sceneRoot, 0);
            sceneFade = go.GetComponent<CanvasGroup>();
            sceneFade.alpha = 0;
            scene = TutorialScene.Create(kind);
            scene.Build(sceneRoot);
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
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

            BuildBackground(root);
            BuildStage(root);
            BuildRight(root);
        }

        // A quiet ground: the Start hall, very dark, nothing moving on it.
        void BuildBackground(RectTransform root)
        {
            Fill(root, "Ground", Night);
            TutorialArt.Cover(root, "Backdrop", showcase.folder + "/" + showcase.backdrop, 4096f / 1080f, new Color(1f, 1f, 1f, .25f));
        }

        // Left half: the page's moving picture in a thin gold frame.
        void BuildStage(RectTransform root)
        {
            var frame = Box(root, "Stage Frame", .02f, .08f, .5f, .92f);
            var back = frame.gameObject.AddComponent<Image>();
            back.color = UiThemeKit.Alpha(Night, .95f);
            back.raycastTarget = false;
            stage = Box(frame, "Stage", 0, 0, 1, 1);
            stage.gameObject.AddComponent<RectMask2D>();
            UiThemeKit.Ring(frame, "Trim", UiThemeKit.Shape.Rounded, UiThemeKit.Alpha(Gold, .8f), 4, 0).raycastTarget = false;
        }

        // Right half: HOW TO PLAY and the page number, the heading (Thai under it), three rule lines, the page bars.
        void BuildRight(RectTransform root)
        {
            column = Box(root, "Rules", .54f, .08f, .975f, .92f);
            kicker = Label(column, "Kicker", "HOW TO PLAY", null, 0, .86f, .7f, 1f, 58, Gold, TextAlignmentOptions.MidlineLeft);
            kicker.characterSpacing = 5;
            step = Label(column, "Step", "", null, .7f, .86f, 1f, 1f, 58, GoldDim, TextAlignmentOptions.MidlineRight);

            heading = Label(column, "Heading", "", null, 0, .64f, 1, .86f, 170, Color.white, TextAlignmentOptions.MidlineLeft);
            headingThai = Label(column, "Heading Thai", "", "", 0, .52f, 1, .65f, 88, Gold, TextAlignmentOptions.MidlineLeft);
            headingThai.font = GameShowcase.Thai();

            var rule = Box(column, "Rule", 0, .495f, 1, .5f);
            var ruleLine = rule.gameObject.AddComponent<Image>();
            ruleLine.color = UiThemeKit.Alpha(Gold, .35f);
            ruleLine.raycastTarget = false;

            for (int i = 0; i < MaxLines; i++)
            {
                float top = .46f - i * .14f;
                var dot = Box(column, "Bullet " + (i + 1), .004f, top - .085f, .016f, top - .045f);
                bullets[i] = UiThemeKit.Skin(dot.gameObject.AddComponent<Image>(), UiThemeKit.Shape.Rounded, Color.white, 4);
                bullets[i].raycastTarget = false;
                lines[i] = Label(column, "Line " + (i + 1), "", "", .035f, top - .13f, 1, top, 72, Color.white, TextAlignmentOptions.MidlineLeft);
                lines[i].lineSpacing = -8;
            }

            pageBars = new Image[showcase.pages.Length];
            float width = 1f / pageBars.Length;
            for (int i = 0; i < pageBars.Length; i++)
            {
                var track = Box(column, "Page " + (i + 1), i * width + .005f, 0, (i + 1) * width - .005f, .018f);
                var trackImage = track.gameObject.AddComponent<Image>();
                trackImage.color = UiThemeKit.Alpha(Color.white, .12f);
                trackImage.raycastTarget = false;
                var fill = UiThemeKit.Child(track, "Fill");
                UiThemeKit.Stretch(fill, 0);
                pageBars[i] = fill.gameObject.AddComponent<Image>();
                pageBars[i].sprite = UiThemeKit.SpriteFor(UiThemeKit.Shape.Rounded, 0);
                pageBars[i].type = Image.Type.Filled;
                pageBars[i].fillMethod = Image.FillMethod.Horizontal;
                pageBars[i].raycastTarget = false;
            }
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
