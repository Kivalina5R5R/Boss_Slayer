using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // บาส 2026-10-20: the boss rush result screen, laid out like the web prototype and dressed in the Boss Fight
    // "arcane codex" look (BossFantasyTheme: chamfered night-stone plates, gilded borders and rules, carved-gold titles).
    // Wall: RAID COMPLETE + bosses defeated + team damage + the king's fate + MVP on the left, ranked rows sliding in on
    // the right with the damage counting up. Floor: bosses defeated, fate, MVP and confetti over the king's lava floor.
    // The floor's result panel stays (SKIP / Restart / Exit + ResultChoiceFlow); only its space backdrop and its other
    // children are hidden while the rush layout shows. The wall result panel is replaced.
    public sealed partial class BossSlayerGameManager
    {
        int rushKills;
        RectTransform rushResultWall, rushResultFloor;
        readonly List<GameObject> rushHiddenResultParts = new();
        readonly List<Tween> rushResultTweens = new();
        Image rushHiddenPanelBackdrop;
        Texture rushFloorBackgroundBefore;
        static readonly Color RushViolet = new Color(.75f, .45f, 1f);

        // ---------------------------------------------------------------- shared codex styling (perk vote, clock, results)
        TMP_FontAsset CodexTitleFont => floorBossSelection != null && floorBossSelection.fantasyTitle != null ? floorBossSelection.fantasyTitle : BossUiFont;

        // A chamfered night-stone plate with a gilded frame and inner trim; r is in the area's rect space, u = canvas unit.
        UiThemeKit.Plate CodexPlate(RectTransform parent, string name, Rect r, Rect area, float u, Color? accent = null, bool glow = false)
        {
            var trim = accent ?? BossFantasyTheme.GoldDeep;
            var plate = UiThemeKit.MakePlate(parent, name, UiThemeKit.Alpha(BossFantasyTheme.Gold, .85f), UiThemeKit.Alpha(BossFantasyTheme.Night, .97f),
                34f * u, 5f * u, true, UiThemeKit.Alpha(trim, .6f), glow, UiThemeKit.Shape.Chamfer);
            Place(plate.rect, r, area);
            if (glow && plate.glow != null) plate.glow.color = UiThemeKit.Alpha(accent ?? BossFantasyTheme.Gold, .4f);
            return plate;
        }

        // Latin headings in carved gold (Cinzel); Thai lines in the body font (Cinzel has no Thai glyphs).
        void CodexGold(TMP_Text t, bool latin, float spacing = 6) => BossFantasyTheme.GoldText(t, latin ? CodexTitleFont : BossUiFont, spacing, .12f);
        void CodexLetter(TMP_Text t, Color face, float spacing = 2) => BossFantasyTheme.Outlined(t, BossUiFont, face, .14f, spacing);

        // ---------------------------------------------------------------- result layout
        void ShowRushResultLayout(PlayerState mvp)
        {
            HideRushResultLayout();
            var ranked = new List<PlayerState>();
            for (int i = 0; i < players.Count && i < playerCount; i++) ranked.Add(players[i]);
            ranked.Sort((a, b) => a.Damage != b.Damage ? b.Damage.CompareTo(a.Damage) : a.Index.CompareTo(b.Index));
            int teamDamage = 0; foreach (var p in ranked) teamDamage += p.Damage;
            int bosses = rushKills + (rushKingDefeated ? 1 : 0);
            string fate = rushKingDefeated ? "ABYSSAL KING ล้มแล้ว!" : "THE KING ENDURES...";
            var owned = new List<string>(); foreach (var id in rushPerks) { var perk = PerkById(id); if (perk != null) owned.Add(perk.Name); }

            // ---- wall
            if (cutsceneArea != null)
            {
                if (resultPanel != null) resultPanel.gameObject.SetActive(false);
                rushResultWall = NewLayer(cutsceneArea, "Rush Result (wall)");
                var area = cutsceneArea.rect; float u = area.height / 2160f;
                CodexPlate(rushResultWall, "Summary Plate", Sub(area, new Rect(.02f, .05f, .36f, .9f)), area, u, RushViolet, true);
                var title = Txt(rushResultWall, area, "Title", new Rect(.04f, .77f, .32f, .14f), rushKingDefeated ? "RAID COMPLETE" : "RAID OVER", Color.white, FontStyles.Normal);
                CodexGold(title, true, 10);
                BossFantasyTheme.Rule(rushResultWall, "Title Rule Left", .06f, .17f, .75f, 4 * u, true);
                BossFantasyTheme.Rule(rushResultWall, "Title Rule Right", .23f, .34f, .75f, 4 * u, false);
                CodexGold(Txt(rushResultWall, area, "Bosses", new Rect(.04f, .55f, .32f, .18f), $"BOSSES DEFEATED {bosses}/{RushTotalBosses}", Color.white, FontStyles.Normal), false, 2);
                CodexLetter(Txt(rushResultWall, area, "Team Damage", new Rect(.04f, .46f, .32f, .08f), $"ดาเมจทั้งทีม {teamDamage:n0}", Color.white, FontStyles.Normal), BossFantasyTheme.ParchmentDim);
                CodexLetter(Txt(rushResultWall, area, "Fate", new Rect(.04f, .37f, .32f, .08f), fate, Color.white, FontStyles.Normal),
                    rushKingDefeated ? BossFantasyTheme.GoldLight : new Color(1f, .5f, .82f));
                if (mvp != null)
                {
                    CodexPlate(rushResultWall, "MVP Plate", Sub(area, new Rect(.07f, .09f, .26f, .25f)), area, u, PlayerColor(mvp.Index), true);
                    CodexGold(Txt(rushResultWall, area, "MVP Label", new Rect(.08f, .24f, .24f, .08f), "MVP", Color.white, FontStyles.Normal), true, 14);
                    CodexLetter(Txt(rushResultWall, area, "MVP", new Rect(.08f, .11f, .24f, .13f), "PLAYER " + (mvp.Index + 1), Color.white, FontStyles.Normal),
                        Color.Lerp(PlayerColor(mvp.Index), Color.white, .25f), 6);
                }
                if (owned.Count > 0)
                    CodexLetter(Txt(rushResultWall, area, "Perks", new Rect(.42f, .03f, .52f, .07f), "พลังทีม: " + string.Join("  •  ", owned), Color.white, FontStyles.Normal), BossFantasyTheme.ParchmentDim);
                Punch(title.rectTransform, 0f);

                float rowH = Mathf.Min(.17f, .8f / Mathf.Max(1, ranked.Count)), top = .93f;
                for (int rank = 0; rank < ranked.Count; rank++)
                {
                    var p = ranked[rank];
                    var rowRect = Sub(area, new Rect(.41f, top - (rank + 1) * rowH, .55f, rowH * .84f));
                    var row = NewLayer(rushResultWall, "Rank " + (rank + 1));
                    row.anchorMin = row.anchorMax = new Vector2(.5f, .5f); row.sizeDelta = rowRect.size; row.anchoredPosition = rowRect.center - area.center;
                    var local = new Rect(-rowRect.width * .5f, -rowRect.height * .5f, rowRect.width, rowRect.height);
                    var plate = CodexPlate(row, "Row Plate", local, local, u, rank == 0 ? BossFantasyTheme.GoldLight : PlayerColor(p.Index), rank == 0);
                    if (rank == 0) UiThemeKit.Recolor(plate, BossFantasyTheme.GoldLight, UiThemeKit.Alpha(BossFantasyTheme.StoneLight, .95f));
                    // the player's colour as a gem on the left edge
                    var gem = new GameObject("Player Gem", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    gem.rectTransform.SetParent(row, false); gem.raycastTarget = false; gem.color = PlayerColor(p.Index);
                    gem.rectTransform.anchorMin = gem.rectTransform.anchorMax = new Vector2(0, .5f);
                    gem.rectTransform.sizeDelta = Vector2.one * local.height * .36f; gem.rectTransform.anchoredPosition = new Vector2(local.height * .45f, 0);
                    gem.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                    CodexGold(RowTxt(row, local, new Rect(.08f, .12f, .12f, .76f), "#" + (rank + 1)), true, 2);
                    CodexLetter(RowTxt(row, local, new Rect(.22f, .12f, .38f, .76f), "PLAYER " + (p.Index + 1), TextAlignmentOptions.Left), Color.Lerp(PlayerColor(p.Index), Color.white, .3f), 4);
                    var score = RowTxt(row, local, new Rect(.58f, .12f, .38f, .76f), "0", TextAlignmentOptions.Right);
                    if (rank == 0) CodexGold(score, true, 2); else CodexLetter(score, BossFantasyTheme.Parchment, 2);
                    // slide in from the left one by one, then count the damage up
                    float delay = .6f + rank * .35f, rest = row.anchoredPosition.x;
                    row.anchoredPosition = new Vector2(rest - area.width * .6f, row.anchoredPosition.y);
                    rushResultTweens.Add(row.DOAnchorPosX(rest, .5f).SetDelay(delay).SetEase(Ease.OutCubic).SetUpdate(true));
                    int target = p.Damage;
                    rushResultTweens.Add(DOVirtual.Float(0, target, 1.5f, v => score.text = Mathf.RoundToInt(v).ToString("n0")).SetDelay(delay + .4f).SetEase(Ease.OutCubic).SetUpdate(true));
                }
            }

            // ---- floor
            if (gameplayResultPanel != null)
            {
                // The panel's own full-screen space backdrop would cover everything: hide it and show the king's lava floor.
                if (gameplayResultPanel.TryGetComponent(out Image backdrop) && backdrop.enabled) { backdrop.enabled = false; rushHiddenPanelBackdrop = backdrop; }
                if (bossFloorBackgroundImage != null)
                {
                    rushFloorBackgroundBefore = bossFloorBackgroundImage.texture;
                    var lava = LoadBackgroundTexture("BossBackgrounds/BGFloorHellBoss");
                    if (lava != null) { bossFloorBackgroundImage.texture = lava; PlayBossBackgroundMovie(bossFloorBackgroundImage, ref floorBackgroundVideo, lava); }
                }
                foreach (Transform child in gameplayResultPanel)
                    if (child.gameObject.activeSelf && child.GetComponentsInChildren<Button>(true).Length == 0) { child.gameObject.SetActive(false); rushHiddenResultParts.Add(child.gameObject); }
                // First child of the result panel (its buttons stay on top). The panel has a HorizontalLayoutGroup that
                // would grab and resize this layer - opt out of it. The panel spans the floor canvas = the floor camera.
                rushResultFloor = NewLayer(gameplayResultPanel, "Rush Result (floor)");
                rushResultFloor.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                rushResultFloor.SetAsFirstSibling();
                var area = gameplayResultPanel.rect; float u = area.height / FloorDisplaySettings.Height;
                var dim = rushResultFloor.gameObject.AddComponent<Image>(); dim.raycastTarget = false; dim.color = UiThemeKit.Alpha(BossFantasyTheme.Night, .55f);
                Confetti(rushResultFloor, area);
                CodexPlate(rushResultFloor, "Summary Plate", Sub(area, new Rect(.2f, .34f, .6f, .5f)), area, u, RushViolet, true);
                CodexGold(Txt(rushResultFloor, area, "Raid", new Rect(.25f, .72f, .5f, .09f), rushKingDefeated ? "RAID COMPLETE" : "RAID OVER", Color.white, FontStyles.Normal), true, 8);
                BossFantasyTheme.Rule(rushResultFloor, "Raid Rule", .3f, .7f, .7f, 3 * u, true);
                CodexGold(Txt(rushResultFloor, area, "Bosses", new Rect(.25f, .54f, .5f, .15f), $"BOSSES DEFEATED {bosses}/{RushTotalBosses}", Color.white, FontStyles.Normal), false, 2);
                CodexLetter(Txt(rushResultFloor, area, "Fate", new Rect(.25f, .46f, .5f, .07f), fate, Color.white, FontStyles.Normal), rushKingDefeated ? BossFantasyTheme.GoldLight : new Color(1f, .5f, .82f));
                if (mvp != null)
                    CodexLetter(Txt(rushResultFloor, area, "MVP", new Rect(.25f, .37f, .5f, .08f), $"MVP  PLAYER {mvp.Index + 1}  •  {mvp.Damage:n0}", Color.white, FontStyles.Normal),
                        Color.Lerp(PlayerColor(mvp.Index), Color.white, .25f), 4);
            }
        }

        void HideRushResultLayout()
        {
            foreach (var tween in rushResultTweens) tween?.Kill();
            rushResultTweens.Clear();
            if (rushResultWall != null) Destroy(rushResultWall.gameObject);
            if (rushResultFloor != null) Destroy(rushResultFloor.gameObject);
            rushResultWall = rushResultFloor = null;
            foreach (var part in rushHiddenResultParts) if (part != null) part.SetActive(true);
            rushHiddenResultParts.Clear();
            if (rushHiddenPanelBackdrop != null) { rushHiddenPanelBackdrop.enabled = true; rushHiddenPanelBackdrop = null; }
            if (rushFloorBackgroundBefore != null && bossFloorBackgroundImage != null) bossFloorBackgroundImage.texture = rushFloorBackgroundBefore;
            rushFloorBackgroundBefore = null;
            RefreshFireArena();
        }

        // ---------------------------------------------------------------- small layout helpers (rects are fractions of the area)
        static RectTransform NewLayer(RectTransform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            return rect;
        }
        static Rect Sub(Rect area, Rect f) => new Rect(area.xMin + area.width * f.x, area.yMin + area.height * f.y, area.width * f.width, area.height * f.height);
        static void PlaceIn(RectTransform rect, Rect area, Rect f) => Place(rect, Sub(area, f), area);

        TextMeshProUGUI Txt(RectTransform parent, Rect area, string name, Rect f, string text, Color color, FontStyles style)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false); PlaceIn(label.rectTransform, area, f);
            BossFantasyTheme.UseFont(label, BossUiFont);
            label.text = text; label.color = color; label.fontStyle = style; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.enableAutoSizing = true; label.fontSizeMin = 10; label.fontSizeMax = area.height * f.height * .8f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        TextMeshProUGUI RowTxt(RectTransform row, Rect local, Rect f, string text, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var label = Txt(row, local, "Text", f, text, Color.white, FontStyles.Normal);
            label.alignment = align;
            return label;
        }

        void Punch(RectTransform rect, float delay)
        {
            rect.localScale = Vector3.one * 1.6f;
            rushResultTweens.Add(rect.DOScale(1f, .45f).SetDelay(delay).SetEase(Ease.OutBack).SetUpdate(true));
        }

        void Confetti(RectTransform parent, Rect area)
        {
            Color[] colors = { BossFantasyTheme.GoldLight, RushViolet, new Color(.35f, .65f, 1f), new Color(.35f, 1f, .55f) };
            for (int i = 0; i < 70; i++)
            {
                var piece = new GameObject("Confetti", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                piece.rectTransform.SetParent(parent, false); piece.raycastTarget = false; piece.color = colors[i % colors.Length];
                float size = area.height * Random.Range(.012f, .022f);
                piece.rectTransform.sizeDelta = new Vector2(size * 1.6f, size);
                float x = Random.Range(area.xMin, area.xMax) - area.center.x, fall = Random.Range(3.5f, 6.5f);
                var rect = piece.rectTransform;
                // falls from the top edge to the bottom edge of the floor only (no mask on this flat world-space canvas)
                rect.anchoredPosition = new Vector2(x, area.height * .4f); // below the top edge: the wall camera sees a sliver of the floor seam
                rushResultTweens.Add(rect.DOAnchorPosY(-area.height * .48f, fall).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetDelay(Random.Range(0, fall)).SetUpdate(true));
                rushResultTweens.Add(rect.DOLocalRotate(new Vector3(0, 0, Random.Range(-720f, 720f)), fall, RotateMode.FastBeyond360) /* local: the floor canvas lies flat in world space */.SetLoops(-1, LoopType.Restart).SetUpdate(true));
            }
        }
    }
}
