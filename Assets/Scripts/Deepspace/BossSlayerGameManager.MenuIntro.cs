using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // บาส 2026-10-22: the menu animation (MenuIntroVideo, like Deepspace's) for Boss Slayer, which has no menu panel of
    // its own: full screen on both screens while the lobby waits for players, on the wall through the player pads too,
    // with the lobby card kept on top. The boss select's wall logo screen plays it behind the logo
    // (BossSelectionPanel.ApplyWallLogo).
    public sealed partial class BossSlayerGameManager
    {
        BossStartWall startWall;   // 2026-11-18: the art team's Start screen on the wall while players pick their cards
        bool introAttached;

        // 2026-11-19: "ทำไมกดเล่นกลายเป็น VDO ทั้งที่บอกให้ลบออกไปเลย" - the menu video is gone from Boss Slayer. While the
        // game waits (for tags, or for the launcher's START) the wall shows the Start screen and the floor shows the
        // player select's rune floor with no pads; the pads come in once START is pressed and the tags have settled.
        void UpdateMenuIntro()
        {
            if (!introAttached)
            {
                introAttached = true;
                HideBakedMenuVideo(cutsceneArea);
                HideBakedMenuVideo(playArea);
                startWall = BossStartWall.Attach(cutsceneArea, CodexTitleFont);
            }
            if (startWall != null) startWall.Show(pickingPlayers);
            if (!waitingForPlayers) return;
            if (pads != null && !pads.Showing) pads.ShowBackdrop();
            SkinLobbyCard(cutsceneArea, CodexTitleFont, BossUiFont); SkinLobbyCard(playArea, CodexTitleFont, BossUiFont);
        }

        // 2026-11-19: everybody has picked: the wall's Start screen (logo, embers and all) cracks and crumbles like stone onto
        // the boss select (BossWallCrumble; it replaced the sword cut, BossWallSlash). The live Start screen is hidden at
        // once; the falling pieces are a photo of it.
        void PlayWallSlashTransition()
        {
            if (startWall == null || !startWall.gameObject.activeInHierarchy) return;
            BossWallCrumble.Play(cutsceneArea, wallDisplayCamera);
            BossFightAudio.PlayUi("wall_crumble", .7f, .5f, 4);   // 2026-11-25: the Start wall breaking apart
            // ...and the pieces raining down onto the floor once the cracks have spread (BossWallCrumble: falling from .85 s).
            DG.Tweening.DOVirtual.DelayedCall(.85f, () => BossFightAudio.PlayUi("wall_rocks_fall", .7f, .5f, 4), true);
            // ...then one big block slamming down in the thick of the fall (2026-11-25 "หินก้อนใหญ่ร่วง").
            DG.Tweening.DOVirtual.DelayedCall(2.3f, () => BossFightAudio.PlayUi("wall_boulder_fall", .8f, .5f, 4), true);
            startWall.Show(false);
            bossRevealDelay = BossWallCrumble.Duration;   // the floor's boss cards come in once the pieces are gone
        }

        // Seconds before the next boss select's cards start coming in (ShowBossSelection reads and clears it).
        float bossRevealDelay;

        void RevealBossCards()
        {
            if (floorBossSelection != null) floorBossSelection.PlayReveal(bossRevealDelay);
            bossRevealDelay = 0f;
        }

        // Scenes baked before 2026-11-19 still hold the (hidden) menu video objects: keep them off.
        static void HideBakedMenuVideo(RectTransform area)
        {
            var video = area != null ? area.Find("RawImage == Menu Intro Video") : null;
            if (video != null) video.gameObject.SetActive(false);
        }

        // บาส 2026-10-27: the shared lobby card ("PLAYERS / 3 PLAYERS • 2") in the codex look: a night tablet with a gilded
        // trim, carved-gold title, Cinzel lettering, over a lighter veil so the menu video still shows. PlayerLobby's card
        // repaints itself plain when it is (re)made in Play, so this re-applies whenever it finds the plain colour again.
        static readonly Color LobbyTablet = UiThemeKit.Alpha(BossFantasyTheme.Night, .9f);

        static void SkinLobbyCard(RectTransform area, TMP_FontAsset title, TMP_FontAsset body)
        {
            var root = area != null ? area.Find("Panel == Player Lobby") : null;
            var box = root != null ? root.Find("Card") as RectTransform : null;
            if (box == null || !box.TryGetComponent(out Image back)) return;
            var row = box.Find("Players");   // chips are made as players arrive: style each new one
            if (row != null && body != null)
                foreach (Transform chip in row)
                    if (chip.Find("Label") is Transform l && l.TryGetComponent(out TMP_Text chipLabel) && chipLabel.font != body)
                        BossFantasyTheme.Outlined(chipLabel, body, Color.white, .2f, 2);
            if (back.color == LobbyTablet) return;
            if (root.TryGetComponent(out Image veil)) veil.color = new Color(0, 0, 0, .4f);
            float corner = Mathf.Max(8f, area.rect.height * .035f);
            UiThemeKit.Skin(back, UiThemeKit.Shape.Chamfer, LobbyTablet, corner);
            var trim = UiThemeKit.Ring(box, "Codex Trim", UiThemeKit.Shape.Chamfer, BossFantasyTheme.Gold, Mathf.Max(2f, area.rect.height * .004f), corner * .35f);
            trim.transform.SetSiblingIndex(0);
            if (box.Find("Title") is Transform t && t.TryGetComponent(out TMP_Text titleText)) BossFantasyTheme.GoldText(titleText, title, 12);
            if (box.Find("Status") is Transform s && s.TryGetComponent(out TMP_Text status)) BossFantasyTheme.Outlined(status, title, BossFantasyTheme.Parchment, .2f, 6);
            if (box.Find("Hint") is Transform h && h.TryGetComponent(out TMP_Text hint)) BossFantasyTheme.Letter(hint, body, BossFantasyTheme.ParchmentDim, 3);
        }

#if UNITY_EDITOR
        // Editor bake (with the player select): the lobby's menu videos and lobby cards as real, hidden scene objects.
        void BakeMenuIntroUi(TMP_FontAsset title, TMP_FontAsset body)
        {
            foreach (var (area, wall) in new[] { (cutsceneArea, true), (playArea, false) })
            {
                if (area == null) continue;
                PlayerDropoutGuard.Card.Create(area, wall, body, "Panel == Player Lobby");
                if (wall) BossStartWall.Attach(area, title);
                SkinLobbyCard(area, title, body);
            }
        }
#endif
    }
}
