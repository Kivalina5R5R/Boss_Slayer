using System;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // Boss Slayer's rising embers: red squares (the art team's Particle sprite) and orange shards drifting up from the
    // bottom of the wall, swaying and fading as they climb. Moved out of BossStartWall on 2026-11-19 so the boss select
    // wall can show the same ones ("เอา particle แบบหน้าเลือก player ไปใส่หน้า Select boss ด้วย").
    // Put it on a full-size layer. Children are named "Particle i", so a baked scene's particles are reused.
    // The boss select also gives it:
    // - a hall view (BossHallSpace), so the embers live in the picture and zoom / float with the wall camera;
    // - a lower climb (MaxRise), and boss colours (Recolour) for the boss a player stands on.
    public sealed class BossRisingParticles : MonoBehaviour
    {
        const string Folder = "Art/UI/Start/";
        const int ParticleCount = 120;
        static readonly Color[] ShardColors = { new Color32(214, 120, 60, 235), new Color32(190, 92, 50, 235), new Color32(232, 150, 80, 235), new Color32(160, 70, 45, 235) };
        const float RecolourSeconds = .8f;

        RectTransform layer;
        Particle[] particles;
        float clock;
        Func<Rect> view;                       // the part of the picture on screen; null = the layer is the picture
        float minRise = 1100f, maxRise = 2000f; // how high a particle climbs, in 2160-high mockup pixels
        int shownCount = ParticleCount;         // the boss select shows fewer
        bool themed;                            // squares are white and take the theme colour (the red sprite cannot)
        Color squareTheme = Color.white, shardTheme = Color.white, squareFrom, shardFrom, squareTo, shardTo;
        float recolourAge = 99f;

        struct Particle
        {
            public RectTransform Rect;
            public RawImage Image;
            public Color Tint;
            public bool Square;
            public float X, Rise, Travel, Size, Sway, SwayRate, SwayPhase, Spin, Angle, Bright;
        }

        // Finds or builds the particle layer (full size, under parent) and its particles.
        public static BossRisingParticles Attach(RectTransform parent, string name = "Particles")
        {
            var layer = BossHallSpace.Layer(parent, name);
            if (layer == null) return null;
            if (!layer.TryGetComponent(out BossRisingParticles view)) view = layer.gameObject.AddComponent<BossRisingParticles>();
            view.Build();
            return view;
        }

        // Boss select: `count` embers placed in the hall picture (seen through `hallView`), climbing `lowestRise`..
        // `highestRise` mockup pixels, in colours set by Recolour. Call before Scatter.
        public void UseHall(Func<Rect> hallView, int count, float lowestRise, float highestRise, Color square, Color shard)
        {
            view = hallView; minRise = lowestRise; maxRise = highestRise;
            shownCount = Mathf.Clamp(count, 0, particles.Length);
            for (int i = 0; i < particles.Length; i++) particles[i].Image.enabled = i < shownCount;
            if (!themed)
            {
                themed = true;
                var white = BossHallSpace.SoftSquare();
                for (int i = 0; i < particles.Length; i++) if (particles[i].Square) { particles[i].Image.texture = white; particles[i].Tint = Color.white; }
            }
            squareTheme = squareTo = square; shardTheme = shardTo = shard; recolourAge = 99f;
        }

        // Fades the embers to new colours (themed layers only).
        public void Recolour(Color square, Color shard)
        {
            if (!themed || (square == squareTo && shard == shardTo)) return;
            squareFrom = squareTheme; shardFrom = shardTheme; squareTo = square; shardTo = shard; recolourAge = 0f;
        }

        void Build()
        {
            layer = (RectTransform)transform;
            var square = GameArt.Load<Texture2D>(Folder + "Particle");
            var shard = GameArt.Load<Texture2D>(Folder + "Shard");
            particles = new Particle[ParticleCount];
            for (int i = 0; i < ParticleCount; i++)
            {
                var image = BossHallSpace.Sprite(layer, "Particle " + i);
                bool isSquare = i % 20 < 11;
                image.texture = isSquare ? (themed ? BossHallSpace.SoftSquare() : square) : shard;
                particles[i] = new Particle
                {
                    Rect = image.rectTransform, Image = image, Square = isSquare,
                    Tint = isSquare ? Color.white : ShardColors[i % ShardColors.Length],
                };
            }
            Scatter();
        }

        // Gives every particle a fresh path, spread over its whole climb so the screen starts full.
        public void Scatter()
        {
            if (particles == null) return;
            for (int i = 0; i < particles.Length; i++) Respawn(ref particles[i], UnityEngine.Random.value);
        }

        void Respawn(ref Particle p, float progress)
        {
            p.X = UnityEngine.Random.Range(0f, BossHallSpace.DesignW);
            p.Travel = UnityEngine.Random.Range(minRise, maxRise);
            p.Rise = p.Travel * progress;
            p.Size = p.Square ? UnityEngine.Random.Range(16f, 64f) : UnityEngine.Random.Range(26f, 60f);
            p.Sway = UnityEngine.Random.Range(10f, 50f);
            p.SwayRate = UnityEngine.Random.Range(.3f, .9f);
            p.SwayPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            p.Spin = p.Square ? (UnityEngine.Random.value < .5f ? 0f : UnityEngine.Random.Range(-30f, 30f)) : UnityEngine.Random.Range(-120f, 120f);
            p.Angle = UnityEngine.Random.Range(0f, 360f);
            p.Bright = UnityEngine.Random.Range(.6f, 1f);
        }

        // LateUpdate: after the wall camera has set this frame's view, so the particles move with the picture.
        void LateUpdate()
        {
            if (particles == null) return;
            float dt = Time.unscaledDeltaTime;
            clock += dt;
            UpdateTheme(dt);
            var shown = view != null ? view() : new Rect(0, 0, 1, 1);
            float unit = layer.rect.height / BossHallSpace.DesignH;
            for (int i = 0; i < shownCount; i++)
            {
                ref var p = ref particles[i];
                p.Rise += dt * Mathf.Lerp(80f, 170f, p.Size / 64f);
                if (p.Rise >= p.Travel) Respawn(ref p, 0f);
                float u = p.Rise / p.Travel;
                float alpha = Mathf.Min(1f, u / .08f) * Mathf.Pow(1f - u, .9f) * p.Bright;
                float x = p.X + p.Sway * Mathf.Sin(clock * p.SwayRate * Mathf.PI * 2f + p.SwayPhase);
                p.Angle += p.Spin * dt;
                BossHallSpace.Place(p.Rect, new Vector2(x, p.Rise - 30f), p.Size, p.Angle, shown, unit);
                var c = themed ? (p.Square ? squareTheme : shardTheme) : p.Tint;
                c.a *= alpha;
                p.Image.color = c;
            }
        }

        void UpdateTheme(float dt)
        {
            if (recolourAge >= RecolourSeconds) return;
            recolourAge += dt;
            float k = Mathf.SmoothStep(0f, 1f, recolourAge / RecolourSeconds);
            squareTheme = Color.Lerp(squareFrom, squareTo, k); shardTheme = Color.Lerp(shardFrom, shardTo, k);
        }
    }
}
