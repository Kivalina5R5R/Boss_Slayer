using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-19: the art team's "BG Fire" set (Downloads/Boss Slayer/Game Play/BG Fire) on the gameplay wall: over the
    // fire wall picture (BGFireWall) it lays their "Red Alpha" glow along the bottom and sends their red "Particle"
    // shards drifting up the wall ("เติม alpha จอกำแพงและ particle ด้วย"). Lives on a full-size layer under the wall's
    // RawImage, so it moves with the wall (shakes included); shown only while the wall shows the Fire art.
    public sealed class BossFireWall : MonoBehaviour
    {
        const string Folder = "Art/BG/Fire/";
        // 2026-11-24 "alpha ไม่ควรเต็มจอ ควรครึ่งหนึ่ง" + "เริ่มลดตั้งแต่ 30% ก่อนจะถึงตรงกลาง ค่อยๆ fade ไป": the glow covers the
        // bottom half of the wall (was 81%, the picture's own 1757/2160). Its alpha starts easing down FadeSpan of the wall
        // height below the middle and is gone at the middle (50%). 2026-11-24 "red alpha ปรับเป็น 1 จาก 0.6": the fade now
        // runs the glow's whole height (Fade 1), from full at the bottom edge to clear at the middle.
        const float GlowHeight = .5f, FadeSpan = .5f, GlowFade = FadeSpan / GlowHeight, GlowBreatheSeconds = 4f, GlowBreatheDepth = .15f;
        // 2026-11-26 "ลดค่า Alpha ของ Alpha light ของแต่ละแมพลง 40% มันบังฉาก": every map's glow (Fire's Red Alpha, Water's and
        // Land's Alpha Light) shows at 60% of its old strength. Then "ลดลงอีก มันก็ยังบังแมพอยู่": .6 -> .35.
        const float GlowStrength = .35f;
        // Particles: how many, their height (wall pixels of 2160), how high they climb and how fast.
        const int ParticleCount = 36;
        const float SizeMin = 45f, SizeMax = 120f, RiseMin = 900f, RiseMax = 1900f, SpeedMin = 90f, SpeedMax = 180f;

        struct Shard
        {
            public RectTransform Rect;
            public RawImage Image;
            public float X, Rise, Travel, Speed, Size, Sway, SwayRate, SwayPhase, Spin, Angle, Bright;
        }

        RawImage glow;
        Shard[] shards;
        // 2026-11-25 "ฉากดินกับน้ำ ... เอา Alpha Light ไปใส่ด้วยเป็นสีของตัวเอง ค่าและตำแหน่งทุกอย่างเหมือน red alpha": the
        // Water and Land walls get the art team's white "Alpha Light" in their own colour, laid out exactly like the
        // Red Alpha (same height, fade and breathing). The rising particles stay Fire's only.
        Texture redAlpha, fireParticle;
        Color glowTint = Color.white;
        bool particlesOn = true;
        int particlesShown = ParticleCount;   // how many of the shards rise (SetElement's share)
        float aspect = 190f / 242f;   // the particle picture's width / height

        public static BossFireWall Attach(RawImage wall)
        {
            if (wall == null) return null;
            var layer = BossHallSpace.Layer(wall.rectTransform, "Fire Wall");
            if (!layer.TryGetComponent(out BossFireWall view)) view = layer.gameObject.AddComponent<BossFireWall>();
            view.Build(layer);
            return view;
        }

        public void Show(bool on)
        {
            if (gameObject.activeSelf == on) return;
            gameObject.SetActive(on);
            if (on) Scatter();
        }

        void Build(RectTransform layer)
        {
            glow = BossHallSpace.Sprite(layer, "Red Alpha");
            redAlpha = GameArt.Load<Texture2D>(Folder + "RedAlpha");
            var g = glow.rectTransform;
            g.anchorMin = Vector2.zero; g.anchorMax = new Vector2(1f, GlowHeight);
            g.offsetMin = g.offsetMax = Vector2.zero;
            SetGlowTexture(redAlpha);
            if (!glow.TryGetComponent(out BossVerticalFade fadeOut)) fadeOut = glow.gameObject.AddComponent<BossVerticalFade>();
            fadeOut.SetFade(GlowFade);

            var particle = GameArt.Load<Texture2D>(Folder + "Particle");
            fireParticle = particle;
            if (particle != null) aspect = (float)particle.width / particle.height;
            shards = new Shard[ParticleCount];
            for (int i = 0; i < ParticleCount; i++)
            {
                var image = BossHallSpace.Sprite(layer, "Fire Particle " + i);
                image.texture = particle;
                shards[i] = new Shard { Rect = image.rectTransform, Image = image };
            }
            Scatter();
        }

        // Its bottom part at the art's own size (Red Alpha is 1757 of the wall's 2160 tall), not squashed.
        void SetGlowTexture(Texture texture)
        {
            glow.texture = texture;
            float artHeight = texture != null ? texture.height : 1757f;
            glow.uvRect = new Rect(0f, 0f, 1f, Mathf.Min(1f, GlowHeight * 2160f / artHeight));
        }

        // Fire: Red Alpha as painted plus its particles. Other elements: glowTexture (white) in tint, and their own
        // particle picture rising the same way (2026-11-25: Water's ice shard, Land's rock; null = no particles).
        // share: the part of the ParticleCount that rises (1 = all of them).
        public void SetElement(Texture glowTexture, Color tint, Texture particle, float share = 1f)
        {
            if (glow == null) return;
            SetGlowTexture(glowTexture != null ? glowTexture : redAlpha);
            glowTint = tint;
            particlesOn = particle != null;
            particlesShown = Mathf.Clamp(Mathf.RoundToInt(ParticleCount * share), 0, ParticleCount);
            if (particle != null) aspect = (float)particle.width / particle.height;
            if (shards == null) return;
            for (int i = 0; i < shards.Length; i++)
            {
                var s = shards[i];
                if (s.Rect == null) continue;
                bool on = particlesOn && i < particlesShown;
                s.Rect.gameObject.SetActive(on);
                if (on) s.Image.texture = particle;
            }
        }

        public void SetFire() => SetElement(redAlpha, Color.white, fireParticle);

        // Spreads the particles over their whole climb, so the wall starts full.
        void Scatter()
        {
            if (shards == null) return;
            for (int i = 0; i < shards.Length; i++) Respawn(ref shards[i], Random.value);
        }

        static void Respawn(ref Shard s, float progress)
        {
            s.X = Random.value;
            s.Travel = Random.Range(RiseMin, RiseMax);
            s.Rise = s.Travel * progress;
            s.Speed = Random.Range(SpeedMin, SpeedMax);
            s.Size = Mathf.Lerp(SizeMin, SizeMax, Random.value * Random.value);
            s.Sway = Random.Range(15f, 60f);
            s.SwayRate = Random.Range(.25f, .7f);
            s.SwayPhase = Random.Range(0f, Mathf.PI * 2f);
            s.Spin = Random.Range(-90f, 90f);
            s.Angle = Random.Range(0f, 360f);
            s.Bright = Random.Range(.55f, 1f);
        }

        void Update()
        {
            if (shards == null) return;
            float dt = Time.unscaledDeltaTime, time = Time.unscaledTime;
            if (glow != null)
            {
                var c = glowTint;
                c.a *= GlowStrength * (1f - GlowBreatheDepth * (.5f + .5f * Mathf.Sin(time * 2f * Mathf.PI / GlowBreatheSeconds)));
                glow.color = c;
            }
            if (!particlesOn) return;

            var area = ((RectTransform)transform).rect;
            float unit = area.height / BossHallSpace.DesignH;
            float widthInWallPixels = area.height > 0f ? BossHallSpace.DesignH * area.width / area.height : BossHallSpace.DesignW;
            for (int i = 0; i < particlesShown && i < shards.Length; i++)
            {
                ref var s = ref shards[i];
                s.Rise += dt * s.Speed;
                if (s.Rise >= s.Travel) Respawn(ref s, 0f);
                float u = s.Rise / s.Travel;
                s.Angle += s.Spin * dt;
                float x = s.X + s.Sway * Mathf.Sin(time * s.SwayRate * Mathf.PI * 2f + s.SwayPhase) / widthInWallPixels;
                s.Rect.anchorMin = s.Rect.anchorMax = new Vector2(x, (s.Rise - 60f) / BossHallSpace.DesignH);
                s.Rect.sizeDelta = new Vector2(s.Size * aspect, s.Size) * unit;
                s.Rect.localEulerAngles = new Vector3(0f, 0f, s.Angle);
                s.Image.color = new Color(1f, 1f, 1f, Mathf.Min(1f, u / .1f) * Mathf.Pow(1f - u, .8f) * s.Bright);
            }
        }
    }
}
