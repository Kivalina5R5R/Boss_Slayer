using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // บาส 2026-11-18: Boss Slayer's floor player select (Display 1) follows the art team's mockup ("Select Player"):
    // the rune-circle floor, and one gold-framed card per player in a row across the middle. A free card is dark with a
    // cream number and PLAYER on its gold band; a taken card turns gold with a dark number and READY. While someone
    // stands on a free card it glows toward gold. No title, hint or status line, as in the mockup.
    // Built through PlayerPadSelect's Decorate hooks (so the Editor bake keeps it) and only for Boss Slayer: the shared
    // PlayerPadSelect / PlayerPadArt and the other games are not touched.
    // Art: Resources/BossSlayer/PlayerSelectCards (Floor, Card, CardReady: the art team's pictures). The number and the
    // word are text in Jersey 15 (บาส 2026-11-18, Google Fonts, SIL OFL: Resources/BossSlayer/Fonts), made into a dynamic
    // TextMeshPro font when first needed, so there is no font asset to bake.
    public static class BossPlayerCards
    {
        const string Folder = "Art/UI/PlayerSelectCards";

        // The card's frame (the part players stand on) as a floor fraction. Six cards span .1 to .9 like the mockup.
        const float CardWidth = .087f, Step = .16f, CenterY = .5f;

        // The card picture is 460x544 with its frame at x 97..360, y 78..440 (glow and side gems lie outside it).
        const float BodyWidthPx = 263f, BodyHeightPx = 362f;
        static readonly Vector4 ArtAroundBody = new(-97f / BodyWidthPx, -104f / BodyHeightPx, 1f + 100f / BodyWidthPx, 1f + 78f / BodyHeightPx);

        // Inside the frame: the number in the dark part, the word on the gold band.
        static readonly Vector4 NumberBox = new(.18f, .32f, .82f, .88f);
        static readonly Vector4 WordBox = new(.06f, .045f, .94f, .225f);
        const string FontPath = "Fonts/Jersey15-Regular";
        public const string FontAssetPath = "Fonts/Jersey15 SDF";

        static readonly Color CreamInk = new Color32(250, 207, 140, 255);
        static readonly Color BrownInk = new Color32(44, 26, 11, 255);
        static readonly Color ReadyInk = new Color32(253, 232, 190, 255);

        // How strongly the gold card shows through while a player holds a free card (1 = taken).
        const float HoldGlow = .55f;
        const float HoldGrow = .05f;

        // 2026-11-19: "ถ้า player ไปโดนให้มี animation เด้งรับ" - a card wobbles when a player steps onto it (free or
        // taken), the same springy punch the boss cards give, so players can see they reached it.
        const float StepBounce = .16f, StepBounceSeconds = .45f, StepBounceWobbles = 2.5f;
        static readonly float[] bouncedAt = { -9f, -9f, -9f, -9f, -9f, -9f };

        static readonly Dictionary<string, Texture2D> art = new();
        static TMP_FontAsset jersey;

        public static void Apply(PlayerPadSelect.Style style, Func<RectTransform> floor)
        {
            style.PlainPads = true;
            style.Sound = id => BossFightAudio.PlayUi(id, .5f, .03f);
            style.CursorRing = null;
            style.PadLabel = k => (k + 1).ToString();
            style.PadRect = (k, n) => CardRect(floor?.Invoke(), k, n);
            style.DecorateFloor = DecorateFloor;
            style.DecoratePad = DecoratePad;
            style.AnimatePad = Animate;
        }

        // Card k of n: a centred row, one Step apart, the frame's own aspect kept on the floor.
        static Rect CardRect(RectTransform floor, int k, int n)
        {
            float aspect = floor != null && floor.rect.height > 0 ? floor.rect.width / floor.rect.height : FloorDisplaySettings.Width / (float)FloorDisplaySettings.Height;
            float height = CardWidth * BodyHeightPx / BodyWidthPx * aspect;
            float centerX = .5f + (k - (n - 1) / 2f) * Step;
            return new Rect(centerX - CardWidth / 2, CenterY - height / 2, CardWidth, height);
        }

        static void DecorateFloor(RectTransform floor)
        {
            // earlier looks' pieces in a baked scene
            // (the gilded frame and the title's gold rules too: บาส 2026-11-18 "ลบพวกขอบจากอันเดิมออก")
            foreach (var old in new[] { "Codex Backdrop", "Theme Backdrop", "Status Plaque", "Gilded Border", "Title Rule Left", "Title Rule Right" })
                Drop(floor.Find(old));
            foreach (var text in new[] { "Title", "Hint", "Status" })
                if (floor.Find(text) is Transform t) t.gameObject.SetActive(false);

            var backdrop = Raw(floor, "Card Backdrop", "Floor");
            UiThemeKit.Stretch(backdrop.rectTransform, 0);
            backdrop.transform.SetAsFirstSibling();
            BossFloorMagicVideo.Attach(backdrop);   // the looping rune glow (บาส 2026-11-18)
        }

        static void DecoratePad(RectTransform pad, int k)
        {
            // earlier looks' pieces in a baked scene
            Drop(pad.Find("Sigil"));
            Drop(pad.Find("Plaque"));
            // earlier versions' pictures of the lettering
            Drop(pad.Find("Number Art"));
            Drop(pad.Find("Word Art"));
            if (pad.Find("Caption") is Transform caption) caption.gameObject.SetActive(false);

            Around(Raw(pad, "Card", "Card").rectTransform);
            Around(Raw(pad, "Card Ready", "CardReady").rectTransform);
            Letter(pad, "Number", NumberBox);
            Letter(pad, "State", WordBox);

            int order = 0;
            foreach (var part in new[] { "Card", "Card Ready", "Number", "State" })
                if (pad.Find(part) is Transform p) p.SetSiblingIndex(order++);
            Animate(pad, k, false, 0);
        }

        // Card k (0-based) wobbles once from now (BossPlayerReadySelection calls it when a player steps onto it).
        public static void Bounce(int k)
        {
            if (k >= 0 && k < bouncedAt.Length) bouncedAt[k] = Time.unscaledTime;
        }

        // The card's size factor for its step bounce: a few shrinking wobbles, then exactly 1.
        static float BounceScale(int k)
        {
            if (k < 0 || k >= bouncedAt.Length) return 1f;
            float u = (Time.unscaledTime - bouncedAt[k]) / StepBounceSeconds;
            if (u < 0f || u >= 1f) return 1f;
            return 1f + StepBounce * Mathf.Sin(u * Mathf.PI * StepBounceWobbles) * (1f - u);
        }

        static void Around(RectTransform card) => UiThemeKit.Place(card, ArtAroundBody.x, ArtAroundBody.y, ArtAroundBody.z, ArtAroundBody.w);

        // Free: dark card, cream number, PLAYER. Holding: the gold card fades in and the card grows a little.
        // Taken: gold card, dark number, READY.
        static void Animate(RectTransform pad, int k, bool owned, float progress)
        {
            float gold = owned ? 1f : progress * HoldGlow;
            if (pad.Find("Card Ready") is Transform r && r.TryGetComponent(out RawImage ready)) ready.color = UiThemeKit.Alpha(Color.white, gold);
            if (pad.Find("Number") is Transform n && n.TryGetComponent(out TMP_Text number)) number.color = owned ? BrownInk : CreamInk;
            if (pad.Find("State") is Transform w && w.TryGetComponent(out TMP_Text word))
            {
                // after PlayerPadSelect has written its own state (READY / HOLD... / STAND HERE) this frame
                string text = owned ? "READY" : "PLAYER";
                if (word.text != text) word.text = text;
                word.color = owned ? ReadyInk : BrownInk;
            }
            if (!owned && progress > 0) pad.localScale *= 1f + HoldGrow * progress;   // PlayerPadSelect resets the scale every frame
            pad.localScale *= BounceScale(k);
        }

        // PlayerPadSelect's own number / state text, moved into the card and set in Jersey 15, sized to fit its box.
        static void Letter(RectTransform pad, string name, Vector4 box)
        {
            if (!(pad.Find(name) is RectTransform rect) || !rect.TryGetComponent(out TMP_Text text)) return;
            rect.gameObject.SetActive(true);
            UiThemeKit.Place(rect, box.x, box.y, box.z, box.w);
            var font = Jersey();
            if (font != null) { text.font = font; text.fontSharedMaterial = font.material; }
            text.fontStyle = FontStyles.Normal; text.enableVertexGradient = false;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true; text.fontSizeMin = 8; text.fontSizeMax = 1000;
            text.margin = Vector4.zero; text.characterSpacing = 0;
            text.raycastTarget = false;
        }

        // Shared with the wall's Start screen (BossStartWall) and the boss cards, so every screen uses the same font.
        // The saved static asset (made by the Bake Player Select tool: a scene can only keep text that points at a saved
        // font) comes first; without it the TTF becomes a dynamic font at run time.
        public static TMP_FontAsset Jersey()
        {
            if (jersey != null) return jersey;
            jersey = GameArt.Load<TMP_FontAsset>(FontAssetPath);
            if (jersey != null) return jersey;
            var source = GameArt.Load<Font>(FontPath);
            if (source == null) { Debug.LogWarning($"[Boss player cards] Missing Resources/{FontPath}.ttf"); return null; }
            jersey = TMP_FontAsset.CreateFontAsset(source);
            if (jersey != null) jersey.name = "Jersey 15 (dynamic)";
            return jersey;
        }

        static RawImage Raw(RectTransform parent, string name, string picture)
        {
            var rect = UiThemeKit.Child(parent, name);
            rect.gameObject.SetActive(true);
            if (!rect.TryGetComponent(out RawImage image)) image = rect.gameObject.AddComponent<RawImage>();
            image.texture = Art(picture); image.color = Color.white; image.raycastTarget = false;
            return image;
        }

        static Texture2D Art(string picture)
        {
            if (art.TryGetValue(picture, out var texture) && texture != null) return texture;
            texture = GameArt.Load<Texture2D>(Folder + "/" + picture);
            if (texture == null) Debug.LogWarning($"[Boss player cards] Missing Resources/{Folder}/{picture}");
            art[picture] = texture;
            return texture;
        }

        static void Drop(Transform old)
        {
            if (old == null) return;
            old.SetParent(null, false);   // gone from Find at once (Destroy only lands at the end of the frame)
            if (Application.isPlaying) UnityEngine.Object.Destroy(old.gameObject); else UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
    }
}
