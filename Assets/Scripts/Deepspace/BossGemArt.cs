using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // บาส 2026-11-18: Boss Slayer's floor gems from the art team ("Game Play/Gem"): a pixel-art diamond over a cloud of
    // pixel particles in its colour, replacing the 3D gem model, the coloured disc and the square glow frames.
    // Attack = red, Heal = green, Stun = yellow, Ultimate = blue.
    // บาส 2026-11-18 (2): "ทำให้หลอดโหลดของ Gem มันโหลดไปตามตัว gem และ Particle มีการขยับ ไม่ใหญ่เกินตัว gem ไปมาก":
    //   * the charge bar is a pixel diamond outline just outside the gem, filling round it from the top, and it floats
    //     and grows with the gem (it was a round ring standing still);
    //   * the particle cloud is about the gem's size (was 2.2x the spot), and pixel sparks in the gem's colour rise
    //     from it and fade, so the particles move.
    // Art: Resources/BossSlayer/Gems/<Colour>.png and <Colour>Particles.png. A gem type without art keeps the old look.
    public static class BossGemArt
    {
        public const string GemName = "Gem Art", ParticlesName = "Gem Particles";
        const string ChargeName = "Gem Charge", TrackName = "Gem Charge Track", SparksName = "Gem Sparks";
        const string Folder = "BossSlayer/Gems/";

        // The diamond's height as a multiple of the spot's diameter, and the particle cloud's width (same multiple).
        const float DiamondScale = 1.15f, ParticleScale = 1.3f;
        // The diamond in the art is 135 x 199 px; the charge outline sits this much bigger around it.
        const float DiamondAspect = 135f / 199f, ChargeScale = 1.22f;
        // Idle motion: the gem floats up and down, the cloud breathes; both grow a little as the charge fills.
        const float BobFraction = .05f, BobSpeed = 2.2f, BreatheSpeed = 1.7f, ChargeGrow = .14f;
        // Sparks: how many, how long each takes to rise, how far it rises and how big it is (all in spot diameters).
        const int SparkCount = 8;
        const float SparkSeconds = 1.6f, SparkRise = .75f, SparkSpread = .45f, SparkSize = .07f;
        // The sparkle picture is a small diamond inside a soft glow, so it is drawn larger than the old plain square.
        const float SparkleSize = .2f;

        static Sprite diamondOutline;

        static string Picture(string gemModelName) => gemModelName switch
        {
            "Attack" => "Red",
            "Heal" => "Green",
            "Stun" => "Yellow",
            "Ultimate" => "Blue",
            _ => null,
        };

        // How much of each picture's height the diamond itself fills (the rest is its glow). Measured on the art.
        static float DiamondFraction(string picture) => picture == "Red" ? .515f : .745f;

        // Puts the gem on a spot view (a circle diameter wide) and returns its charge bar (a radial-filled Image),
        // or null when this gem type has no art.
        public static Image Attach(RectTransform view, string gemModelName, float diameter, Color color)
        {
            string picture = Picture(gemModelName);
            if (view == null || picture == null) return null;
            var gem = Resources.Load<Texture2D>(Folder + picture);
            if (gem == null) return null;
            color.a = 1f;
            // 2026-11-25 "gems เอา particle พื้นหลังออก เอาออกไปเลย": no particle cloud behind the gem any more.
            // 2026-11-25 (later) "particle เป็นของ gems": the art team's white diamond sparkle (GemSparkle), in the gem's
            // colour, rises and fades around every gem (the spark motion below), in place of the old square sparks.
            var sparkle = Resources.Load<Texture2D>(Folder + "GemSparkle");
            var sparks = Rect(view, SparksName, Vector2.zero);
            float sparkSize = diameter * (sparkle != null ? SparkleSize : SparkSize);
            for (int i = 0; i < SparkCount; i++)
                Raw(sparks, "Spark " + (i + 1), sparkle, new Vector2(sparkSize * (sparkle != null ? (float)sparkle.width / sparkle.height : 1f), sparkSize), color);

            float diamond = diameter * DiamondScale;
            var outline = new Vector2(diamond * DiamondAspect, diamond) * ChargeScale;
            var track = Outline(view, TrackName, outline, new Color(0f, 0f, 0f, .5f));
            track.type = Image.Type.Simple;
            var charge = Outline(view, ChargeName, outline, color);
            charge.type = Image.Type.Filled;
            charge.fillMethod = Image.FillMethod.Radial360;
            charge.fillOrigin = (int)Image.Origin360.Top;
            charge.fillClockwise = true;
            charge.fillAmount = 0f;

            float height = diamond / DiamondFraction(picture);
            Raw(view, GemName, gem, new Vector2(height * gem.width / gem.height, height), Color.white);
            return charge;
        }

        // Every frame while the spot is up. charge is 0..1. False when the view has no art.
        public static bool Animate(RectTransform view, float charge)
        {
            if (view == null || !(view.Find(GemName) is RectTransform gem)) return false;
            float seed = view.anchoredPosition.x * .013f + view.anchoredPosition.y * .007f;
            float t = Time.time;
            float diameter = view.rect.height;

            // the gem and its charge outline float and grow together
            var bob = new Vector2(0f, Mathf.Sin(t * BobSpeed + seed) * diameter * BobFraction);
            var grow = Vector3.one * (1f + ChargeGrow * charge);
            foreach (var part in new[] { gem, view.Find(TrackName) as RectTransform, view.Find(ChargeName) as RectTransform })
            {
                if (part == null) continue;
                part.anchoredPosition = bob;
                part.localScale = grow;
            }

            if (view.Find(ParticlesName) is RectTransform cloud && cloud.TryGetComponent(out RawImage image))
            {
                float breathe = .5f + .5f * Mathf.Sin(t * BreatheSpeed + seed * 2f);
                cloud.localScale = Vector3.one * (1f + .06f * breathe + ChargeGrow * charge);
                cloud.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * .9f + seed) * 4f);
                image.color = new Color(1f, 1f, 1f, Mathf.Lerp(.55f, 1f, Mathf.Max(breathe * .6f, charge)));
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

        static Image Outline(RectTransform parent, string name, Vector2 size, Color color)
        {
            var image = Rect(parent, name, size).gameObject.AddComponent<Image>();
            image.sprite = DiamondOutline();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // A pixel-art diamond outline in the gem's proportions (point-filtered so its steps stay crisp).
        static Sprite DiamondOutline()
        {
            if (diamondOutline != null) return diamondOutline;
            const int w = 68, h = 100;
            const float thickness = .11f;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Boss Gem Charge Outline", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // distance in the diamond's own shape: 1 on its edge, 0 at the centre
                    float d = Mathf.Abs((x + .5f) / w - .5f) * 2f + Mathf.Abs((y + .5f) / h - .5f) * 2f;
                    bool on = d <= 1f && d >= 1f - thickness;
                    pixels[y * w + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            diamondOutline = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f);
            return diamondOutline;
        }
    }
}
