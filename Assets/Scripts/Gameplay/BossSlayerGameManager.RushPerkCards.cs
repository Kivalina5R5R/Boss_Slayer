using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-27 "เอา UI Skill ต่างๆไปใส่": the wall's team perk cards are the designer's skill card (Art/UI/Perk, the
    // sample EX.png): the skill's picture inside the gold SkillFrame, the name on the BG Name band, then the BG Text box
    // with "LINE : TIER", what the perk does and the votes. The footer sits on BG Line Text. Text is in Jersey 15 and
    // English unless the launcher switched the game to Thai. The floor keeps its codex cards until its own art arrives.
    public sealed partial class BossSlayerGameManager
    {
        const string PerkArtFolder = "Art/UI/Perk/";
        const string PerkIconFolder = "Art/UI/Perk/Icons/";

        // The card in units of its width, measured from the top (the pictures' own proportions).
        const float CardTall = 1.82f;
        const float FrameTop = 0f, FrameHeight = .98f;
        const float IconTop = .07f, IconHeight = .87f, IconInset = .04f;
        const float BandTop = .895f, BandHeight = .16f, BandOverhang = .035f;
        const float BodyTop = .99f, BodyHeight = .83f, BodyInset = .01f;

        // Share of the wall's height the cards take, where their tops sit, and the gap between card centres in card widths.
        const float WallCardHeight = .62f, WallCardTop = .16f, WallCardSpacing = 1.55f;

        readonly List<(RawImage icon, CanvasGroup group)> perkWallArt = new();
        readonly Dictionary<string, Texture2D> perkArt = new();

        (Image, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI) BuildWallPerkCard(RectTransform root, int index, Rect area)
        {
            float height = area.height * WallCardHeight, width = height / CardTall;
            float centerX = area.center.x + (index - 1) * width * WallCardSpacing;
            float top = area.yMax - area.height * WallCardTop;
            var card = new GameObject("Perk Card " + (index + 1), typeof(RectTransform), typeof(Image), typeof(CanvasGroup)).GetComponent<Image>();
            card.rectTransform.SetParent(root, false);
            Place(card.rectTransform, new Rect(centerX - width / 2, top - height, width, height), area);
            card.color = Color.clear; card.raycastTarget = false;
            var rect = card.rectTransform;

            // back to front: picture, frame over its edges, text box, name band over the seam
            var icon = CardPicture(rect, "Icon", null, IconInset, 1 - IconInset, IconTop, IconHeight);
            CardPicture(rect, "Frame", PerkTexture(PerkArtFolder + "SkillFrame"), 0, 1, FrameTop, FrameHeight);
            CardPicture(rect, "Text Box", PerkTexture(PerkArtFolder + "BG Text"), BodyInset, 1 - BodyInset, BodyTop, BodyHeight);
            CardPicture(rect, "Name Band", PerkTexture(PerkArtFolder + "BG Name"), -BandOverhang, 1 + BandOverhang, BandTop, BandHeight);

            var name = CardLabel(rect, "Name", .08f, .92f, BandTop + .035f, BandHeight - .06f, width * .11f, Color.white);
            var tier = CardLabel(rect, "Tier", .08f, .92f, 1.07f, .1f, width * .075f, Color.white);
            var detail = CardLabel(rect, "Detail", .1f, .9f, 1.19f, .38f, width * .068f, BossFantasyTheme.Parchment);
            detail.textWrappingMode = TextWrappingModes.Normal;
            var votes = CardLabel(rect, "Votes", .08f, .92f, 1.6f, .14f, width * .1f, Color.white);
            perkWallArt.Add((icon, card.GetComponent<CanvasGroup>()));
            BuildWallCardFx(rect, card.GetComponent<CanvasGroup>(), area, width, height);
            return (card, tier, name, detail, null, votes);
        }

        // The footer line under the cards: BG Line Text as wide as the three cards, the text on it.
        TextMeshProUGUI BuildWallPerkFooter(RectTransform root, Rect area)
        {
            float cardWidth = area.height * WallCardHeight / CardTall;
            float width = cardWidth * (1 + 2 * WallCardSpacing), height = area.height * .1f;
            var band = new GameObject("Footer Band", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            band.rectTransform.SetParent(root, false);
            Place(band.rectTransform, new Rect(area.center.x - width / 2, area.yMax - area.height * .93f, width, height), area);
            band.texture = PerkTexture(PerkArtFolder + "BG Line Text"); band.raycastTarget = false;
            band.color = band.texture != null ? Color.white : Color.clear;
            var footer = CardText(band.rectTransform, "Footer", PerkCardFont, new Vector2(.12f, .18f), new Vector2(.88f, .82f), Vector2.zero, Vector2.zero, height * .45f, TextAlignmentOptions.Center);
            footer.fontStyle = FontStyles.Normal; footer.color = BossFantasyTheme.Parchment;
            footer.enableAutoSizing = true; footer.fontSizeMin = 8f; footer.fontSizeMax = height * .45f;
            return footer;
        }

        // Puts the perk's picture on wall card index; its reveal and pick motion is AnimateWallPerkCard (RushPerkAnim.cs).
        void StyleWallPerkCard(int index, RushPerk perk, bool chosen)
        {
            if (index >= perkWallArt.Count) return;
            var (icon, _) = perkWallArt[index];
            var texture = PerkTexture(PerkIconFolder + perk.Art);
            if (icon.texture != texture) icon.texture = texture;
            icon.color = texture != null ? Color.white : Color.clear;
            AnimateWallPerkCard(index, chosen, TierColors[perk.Tier]);
        }

        // ---------------------------------------------------------------- floor cards
        // 2026-11-27 "ไม่ใช่เอารูปเราไปแปะไว้ อยากได้ปุ่มใหม่เลยที่ดูแล้วรู้": each floor card is a stepping pad of its own
        // (Art/UI/Perk/FloorPad, an arched gold-rimmed stone plate after FoodIsekai's perk pads) with the perk's name and
        // its votes in Jersey 15. It bounces when a player steps on it and swells a little while anyone stands on it.
        const float FloorBounce = .14f, FloorBounceSeconds = .45f, FloorBounceWobbles = 2.5f, FloorStandGrow = .06f;
        // the pad picture's aspect (height / width), used for the card's floor rectangle
        const float FloorPadTall = 1.33f;

        readonly List<(TextMeshProUGUI name, TextMeshProUGUI votes, CanvasGroup group)> perkFloorText = new();
        readonly List<(RectTransform pad, int standing, float bouncedAt)> perkFloorPads = new();
        readonly List<bool> perkFloorChosen = new();

        void BuildFloorPerkArt(RectTransform card)
        {
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            var pad = new GameObject("Pad", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            pad.rectTransform.SetParent(card, false); UiThemeKit.Stretch(pad.rectTransform, 0);
            pad.texture = PerkTexture(PerkArtFolder + "FloorPad"); pad.raycastTarget = false;
            pad.color = pad.texture != null ? Color.white : UiThemeKit.Alpha(BossFantasyTheme.Night, .95f);
            float width = card.rect.width;
            var name = CardText(card, "Name", PerkCardFont, new Vector2(.14f, .42f), new Vector2(.86f, .72f), Vector2.zero, Vector2.zero, width * .16f, TextAlignmentOptions.Center);
            name.fontStyle = FontStyles.Normal; name.color = BossFantasyTheme.Parchment;
            name.enableAutoSizing = true; name.fontSizeMin = 8f; name.fontSizeMax = width * .16f;
            name.textWrappingMode = TextWrappingModes.Normal;
            var votes = CardText(card, "Votes", PerkCardFont, new Vector2(.14f, .16f), new Vector2(.86f, .34f), Vector2.zero, Vector2.zero, width * .12f, TextAlignmentOptions.Center);
            votes.fontStyle = FontStyles.Normal; votes.color = Color.white;
            votes.enableAutoSizing = true; votes.fontSizeMin = 8f; votes.fontSizeMax = width * .12f;
            perkFloorText.Add((name, votes, group));
            perkFloorPads.Add((card, 0, -9f));
            perkFloorChosen.Add(false);
        }

        void StyleFloorPerkCard(int index, RushPerk perk, bool chosen, int votes)
        {
            if (index >= perkFloorText.Count) return;
            var (name, voteLabel, group) = perkFloorText[index];
            if (name.text != perk.Name) name.text = perk.Name;
            name.color = chosen ? TierColors[perk.Tier] : BossFantasyTheme.Parchment;
            voteLabel.text = VotesLabel(votes);
            group.alpha = FloorPerkReveal(index, chosen).alpha;
            perkFloorChosen[index] = chosen;
        }

        // Called every frame of the vote: counts who stands on each card / the reroll pad and springs the ones just reached.
        void AnimateFloorPerkPads()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < perkFloorPads.Count; i++)
            {
                var (pad, before, bouncedAt) = perkFloorPads[i];
                if (pad == null) continue;
                bool reroll = perkRerollPad != null && pad == perkRerollPad.rectTransform;
                int standing = 0;
                foreach (var p in players)
                    if (TryGetInputLocalPoint(p.Index, out var local) && (reroll ? RerollRect().Contains(local) : PerkCardAt(local) == i)) standing++;
                if (standing > before) { bouncedAt = now; if (pad.gameObject.activeInHierarchy) BossFightAudio.PlayUi("hover", .35f, .03f); }
                perkFloorPads[i] = (pad, standing, bouncedAt);
                float u = (now - bouncedAt) / FloorBounceSeconds;
                float bounce = u >= 0f && u < 1f ? FloorBounce * Mathf.Sin(u * Mathf.PI * FloorBounceWobbles) * (1f - u) : 0f;
                bool chosen = !reroll && i < perkFloorChosen.Count && perkFloorChosen[i];
                float hold = standing > 0 && !rushPicked ? FloorStandGrow : chosen ? .08f : 0f;
                float reveal = reroll ? (perkRerollClosedAt >= 0f ? PopAway((now - perkRerollClosedAt) / PopAwaySeconds) : 1f) : FloorPerkReveal(i, chosen).scale;
                pad.localScale = Vector3.one * (1f + hold + bounce) * reveal;
            }
        }

        static string VotesLabel(int votes) => Lang.T(votes == 1 ? "1 VOTE" : votes + " VOTES", votes + " โหวต");

        TMP_FontAsset PerkCardFont => BossPlayerCards.Jersey() ?? BossUiFont;

        Texture2D PerkTexture(string path)
        {
            if (perkArt.TryGetValue(path, out var texture) && texture != null) return texture;
            texture = GameArt.Load<Texture2D>(path);
            if (texture == null) Debug.LogWarning($"[Boss Slayer] Missing perk art {path}", this);
            perkArt[path] = texture;
            return texture;
        }

        // A picture placed in card-width units: x from left to right, top and height measured down from the card's top.
        static RawImage CardPicture(RectTransform card, string name, Texture2D texture, float left, float right, float top, float height)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.rectTransform.SetParent(card, false);
            SetCardAnchors(image.rectTransform, left, right, top, height);
            image.texture = texture; image.raycastTarget = false;
            image.color = texture != null ? Color.white : Color.clear;
            return image;
        }

        TextMeshProUGUI CardLabel(RectTransform card, string name, float left, float right, float top, float height, float maxSize, Color color)
        {
            var label = CardText(card, name, PerkCardFont, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, maxSize, TextAlignmentOptions.Center);
            SetCardAnchors(label.rectTransform, left, right, top, height);
            label.fontStyle = FontStyles.Normal; label.color = color;
            label.enableAutoSizing = true; label.fontSizeMin = 8f; label.fontSizeMax = maxSize;
            return label;
        }

        static void SetCardAnchors(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(left, 1 - (top + height) / CardTall);
            rect.anchorMax = new Vector2(right, 1 - top / CardTall);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
