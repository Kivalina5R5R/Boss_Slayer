using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    public sealed class BossNextBossCard : MonoBehaviour
    {
        TMP_Text countdown;
        public string BossName { get; private set; }
        public BossPassiveState.Kind BossKind { get; private set; }
        public static string NameFor(BossPassiveState.Kind kind)
        {
            switch (kind)
            {
                case BossPassiveState.Kind.Fish: return "GIANT FISH";
                case BossPassiveState.Kind.Jellyfish: return "JELLYFISH";
                case BossPassiveState.Kind.Golem: return "CRYSTAL GOLEM";
                case BossPassiveState.Kind.Wraith: return "VOID WRAITH";
                case BossPassiveState.Kind.Serpent: return "SEA SERPENT";
                case BossPassiveState.Kind.AbyssalKing: return "ABYSSAL KING";
                default: return kind == BossPassiveState.Kind.None ? "MYSTERY BOSS" : kind.ToString().ToUpperInvariant();
            }
        }
        public static BossNextBossCard Create(RectTransform parent, TMP_FontAsset font)
        {
            var root = new GameObject("Next Boss Preview", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Place((RectTransform)root.transform, Vector2.zero, Vector2.one);
            var card = root.AddComponent<BossNextBossCard>();
            card.Label("Heading", "NEXT BOSS", font, .81f, .91f, 74, new Color(.65f,.78f,.9f));
            card.Label("Boss Name", "", font, .23f, .34f, 104, Color.white);
            card.countdown = card.Label("Countdown", "", font, .09f, .19f, 60, new Color(1,.82f,.34f));
            var holder = new GameObject("Boss Emblem Area", typeof(RectTransform));
            holder.transform.SetParent(root.transform, false);
            Place((RectTransform)holder.transform, new Vector2(.29f,.37f), new Vector2(.71f,.78f));
            var icon = new GameObject("Boss Emblem", typeof(RectTransform), typeof(BossEmblemGraphic), typeof(AspectRatioFitter));
            icon.transform.SetParent(holder.transform, false);
            var fit = icon.GetComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1;
            icon.GetComponent<BossEmblemGraphic>().raycastTarget = false;
            root.SetActive(false);
            return card;
        }
        static void Place(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        TMP_Text Label(string name, string value, TMP_FontAsset font, float minY, float maxY, float maxSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(transform, false);
            Place((RectTransform)go.transform, new Vector2(.08f,minY), new Vector2(.92f,maxY));
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value; text.color = color; text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold; text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = maxSize;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
            return text;
        }
        public void Show(string modelName, float seconds)
        {
            BossKind = BossPassiveState.Identify(modelName); BossName = NameFor(BossKind);
            transform.Find("Boss Name").GetComponent<TMP_Text>().text = BossName;
            var emblem = GetComponentInChildren<BossEmblemGraphic>(true); emblem.Kind = BossKind; emblem.SetVerticesDirty();
            gameObject.SetActive(true); SetSeconds(seconds);
        }
        // บาส 2026-09-29: per-boss theme (heading/name/countdown tint). NOTE: nothing calls Show() today - the
        // boss-select screen replaced this card - so this only matters if the card comes back. strength 0 = old look.
        public void ApplyTheme(BossThemePalette.BossUiTheme theme, float strength)
        {
            strength = Mathf.Clamp01(strength);
            var heading = transform.Find("Heading"); var name = transform.Find("Boss Name");
            if (heading != null) heading.GetComponent<TMP_Text>().color = Color.Lerp(new Color(.65f,.78f,.9f), Color.Lerp(theme.secondary, Color.white, .55f), strength);
            if (name != null) name.GetComponent<TMP_Text>().color = Color.Lerp(Color.white, theme.textLight, strength);
            if (countdown != null) countdown.color = Color.Lerp(new Color(1,.82f,.34f), Color.Lerp(theme.glow, Color.white, .4f), strength);
        }
        public void SetSeconds(float seconds) { countdown.text = "STARTS IN " + Mathf.CeilToInt(Mathf.Max(0, seconds)) + "s"; }
        public void Hide() { gameObject.SetActive(false); }
    }

}
