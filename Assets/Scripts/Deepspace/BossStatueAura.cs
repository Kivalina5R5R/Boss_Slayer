using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // 2026-11-19: with the statue eyes (BossStatueEyes), the looked-at or picked boss's statue on the boss select wall
    // gets the art team's "Under light" (a soft glow rising round it from below) and its little glowing squares
    // ("StatueParticle") drifting up around it, both in that boss's colour. In the hall view, so they zoom and float with
    // the wall camera. Art: Resources/BossSlayer/SelectBoss/Eyes (white, tinted here).
    public sealed class BossStatueAura : MonoBehaviour
    {
        const string Folder = "BossSlayer/SelectBoss/Eyes/";
        const float FadeInSeconds = .5f, FadeOutSeconds = .4f;
        // Under light: its height as a share of the statue's box, and how high its centre sits in the box (0 = bottom).
        // 2026-11-19: "Under light ชัดเกินไป ... มันบังตัวละคร" - strength 1 -> .45, and a little lower so it lights the base.
        const float LightHeight = 1.05f, LightCentre = .38f, LightStrength = .3f;   // then "ก็ยังชัดอยู่": .45 -> .22, then "สว่างขึ้นอีกนิด": .3
        // Particles per statue, their size (picture pixels) and how far up the box they rise.
        // 2026-11-19: "ก็ยังชัดอยู่ทั้ง light และ particle" - fewer (was 22), smaller (was 18-36) and fainter (SparkStrength).
        const int ParticleCount = 14;
        const float SparkStrength = .7f;   // "สว่างขึ้นอีกนิด": .55 -> .7
        const float ParticleMin = 12f, ParticleMax = 26f, RiseSeconds = 2.2f;

        // Aura colours by boss kind 1..9 (Demon, Fish, Dragon, Jellyfish, Golem, Wraith, Sentinel, Serpent, Hydra).
        static readonly Color[] Colours =
        {
            new Color32(255, 70, 40, 255), new Color32(80, 240, 220, 255), new Color32(255, 150, 40, 255),
            new Color32(255, 110, 220, 255), new Color32(140, 130, 255, 255), new Color32(190, 90, 255, 255),
            new Color32(255, 210, 90, 255), new Color32(70, 190, 255, 255), new Color32(130, 255, 80, 255),
        };

        sealed class Glow
        {
            public RawImage Light;
            public RawImage[] Sparks;
            public Vector2[] SparkAt;     // x, start height in the box (0..1)
            public float[] SparkAge, SparkSize;
            public float Open;
        }

        Func<Rect> view;
        Func<int, bool> hidden;
        readonly Glow[] glows = new Glow[9];
        int looked, picked;

        public static BossStatueAura Attach(RectTransform parent, string name, Func<Rect> hallView, Func<int, bool> noAura)
        {
            var layer = BossHallSpace.Layer(parent, name);
            if (layer == null) return null;
            if (!layer.TryGetComponent(out BossStatueAura aura)) aura = layer.gameObject.AddComponent<BossStatueAura>();
            aura.view = hallView; aura.hidden = noAura;
            aura.Build(layer);
            return aura;
        }

        void Build(RectTransform layer)
        {
            var light = Resources.Load<Texture2D>(Folder + "UnderLight");
            var spark = Resources.Load<Texture2D>(Folder + "StatueParticle");
            for (int k = 0; k < glows.Length; k++)
            {
                var g = glows[k] ??= new Glow();
                g.Light ??= BossHallSpace.Sprite(layer, "Under Light " + (k + 1));
                g.Light.texture = light;
                if (g.Sparks == null)
                {
                    g.Sparks = new RawImage[ParticleCount];
                    g.SparkAt = new Vector2[ParticleCount];
                    g.SparkAge = new float[ParticleCount];
                    g.SparkSize = new float[ParticleCount];
                    for (int i = 0; i < ParticleCount; i++)
                    {
                        g.Sparks[i] = BossHallSpace.Sprite(layer, "Statue Spark " + (k + 1) + "." + i);
                        Respawn(g, i, UnityEngine.Random.value);
                    }
                }
                foreach (var s in g.Sparks) s.texture = spark;
                g.Open = 0f;
            }
            looked = picked = 0;
        }

        static void Respawn(Glow g, int i, float progress)
        {
            g.SparkAt[i] = new Vector2(UnityEngine.Random.Range(.08f, .92f), UnityEngine.Random.Range(0f, .25f));
            g.SparkSize[i] = UnityEngine.Random.Range(ParticleMin, ParticleMax);
            g.SparkAge[i] = progress * RiseSeconds;
        }

        // The statue the camera looks at and the picked boss (kinds 1..9, 0 = none): both glow.
        public void Focus(int lookedKind, int pickedKind) { looked = lookedKind; picked = pickedKind; }

        void LateUpdate()
        {
            var shown = view != null ? view() : new Rect(0, 0, 1, 1);
            float dt = Time.unscaledDeltaTime;
            for (int k = 0; k < glows.Length; k++)
            {
                var g = glows[k];
                int kind = k + 1;
                bool on = (kind == looked || kind == picked) && (hidden == null || !hidden(kind));
                g.Open = Mathf.MoveTowards(g.Open, on ? 1f : 0f, dt / (on ? FadeInSeconds : FadeOutSeconds));
                float open = Mathf.SmoothStep(0f, 1f, g.Open);
                var box = BossStatueShatter.StatueBox(kind);
                var colour = Colours[k];

                g.Light.enabled = open > 0f && g.Light.texture != null;
                if (g.Light.enabled)
                {
                    float h = box.height * LightHeight;
                    float w = h * g.Light.texture.width / g.Light.texture.height * (1024f / 5328f);   // picture pixels are square
                    var place = new Rect(box.center.x - w * .5f, box.yMin + box.height * LightCentre - h * .5f, w, h);
                    Place(g.Light.rectTransform, place, shown);
                    var c = colour; c.a = open * LightStrength;
                    g.Light.color = c;
                }

                for (int i = 0; i < g.Sparks.Length; i++)
                {
                    var spark = g.Sparks[i];
                    spark.enabled = open > 0f && spark.texture != null;
                    if (!spark.enabled) continue;
                    g.SparkAge[i] += dt;
                    float u = g.SparkAge[i] / RiseSeconds;
                    if (u >= 1f) { Respawn(g, i, 0f); u = 0f; }
                    float x = box.xMin + box.width * (g.SparkAt[i].x + .03f * Mathf.Sin((u * 3f + i) * 2.1f));
                    float y = box.yMin + box.height * (g.SparkAt[i].y + u * .75f);
                    float sw = g.SparkSize[i] / 5328f, sh = g.SparkSize[i] * 55f / 48f / 1024f;
                    Place(spark.rectTransform, new Rect(x - sw * .5f, y - sh * .5f, sw, sh), shown);
                    var c = colour; c.a = open * SparkStrength * Mathf.Clamp01(u / .15f) * (1f - u);
                    spark.color = c;
                }
            }
        }

        static void Place(RectTransform rect, Rect place, Rect shown)
        {
            rect.anchorMin = new Vector2((place.xMin - shown.x) / shown.width, (place.yMin - shown.y) / shown.height);
            rect.anchorMax = new Vector2((place.xMax - shown.x) / shown.width, (place.yMax - shown.y) / shown.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localEulerAngles = Vector3.zero;
        }
    }
}
