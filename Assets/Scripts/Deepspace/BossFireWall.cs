using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // 2026-11-19: the art team's "BG Fire" set (Downloads/Boss Slayer/Game Play/BG Fire) on the gameplay wall: over the
    // fire wall picture (BGFireWall) it lays their "Red Alpha" glow along the bottom and sends their red "Particle"
    // shards drifting up the wall ("เติม alpha จอกำแพงและ particle ด้วย"). Lives on a full-size layer under the wall's
    // RawImage, so it moves with the wall (shakes included); shown only while the wall shows the Fire art.
    public sealed class BossFireWall : MonoBehaviour
    {
        const string Folder = "BossBackgrounds/Fire/";
        // The glow picture is 8181x1757 on the 8192x2160 wall: it covers the bottom 81%.
        const float GlowHeight = 1757f / 2160f, GlowBreatheSeconds = 4f, GlowBreatheDepth = .15f;
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
            glow.texture = Resources.Load<Texture2D>(Folder + "RedAlpha");
            var g = glow.rectTransform;
            g.anchorMin = Vector2.zero; g.anchorMax = new Vector2(1f, GlowHeight);
            g.offsetMin = g.offsetMax = Vector2.zero;

            var particle = Resources.Load<Texture2D>(Folder + "Particle");
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
                glow.color = new Color(1f, 1f, 1f, 1f - GlowBreatheDepth * (.5f + .5f * Mathf.Sin(time * 2f * Mathf.PI / GlowBreatheSeconds)));

            var area = ((RectTransform)transform).rect;
            float unit = area.height / BossHallSpace.DesignH;
            float widthInWallPixels = area.height > 0f ? BossHallSpace.DesignH * area.width / area.height : BossHallSpace.DesignW;
            for (int i = 0; i < shards.Length; i++)
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
