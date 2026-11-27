using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-26 "วัตถุต่างๆลอยขึ้นลงได้ด้วย": the Final Boss wall's floating rocks beside its centre video (which floats
    // only what is inside it) were cut out of the picture (Art/BG/Final/Float0..8) and painted out of BGFinalBossScene,
    // and float here in their own places: each bobs up and down, sways a little and tilts, on its own slow rhythm.
    // Rects are where each piece sat in the 8192x2160 picture (left, top, width, height in pixels).
    // 2026-11-26 "สิ่งก่อสร้างต้องลอยไปมาตาม VDO ด้วย อันนี้มันแค่ลอยขึ้นลงนิดหน่อยเอง": like the rocks in the centre video,
    // each piece now wanders around on a slow loop of its own (two unrelated sines per axis, so the path never looks
    // like a plain bob), much further than before, and turns back and forth as it goes.
    public sealed class BossWallFloaters : MonoBehaviour
    {
        const string FinalWallName = "BGFinalBossScene", FloatFolder = "Art/BG/Final/Float";
        const float WallW = 8192f, WallH = 2160f;

        static readonly Rect[] Pieces =
        {
            new Rect(289, 32, 196, 199), new Rect(640, 185, 326, 516), new Rect(639, 1178, 172, 193),
            new Rect(801, 79, 1034, 996), new Rect(7256, 460, 50, 90), new Rect(7126, 739, 110, 110),
            new Rect(7436, 1010, 35, 47), new Rect(7240, 1185, 140, 180), new Rect(7599, 1584, 95, 106),
        };

        struct Floater
        {
            public RectTransform Rect;
            public float Drift, Period, Phase, Tilt;
        }

        Floater[] floaters;

        public static BossWallFloaters Attach(RawImage wall)
        {
            if (wall == null) return null;
            var layer = BossHallSpace.Layer(wall.rectTransform, "Wall Floaters");
            if (!layer.TryGetComponent(out BossWallFloaters view)) view = layer.gameObject.AddComponent<BossWallFloaters>();
            view.Build(layer);
            return view;
        }

        // Shows the floating pieces over the Final Boss wall only.
        public void Show(Texture wallArt) => gameObject.SetActive(wallArt != null && wallArt.name == FinalWallName && floaters.Length > 0);

        void Build(RectTransform layer)
        {
            if (floaters != null) return;
            var built = new System.Collections.Generic.List<Floater>();
            for (int i = 0; i < Pieces.Length; i++)
            {
                var art = GameArt.Load<Texture2D>(FloatFolder + i);
                if (art == null) continue;
                var image = BossHallSpace.Sprite(layer, "Floater " + i);
                image.texture = art;
                var piece = Pieces[i];
                var rect = image.rectTransform;
                rect.anchorMin = new Vector2(piece.xMin / WallW, 1f - piece.yMax / WallH);
                rect.anchorMax = new Vector2(piece.xMax / WallW, 1f - piece.yMin / WallH);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                // Big pieces drift slower and further; every piece starts at a different point of its rhythm.
                float size = Mathf.Clamp01(Mathf.Max(piece.width, piece.height) / 1000f);
                built.Add(new Floater
                {
                    Rect = rect,
                    Drift = Mathf.Lerp(45f, 80f, size),
                    Period = Mathf.Lerp(6f, 10f, size) + i * .41f,
                    Phase = i * 1.9f,
                    Tilt = Mathf.Lerp(9f, 3.5f, size),
                });
            }
            floaters = built.ToArray();
        }

        void Update()
        {
            float time = Time.unscaledTime;
            var area = ((RectTransform)transform).rect;
            float unit = area.height / WallH;
            foreach (var f in floaters)
            {
                float w = time * Mathf.PI * 2f / f.Period + f.Phase;
                // Offsets in wall pixels, scaled to the layer: a wandering loop, not a straight bob.
                float x = Mathf.Sin(w * .63f) * .7f + Mathf.Sin(w * 1.37f + 2.1f) * .3f;
                float y = Mathf.Sin(w) * .7f + Mathf.Sin(w * .41f + .8f) * .3f;
                f.Rect.anchoredPosition = new Vector2(x, y) * f.Drift * unit;
                f.Rect.localEulerAngles = new Vector3(0f, 0f, (Mathf.Sin(w * .52f + 1f) * .75f + Mathf.Sin(w * 1.21f) * .25f) * f.Tilt);
            }
        }
    }
}
