using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // บาส 2026-11-18: Boss Slayer's floor gems from the art team: a pixel-art gem picture with pixel sparkles rising
    // round it, replacing the 3D gem model, the coloured disc and the square glow frames.
    // 2026-11-26 "เอารูปไปใส่แทน gems อันเดิม ตามสีเลย หลอดโหลดก็เอาไปใส่ ไว้ด้านบน แก้ particle ของ gems ... ให้เป็นสีขาว":
    //   * the gems are the art team's icons (Attack = sword on red, Heal = green, Stun = yellow, Ultimate = blue);
    //   * the charge is their horizontal load bar (GemLoadBarBG + GemLoadBar) floating just above the gem;
    //   * the sparkles are white on every gem.
    // "ปิดหลอดไว้ก่อน player ไปเหยียบค่อยเปิด ... Item แต่ละอันขนาดไม่เท่ากันเว้นช่องไฟให้สวยงาม": the bar stays hidden until
    // the gem starts charging. The four pictures have different shapes and margins, so each is sized by its own body
    // (the solid shape, measured below) to the same area and centred on it, and the bar keeps one gap above every body.
    // Art: Art/UI/Gems/Gem<Type>.png, GemLoadBar.png, GemLoadBarBG.png, GemSparkle.png. A gem type without art keeps the old look.
    public static class BossGemArt
    {
        public const string GemName = "Gem Art", ParticlesName = "Gem Particles";
        const string BarName = "Gem Charge Bar", ChargeName = "Gem Charge", TrackName = "Gem Charge Track", GoldName = "Gem Charge Gold";
        const string SparksName = "Gem Sparks";
        const string Folder = "Art/UI/Gems/";

        // Every gem's body covers the area of a square this many spot diameters wide.
        const float BodySize = 1f;
        // The load bar: its width (spot diameters), its picture's aspect (363 x 50 px) and the gap above the body.
        const float BarWidth = .85f, BarAspect = 50f / 363f, BarGap = .08f;
        // Idle motion: the gem floats up and down; it grows a little as the charge fills.
        const float BobFraction = .05f, BobSpeed = 2.2f, ChargeGrow = .14f;
        // Sparks: how many, how long each takes to rise, how far it rises and how big it is (all in spot diameters).
        const int SparkCount = 8;
        const float SparkSeconds = 1.6f, SparkRise = .75f, SparkSpread = .45f, SparkSize = .07f;
        // The sparkle picture is a small diamond inside a soft glow, so it is drawn larger than the old plain square.
        const float SparkleSize = .2f;

        static readonly Color GoldFrame = new Color(1f, .84f, .3f, 1f), NoFrame = new Color(1f, .84f, .3f, 0f);
        static Sprite barSprite;

        static string Picture(string gemModelName) => gemModelName switch
        {
            "Attack" => "GemAttack",
            "Heal" => "GemHeal",
            "Stun" => "GemStun",
            "Ultimate" => "GemUltimate",
            _ => null,
        };

        // Each picture's body (solid pixels, without the glow) in the file's pixels from the top-left: x0, y0, x1, y1.
        // Measured on the art team's files; redo it if a picture changes.
        static Vector4 Body(string picture) => picture switch
        {
            "GemAttack" => new Vector4(50, 72, 447, 471),
            "GemHeal" => new Vector4(50, 50, 391, 442),
            "GemStun" => new Vector4(50, 50, 413, 468),
            _ => new Vector4(50, 50, 418, 475),   // GemUltimate
        };

        // Puts the gem on a spot view (a circle diameter wide) and returns its charge bar (a horizontally filled Image),
        // or null when this gem type has no art. color is the gem's colour; the art carries it, so it is unused here.
        public static Image Attach(RectTransform view, string gemModelName, float diameter, Color color)
        {
            string picture = Picture(gemModelName);
            if (view == null || picture == null) return null;
            var gem = GameArt.Load<Texture2D>(Folder + picture);
            var bar = BarSprite();
            if (gem == null || bar == null) return null;

            var sparkle = GameArt.Load<Texture2D>(Folder + "GemSparkle");
            var sparks = Rect(view, SparksName, Vector2.zero);
            float sparkSize = diameter * (sparkle != null ? SparkleSize : SparkSize);
            for (int i = 0; i < SparkCount; i++)
                Raw(sparks, "Spark " + (i + 1), sparkle, new Vector2(sparkSize * (sparkle != null ? (float)sparkle.width / sparkle.height : 1f), sparkSize), Color.white);

            // sized so every body covers the same area, and centred on its body (not on the picture with its glow)
            var body = Body(picture);
            float bodyW = body.z - body.x, bodyH = body.w - body.y;
            float scale = diameter * BodySize / Mathf.Sqrt(bodyW * bodyH);   // UI units per picture pixel
            var art = Raw(view, GemName, gem, new Vector2(gem.width, gem.height) * scale, Color.white).rectTransform;
            art.pivot = new Vector2((body.x + body.z) * .5f / gem.width, 1f - (body.y + body.w) * .5f / gem.height);

            // The bar rides on the gem (so it floats with it), the same gap above every body; hidden until charging.
            var barSize = new Vector2(diameter * BarWidth, diameter * BarWidth * BarAspect);
            var holder = Rect(art, BarName, barSize);
            holder.anchorMin = holder.anchorMax = art.pivot;
            holder.anchoredPosition = new Vector2(0f, bodyH * scale * .5f + diameter * BarGap + barSize.y * .5f);
            var gold = Rect(holder, GoldName, barSize + Vector2.one * barSize.y * .35f).gameObject.AddComponent<Image>();
            gold.color = NoFrame;
            gold.raycastTarget = false;
            var track = GameArt.Load<Texture2D>(Folder + "GemLoadBarBG");
            if (track != null) Raw(holder, TrackName, track, barSize, Color.white);
            var charge = Rect(holder, ChargeName, barSize).gameObject.AddComponent<Image>();
            charge.sprite = bar;
            charge.raycastTarget = false;
            charge.type = Image.Type.Filled;
            charge.fillMethod = Image.FillMethod.Horizontal;
            charge.fillOrigin = (int)Image.OriginHorizontal.Left;
            charge.fillAmount = 0f;
            holder.gameObject.SetActive(false);
            return charge;
        }

        // Balance Note v3 §5.2 (Join Forces): a gold frame shows round the load bar while the gem is shared and worth 2.5x.
        public static void SetGolden(RectTransform view, bool golden)
        {
            if (view == null || !(view.Find(GemName + "/" + BarName + "/" + GoldName) is RectTransform frame) || !frame.TryGetComponent(out Graphic image)) return;
            var want = golden ? GoldFrame : NoFrame;
            if (image.color != want) image.color = want;
        }

        // Every frame while the spot is up. charge is 0..1. False when the view has no art.
        public static bool Animate(RectTransform view, float charge)
        {
            if (view == null || !(view.Find(GemName) is RectTransform gem)) return false;
            float seed = view.anchoredPosition.x * .013f + view.anchoredPosition.y * .007f;
            float t = Time.time;
            float diameter = view.rect.height;

            // the gem floats and grows; its bar rides on it, keeps its size and shows once a player starts charging it
            gem.anchoredPosition = new Vector2(0f, Mathf.Sin(t * BobSpeed + seed) * diameter * BobFraction);
            float grow = 1f + ChargeGrow * charge;
            gem.localScale = Vector3.one * grow;
            if (gem.Find(BarName) is RectTransform bar)
            {
                bool charging = charge > 0f;
                if (bar.gameObject.activeSelf != charging) bar.gameObject.SetActive(charging);
                bar.localScale = Vector3.one / grow;
            }

            if (view.Find(SparksName) is Transform sparks)
            {
                float speed = 1f + charge;   // they rise faster while the gem charges
                for (int i = 0; i < sparks.childCount; i++)
                {
                    var spark = (RectTransform)sparks.GetChild(i);
                    float lane = Hash(i * 7.31f + seed);
                    float life = Mathf.Repeat(t * speed / SparkSeconds + i / (float)sparks.childCount + seed, 1f);
                    float x = (lane - .5f) * 2f * SparkSpread * diameter + Mathf.Sin(t * 2.3f + i) * diameter * .04f;
                    float y = Mathf.Lerp(-.35f, -.35f + SparkRise, life) * diameter;
                    spark.anchoredPosition = new Vector2(x, y);
                    spark.localScale = Vector3.one * Mathf.Lerp(1f, .4f, life);
                    if (spark.TryGetComponent(out RawImage dot))
                    {
                        var c = dot.color;
                        c.a = Mathf.Sin(life * Mathf.PI);   // fade in, then out at the top
                        dot.color = c;
                    }
                }
            }
            return true;
        }

        static float Hash(float n) => Mathf.Repeat(Mathf.Sin(n * 12.9898f) * 43758.547f, 1f);

        static RectTransform Rect(RectTransform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        static RawImage Raw(RectTransform parent, string name, Texture2D texture, Vector2 size, Color color)
        {
            var image = Rect(parent, name, size).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // The load bar's fill picture as a sprite, so the Image can fill it from the left.
        static Sprite BarSprite()
        {
            if (barSprite != null) return barSprite;
            var texture = GameArt.Load<Texture2D>(Folder + "GemLoadBar");
            if (texture == null) return null;
            barSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            return barSprite;
        }
    }
}
