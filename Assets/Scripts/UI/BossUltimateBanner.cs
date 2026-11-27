using System.IO;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace BossSlayer
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
        const string Folder = "Art/UI/Ultimate/";
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
        // 2026-11-27 "เอาตัวหลอดโหลดอันติไปไว้ตรงนี้ ... ไม่ต้องใส่ชื่อของอันติ": the big move's charge bar (was on the wall HUD
        // under the move's name) sits under the ULTIMATE SKILL words, with no name. Then "เข้าไปแก้เองตรงไหนที่ prefab": its
        // place, size and tilt come from Prefab/UI/UltimateChargeBar. That prefab's root is the words picture (2146 x 630,
        // with a see-through copy of the lettering as a guide, hidden in the game); move, resize or rotate its "Charge Bar"
        // there. It is scaled with the words, so it keeps its spot on any screen. Art: ChargeBack / ChargeFill (2442 x 125,
        // the fill runs from x 62 to 2375).
        const string BarPrefab = "Prefab/UI/UltimateChargeBar";
        const float WordsArtWidth = 2146f;
        const float FillStart = 62f / 2442f, FillSpan = (2375f - 62f) / 2442f;
        // The bar is fed every frame of the wind-up; when that stops this long, the charge is over.
        const float ChargeTimeout = .3f;

        RectTransform root, burst, words, barLayout, bar, barFill;
        RawImage burstImage, wordsImage, barFillImage;
        float chargeProgress, lastChargeAt = -1f;
        bool leaving;
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
            BuildBar();
            video =burst.gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.isLooping = true;
            video.renderMode = VideoRenderMode.APIOnly;
            video.source = VideoSource.Url;
            video.audioOutputMode = VideoAudioOutputMode.None;
            // 2026-11-25 "ให้มันช้ากว่านี้หน่อย มันไวไป": the speed lines play at 60 % of the generated speed.
            video.playbackSpeed = PlaybackSpeed;
            video.prepareCompleted += _ => UseVideoIfShowing();
            var shader = GameArt.Load<Shader>("Art/Shaders/BossUiAdditive");
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

        // A child of the words, so it slides, punches and breathes with them.
        void BuildBar()
        {
            var prefab = GameArt.Load<GameObject>(BarPrefab);
            if (prefab == null) { Debug.LogWarning("[BossUltimateBanner] " + BarPrefab + " is missing, no charge bar."); return; }
            barLayout = (RectTransform)Instantiate(prefab, words, false).transform;
            barLayout.anchorMin = barLayout.anchorMax = barLayout.pivot = Vector2.one * .5f;
            barLayout.anchoredPosition = Vector2.zero;
            foreach (Transform child in barLayout)
                if (child.name.StartsWith("Guide")) child.gameObject.SetActive(false);
            bar = barLayout.Find("Charge Bar") as RectTransform;
            barFill = bar != null ? bar.Find("Fill") as RectTransform : null;
            barFillImage = barFill != null ? barFill.GetComponent<RawImage>() : null;
            if (barFillImage == null) { bar = null; return; }
            foreach (var image in barLayout.GetComponentsInChildren<Graphic>(true)) image.raycastTarget = false;
            // The fill grows from the bar's left end, as tall as the bar.
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = Vector2.up;
            barFill.pivot = new Vector2(0f, .5f);
            barFill.anchoredPosition = Vector2.zero;
            bar.gameObject.SetActive(false);
        }

        // progress 0..1, every frame of the big move's wind-up. The banner stays up while this keeps coming.
        public void SetCharge(float progress)
        {
            chargeProgress = Mathf.Clamp01(progress);
            lastChargeAt = Time.unscaledTime;
            ApplyCharge();
        }

        bool Charging => lastChargeAt >= 0f && Time.unscaledTime - lastChargeAt < ChargeTimeout;

        void ApplyCharge()
        {
            if (bar == null) return;
            bar.gameObject.SetActive(Charging);
            // The fill picture is cut at the progress point, so its left end keeps the art's own shape.
            float cut = FillStart + chargeProgress * FillSpan;
            barFill.sizeDelta = new Vector2(bar.rect.width * cut, 0f);
            barFillImage.uvRect = new Rect(0f, 0f, cut, 1f);
        }

        void LayOutBar(float wordsW)
        {
            if (barLayout == null) return;
            barLayout.localScale = Vector3.one * (wordsW / WordsArtWidth);
            ApplyCharge();
        }

        // stage: 1 fire, 2 water, 3 land, 4 final boss.
        // 2026-11-27 "แก้สีของหลอดให้เป็นไปตามแต่ละด่าน": ChargeFill is a white copy of the art team's purple fill, tinted
        // with the main colour of the stage's burst art (sampled from BG1..BG4): fire red, water blue, land orange, purple.
        static Color BarTint(int stage) => stage switch
        {
            1 => new Color32(214, 18, 19, 255),
            2 => new Color32(10, 61, 209, 255),
            3 => new Color32(228, 113, 4, 255),
            _ => new Color32(150, 16, 228, 255),
        };

        public void Play(int stage)
        {
            var burstArt = GameArt.Load<Texture2D>(Folder + "BG" + stage);
            var wordsArt = GameArt.Load<Texture2D>(Folder + "Text" + stage);
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
            LayOutBar(wordsW);
            homeOffset = new Vector2(burstW * 1.1f, 0f);
            burstHomePos = burstHome;
            wordsHomePos = wordsHome;
            leaving = false;

            show?.Kill();
            burst.anchoredPosition = burstHome - new Vector2(burstW * 1.1f, 0f);
            words.anchoredPosition = wordsHome - new Vector2(burstW * 1.1f, 0f);
            words.localScale = Vector3.one;
            burstImage.color = wordsImage.color = Color.white;
            if (barFillImage != null) barFillImage.color = BarTint(stage);
            show = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            show.Append(burst.DOAnchorPos(burstHome, InSeconds).SetEase(Ease.OutBack, 1.4f));
            show.Insert(TextLag, words.DOAnchorPos(wordsHome, InSeconds).SetEase(Ease.OutBack, 1.7f));
            show.Insert(TextLag + InSeconds, words.DOPunchScale(Vector3.one * .12f, .3f, 6, .6f));
        }

        Vector2 homeOffset, burstHomePos, wordsHomePos;

        // The banner holds at least HoldSeconds, and on for as long as the charge bar is still filling.
        void Leave()
        {
            leaving = true;
            show?.Kill();
            show = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            show.Insert(0f, burst.DOAnchorPos(burstHomePos - homeOffset, OutSeconds).SetEase(Ease.InBack));
            show.Insert(0f, words.DOAnchorPos(wordsHomePos - homeOffset, OutSeconds).SetEase(Ease.InBack));
            show.OnComplete(() => { show = null; showing = false; leaving = false; video.Stop(); gameObject.SetActive(false); });
        }

        void Update()
        {
            if (!showing || words == null) return;
            float t = Time.unscaledTime - shownAt;
            bool charging = Charging;
            if (bar != null && bar.gameObject.activeSelf != charging) bar.gameObject.SetActive(charging);
            if (!leaving && !charging && t >= InSeconds + HoldSeconds) { Leave(); return; }
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
            leaving = false;
            lastChargeAt = -1f;
            if (video != null) video.Stop();
            gameObject.SetActive(false);
        }
    }
}
