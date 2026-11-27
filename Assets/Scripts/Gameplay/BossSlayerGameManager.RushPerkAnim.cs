using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-27 "animation ตอนเปิดการ์ด ... ใส่ animation เลือกการ์ด ให้รู้ว่า player เลือกไปไหน": the perk vote's
    // reveal and pick, after the user's Pinterest reference (a gold card draw):
    //   reveal - light bursts from the top of the wall, the card backs land one by one from left to right glowing
    //            gold, the glow settles, then each card's face dissolves in over its back. The floor pads pop in on
    //            the same beat. Votes count only once the faces are showing.
    //   voting - 2026-11-27 "ไม่ใส่ดีกว่า ว่า player คนไหนเลือก": no name tags (six overflowed); each card's vote count says it.
    //   pick   - the team's card flashes, swells and slides to the middle while the others sink and fade; the break
    //            ends as soon as this is over (see TickRush).
    // Everything is worked out from the clock every frame (no tweens), so a reroll simply starts the reveal again.
    public sealed partial class BossSlayerGameManager
    {
        // Reveal timing (seconds after perkRevealAt).
        const float RevealFirstCard = .25f, RevealCardStagger = .15f, RevealLand = .4f, RevealGlowFade = .7f;
        const float RevealDissolveStart = 1.2f, RevealDissolveStagger = .1f, RevealDissolve = .5f;
        // Votes are ignored until every face is showing.
        const float RevealSeconds = RevealDissolveStart + 2 * RevealDissolveStagger + RevealDissolve;
        // How long the pick plays before the break ends: the slide to the middle, the climax, then a moment to enjoy it.
        const float PerkPickShowSeconds = 2.6f;
        // 2026-11-27 "ตอนเลือกการ์ดมาอยู่กลางจอ ... ให้มี Effect อลังการแต่ไม่เยอะ": as the team's card lands in the middle,
        // one climax - rays in the card's tier colour open behind it, a single shockwave ring, a dozen sparkles and a
        // punch on the card. Seconds after the pick (the slide ends at .9).
        // (2026-11-27 "เอา Effect การ์ดแบบตอนแรกสุด ดีสุดละ": the later extras - wall flash, second ring, falling beam,
        // bottom-edge hit and the floor's effects - were taken out again; the floor stays clear apart from its buttons.)
        const float ClimaxAt = .85f, ClimaxRays = 1.5f, ClimaxRing = .7f, ClimaxSparkles = 1.1f, ClimaxPunch = .35f;
        const int ClimaxSparkleCount = 12;
        // The floor buttons' exit: grow, then shrink away (the chosen pad on the pick, the reroll pad when it closes).
        const float PopAwaySeconds = .6f;
        // 2026-11-27 "ให้การ์ดมี animation ลอยขึ้นลงเล็กน้อย": once landed, the wall cards bob gently, each a little out of
        // step with its neighbour. Height as a share of the card's height; the bob settles out as the pick starts.
        const float BobHeight = .012f, BobSpeed = .5f, BobPhaseStep = 1.1f, BobSettle = .3f;

        sealed class WallCardFx
        {
            public RectTransform Card;
            public Vector2 BasePosition;
            public CanvasGroup Group, Back;
            public Image Flash;
        }

        readonly List<WallCardFx> perkWallFx = new();
        RawImage perkWallBurst, perkClimaxRays, perkClimaxRing;
        readonly List<(RectTransform rect, Image image, Vector2 direction, float distance, float size)> perkClimaxSparkles = new();
        bool perkClimaxSounded;
        static Texture2D perkRingTexture;
        static Sprite perkSparkleSprite;
        float perkRevealAt = -99f, perkPickAt = -99f;
        int perkRevealSounded;
        static Texture2D perkBurstTexture;

        float PerkRevealTime => Time.unscaledTime - perkRevealAt;
        bool PerkRevealDone => PerkRevealTime >= RevealSeconds;

        void StartPerkReveal()
        {
            perkRevealAt = Time.unscaledTime; perkPickAt = -99f; perkRevealSounded = 0;
        }

        void StartPerkPick() { perkPickAt = Time.unscaledTime; perkClimaxSounded = false; }

        // ---------------------------------------------------------------- build
        // The card back (a dark plate in the gold frame with a diamond seal) and the flash over it.
        void BuildWallCardFx(RectTransform card, CanvasGroup group, Rect area, float width, float height)
        {
            var back = new GameObject("Back", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            back.SetParent(card, false); UiThemeKit.Stretch(back, 0);
            // the card's own silhouette with nothing on it: a dark plate, the frame and an empty text box
            var fill = NewFxImage(back, "Back Fill", BossFantasyTheme.Night);
            SetCardAnchors(fill.rectTransform, IconInset, 1 - IconInset, IconTop, CardTall - IconTop - .04f);
            CardPicture(back, "Back Frame", PerkTexture(PerkArtFolder + "SkillFrame"), 0, 1, FrameTop, FrameHeight);
            CardPicture(back, "Back Box", PerkTexture(PerkArtFolder + "BG Text"), BodyInset, 1 - BodyInset, BodyTop, BodyHeight);
            // a gold diamond seal in the picture's place
            var seal = new GameObject("Seal", typeof(RectTransform)).GetComponent<RectTransform>();
            seal.SetParent(back, false);
            SetCardAnchors(seal, 0, 1, IconTop, IconHeight);
            for (int i = 0; i < 3; i++)
            {
                float size = width * new[] { .46f, .34f, .12f }[i];
                var diamond = NewFxImage(seal, "Seal " + i, i == 1 ? BossFantasyTheme.Night : UiThemeKit.Alpha(BossFantasyTheme.Gold, i == 0 ? .85f : 1f));
                var r = diamond.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.sizeDelta = new Vector2(size, size);
                r.localRotation = Quaternion.Euler(0, 0, 45f);
            }

            var flash = NewFxImage(card, "Flash", Color.clear);
            SetCardAnchors(flash.rectTransform, IconInset, 1 - IconInset, IconTop, CardTall - IconTop - .04f);

            perkWallFx.Add(new WallCardFx { Card = card, BasePosition = card.anchoredPosition, Group = group, Back = back.GetComponent<CanvasGroup>(), Flash = flash });
            if (perkWallBurst == null) BuildWallBurst((RectTransform)card.parent, area);
        }

        // The burst of light the cards are drawn out of, above the middle card.
        void BuildWallBurst(RectTransform root, Rect area)
        {
            perkWallBurst = new GameObject("Reveal Burst", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            var rect = perkWallBurst.rectTransform;
            rect.SetParent(root, false);
            rect.SetSiblingIndex(1);   // over the backdrop, under the cards
            float size = area.height * 1.3f;
            Place(rect, new Rect(area.center.x - size / 2, area.yMax - area.height * .2f - size / 2, size, size), area);
            perkWallBurst.texture = BurstTexture(); perkWallBurst.raycastTarget = false;
            perkWallBurst.color = Color.clear;
            BuildPickClimax(root, area);
        }

        // The pick's climax pieces, hidden until a card is chosen: rays and the ring go under the chosen card, sparkles over it.
        void BuildPickClimax(RectTransform root, Rect area)
        {
            float size = area.height * .9f;
            perkClimaxRays = ClimaxImage(root, "Pick Rays", BurstTexture(), size);
            perkClimaxRing = ClimaxImage(root, "Pick Ring", RingTexture(), size);
            var random = new System.Random(7);
            for (int i = 0; i < ClimaxSparkleCount; i++)
            {
                var image = NewFxImage(root, "Pick Sparkle " + i, Color.clear);
                image.sprite = SparkleSprite();
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                // spread evenly round the card with a little jitter, so it reads as a burst and not a spray
                float angle = (i + (float)random.NextDouble() * .6f) / ClimaxSparkleCount * Mathf.PI * 2f;
                perkClimaxSparkles.Add((rect, image, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                    area.height * (.28f + .14f * (float)random.NextDouble()), area.height * (.035f + .025f * (float)random.NextDouble())));
            }
        }

        static RawImage ClimaxImage(RectTransform root, string name, Texture2D texture, float size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            var rect = image.rectTransform;
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(size, size);
            image.texture = texture; image.raycastTarget = false; image.color = Color.clear;
            return image;
        }

        static Image NewFxImage(RectTransform parent, string name, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.color = color; image.raycastTarget = false;
            return image;
        }

        // Soft rays fading out from the centre.
        static Texture2D BurstTexture()
        {
            if (perkBurstTexture != null) return perkBurstTexture;
            const int size = 256;
            perkBurstTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Perk Burst" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2 - 1, dy = (y + .5f) / size * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float rays = .35f + .65f * Mathf.Pow(Mathf.Abs(Mathf.Cos(Mathf.Atan2(dy, dx) * 7f)), 8f);
                    float a = Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d);
                    a = Mathf.Clamp01(a * rays + Mathf.Clamp01(1 - d * 3f) * .8f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            perkBurstTexture.SetPixels32(pixels); perkBurstTexture.Apply();
            return perkBurstTexture;
        }

        // A thin bright ring with a soft inner glow, for the shockwave.
        static Texture2D RingTexture()
        {
            if (perkRingTexture != null) return perkRingTexture;
            const int size = 256;
            perkRingTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Perk Ring" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2 - 1, dy = (y + .5f) / size * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Exp(-Mathf.Pow((d - .9f) / .035f, 2));
                    float inner = d < .92f ? Mathf.Clamp01((d - .55f) / .35f) * .25f : 0f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(edge + inner) * 255));
                }
            perkRingTexture.SetPixels32(pixels); perkRingTexture.Apply();
            return perkRingTexture;
        }

        // A four-point star with a glowing core.
        static Sprite SparkleSprite()
        {
            if (perkSparkleSprite != null) return perkSparkleSprite;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Perk Sparkle" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x + .5f) / size * 2 - 1), dy = Mathf.Abs((y + .5f) / size * 2 - 1);
                    float star = Mathf.Clamp01(1 - (Mathf.Sqrt(dx) + Mathf.Sqrt(dy)) / 1.05f);
                    float core = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy) * 2.2f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(star * 2.2f + core) * 255));
                }
            texture.SetPixels32(pixels); texture.Apply();
            perkSparkleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
            return perkSparkleSprite;
        }

        // ---------------------------------------------------------------- per frame
        void AnimateWallPerkCard(int index, bool chosen, Color tint)
        {
            if (index >= perkWallFx.Count) return;
            var fx = perkWallFx[index];
            float r = PerkRevealTime, appear = RevealFirstCard + RevealCardStagger * index;
            float land = Mathf.Clamp01((r - appear) / RevealLand);
            float alpha = Mathf.Clamp01((r - appear) / .2f);
            float glow = alpha * (1 - Mathf.Clamp01((r - appear - .15f) / RevealGlowFade));
            float dissolve = Mathf.Clamp01((r - RevealDissolveStart - RevealDissolveStagger * index) / RevealDissolve);
            float scale = 1 + .1f * (1 - EaseOutCubic(land));
            Vector2 position = fx.BasePosition + new Vector2(0, fx.Card.rect.height * .06f * (1 - EaseOutCubic(land)));
            if (r >= appear && perkRevealSounded <= index) { perkRevealSounded = index + 1; BossFightAudio.PlayUi("hover", .45f, .02f); }

            Color flash = UiThemeKit.Alpha(Color.Lerp(BossFantasyTheme.Gold, Color.white, .45f), glow * .85f);
            if (rushPicked && perkPickAt > 0)
            {
                float p = Time.unscaledTime - perkPickAt;
                if (chosen)
                {
                    scale = 1 + .16f * EaseOutBack(Mathf.Clamp01(p / .45f));
                    // the punch as it lands in the middle
                    float punch = Mathf.Clamp01((p - ClimaxAt) / ClimaxPunch);
                    if (punch > 0f && punch < 1f) scale += .1f * Mathf.Sin(punch * Mathf.PI) * (1 - punch);
                    float slide = EaseInOutSine(Mathf.Clamp01((p - .3f) / .6f));
                    var middle = perkWallFx.Count > 1 ? perkWallFx[perkWallFx.Count / 2].BasePosition.x : fx.BasePosition.x;
                    position = new Vector2(Mathf.Lerp(fx.BasePosition.x, middle, slide), fx.BasePosition.y);
                    flash = UiThemeKit.Alpha(Color.Lerp(tint, Color.white, .5f), (1 - Mathf.Clamp01(p / .6f)) * .8f);
                    // a second, shorter white flash on the landing
                    float landFlash = 1 - Mathf.Clamp01((p - ClimaxAt) / .25f);
                    if (p >= ClimaxAt && landFlash * .7f > flash.a) flash = UiThemeKit.Alpha(Color.white, landFlash * .7f);
                    fx.Card.SetAsLastSibling();
                    AnimatePickClimax(fx, position, p, tint);
                }
                else
                {
                    float sink = Mathf.Clamp01(p / .4f);
                    alpha *= 1 - sink;
                    scale *= 1 - .15f * sink;
                    position.y -= fx.Card.rect.height * .08f * sink;
                }
            }
            float bob = EaseOutCubic(land);
            if (rushPicked && perkPickAt > 0) bob *= 1 - Mathf.Clamp01((Time.unscaledTime - perkPickAt) / BobSettle);
            position.y += fx.Card.rect.height * BobHeight * bob * Mathf.Sin(Time.unscaledTime * BobSpeed * Mathf.PI + index * BobPhaseStep);

            fx.Group.alpha = alpha;
            fx.Card.localScale = Vector3.one * scale;
            fx.Card.anchoredPosition = position;
            fx.Back.alpha = 1 - dissolve;
            fx.Flash.color = flash;

            if (index == 0 && perkWallBurst != null)
            {
                float burst = Mathf.Clamp01(r / .3f) * (1 - Mathf.Clamp01((r - .5f) / 1f));
                perkWallBurst.color = UiThemeKit.Alpha(BossFantasyTheme.Gold, burst * .9f);
                perkWallBurst.rectTransform.localScale = Vector3.one * (.7f + .5f * EaseOutCubic(Mathf.Clamp01(r / 1.2f)));
                perkWallBurst.rectTransform.localRotation = Quaternion.Euler(0, 0, r * 12f);
            }
        }

        // The climax around the chosen card, centred on where the card is this frame. Hidden before and after it plays.
        void AnimatePickClimax(WallCardFx fx, Vector2 cardCentre, float p, Color tint)
        {
            if (perkClimaxRays == null) return;
            float t = p - ClimaxAt;
            if (t >= 0f && !perkClimaxSounded) { perkClimaxSounded = true; sfx?.Play("ultimate_ready", .6f, 0, .2f, 4); }
            Color bright = Color.Lerp(tint, Color.white, .35f);
            // the rays and ring sit just under the chosen card
            int under = Mathf.Max(0, fx.Card.GetSiblingIndex() - 1);
            perkClimaxRays.rectTransform.SetSiblingIndex(under);
            perkClimaxRing.rectTransform.SetSiblingIndex(under);

            float rays = Mathf.Clamp01(t / ClimaxRays);
            float raysAlpha = t < 0f ? 0f : Mathf.Clamp01(t / .12f) * (1 - EaseInOutSine(rays));
            perkClimaxRays.color = UiThemeKit.Alpha(bright, raysAlpha * .85f);
            perkClimaxRays.rectTransform.anchoredPosition = cardCentre;
            perkClimaxRays.rectTransform.localScale = Vector3.one * (.5f + 1.1f * EaseOutCubic(rays));
            perkClimaxRays.rectTransform.localRotation = Quaternion.Euler(0, 0, -t * 25f);

            float ring = Mathf.Clamp01(t / ClimaxRing);
            perkClimaxRing.color = UiThemeKit.Alpha(Color.Lerp(tint, Color.white, .6f), t < 0f ? 0f : (1 - ring) * .9f);
            perkClimaxRing.rectTransform.anchoredPosition = cardCentre;
            perkClimaxRing.rectTransform.localScale = Vector3.one * (.25f + 1.6f * EaseOutCubic(ring));

            float spark = Mathf.Clamp01(t / ClimaxSparkles);
            foreach (var sparkle in perkClimaxSparkles)
            {
                sparkle.rect.SetAsLastSibling();
                if (t < 0f || spark >= 1f) { sparkle.image.color = Color.clear; continue; }
                float travel = EaseOutCubic(spark);
                sparkle.rect.anchoredPosition = cardCentre + sparkle.direction * sparkle.distance * (.35f + .65f * travel);
                float twinkle = .75f + .25f * Mathf.Sin(t * 30f + sparkle.distance);
                sparkle.rect.sizeDelta = Vector2.one * sparkle.size * (1 - spark * .6f) * twinkle;
                sparkle.rect.localRotation = Quaternion.Euler(0, 0, t * 90f);
                sparkle.image.color = UiThemeKit.Alpha(spark < .5f ? Color.white : bright, 1 - spark * spark);
            }
        }

        // Scale and alpha for floor pad index on the same beat as its wall card.
        (float scale, float alpha) FloorPerkReveal(int index, bool chosen)
        {
            float r = PerkRevealTime, appear = RevealFirstCard + RevealCardStagger * index;
            float scale = EaseOutBack(Mathf.Clamp01((r - appear) / .35f)), alpha = Mathf.Clamp01((r - appear) / .2f);
            if (rushPicked && perkPickAt > 0)
            {
                float since = Time.unscaledTime - perkPickAt;
                float p = Mathf.Clamp01(since / .4f);
                // the team's pad grows, then shrinks away; the others fade
                if (chosen) { scale *= PopAway(since / PopAwaySeconds); alpha *= 1 - Mathf.Clamp01((since / PopAwaySeconds - .7f) / .3f); }
                else { alpha *= 1 - p; scale *= 1 - .2f * p; }
            }
            return (scale, alpha);
        }

        // u 0..1: grows to 1.2 over the first third, then shrinks to nothing.
        static float PopAway(float u)
        {
            u = Mathf.Clamp01(u);
            if (u < .35f) return 1f + .2f * EaseOutCubic(u / .35f);
            float k = (u - .35f) / .65f;
            return 1.2f * (1f - k * k);
        }

        static float EaseOutCubic(float t) { t = 1 - t; return 1 - t * t * t; }
        static float EaseInOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1) / 2;
        static float EaseOutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1; float u = t - 1; return 1 + c3 * u * u * u + c1 * u * u; }
    }
}
