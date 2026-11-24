using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // บาส 2026-11-18: the floor's boss select follows the art team's mockup ("Select Boss / FloorMenu"): the rune-circle
    // floor and nine pixel-art name cards in three rows, red on top, blue in the middle, brown at the bottom. A card
    // shows only its row's element word and the boss name (Jersey 15). A beaten boss's card fades with the CLEAR art
    // on it; a locked one just fades. The old emblems, plaques, title, round line and countdown are gone from the floor
    // (the wall still shows the round and the countdown).
    // Art: Resources/BossSlayer/SelectBoss (CardRed, CardBlue, CardBrown, Clear) on the looping rune floor. If the
    // card art is missing the floor keeps the old codex look.
    // The slot's own rect stays the card's frame, so the LED stepping target (BossSelectionLedInput) is the frame.
    public sealed partial class BossSelectionPanel
    {
        const string CardFolder = "BossSlayer/SelectBoss/";
        static readonly string[] RowCards = { "CardRed", "CardBlue", "CardBrown" };
        static readonly string[] RowWords = { "INFERNO", "UNDERWATER", "STONE" };
        static readonly Color[] RowWordInk = { new Color32(255, 178, 31, 255), new Color32(207, 225, 255, 255), new Color32(253, 235, 214, 255) };
        static readonly Color[] RowNameInk = { new Color32(255, 158, 154, 255), new Color32(141, 203, 247, 255), new Color32(247, 210, 176, 255) };

        // Card centres on the floor as fractions (x from the left, y from the top). บาส 2026-11-18: "กระจายปุ่มเลือกบอสหน่อย
        // มันติดกันเกิน ทำให้ห่างๆระยะเท่าๆกัน" - the mockup's grid (.242/.512/.782 x .288/.5/.712, cards .215 wide) left
        // ~155 px between columns and ~110 px between rows. Now the cards are a little narrower (.2 = 563 px, frame 148 px
        // tall) and centred on the floor with the same 200 px gap between frames across and down:
        //   columns 563 + 200 = 763 px apart = .271 of 2816;  rows 148 + 200 = 348 px apart = .272 of 1280.
        // 2026-11-19: "ดันช่องขึ้นด้านบนหน่อย ด้านล่างจะใส่ปุ่ม select" - the rows moved up (~145 px between frames now)
        // to leave the bottom strip for the SELECT button.
        static readonly float[] ColumnX = { .229f, .5f, .771f };
        static readonly float[] RowY = { .2f, .43f, .66f };
        const float CardWidth = .2f;

        // SELECT button (Resources SelectButton, 685x338, frame at x 103..603, y 57..230) centred under the cards.
        // It is the panel's old confirm Button, so the LED floor and the walking cursor can step on it.
        const float SelectWidth = .17f, SelectCenterY = .885f;
        const float SelectFrameWidthPx = 500f, SelectFrameHeightPx = 173f;
        static readonly Vector4 SelectArtAroundFrame = new(-103f / SelectFrameWidthPx, -107f / SelectFrameHeightPx, 1f + 81f / SelectFrameWidthPx, 1f + 57f / SelectFrameHeightPx);
        // 2026-11-19: "select ขนาดตัวหนังสือเท่ากับอันอื่นๆ" - the label box is as tall (on the floor) as a card's name
        // box, so both autosize to the same letter size: card name box .54 of a frame that is .2 * 175/665 wide-to-tall,
        // over a SELECT frame of .17 * 173/500 (the floor aspect cancels out).
        // Properties, not fields: NameBox is declared further down and static fields initialise in order.
        static float SelectLabelHeight => (NameBox.w - NameBox.y) * (CardWidth * FrameHeightPx / FrameWidthPx) / (SelectWidth * SelectFrameHeightPx / SelectFrameWidthPx);
        static Vector4 SelectLabelBox => new(.08f, .5f - SelectLabelHeight / 2, .92f, .5f + SelectLabelHeight / 2);
        static readonly Color SelectInk = new Color32(92, 44, 4, 255);
        const string SelectText = "SELECT";

        // The card picture is 843x339 with its frame at x 99..764, y 57..231 (the glow and side gems lie outside it).
        const float FrameWidthPx = 665f, FrameHeightPx = 175f;
        static readonly Vector4 ArtAroundFrame = new(-99f / FrameWidthPx, -108f / FrameHeightPx, 1f + 78f / FrameWidthPx, 1f + 57f / FrameHeightPx);
        // Inside the frame: the element word on the light top band, the name in the dark part under it.
        static readonly Vector4 WordBox = new(.07f, .63f, .78f, .93f);
        static readonly Vector4 NameBox = new(.06f, .08f, .94f, .62f);
        // The CLEAR picture (864x166) over the frame, a third wider than it, as in the mockup. 2026-11-19: "เอา clear มาไว้
        // ตรงกลางของปุ่ม" - the solid word fills only rows 8..99 of the picture (the rest is a faint glow under it), so a
        // centred picture put the word high; the box is lowered .173 of the frame so the WORD sits in the middle.
        static readonly Vector4 ClearBox = new(-.165f, -.158f, 1.165f, .812f);

        // 2026-11-19: "ลดค่า alpha ลงให้มันมืดลงหน่อยเหมือนกดไม่ได้" - a beaten boss's card goes darker (.45 -> .22), under
        // the full-strength CLEAR. FadedAlpha stays for SELECT before a pick.
        const float SelectedGrow = .08f, FadedAlpha = .45f, ClearedAlpha = .22f, LockedAlpha = .3f;

        bool cardLayout;

        // Builds the card look on the floor. Idempotent (children are found or created), so Bind and the Editor bake
        // can both run it. Returns false when the art is missing, so the caller keeps the old look.
        bool ApplyFloorCards()
        {
            if (Resources.Load<Texture2D>(CardFolder + RowCards[0]) == null) return false;
            cardLayout = true;
            var root = (RectTransform)transform;
            // The rune floor itself is the "Magic Backdrop" (BossFloorMagicVideo) that ShowBossSelection puts behind this.
            if (TryGetComponent(out Image backdrop)) backdrop.color = Color.black;
            if (root.Find("Floor Art") is Transform stillFloor) RemoveChild(stillFloor.gameObject);

            foreach (var old in new[] { "Fantasy Border", "Title Rule Left", "Title Rule Right" })
                if (root.Find(old) is Transform leftover) RemoveChild(leftover.gameObject);
            foreach (var text in new[] { heading, detail, selectedName })
                if (text != null) text.gameObject.SetActive(false);
            float aspect = root.rect.height > 0 ? root.rect.width / root.rect.height : FloorDisplaySettings.Width / (float)FloorDisplaySettings.Height;
            float height = CardWidth * FrameHeightPx / FrameWidthPx * aspect;
            var font = BossPlayerCards.Jersey();
            for (int i = 0; i < slots.Length; i++) BuildCard(i, height, font);
            BuildSelectButton(aspect, font);
            return true;
        }

        void BuildSelectButton(float aspect, TMP_FontAsset font)
        {
            if (confirm == null) return;
            var button = (RectTransform)confirm.transform;
            float height = SelectWidth * SelectFrameHeightPx / SelectFrameWidthPx * aspect, y = 1f - SelectCenterY;
            SetAnchors(button, new Vector2(.5f - SelectWidth / 2, y - height / 2), new Vector2(.5f + SelectWidth / 2, y + height / 2));
            // The old bar Image stays as the Button's (invisible) target graphic.
            if (confirm.targetGraphic is Image bar) { bar.sprite = null; bar.color = Color.clear; bar.raycastTarget = true; }
            if (button.Find("Gilded Border") is Transform border) RemoveChild(border.gameObject);
            var art = Picture(button, "Select Art", CardFolder + "SelectButton", SelectArtAroundFrame);
            art.transform.SetAsFirstSibling();
            var label = confirm.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { CardText(label, font, SelectLabelBox, SelectText, SelectInk, TextAlignmentOptions.Center); label.transform.SetAsLastSibling(); }
            confirm.gameObject.SetActive(true);
        }

        // The SELECT button always reads SELECT (the 3-2-1 is on the wall only, 2026-11-19).
        void ShowSelectLabel()
        {
            if (!cardLayout || confirm == null) return;
            var label = confirm.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = SelectText;
        }

        void BuildCard(int index, float height, TMP_FontAsset font)
        {
            int row = Mathf.Min(index / 3, RowCards.Length - 1), column = index % 3;
            var frame = (RectTransform)slots[index].transform;
            float x = ColumnX[column], y = 1f - RowY[Mathf.Min(row, RowY.Length - 1)];
            SetAnchors(frame, new Vector2(x - CardWidth / 2, y - height / 2), new Vector2(x + CardWidth / 2, y + height / 2));
            // The frame Image stays as the Button's (invisible) target graphic for mouse picks in the Editor.
            frames[index].sprite = null; frames[index].color = Color.clear; frames[index].raycastTarget = true;
            foreach (var old in new[] { "Clear Badge", "Gilded Border" })
                if (frame.Find(old) is Transform leftover) RemoveChild(leftover.gameObject);

            // "Portrait Background" now just holds the card (art, word, name, CLEAR), so the selected card can grow.
            var card = frame.Find("Portrait Background") as RectTransform ?? Rect(frame, "Portrait Background", Vector2.zero, Vector2.one);
            SetAnchors(card, Vector2.zero, Vector2.one);
            if (card.TryGetComponent(out Image portrait)) portrait.enabled = false;
            foreach (var old in new[] { "Gilded Border", "Name Plaque", "Boss Face" })
                if (card.Find(old) is Transform leftover) RemoveChild(leftover.gameObject);

            var art = Picture(card, "Card Art", CardFolder + RowCards[row], ArtAroundFrame);
            art.transform.SetAsFirstSibling();
            var word = card.Find("Element") is Transform w && w.TryGetComponent(out TMP_Text existing) ? existing : Label(card, "Element", font, RowWords[row], Vector2.zero, Vector2.one, 200);
            CardText(word, font, WordBox, RowWords[row], RowWordInk[row], TextAlignmentOptions.Left);
            var name = slotLabels[index];
            if (name.transform.parent != card) name.transform.SetParent(card, false);
            CardText(name, font, NameBox, BossNextBossCard.NameFor((BossPassiveState.Kind)(index + 1)), RowNameInk[row], TextAlignmentOptions.Center);
            var clear = Picture(card, "Clear", CardFolder + "Clear", ClearBox);
            clear.transform.SetAsLastSibling();
            clear.gameObject.SetActive(false);

            // 2026-11-19: sparks around the picked card so the players can see which boss is chosen.
            var sparkleRect = card.Find("Selected Sparkle") as RectTransform ?? Rect(card, "Selected Sparkle", Vector2.zero, Vector2.one);
            if (!sparkleRect.TryGetComponent(out BossCardSparkle sparkle)) sparkle = sparkleRect.gameObject.AddComponent<BossCardSparkle>();
            sparkle.color = RowSparkInk[row]; sparkle.raycastTarget = false;
            sparkleRect.SetAsLastSibling();
            sparkleRect.gameObject.SetActive(false);
        }

        static readonly Color[] RowSparkInk = { new Color32(255, 210, 80, 255), new Color32(150, 225, 255, 255), new Color32(255, 225, 160, 255) };

        // Called from Display: name only, selected card a little bigger, beaten cards faded with CLEAR, locked ones faded.
        void ShowCards(int selected, HashSet<int> available, HashSet<int> cleared)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int kind = i + 1;
                bool done = cleared.Contains(kind), open = available.Contains(kind) && !done;
                slots[i].interactable = open;
                slotLabels[i].text = BossNextBossCard.NameFor((BossPassiveState.Kind)kind);
                if (!(slots[i].transform.Find("Portrait Background") is RectTransform card)) continue;
                float alpha = done ? ClearedAlpha : open ? 1f : LockedAlpha;
                foreach (var part in new[] { "Card Art", "Element" })
                    if (card.Find(part) is Transform t && t.TryGetComponent(out Graphic graphic)) Fade(graphic, alpha);
                Fade(slotLabels[i], alpha);
                if (card.Find("Clear") is Transform clear) clear.gameObject.SetActive(done);
                bool picked = selected == kind && open;
                card.localScale = Vector3.one * (picked ? 1f + SelectedGrow : 1f);
                if (card.Find("Selected Sparkle") is Transform sparkle && sparkle.gameObject.activeSelf != picked) sparkle.gameObject.SetActive(picked);
                if (selected == kind) slots[i].transform.SetAsLastSibling();   // its glow over the neighbours
            }
            if (confirm == null) return;
            // SELECT is dim until a boss is picked.
            if (confirm.transform.Find("Select Art") is Transform art && art.TryGetComponent(out Graphic artGraphic)) Fade(artGraphic, confirm.interactable ? 1f : FadedAlpha);
            var label = confirm.GetComponentInChildren<TMP_Text>(true);
            if (label != null) Fade(label, confirm.interactable ? 1f : FadedAlpha);
            ShowSelectLabel();
        }

        static void Fade(Graphic graphic, float alpha) { var c = graphic.color; c.a = alpha; graphic.color = c; }

        RawImage Picture(RectTransform parent, string name, string texture, Vector4 box)
        {
            var rect = parent.Find(name) as RectTransform ?? Rect(parent, name, Vector2.zero, Vector2.one);
            if (!rect.TryGetComponent(out RawImage image)) image = rect.gameObject.AddComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>(texture); image.color = Color.white; image.raycastTarget = false;
            SetAnchors(rect, new Vector2(box.x, box.y), new Vector2(box.z, box.w));
            return image;
        }

        static void CardText(TMP_Text text, TMP_FontAsset font, Vector4 box, string value, Color ink, TextAlignmentOptions alignment)
        {
            text.gameObject.SetActive(true);
            SetAnchors(text.rectTransform, new Vector2(box.x, box.y), new Vector2(box.z, box.w));
            if (font != null) { text.font = font; text.fontSharedMaterial = font.material; }
            text.text = value;
            text.color = ink;
            text.fontStyle = FontStyles.Normal; text.enableVertexGradient = false;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true; text.fontSizeMin = 8; text.fontSizeMax = 1000;
            text.margin = Vector4.zero; text.characterSpacing = 2; text.lineSpacing = 0;
            text.raycastTarget = false;
        }
    }
}
