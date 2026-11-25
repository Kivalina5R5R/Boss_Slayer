using System.IO;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Deepspace
{
    // 2026-11-25 "เอา UI Ultimate skill ไปใส่": when a boss starts its ultimate, the art team's "ULTIMATE SKILL" banner
    // (Downloads/Unti, laid out like their screenshot) shoots in from the left edge of the wall, holds about two seconds
    // and slides back out. One colour per stage: 1 red = fire, 2 blue = water, 3 orange = land, 4 purple = the final boss.
    // Art: Resources/BossSlayer/Ultimate/BG<n> (the burst) and Text<n> (the words, over the burst).
    // A stage whose burst has a loop (StreamingAssets/BossBackgroundMovies/UltimateBG<n>.mp4, made with Higgsfield on
    // black: the streak tips shoot out and snap back) shows that video, added onto the wall so its black vanishes; the
    // still picture stands in until the video is ready.
    public sealed class BossUltimateBanner : MonoBehaviour
    {
        const string Folder = "BossSlayer/Ultimate/";
        // Layout as fractions of the wall (from the art team's screenshot): the burst's left edge on the wall's left
        // edge, a third of the wall wide, its middle a little above the wall's middle; the words are placed from the star (below).
        const float BurstWidth = .33f, BurstCentreY = .55f;
        const float InSeconds = .35f, HoldSeconds = 2f, OutSeconds = .3f, TextLag = .07f;
        const float PlaybackSpeed = .6f;
        // The picture's clear left margin (57 px of 3402) plus a little more, so the burst's left end always runs off
        // the screen edge with no gap ("ตัวขอบตรงนี้ต้องไม่แหว่ง เลื่อนไปฝั่งซ้ายอีก").
        const float LeftMargin = 57f / 3402f + .1f;   // 2026-11-25 "ขยับฝั่งซ้ายอีกนิด": .06 -> .1
        // 2026-11-25 "ขนาดและตำแหน่งของตัวหนังสือ ... ดูจากดาวเป็นจุดอ้างอิง": the words are placed from the big star, as in
        // the art team's mock-up: the star's centre is at (.4606, .412) of the burst picture (from its left / top); the
        // words picture is .78 of the burst picture's width, its lettering starting .27 of the lettering's width left of
        // the star, the star's arm .41 of the way down the lettering. (Lettering inside Text<n>: x 39..2079 of 2146,
        // y 52..590 of 630.)
        const float StarX = .4606f, StarY = .412f, WordsToBurst = .78f, WordsLeadIn = .27f, WordsArmDown = .41f;

        RectTransform root, burst, words;
        RawImage burstImage, wordsImage;
        Sequence show;
        VideoPlayer video;
        Material additive;
        string videoUrl;
        bool showing;
        float shownAt;
        // 2026-11-25 "ให้ตัวหนังสือขยับ animation เล็กน้อย มันดูแข็งๆ" then "ไม่ต้องเอียง ไม่ต้องเต้น แค่หดขยายพอ": while the
        // banner holds, the words only breathe - a gentle grow and shrink.
        const float IdleScale = .03f, IdleSpeed = 2.2f;

        // wall: any rect on the wall's canvas. The banner gets its own overlay canvas on that display, so its edges are
        // the screen's edges (the wall canvas can reach past what the wall shows) and it draws over the wall HUD.
        public static BossUltimateBanner Create(RectTransform wall, Transform owner)
        {
            if (wall == null) return null;
            var source = wall.GetComponentInParent<Canvas>()?.rootCanvas;
            var go = new GameObject("Ultimate Banner", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(owner, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (source != null) canvas.targetDisplay = source.targetDisplay;
            canvas.sortingOrder = 210;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var banner = go.AddComponent<BossUltimateBanner>();
            banner.Build((RectTransform)go.transform);
            return banner;
        }

        void Build(RectTransform rect)
        {
            root = rect;
            burstImage = Picture("Burst", out burst);
            wordsImage = Picture("Words", out words);
            video = burst.gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.isLooping = true;
            video.renderMode = VideoRenderMode.APIOnly;
            video.source = VideoSource.Url;
            video.audioOutputMode = VideoAudioOutputMode.None;
            // 2026-11-25 "ให้มันช้ากว่านี้หน่อย มันไวไป": the speed lines play at 60 % of the generated speed.
            video.playbackSpeed = PlaybackSpeed;
            video.prepareCompleted += _ => UseVideoIfShowing();
            var shader = Resources.Load<Shader>("BossUiAdditive");
            if (shader != null && shader.isSupported) additive = new Material(shader) { name = "Ultimate Banner Additive", hideFlags = HideFlags.HideAndDontSave };
            gameObject.SetActive(false);
        }

        RawImage Picture(string name, out RectTransform rect)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            rect = image.rectTransform;
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            image.raycastTarget = false;
            return image;
        }

        // stage: 1 fire, 2 water, 3 land, 4 final boss.
        public void Play(int stage)
        {
            var burstArt = Resources.Load<Texture2D>(Folder + "BG" + stage);
            var wordsArt = Resources.Load<Texture2D>(Folder + "Text" + stage);
            if (burstArt == null || wordsArt == null) return;
            gameObject.SetActive(true);
            root.SetAsLastSibling();
            var area = root.rect.size;
            burstImage.texture = burstArt;
            burstImage.material = null;
            wordsImage.texture = wordsArt;
            showing = true;
            shownAt = Time.unscaledTime;
            StartVideo(stage);
            float burstW = area.x * BurstWidth, burstH = burstW * burstArt.height / burstArt.width;
            float wordsW = burstW * WordsToBurst, wordsH = wordsW * wordsArt.height / wordsArt.width;
            burst.sizeDelta = new Vector2(burstW, burstH);
            words.sizeDelta = new Vector2(wordsW, wordsH);
            // "ปลายฝั่งซ้ายต้องอยู่ติดขอบ": the picture has a 57 px clear margin on its left (of 3402), pushed off-screen.
            var burstHome = new Vector2(burstW * (.5f - LeftMargin), area.y * BurstCentreY);
            float starX = burstHome.x - burstW * .5f + StarX * burstW, starY = burstHome.y + burstH * .5f - StarY * burstH;
            float letteringW = wordsW * 2040f / 2146f, letteringH = wordsH * 538f / 630f;
            float letteringLeft = starX - WordsLeadIn * letteringW, letteringTop = starY + WordsArmDown * letteringH;
            var wordsHome = new Vector2(letteringLeft - wordsW * 39f / 2146f + wordsW * .5f, letteringTop + wordsH * 52f / 630f - wordsH * .5f);

            show?.Kill();
            burst.anchoredPosition = burstHome - new Vector2(burstW * 1.1f, 0f);
            words.anchoredPosition = wordsHome - new Vector2(burstW * 1.1f, 0f);
            words.localScale = Vector3.one;
            burstImage.color = wordsImage.color = Color.white;
            show = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            show.Append(burst.DOAnchorPos(burstHome, InSeconds).SetEase(Ease.OutBack, 1.4f));
            show.Insert(TextLag, words.DOAnchorPos(wordsHome, InSeconds).SetEase(Ease.OutBack, 1.7f));
            show.Insert(TextLag + InSeconds, words.DOPunchScale(Vector3.one * .12f, .3f, 6, .6f));
            float leave = InSeconds + HoldSeconds;
            show.Insert(leave, burst.DOAnchorPos(burstHome - new Vector2(burstW * 1.1f, 0f), OutSeconds).SetEase(Ease.InBack));
            show.Insert(leave, words.DOAnchorPos(wordsHome - new Vector2(burstW * 1.1f, 0f), OutSeconds).SetEase(Ease.InBack));
            show.OnComplete(() => { show = null; showing = false; video.Stop(); gameObject.SetActive(false); });
        }

        void Update()
        {
            if (!showing || words == null) return;
            float t = Time.unscaledTime - shownAt;
            float settled = TextLag + InSeconds + .3f;   // after the slide-in and its punch
            if (t < settled) return;
            float idle = t - settled;
            float ease = Mathf.Clamp01(idle / .25f);   // blend in, no jump from the punch
            words.localScale = Vector3.one * (1f + ease * IdleScale * Mathf.Sin(idle * IdleSpeed * Mathf.PI));
        }

        void StartVideo(int stage)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "BossBackgroundMovies", "UltimateBG" + stage + ".mp4");
            if (additive == null || !File.Exists(path)) return;
            string url = new System.Uri(path).AbsoluteUri;
            if (url == videoUrl && video.isPrepared) { UseVideoIfShowing(); return; }
            videoUrl = url;
            video.url = url;
            video.Prepare();
        }

        void UseVideoIfShowing()
        {
            if (!showing || video == null || video.texture == null) return;
            burstImage.texture = video.texture;
            burstImage.material = additive;
            if (!video.isPlaying) video.Play();
        }

        public void Hide()
        {
            show?.Kill();
            show = null;
            showing = false;
            if (video != null) video.Stop();
            gameObject.SetActive(false);
        }
    }
}
