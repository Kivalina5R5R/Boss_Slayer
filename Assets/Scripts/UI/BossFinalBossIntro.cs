using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-26: "หลังจากเลือก perk เสร็จจะตัดไป Final Boss มี cut scene รูปมันโผล่ออกมาจากตรงพื้น ก่อนตัดไปหน้าบอส" -
    // after the second perk break the boss statue hall fades in, the Abyssal King's stone statue
    // (Resources/BossSlayer/FinalBoss/AbyssalKing, made with Higgsfield) bursts up from the bottom edge of the screen with
    // the screen shaking, holds a moment, flashes, and the fight starts (BossSlayerGameManager.Rush.cs, FinalBossEntrance).
    // 2026-11-26 "เส้นขีดม่วงๆไม่ควรมี มันควรเด้งจากพื้นเลย ตัวฐานต้องอยู่ที่ขอบจอ ถ้าตัวเล็กไปให้ขยายขนาดแทน": no floor line;
    // he pops up from the screen's bottom edge, his pedestal sitting on that edge, nearly screen-tall.
    // "ฐานต้องหยุดอยู่ที่ขอบจอไม่กระโดดเด้งจนดูเหมือนลอย": the rise slows into place, never overshooting the edge.
    // 2026-11-26 "เปลี่ยนใหม่ ให้เป็นตาแดงแล้วก็ค่อยๆซูมกล้องเข้าไป" (instead of the planned crack video): once he has
    // landed, his visor slowly turns red while the camera slowly pushes in on his face, then the flash.
    // "ใช้รูปตาแดงที่ส่งมา ไม่ย้อมสีเพิ่ม": AbyssalKingEyes is the user's own red-eyed Y picture, laid on the helmet's
    // slit at an even scale and shown as it is (no tint). "ตอนวูมเข้าไปก็ให้ค่อยๆสว่าง": it brightens slowly
    // over the whole push-in, in step with the camera, and is fully lit only when the zoom ends.
    // With no sprite in place it plays nothing, so the old straight cut still works.
    public sealed class BossFinalBossIntro : MonoBehaviour
    {
        const string SpritePath = "Art/UI/FinalBoss/AbyssalKing";
        // The king's height on screen once risen, as a fraction of the screen's height (his pedestal on the bottom edge).
        const float KingHeight = .96f;
        const float FadeInSeconds = .7f, RiseSeconds = 1.6f, HoldSeconds = .5f, FlashSeconds = .35f;
        const float ShakePixels = 14f;
        // How far below the bottom edge he rests (fraction of the screen height), past the picture's clear margin, so
        // the pedestal sits on the edge.
        const float SinkFraction = 14f / 2160f;
        const string EyesPath = "Art/UI/FinalBoss/AbyssalKingEyes";
        // Where the eyes picture sits in the king picture (0..1, bottom = 0) and the point between his eyes the camera
        // pushes in on. Measured on AbyssalKing.png (1360 x 2048): the eyes picture (234 x 212) is scaled 1.16x so its
        // Y's top corners meet the slit's, covering x 545..816, y 489..735 (pixels from the top). Redo both if either
        // picture changes.
        static readonly Rect EyesPlace = new Rect(545f / 1360f, 1f - 735f / 2048f, 271f / 1360f, 246f / 2048f);
        static readonly Vector2 FacePoint = new Vector2(678f / 1360f, 1f - 568f / 2048f);
        // How close the camera ends up (2 = twice as close) and how long the push-in (and the eyes lighting up) takes.
        const float ZoomTo = 2.8f, ZoomSeconds = 3.2f;

        RectTransform root, cam, shaker, king;
        Image backdrop, flash;
        RawImage kingImage, hall, eyes;
        CanvasGroup hallFade;
        // 2026-11-27 "รูปปั้นอันไหนที่พังก็ให้เห็นพังด้วย ไม่ใช่ยืนอยู่ที่เดิม": the beaten bosses' rubble (and the Fish's
        // patch) from the boss select (BossStatueShatter), laid on the hall at their statues' places.
        readonly List<RawImage> rubble = new List<RawImage>();
        float eyesOpen;
        const string HallPath = "Art/UI/SelectBoss/SelectBossBG_v7";
        // How bright the hall shows behind him (1 = as painted), so the king stands out.
        const float HallShade = .6f;
        Sequence show;

        public bool IsPlaying => show != null && show.IsActive() && show.IsPlaying();
        public static bool HasArt => GameArt.Load<Texture2D>(SpritePath) != null;

        // wall: any rect on the wall's canvas. The intro gets its own overlay canvas on that display, above the
        // session fade (so it plays on the black) and the wall HUD.
        public static BossFinalBossIntro Create(RectTransform wall, Transform owner)
        {
            if (wall == null) return null;
            var source = wall.GetComponentInParent<Canvas>()?.rootCanvas;
            var go = new GameObject("Final Boss Intro", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(owner, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (source != null) canvas.targetDisplay = source.targetDisplay;
            canvas.sortingOrder = 220;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var intro = go.AddComponent<BossFinalBossIntro>();
            intro.Build((RectTransform)go.transform);
            return intro;
        }

        void Build(RectTransform rect)
        {
            root = rect;
            backdrop = Fill("Black", root, Color.black);
            // The "camera": everything under it zooms together (the push-in on his face).
            cam = Stretch(new GameObject("Camera", typeof(RectTransform)).GetComponent<RectTransform>(), root);
            shaker = Stretch(new GameObject("Shake", typeof(RectTransform)).GetComponent<RectTransform>(), cam);
            // 2026-11-26 "เอามาพร้อมกับฉากหลัง": the boss statue hall behind him, a little past the screen's edges so
            // the shake never shows its border.
            hall = new GameObject("Statue Hall", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            Stretch(hall.rectTransform, shaker);
            hall.rectTransform.offsetMin = new Vector2(-ShakePixels * 2f, -ShakePixels * 2f);
            hall.rectTransform.offsetMax = new Vector2(ShakePixels * 2f, ShakePixels * 2f);
            hall.raycastTarget = false;
            // the hall fades in as a whole, with the rubble laid on it (ShowBrokenStatues)
            hallFade = hall.gameObject.AddComponent<CanvasGroup>();
            hallFade.blocksRaycasts = false; hallFade.interactable = false;
            // The screen itself clips him (not the shaking layer), so his pedestal always meets the bottom edge.
            // (It zooms with the camera, but by then the pedestal is below the screen anyway.)
            var onScreen = new GameObject("On Screen", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            Stretch(onScreen, cam);
            king = new GameObject("Abyssal King", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
            king.SetParent(onScreen, false);
            king.anchorMin = king.anchorMax = new Vector2(.5f, 0f);
            king.pivot = new Vector2(.5f, 0f);
            kingImage = king.GetComponent<RawImage>();
            kingImage.raycastTarget = false;
            eyes = new GameObject("Red Eyes", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            eyes.rectTransform.SetParent(king, false);
            eyes.rectTransform.anchorMin = EyesPlace.min;
            eyes.rectTransform.anchorMax = EyesPlace.max;
            eyes.rectTransform.offsetMin = eyes.rectTransform.offsetMax = Vector2.zero;
            eyes.raycastTarget = false;
            flash = Fill("Flash", root, Color.white);
            gameObject.SetActive(false);
        }

        // Plays the whole cutscene; onRisen runs under the closing flash (start the fight there), then the intro fades
        // away and onDone runs. Returns false (and runs nothing) when the sprite is missing.
        // onShake(seconds, strength) runs whenever the hall shakes (strength 1 = the rise), so the floor can shake with it.
        // brokenStatues: the boss kinds (1..9) beaten this session, whose statues show as rubble in the hall.
        public bool Play(System.Action onRisen, System.Action onDone, System.Action<float, float> onShake = null, ICollection<int> brokenStatues = null)
        {
            var art = GameArt.Load<Texture2D>(SpritePath);
            if (art == null) return false;
            gameObject.SetActive(true);
            float screenH = root.rect.height;
            float kingH = screenH * KingHeight, kingW = kingH * art.width / art.height;
            kingImage.texture = art;
            king.sizeDelta = new Vector2(kingW, kingH);
            float sink = screenH * SinkFraction;
            king.anchoredPosition = new Vector2(0f, -kingH - sink);
            eyes.texture = GameArt.Load<Texture2D>(EyesPath);
            eyesOpen = 0f; ShowEyes();
            cam.localScale = Vector3.one;
            // the camera pushes in on the point between his eyes once he has risen (screen fractions, bottom = 0)
            float facePx = (FacePoint.x - .5f) * kingW, facePy = -sink + FacePoint.y * kingH;
            cam.pivot = new Vector2(.5f + facePx / root.rect.width, facePy / screenH);
            kingImage.color = new Color(.3f, .3f, .3f, 1f);   // rises in shadow, lights up at the top
            var hallArt = GameArt.Load<Texture2D>(HallPath);
            hall.texture = hallArt;
            hall.enabled = hallArt != null;
            if (hallArt != null)
            {
                // crop the wide hall to the screen's shape around its middle (the dragon's arch)
                var size = hall.rectTransform.rect.size;
                float w = Mathf.Min(1f, size.x / size.y * hallArt.height / hallArt.width);
                hall.uvRect = new Rect(.5f - w * .5f, 0f, w, 1f);
            }
            hall.color = new Color(HallShade, HallShade, HallShade, 1f);
            hallFade.alpha = 0f;
            ShowBrokenStatues(hallArt != null ? brokenStatues : null);
            SetAlpha(backdrop, 1f); SetAlpha(flash, 0f);
            shaker.anchoredPosition = Vector2.zero;
            var rest = new Vector2(0f, -sink);

            show?.Kill();
            show = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            // the hall fades in with the ground rumbling
            show.AppendCallback(() => BossFightAudio.PlayUi("wall_crumble", .6f, .2f, 4));
            show.Append(hallFade.DOFade(1f, FadeInSeconds));
            // the king bursts up from the floor with the ground shaking
            show.AppendCallback(() => BossFightAudio.PlayUi("statue_awaken", .7f, .2f, 4));
            float riseAt = show.Duration();
            show.Append(king.DOAnchorPos(rest, RiseSeconds).SetEase(Ease.OutCubic));
            show.Insert(riseAt, shaker.DOShakeAnchorPos(RiseSeconds, ShakePixels, 28, 90f, false, false));
            show.InsertCallback(riseAt, () => onShake?.Invoke(RiseSeconds, 1f));
            show.Insert(riseAt + RiseSeconds * .5f, kingImage.DOColor(Color.white, RiseSeconds * .5f));
            // he lands with a slam
            show.AppendCallback(() => { BossFightAudio.PlayUi("statue_slam", .8f, .2f, 4); onShake?.Invoke(.3f, 1.5f); });
            // 2026-11-26 "เอาการเด้งเหมือนเยลลี่ตอนสุดท้ายออก มันดูไม่เหมือนหุ่นหินหนักๆ": no squash on the statue; the
            // weight comes from one short hard jolt of the hall instead.
            show.Append(shaker.DOShakeAnchorPos(.3f, new Vector2(ShakePixels * .5f, ShakePixels * 1.5f), 18, 0f, false, true));
            // his eyes burn red while the camera slowly pushes in on his face
            float stareAt = show.Duration();
            show.Append(cam.DOScale(ZoomTo, ZoomSeconds).SetEase(Ease.InOutSine));
            show.Insert(stareAt, DOTween.To(() => eyesOpen, v => { eyesOpen = v; ShowEyes(); }, 1f, ZoomSeconds).SetEase(Ease.InOutSine));
            show.AppendInterval(HoldSeconds);   // a last close stare
            // white flash, the fight starts under it, then everything fades away
            show.Append(flash.DOFade(1f, FlashSeconds * .5f));
            show.AppendCallback(() => { onRisen?.Invoke(); SetAlpha(backdrop, 0f); hallFade.alpha = 0f; kingImage.enabled = false; eyesOpen = 0f; ShowEyes(); });
            show.Append(flash.DOFade(0f, FlashSeconds * 1.5f));
            show.OnComplete(() => { show = null; kingImage.enabled = true; cam.localScale = Vector3.one; gameObject.SetActive(false); onDone?.Invoke(); });
            return true;
        }

        // Lays each beaten boss's rubble over its statue in the hall (all pieces first, then the patches, as on the boss
        // select), shaded like the hall. The pieces are the hall's children, so they zoom, shake and fade with it.
        void ShowBrokenStatues(ICollection<int> kinds)
        {
            foreach (var piece in rubble) if (piece != null) Destroy(piece.gameObject);
            rubble.Clear();
            if (kinds == null) return;
            foreach (int kind in kinds) AddRubble("Rubble " + kind, BossStatueShatter.RubbleArt(kind), BossStatueShatter.StatueBox(kind));
            foreach (int kind in kinds) AddRubble("Patch " + kind, BossStatueShatter.PatchArt(kind), BossStatueShatter.PatchBox(kind));
        }

        // box: 0..1 of the whole hall picture (bottom = 0), placed through the hall's crop (uvRect).
        void AddRubble(string name, Texture2D art, Rect box)
        {
            if (art == null || box.width <= 0f) return;
            var piece = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            piece.rectTransform.SetParent(hall.rectTransform, false);
            var crop = hall.uvRect;
            piece.rectTransform.anchorMin = new Vector2((box.xMin - crop.x) / crop.width, (box.yMin - crop.y) / crop.height);
            piece.rectTransform.anchorMax = new Vector2((box.xMax - crop.x) / crop.width, (box.yMax - crop.y) / crop.height);
            piece.rectTransform.offsetMin = piece.rectTransform.offsetMax = Vector2.zero;
            art.wrapMode = TextureWrapMode.Clamp;
            piece.texture = art;
            piece.color = hall.color;
            piece.raycastTarget = false;
            rubble.Add(piece);
        }

        // The eyes' glow: eyesOpen (0..1). "แสงไม่ต้องกระพริบ แค่ค่อยๆแดงขึ้น": no throb, it only grows steadily red.
        void ShowEyes()
        {
            eyes.enabled = eyes.texture != null && eyesOpen > 0f;
            if (!eyes.enabled) return;
            eyes.color = new Color(1f, 1f, 1f, eyesOpen);
        }

        void OnDestroy() => show?.Kill();

        static Image Fill(string name, RectTransform parent, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Stretch(image.rectTransform, parent);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static RectTransform Stretch(RectTransform rect, RectTransform parent)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static void SetAlpha(Graphic graphic, float alpha) { var c = graphic.color; c.a = alpha; graphic.color = c; }
    }
}
