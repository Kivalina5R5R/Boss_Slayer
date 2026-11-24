using System;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // 2026-11-19: "เพิ่ม particle ปราสาทโบราณที่หินมันจะร่วงลงมาจากด้านบน" - the boss select's old hall crumbles: now and then
    // a few stone chips break off the vaults and tumble down, and fine dust drifts down after them.
    // Lives in the hall picture like the embers (BossHallSpace), so it zooms and floats with the wall camera.
    public sealed class BossFallingDebris : MonoBehaviour
    {
        // 2026-11-19: "หินร่วงลงมา มองไม่เห็นเลย ขอขนาดไม่เท่ากัน แล้วร่วงลงมาบ่อยขึ้น" - more chips (was 26), lighter
        // stone colours (they were as dark as the hall), a wider size range and shorter waits between falls.
        const int ChipCount = 44, DustCount = 40;
        const float Gravity = 1400f;                   // mockup pixels / s², chips only
        static readonly Color[] StoneColors =
        {
            new Color32(138, 156, 150, 255), new Color32(112, 130, 126, 255), new Color32(160, 174, 166, 255), new Color32(96, 114, 112, 255),
        };
        static readonly Color DustColor = new Color32(150, 170, 160, 120);

        RectTransform layer;
        Piece[] pieces;
        Func<Rect> view;

        struct Piece
        {
            public RawImage Image;
            public bool Dust;
            public Color Tint;
            public float Wait, X, Y, Fall, Size, Spin, Angle, Floor, Sway, SwayPhase;
        }

        public static BossFallingDebris Attach(RectTransform parent, string name, Func<Rect> hallView)
        {
            var layer = BossHallSpace.Layer(parent, name);
            if (layer == null) return null;
            if (!layer.TryGetComponent(out BossFallingDebris debris)) debris = layer.gameObject.AddComponent<BossFallingDebris>();
            debris.view = hallView;
            debris.Build();
            return debris;
        }

        void Build()
        {
            layer = (RectTransform)transform;
            // 2026-11-19: "ให้เป็นก้อนหินร่วงลงมา ไม่ใช่ก้อนสามเหลี่ยม" - pixel-art rock chunks (4 shapes, white-grey shaded, tinted
            // by StoneColors) instead of the Start screen's triangle shard.
            var rocks = new Texture2D[4];
            for (int r = 0; r < rocks.Length; r++) rocks[r] = Resources.Load<Texture2D>("BossSlayer/SelectBoss/Rocks/Rock" + r);
            pieces = new Piece[ChipCount + DustCount];
            for (int i = 0; i < pieces.Length; i++)
            {
                bool dust = i >= ChipCount;
                var image = BossHallSpace.Sprite(layer, (dust ? "Dust " : "Chip ") + i);
                image.texture = dust ? BossHallSpace.SoftSquare() : rocks[i % rocks.Length];
                pieces[i] = new Piece { Image = image, Dust = dust, Tint = dust ? DustColor : StoneColors[i % StoneColors.Length] };
                Respawn(ref pieces[i], true);
            }
        }

        // A piece waits a while (spread out so the hall only crumbles now and then), then starts at the top.
        static void Respawn(ref Piece p, bool first)
        {
            p.Wait = first ? UnityEngine.Random.Range(0f, p.Dust ? 5f : 4f) : UnityEngine.Random.Range(p.Dust ? .8f : .6f, p.Dust ? 4f : 5f);
            p.X = UnityEngine.Random.Range(200f, BossHallSpace.DesignW - 200f);
            p.Y = BossHallSpace.DesignH + 40f;
            p.Fall = p.Dust ? UnityEngine.Random.Range(50f, 110f) : UnityEngine.Random.Range(0f, 120f);
            // 2026-11-19: "ขอขนาดไม่เท่ากัน" - mostly small chips, now and then a big chunk (sizes skewed small).
            float r = UnityEngine.Random.value;
            p.Size = p.Dust ? Mathf.Lerp(5f, 22f, r * r) : Mathf.Lerp(16f, 150f, Mathf.Pow(r, 2.2f));
            p.Spin = p.Dust ? 0f : UnityEngine.Random.Range(-260f, 260f) * Mathf.Lerp(1f, .3f, (p.Size - 16f) / 134f);   // big chunks turn slower
            p.Angle = UnityEngine.Random.Range(0f, 360f);
            p.Floor = UnityEngine.Random.Range(380f, 820f);   // the balcony / haze line, where pieces fade out
            p.Sway = p.Dust ? UnityEngine.Random.Range(20f, 60f) : 0f;
            p.SwayPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        // LateUpdate: after the wall camera has set this frame's view, so the particles move with the picture.
        void LateUpdate()
        {
            if (pieces == null) return;
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            var shown = view != null ? view() : new Rect(0, 0, 1, 1);
            float unit = layer.rect.height / BossHallSpace.DesignH;
            for (int i = 0; i < pieces.Length; i++)
            {
                ref var p = ref pieces[i];
                if (p.Wait > 0f) { p.Wait -= dt; p.Image.enabled = false; continue; }
                if (!p.Dust) p.Fall += Gravity * dt;
                p.Y -= p.Fall * dt;
                p.Angle += p.Spin * dt;
                if (p.Y <= p.Floor) { Respawn(ref p, false); p.Image.enabled = false; continue; }
                float drop = (BossHallSpace.DesignH - p.Y) / (BossHallSpace.DesignH - p.Floor);
                float alpha = Mathf.Min(1f, drop / .05f) * (1f - Mathf.SmoothStep(.75f, 1f, drop));
                float x = p.X + p.Sway * Mathf.Sin(now * .8f + p.SwayPhase);
                p.Image.enabled = true;
                BossHallSpace.Place(p.Image.rectTransform, new Vector2(x, p.Y), p.Size, p.Angle, shown, unit);
                var c = p.Tint; c.a *= alpha;
                p.Image.color = c;
            }
        }
    }
}
