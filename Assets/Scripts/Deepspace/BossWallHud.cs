using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // บาส 2026-11-18: Boss Slayer's wall HUD during the fight, from the art team's "Game Play" mockup: the boss name on a
    // gold plate above a long red HP bar (numbers at its right end), a thin white STUN bar under it, and the ULTIMATE bar
    // at the bottom centre. Pixel lettering in Jersey 15. It replaces the old bars (BossSlayerGameManager hides them and
    // feeds this the same numbers); the player HP cards in the bottom-left corner stay as they are.
    // Art: Resources/BossSlayer/Hud, drawn 1:1 for the 8192x2160 wall, so everything is laid out in those pixels and
    // scaled to whatever size the wall canvas has. A bar fills by cropping its fill picture between the fill's own
    // left and right edges (measured on the art), so the rounded ends never stretch.
    public sealed class BossWallHud : MonoBehaviour
    {
        public const string PanelName = "Panel == Boss HUD";
        const string Folder = "BossSlayer/Hud/";
        const float DesignW = 8192f, DesignH = 2160f;

        // Layout in wall pixels (left, top). The HP bar is centred; the name plate sits on its left end; the STUN
        // row hangs under it; the ULTIMATE plate and bar are centred together at the bottom.
        const float HpWidth = 3444f, HpLeft = (DesignW - HpWidth) / 2f, PlateTop = 56f, HpTop = PlateTop + 127f - 6f;
        const float StunTop = HpTop + 121f + 14f, StunPlateLeft = HpLeft + 30f, StunBarLeft = StunPlateLeft + 274f - 18f;
        const float UltimateWidth = 448f + 2074f - 10f, UltimateLeft = (DesignW - UltimateWidth) / 2f, UltimateTop = DesignH - 70f - 111f;

        // After a hit the white "damage taken" part waits this long, then drains at this many bar-widths per second.
        const float EchoDelay = .35f, EchoSpeed = .9f;

        static readonly Color NameInk = new Color32(253, 232, 190, 255);
        static readonly Color NumberInk = Color.white;
        static readonly Color UltimateInk = new Color32(253, 232, 190, 255);
        static readonly Color EchoTint = new Color(1f, 1f, 1f, .75f);

        sealed class Bar
        {
            public RawImage Fill, Echo;
            public float Left, Right, Width;   // the fill's own edges in the picture, and the picture's width
            public float Ratio = -1f, EchoRatio = 1f, EchoWait;
        }

        Bar hp, stun, ultimate;
        TMP_Text bossName, hpNumbers, stunNumbers, ultimateNumbers;

        // Builds the HUD under the wall's gameplay UI panel (it shows and hides with that panel). Safe to call again.
        public static BossWallHud Attach(RectTransform uiPanel)
        {
            if (uiPanel == null) return null;
            var panel = uiPanel.Find(PanelName) as RectTransform;
            if (panel == null)
            {
                var go = new GameObject(PanelName, typeof(RectTransform));
                go.layer = uiPanel.gameObject.layer;
                panel = (RectTransform)go.transform;
                panel.SetParent(uiPanel, false);
                panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
                panel.offsetMin = panel.offsetMax = Vector2.zero;
            }
            if (!panel.TryGetComponent(out BossWallHud hud)) hud = panel.gameObject.AddComponent<BossWallHud>();
            if (hud.hp == null) hud.Build(panel);
            return hud;
        }

        // Every HUD refresh. name is "DEMON : RED BOY" style; the fills take ratios, the texts the real numbers.
        public void Show(string name, int hpNow, int hpMax, float stunNow, int stunNeed, int ultimateNow, int ultimateNeed)
        {
            if (hp == null) return;
            if (bossName.text != name) bossName.text = name;
            SetText(hpNumbers, $"{hpNow}/{hpMax}");
            SetText(stunNumbers, $"{Mathf.RoundToInt(stunNow)}/{stunNeed}");
            SetText(ultimateNumbers, $"{ultimateNow}/{ultimateNeed}");
            SetRatio(hp, hpNow / (float)Mathf.Max(1, hpMax));
            SetRatio(stun, stunNow / Mathf.Max(1, stunNeed));
            SetRatio(ultimate, ultimateNow / (float)Mathf.Max(1, ultimateNeed));
        }

        void Update()
        {
            // the HP bar's white echo drains down to the red fill after each hit
            if (hp == null || hp.Echo == null) return;
            if (hp.EchoRatio <= hp.Ratio) { hp.EchoRatio = hp.Ratio; Crop(hp, hp.Echo, hp.EchoRatio); return; }
            if ((hp.EchoWait -= Time.deltaTime) > 0f) return;
            hp.EchoRatio = Mathf.Max(hp.Ratio, hp.EchoRatio - EchoSpeed * Time.deltaTime);
            Crop(hp, hp.Echo, hp.EchoRatio);
        }

        void Build(RectTransform panel)
        {
            var font = BossPlayerCards.Jersey();

            var plate = Picture(panel, "Boss Name Plate", "NamePlate", HpLeft, PlateTop);
            bossName = Label(plate, "Boss Name", font, NameInk, TextAlignmentOptions.Center, new Vector4(.05f, .2f, .95f, .8f));

            hp = MakeBar(panel, "Boss HP Bar", "HpBack", "HpFill", HpLeft, HpTop, 32f, 3411f, true);
            hpNumbers = Label((RectTransform)hp.Fill.transform.parent, "HP Numbers", font, NumberInk, TextAlignmentOptions.Right, new Vector4(.6f, .2f, .985f, .8f));

            var stunPlate = Picture(panel, "Stun Plate", "StunPlate", StunPlateLeft, StunTop);
            Label(stunPlate, "Stun Caption", font, Color.white, TextAlignmentOptions.Center, new Vector4(.1f, .22f, .9f, .78f)).text = "STUN";
            float stunBarTop = StunTop + (108f - 67f) / 2f;
            stun = MakeBar(panel, "Stun Bar", "StunBack", "StunFill", StunBarLeft, stunBarTop, 14f, 2768f, false);
            stunNumbers = Label((RectTransform)stun.Fill.transform.parent, "Stun Numbers", font, NumberInk, TextAlignmentOptions.Right, new Vector4(.75f, -.35f, .99f, 1.35f));
            stunPlate.SetAsLastSibling();   // the plate overlaps the bar's left end

            ultimate = MakeBar(panel, "Ultimate Bar", "UltimateBack", "UltimateFill", UltimateLeft + 448f - 10f, UltimateTop + 3f, 27f, 2045f, false);
            ultimateNumbers = Label((RectTransform)ultimate.Fill.transform.parent, "Ultimate Numbers", font, NumberInk, TextAlignmentOptions.Right, new Vector4(.7f, .22f, .975f, .78f));
            var ultimatePlate = Picture(panel, "Ultimate Plate", "UltimatePlate", UltimateLeft, UltimateTop);
            Label(ultimatePlate, "Ultimate Caption", font, UltimateInk, TextAlignmentOptions.Center, new Vector4(.08f, .2f, .92f, .8f)).text = "ULTIMATE";
        }

        Bar MakeBar(RectTransform panel, string name, string back, string fill, float left, float top, float fillLeft, float fillRight, bool echo)
        {
            var root = Picture(panel, name, back, left, top);
            var bar = new Bar { Left = fillLeft, Right = fillRight, Width = root.GetComponent<RawImage>().texture is Texture t && t != null ? t.width : 1f };
            if (echo) { bar.Echo = Overlay(root, "Damage Echo", fill); bar.Echo.color = EchoTint; }
            bar.Fill = Overlay(root, "Fill", fill);
            SetRatio(bar, 1f);
            return bar;
        }

        void SetRatio(Bar bar, float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (Mathf.Approximately(ratio, bar.Ratio)) return;
            if (bar.Echo != null && ratio < bar.Ratio) bar.EchoWait = EchoDelay;   // a hit: hold the white part a moment
            bar.Ratio = ratio;
            Crop(bar, bar.Fill, ratio);
        }

        // Shows the fill picture from its left edge up to ratio of the way to its right edge.
        static void Crop(Bar bar, RawImage image, float ratio)
        {
            float shown = ratio <= 0f ? 0f : (bar.Left + ratio * (bar.Right - bar.Left)) / bar.Width;
            var rect = image.rectTransform;
            rect.anchorMax = new Vector2(shown, 1f);
            image.uvRect = new Rect(0f, 0f, shown, 1f);
            image.enabled = shown > 0f;
        }

        // A picture at its own pixel size, placed by its top-left corner in wall pixels.
        static RectTransform Picture(RectTransform panel, string name, string art, float left, float top)
        {
            var texture = Resources.Load<Texture2D>(Folder + art);
            float w = texture != null ? texture.width : 100f, h = texture != null ? texture.height : 20f;
            var rect = NewRect(panel, name);
            rect.anchorMin = new Vector2(left / DesignW, 1f - (top + h) / DesignH);
            rect.anchorMax = new Vector2((left + w) / DesignW, 1f - top / DesignH);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
            if (texture == null) Debug.LogWarning($"[Boss wall HUD] Missing Resources/{Folder}{art}.png");
            return rect;
        }

        // A fill picture over its bar, the same size, cropped from the left by Crop.
        static RawImage Overlay(RectTransform bar, string name, string art)
        {
            var rect = NewRect(bar, name);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, .5f);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>(Folder + art); image.raycastTarget = false;
            return image;
        }

        static TMP_Text Label(RectTransform parent, string name, TMP_FontAsset font, Color ink, TextAlignmentOptions align, Vector4 box)
        {
            var rect = NewRect(parent, name);
            rect.anchorMin = new Vector2(box.x, box.y); rect.anchorMax = new Vector2(box.z, box.w);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) { text.font = font; text.fontSharedMaterial = font.material; }
            text.color = ink; text.alignment = align; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true; text.fontSizeMin = 8f; text.fontSizeMax = 400f;
            rect.SetAsLastSibling();
            return text;
        }

        static void SetText(TMP_Text text, string value) { if (text.text != value) text.text = value; }

        static RectTransform NewRect(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
