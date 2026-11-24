using System;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // 2026-11-19: "เอาลูกตาทั้ง 9 ตัวไปใส่ตอนเลือกตัวนั้น" - on the boss select wall, the statue the camera looks at (a player
    // stands on its card) and the picked boss's statue (kept while it stays picked) open glowing eyes. The art team's eye
    // pictures (Resources/BossSlayer/SelectBoss/Eyes, made white so the colour is set here) sit on the statues of
    // SelectBossBG_v7 in the hall view, so they zoom and float with the camera. "ตาต้องสีแดงทุกตัว": one red for all.
    public sealed class BossStatueEyes : MonoBehaviour
    {
        const string Folder = "BossSlayer/SelectBoss/Eyes/";
        // 2026-11-19: "ตาตอนเร่งให้แดง มันสว่างไป ลดความสว่างตอนแรกลง แล้วเพิ่มมาเป็นปัจจุบัน" - the eyes open slowly
        // (FadeInSeconds, was .45) and stay dim at first, brightening towards the end (OpenCurve), up to today's glow.
        const float FadeInSeconds = 1.4f, FadeOutSeconds = .35f, PulseSeconds = 1.6f, PulseDepth = .25f, OpenCurve = 2.6f;

        // By boss kind 1..9 (Demon, Fish, Dragon, Jellyfish, Golem, Wraith, Sentinel, Serpent, Hydra).
        static readonly string[] Names = { "Demon", "Fish", "Dragon", "Jellyfish", "Golem", "Wraith", "Sentinel", "Serpent", "Hydra" };
        // The eye colour (the pictures are white).
        static readonly Color EyeColour = new Color32(255, 30, 20, 255);
        // 2026-11-19: "ตามันแดงเกินไป หรี่ลงหน่อย" - how strongly the eyes show (1 = the picture at full strength).
        const float EyeStrength = .52f;   // then "ลดอีกนิด": .55 -> .45, then "สว่างขึ้นอีกนิด": .52
        // Where each eye picture sits in SelectBossBG_v7 (0..1 of the picture, bottom = 0), at the picture's own pixel size.
        static readonly Rect[] Places =
        {
            new Rect(.1667f, .6781f, .0302f, .1797f), new Rect(.2624f, .4512f, .0492f, .1924f), new Rect(.4826f, .5824f, .0355f, .1631f),
            new Rect(.3122f, .5676f, .0447f, .1914f), new Rect(.3856f, .5159f, .0342f, .1572f), new Rect(.5856f, .5293f, .0342f, .1465f),
            new Rect(.6573f, .5675f, .0342f, .1465f), new Rect(.7206f, .5782f, .0373f, .1973f), new Rect(.8009f, .5870f, .0580f, .2598f),
        };

        Func<Rect> view;
        Func<int, bool> hidden;   // statues with no eyes to show (broken ones)
        readonly RawImage[] eyes = new RawImage[Names.Length];
        readonly float[] open = new float[Names.Length];
        int looked, picked;

        public static BossStatueEyes Attach(RectTransform parent, string name, Func<Rect> hallView, Func<int, bool> noEyes)
        {
            var layer = BossHallSpace.Layer(parent, name);
            if (layer == null) return null;
            if (!layer.TryGetComponent(out BossStatueEyes view)) view = layer.gameObject.AddComponent<BossStatueEyes>();
            view.view = hallView; view.hidden = noEyes;
            for (int i = 0; i < Names.Length; i++)
            {
                if (view.eyes[i] == null)
                {
                    view.eyes[i] = BossHallSpace.Sprite(layer, "Eyes " + Names[i]);
                    view.eyes[i].texture = Resources.Load<Texture2D>(Folder + Names[i]);
                }
                view.eyes[i].enabled = false;
                view.open[i] = 0f;
            }
            view.looked = view.picked = 0;
            return view;
        }

        // The statue the camera looks at and the picked boss (kinds 1..9, 0 = none): both look out.
        public void Focus(int lookedKind, int pickedKind) { looked = lookedKind; picked = pickedKind; }

        void LateUpdate()
        {
            var shown = view != null ? view() : new Rect(0, 0, 1, 1);
            float dt = Time.unscaledDeltaTime;
            float pulse = 1f - PulseDepth * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / PulseSeconds));
            for (int i = 0; i < Names.Length; i++)
            {
                bool on = (looked == i + 1 || picked == i + 1) && (hidden == null || !hidden(i + 1));
                open[i] = Mathf.MoveTowards(open[i], on ? 1f : 0f, dt / (on ? FadeInSeconds : FadeOutSeconds));
                var image = eyes[i];
                image.enabled = open[i] > 0f && image.texture != null;
                if (!image.enabled) continue;
                var c = EyeColour; c.a = Mathf.Pow(open[i], OpenCurve) * pulse * EyeStrength;
                image.color = c;
                var place = Places[i];
                var rect = image.rectTransform;
                rect.anchorMin = new Vector2((place.xMin - shown.x) / shown.width, (place.yMin - shown.y) / shown.height);
                rect.anchorMax = new Vector2((place.xMax - shown.x) / shown.width, (place.yMax - shown.y) / shown.height);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.localEulerAngles = Vector3.zero;
            }
        }
    }
}
