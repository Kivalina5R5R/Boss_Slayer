using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-19: the art team's "BG Fire" floor (BGFloorFire) comes with an example picture ("EXFloor Fire") showing
    // ten pixel flames on it. The flame was cut out of that picture (Resources/BossBackgrounds/Fire/Flame) and stands
    // at the same ten spots here, flickering, so the floor burns instead of being a still. Lives on a full-size layer
    // under the floor's RawImage; shown only while the floor shows the Fire art.
    public sealed class BossFireFloor : MonoBehaviour
    {
        // Each flame's box in the floor picture (0..1, bottom = 0), as drawn in EXFloor Fire.
        static readonly Rect[] Places =
        {
            new Rect(.4541f, .8089f, .0554f, .1214f), new Rect(.7497f, .7521f, .0258f, .0568f),
            new Rect(.6525f, .6669f, .0224f, .0568f), new Rect(.3536f, .5966f, .0258f, .0568f),
            new Rect(.3128f, .5682f, .0238f, .0568f), new Rect(.5999f, .5185f, .0258f, .0568f),
            new Rect(.2007f, .3374f, .0258f, .0568f), new Rect(.6790f, .3359f, .0251f, .0568f),
            new Rect(.8648f, .2507f, .0258f, .0568f), new Rect(.2704f, .0540f, .0554f, .1214f),
        };
        // Flicker: how much a flame stretches and how often it flips sideways (a pixel flame's "dance").
        const float StretchY = .14f, StretchX = .06f, FlipMin = .12f, FlipMax = .35f;

        RectTransform[] flames;
        float[] nextFlip, seed;

        public static BossFireFloor Attach(RawImage floor)
        {
            if (floor == null) return null;
            var layer = BossHallSpace.Layer(floor.rectTransform, "Fire Floor");
            if (!layer.TryGetComponent(out BossFireFloor view)) view = layer.gameObject.AddComponent<BossFireFloor>();
            view.Build(layer);
            return view;
        }

        public void Show(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        void Build(RectTransform layer)
        {
            var texture = GameArt.Load<Texture2D>("Art/BG/Fire/Flame");
            flames = new RectTransform[Places.Length];
            nextFlip = new float[Places.Length];
            seed = new float[Places.Length];
            for (int i = 0; i < Places.Length; i++)
            {
                var image = BossHallSpace.Sprite(layer, "Flame " + i);
                image.texture = texture;
                var rect = image.rectTransform;
                // stretched from the flame's foot, so it flickers upwards
                rect.pivot = new Vector2(.5f, 0f);
                rect.anchorMin = Places[i].min; rect.anchorMax = Places[i].max;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                flames[i] = rect;
                seed[i] = Random.Range(0f, 100f);
            }
        }

        void Update()
        {
            if (flames == null) return;
            float time = Time.unscaledTime;
            for (int i = 0; i < flames.Length; i++)
            {
                float n = Mathf.PerlinNoise(seed[i], time * 3.5f) * 2f - 1f;
                var scale = flames[i].localScale;
                if (time >= nextFlip[i])
                {
                    scale.x = -Mathf.Sign(scale.x);
                    nextFlip[i] = time + Random.Range(FlipMin, FlipMax);
                }
                flames[i].localScale = new Vector3(Mathf.Sign(scale.x) * (1f + StretchX * n), 1f + StretchY * n, 1f);
            }
        }
    }
}
