using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // บาส 2026-09-29 playtest fixes for the wall (8192x2160, read from several metres on LED):
    //  - player HP cards (were 200x200 white squares + a thin green bar = scene placeholders),
    //  - the final session result screen (was default font, MVP score struck through by the plate edge,
    //    player plates tiny and bunched bottom-left).
    // Layout lives here rather than in the scene because both depend on the live player count (1-6) and, for the
    // result, on the score ranking; the scene's layout groups are switched off so they can't fight it.
    // HP semantics are unchanged: fill = HP ratio, green -> red as it drops, grey while down.
    public sealed partial class BossSlayerGameManager
    {
        // 2026-11-24 "Game Play" mockup (art team, HP Player): one row along the bottom edge of the wall, centred, each
        // player a coloured "PLAYER n" plate (Hud/PlayerPlate1-6) on the left end of a cream-framed bar (Hud/PlayerHpBack)
        // with "HP/MAX" at its right end. The ULTIMATE bar sits just above the row (BossWallHud.UltimateTop).
        // Sizes are the art's own pixels on the 8192x2160 wall canvas; a full row of six shrinks a little to fit.
        const float WallW = 8192f, PlateW = 375f, PlateH = 94f, BarW = 971f, BarH = 83f, BarOverlap = 22f;
        const float SlotW = PlateW - BarOverlap + BarW, SlotGap = 60f, RowSideMargin = 100f, RowBottom = 70f;
        // The fill's own edges inside PlayerHpBack.png (971x83): x 21..949, y 17..64.
        const float FillLeft = 21f, FillRight = 949f, FillBottom = 83f - 64f, FillTop = 83f - 17f;
        static readonly Color CardDownGrey = new Color(.42f, .44f, .48f, 1f), CardHpGreen = new Color32(35, 189, 38, 255),
            CardInk = Color.white, CardDownInk = new Color(1f, .5f, .45f), CardPlateDown = new Color(.5f, .5f, .55f, 1f);

        sealed class PlayerHudCard
        {
            public RawImage Back, Plate; public TextMeshProUGUI Label, Numbers, Down;
            public int ShownDown = -2, ShownHp = int.MinValue, ShownMax = -1;
        }
        readonly PlayerHudCard[] hudCards = new PlayerHudCard[6];

        // Called at the end of ResolveSceneReferences, once `players` exists.
        void BuildPlayerHudCards()
        {
            if (playerHpPanel == null) return;
            foreach (var group in playerHpPanel.GetComponentsInChildren<LayoutGroup>(true)) group.enabled = false;
            foreach (var fitter in playerHpPanel.GetComponentsInChildren<ContentSizeFitter>(true)) fitter.enabled = false;
            var font = BossPlayerCards.Jersey() ?? BossUiFont;

            int n = Mathf.Max(1, players.Count);
            float rowWidth = n * SlotW + (n - 1) * SlotGap;
            float fit = Mathf.Min(1f, (WallW - 2f * RowSideMargin) / rowWidth);

            for (int k = 0; k < players.Count; k++)
            {
                var p = players[k];
                var back = FindRawImageIn(playerHpPanel, $"RawImage == Player {p.Index + 1}");
                if (back == null) continue;
                if (back.transform.parent is RectTransform column && column != playerHpPanel)
                {
                    column.anchorMin = Vector2.zero; column.anchorMax = Vector2.one; column.pivot = new Vector2(.5f, .5f);
                    column.offsetMin = column.offsetMax = Vector2.zero; column.localScale = Vector3.one;
                }
                // Centred row: slot k of n, measured from the middle of the wall.
                var rect = back.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f); rect.pivot = new Vector2(.5f, 0f);
                rect.sizeDelta = new Vector2(SlotW, PlateH); rect.localScale = Vector3.one * fit;
                rect.anchoredPosition = new Vector2((k - (n - 1) / 2f) * (SlotW + SlotGap) * fit, RowBottom);
                back.texture = null; back.color = Color.clear; back.raycastTarget = false;
                if (back.TryGetComponent(out Outline oldEdge)) oldEdge.enabled = false;
                foreach (var old in new[] { "Card Stripe", "Card Label" })
                {
                    var leftover = FindDirectRectIn(rect, old);
                    if (leftover != null) leftover.gameObject.SetActive(false);
                }

                var card = hudCards[p.Index] ?? (hudCards[p.Index] = new PlayerHudCard());
                card.Back = back; card.ShownDown = -2; card.ShownHp = int.MinValue; card.ShownMax = -1;

                float barLeft = PlateW - BarOverlap, barBottom = (PlateH - BarH) / 2f;
                var barBack = CardChild<RawImage>(rect, "Card Bar Back", Vector2.zero, Vector2.zero,
                    new Vector2(barLeft, barBottom), new Vector2(barLeft + BarW, barBottom + BarH));
                barBack.texture = Resources.Load<Texture2D>("BossSlayer/Hud/PlayerHpBack"); barBack.color = Color.white;
                barBack.transform.SetAsFirstSibling();

                // The scene's HP bar becomes the fill area of the art; only our runtime fill shows inside it.
                var bar = p.HpFill != null ? p.HpFill.rectTransform.parent as RectTransform : null;
                if (bar != null && bar.parent == rect)
                {
                    bar.anchorMin = bar.anchorMax = Vector2.zero; bar.pivot = new Vector2(.5f, .5f); bar.localScale = Vector3.one;
                    bar.offsetMin = new Vector2(barLeft + FillLeft, barBottom + FillBottom);
                    bar.offsetMax = new Vector2(barLeft + FillRight, barBottom + FillTop);
                    foreach (var g in bar.GetComponentsInChildren<Graphic>(true)) if (g != p.HpFill) g.enabled = false;
                    bar.SetSiblingIndex(1);
                }

                card.Plate = CardChild<RawImage>(rect, "Card Plate", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(PlateW, PlateH));
                card.Plate.texture = Resources.Load<Texture2D>("BossSlayer/Hud/PlayerPlate" + (p.Index + 1)); card.Plate.color = Color.white;
                card.Plate.transform.SetAsLastSibling();   // the plate covers the bar's left cap
                card.Label = HudText(card.Plate.rectTransform, "Card Label", font, Vector2.zero, Vector2.one, new Vector2(22f, 14f), new Vector2(-22f, -14f), TextAlignmentOptions.Center);
                card.Label.text = ArenaPlayerLabel(p.Index);

                card.Numbers = HudText(rect, "Card Numbers", font, Vector2.zero, Vector2.zero,
                    new Vector2(barLeft + FillLeft + 300f, barBottom + FillBottom - 2f), new Vector2(barLeft + FillRight - 18f, barBottom + FillTop + 2f), TextAlignmentOptions.Right);
                card.Down = HudText(rect, "Card Down", font, Vector2.zero, Vector2.zero,
                    new Vector2(barLeft + FillLeft, barBottom + FillBottom - 2f), new Vector2(barLeft + FillRight, barBottom + FillTop + 2f), TextAlignmentOptions.Center);
                card.Down.characterSpacing = 8f; card.Down.color = CardDownInk; card.Down.gameObject.SetActive(false);
                StylePlayerCard(p);
            }
        }

        // Pixel-font text sized to its box (Jersey 15, like the boss HUD), with the black card outline for the LED wall.
        static TextMeshProUGUI HudText(RectTransform parent, string name, TMP_FontAsset font, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax, TextAlignmentOptions align)
        {
            var label = CardText(parent, name, font, min, max, offMin, offMax, 60f, align);
            label.fontStyle = FontStyles.Normal; label.color = CardInk;
            label.enableAutoSizing = true; label.fontSizeMin = 8f; label.fontSizeMax = 400f;
            label.transform.SetAsLastSibling();
            return label;
        }
        static T CardChild<T>(RectTransform parent, string name, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax) where T : Graphic
        {
            var existing = FindDirectRectIn(parent, name);
            var rect = existing != null ? existing : BossUiRect.Make(parent, name);
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = offMin; rect.offsetMax = offMax;
            var graphic = rect.GetComponent<T>(); if (graphic == null) graphic = rect.gameObject.AddComponent<T>();
            graphic.raycastTarget = false; return graphic;
        }
        static TextMeshProUGUI CardText(RectTransform parent, string name, TMP_FontAsset font, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax, float size, TextAlignmentOptions align)
        {
            var label = CardChild<TextMeshProUGUI>(parent, name, min, max, offMin, offMax);
            BossFantasyTheme.UseFont(label, font);
            label.fontStyle = FontStyles.Bold; label.alignment = align; label.enableAutoSizing = false; label.fontSize = size;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow;
            // บาส 2026-10-22 bug fix: a card made when the lobby raised the player count (P3+) had no font yet, and the
            // outline setter threw (NullReferenceException in SetOutlineThickness), stopping the session before boss select.
            // The card can still be inactive here (TMP not awake), so the outline goes on a shared material per font,
            // not through outlineWidth.
            if (label.font == null) label.font = font != null ? font : TMP_Settings.defaultFontAsset;
            var outlined = CardOutlineMaterial(label.font);
            if (outlined != null) label.fontSharedMaterial = outlined;
            label.margin = Vector4.zero; return label;
        }
        static readonly System.Collections.Generic.Dictionary<TMP_FontAsset, Material> cardOutlineMaterials = new();
        static Material CardOutlineMaterial(TMP_FontAsset font)
        {
            if (font == null || font.material == null) return null;
            if (cardOutlineMaterials.TryGetValue(font, out var m) && m != null) return m;
            m = new Material(font.material) { name = font.name + " Card Outline" };
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, .18f);
            m.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            cardOutlineMaterials[font] = m;
            return m;
        }

        // Called from UpdatePlayerHp (every frame while a player is down, otherwise on HP changes). Only rewrites
        // colours/text when the shown state actually changes, so the per-frame down tick allocates once a second.
        void StylePlayerCard(PlayerState p)
        {
            var card = p.Index >= 0 && p.Index < hudCards.Length ? hudCards[p.Index] : null;
            if (card == null || card.Back == null || card.Numbers == null) return;
            int hp = Mathf.Max(0, p.Hp), max = Mathf.Max(1, playerMaxHp);
            float ratio = Mathf.Clamp01(hp / (float)max);
            int downSeconds = p.DownTimer > 0f ? Mathf.CeilToInt(p.DownTimer) : -1;
            if (downSeconds == card.ShownDown && hp == card.ShownHp && max == card.ShownMax) return;
            bool down = downSeconds >= 0;
            card.ShownDown = downSeconds; card.ShownHp = hp; card.ShownMax = max;
            if (p.HpFill != null) p.HpFill.color = down ? CardDownGrey : PlayerHpColor(ratio);
            card.Plate.color = down ? CardPlateDown : Color.white;
            card.Label.color = down ? CardDownGrey : CardInk;
            card.Numbers.text = hp + "/" + max;
            if (card.Numbers.gameObject.activeSelf == down) card.Numbers.gameObject.SetActive(!down);
            if (card.Down.gameObject.activeSelf != down) card.Down.gameObject.SetActive(down);
            if (down) card.Down.text = "DOWN  " + downSeconds;
        }
        // The art's green while healthy, then yellow around 40% and red at 15% or less (hue sweep keeps the middle bright).
        static Color PlayerHpColor(float ratio) => ratio >= .65f ? CardHpGreen
            : Color.HSVToRGB(Mathf.Lerp(0f, .33f, Mathf.InverseLerp(.15f, .65f, ratio)), .82f, .9f);

        // The plates and bar frame are the art team's fixed colours (one per player), so the boss theme leaves them alone.
        void ThemePlayerCards(BossThemePalette.BossUiTheme t, float k) { }

        // ---------------------------------------------------------------- final session result (wall)
        // Neutral gold/teal (the session result isn't about one boss). Title/MVP centred, player plates in ranked order
        // spread evenly across the bottom band for however many players took part.
        static readonly Color ResultGold = new Color(1f, .84f, .36f), ResultTeal = new Color(.4f, .88f, 1f), ResultPanel = new Color(.015f, .04f, .085f, .92f);
        const float ResultPlateMaxWidth = 1150f, ResultPlateHeight = 380f, ResultPlateGap = 70f, ResultSideMargin = 260f;
        Tween resultGlowTween;

        void StyleResultScreen(PlayerState mvp)
        {
            if (resultPanel == null) return;
            var menu = FindDirectRectIn(resultPanel, "Image == Result Menu") ?? resultPanel;
            foreach (var group in menu.GetComponentsInChildren<LayoutGroup>(true)) group.enabled = false;
            foreach (var fitter in menu.GetComponentsInChildren<ContentSizeFitter>(true)) fitter.enabled = false;
            var font = BossUiFont;

            var title = FindDirectRectIn(menu, "Title");
            if (title != null && title.TryGetComponent(out TextMeshProUGUI titleText))
            {
                Place(title, new Vector2(.5f, 1f), new Vector2(0, -110f), new Vector2(5200f, 300f));
                StyleResultText(titleText, font, "BOSS SLAYER", 250f, ResultGold, 16f);
            }
            var subtitle = CardText(menu, "Result Subtitle", font, new Vector2(.5f, 1f), new Vector2(.5f, 1f), Vector2.zero, Vector2.zero, 92f, TextAlignmentOptions.Center);
            Place(subtitle.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -420f), new Vector2(5200f, 120f));
            int bosses = Mathf.Max(1, sessionBossIndex);
            StyleResultText(subtitle, font, "SESSION COMPLETE   •   " + bosses + (bosses == 1 ? " BOSS" : " BOSSES") + " DEFEATED", 92f, ResultTeal, 22f);

            StyleMvpPlate(mvp, font);
            StyleResultPlates(font);
        }

        void StyleMvpPlate(PlayerState mvp, TMP_FontAsset font)
        {
            if (mvpScoreImage == null) return;
            var plate = mvpScoreImage.rectTransform;
            // 560..1380 units from the top: clear of the subtitle, 200 units above the player plates.
            Place(plate, new Vector2(.5f, 1f), new Vector2(0, -560f), new Vector2(2700f, 820f));
            plate.localScale = Vector3.one;
            mvpScoreImage.color = Color.clear; // the copied plate texture's bottom rule is what struck through the score
            var glow = ResultChrome(plate, "Result Chrome == Glow", BossThemePalette.SoftHalo, 31f / 90f, -90f, Alpha(ResultGold, .55f));
            var back = ResultChrome(plate, "Result Chrome == Back", BossThemePalette.SoftHalo, 31f / 40f, 0f, ResultPanel);
            var rule = ResultChrome(plate, "Result Chrome == Rule", null, 1f, 0f, ResultGold);
            Place(rule.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -26f), new Vector2(2300f, 12f));
            glow.transform.SetAsFirstSibling(); back.transform.SetSiblingIndex(1); rule.transform.SetSiblingIndex(2);
            resultGlowTween?.Kill();
            glow.canvasRenderer.SetAlpha(1f);
            resultGlowTween = DOVirtual.Float(.55f, 1f, 1.3f, a => glow.canvasRenderer.SetAlpha(a)).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(glow.gameObject);

            var tag = FindDirectRectIn(plate, "Text (TMP) == Game Over"); if (tag != null) tag.gameObject.SetActive(false); // title already says it
            var mvpTag = FindDirectRectIn(plate, "Text (TMP) == MVP");
            if (mvpTag != null && mvpTag.TryGetComponent(out TextMeshProUGUI mvpLabel))
            {
                Stretch(mvpTag, new Vector2(.04f, .76f), new Vector2(.96f, .95f));
                StyleResultText(mvpLabel, font, "MVP", 130f, ResultGold, 60f);
            }
            if (mvpScoreText is TextMeshProUGUI score)
            {
                Stretch(score.rectTransform, new Vector2(.04f, .05f), new Vector2(.96f, .76f));
                StyleResultText(score, font, score.text, 170f, Color.white, 6f);
                score.textWrappingMode = TextWrappingModes.NoWrap; score.lineSpacing = -8f;
                if (mvp != null)
                    score.text = "<color=#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(PlayerColor(mvp.Index), Color.white, .35f)) + ">PLAYER " + (mvp.Index + 1)
                        + "</color>\n<size=145%><color=#" + ColorUtility.ToHtmlStringRGB(ResultGold) + ">" + mvp.Damage.ToString("n0") + "</color></size>";
            }
        }

        void StyleResultPlates(TMP_FontAsset font)
        {
            if (playerScorePanel == null) return;
            var panel = playerScorePanel;
            // Bottom band, 150..580 units above the bottom edge.
            panel.anchorMin = new Vector2(0, 0); panel.anchorMax = new Vector2(1, 0); panel.pivot = new Vector2(.5f, 0);
            panel.sizeDelta = new Vector2(0, ResultPlateHeight + 50f); panel.anchoredPosition = new Vector2(0, 150f); panel.localScale = Vector3.one;
            if (panel.TryGetComponent(out Image panelImage)) panelImage.color = Color.clear;
            if (!(panel.parent is RectTransform menu)) return;
            float wallWidth = menu.rect.width > 1f ? menu.rect.width : 8192f;

            var ranked = new System.Collections.Generic.List<PlayerState>(players);
            ranked.Sort((a, b) => a.Damage != b.Damage ? b.Damage.CompareTo(a.Damage) : a.Index.CompareTo(b.Index));
            int n = ranked.Count; if (n == 0) return;
            float width = Mathf.Min(ResultPlateMaxWidth, (wallWidth - 2f * ResultSideMargin - (n - 1) * ResultPlateGap) / n);
            float span = n * width + (n - 1) * ResultPlateGap;
            for (int rank = 0; rank < n; rank++)
            {
                var p = ranked[rank];
                var image = FindResultPlayerRawImage(p.Index);
                if (image == null) continue;
                var plate = image.rectTransform;
                plate.anchorMin = plate.anchorMax = new Vector2(.5f, .5f); plate.pivot = new Vector2(.5f, .5f); plate.localScale = Vector3.one;
                plate.sizeDelta = new Vector2(width, ResultPlateHeight);
                plate.anchoredPosition = new Vector2(-span * .5f + width * .5f + rank * (width + ResultPlateGap), 0);
                image.color = Color.clear;
                bool top = rank == 0 && p.Damage > 0;
                var accent = PlayerColor(p.Index);
                var back = ResultChrome(plate, "Result Chrome == Back", BossThemePalette.SoftHalo, 31f / 34f, 0f, ResultPanel);
                var stripe = ResultChrome(plate, "Result Chrome == Stripe", null, 1f, 0f, top ? ResultGold : accent);
                stripe.rectTransform.anchorMin = new Vector2(.06f, 1f); stripe.rectTransform.anchorMax = new Vector2(.94f, 1f);
                stripe.rectTransform.pivot = new Vector2(.5f, 1f); stripe.rectTransform.sizeDelta = new Vector2(0, 14f); stripe.rectTransform.anchoredPosition = new Vector2(0, -18f);
                back.transform.SetAsFirstSibling(); stripe.transform.SetSiblingIndex(1);

                var label = FindTMPIn(plate, "== Player") as TextMeshProUGUI;
                if (label != null)
                {
                    Stretch(label.rectTransform, new Vector2(.05f, .53f), new Vector2(.95f, .9f));
                    StyleResultText(label, font, "#" + (rank + 1) + "   PLAYER " + (p.Index + 1), 96f, Color.Lerp(accent, Color.white, .3f), 8f);
                }
                var score = FindTMPIn(plate, "Score") as TextMeshProUGUI;
                if (score != null)
                {
                    Stretch(score.rectTransform, new Vector2(.05f, .08f), new Vector2(.95f, .53f));
                    StyleResultText(score, font, p.Damage.ToString("n0"), 150f, top ? ResultGold : Color.white, 4f);
                }
            }
        }

        static void StyleResultText(TextMeshProUGUI text, TMP_FontAsset font, string value, float size, Color color, float spacing)
        {
            BossFantasyTheme.UseFont(text, font);
            text.text = value; text.fontStyle = FontStyles.Bold; text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true; text.fontSizeMax = size; text.fontSizeMin = size * .5f; text.fontSize = size;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
            text.characterSpacing = spacing; text.margin = Vector4.zero; text.color = color; text.raycastTarget = false;
            text.outlineWidth = .16f; text.outlineColor = new Color(0f, 0f, 0f, .9f);
        }
        static Image ResultChrome(RectTransform parent, string name, Sprite sprite, float ppu, float inset, Color color)
        {
            var existing = FindDirectRectIn(parent, name);
            var image = existing != null && existing.TryGetComponent(out Image found) ? found : ThemeImage(parent, name, sprite, ppu, inset);
            image.color = color; return image;
        }
        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, anchor.y); rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
        }
    }
}
