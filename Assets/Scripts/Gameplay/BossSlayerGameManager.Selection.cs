using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    public sealed partial class BossSlayerGameManager
    {
        [SerializeField] BossSelectionPanel floorBossSelection, wallBossSelection;
        bool selectingBoss;
        int selectedBossKind;
        int baseSessionHp, baseSessionDamage;
        float baseSessionGap, baseSessionFinalGap;
        bool sessionBalanceCaptured;
        public bool IsSelectingBoss => selectingBoss;

        // บาส 2026-10-02: stepping on a card used to start a 5 s countdown by itself. 2026-11-19: stepping on a card
        // only picks it (sparks on the card); stepping on SELECT under the cards starts a 3-2-1, and at zero the pick
        // is confirmed. Once the count runs the pick is locked, so players walking off the cards cannot change it.
        [Tooltip("Seconds of the 3-2-1 after SELECT is pressed, before the picked boss starts.")]
        [SerializeField, Min(1f)] float bossSelectCountdownSeconds = 3f;
        float bossAutoConfirmTimer;
        bool bossCountdownRunning;
        // 2026-11-26 "เปลี่ยนจากแค่โดนเป็นต้องยืนค้าง": walking players must stand on SELECT this long (its load bar fills)
        // before the 3-2-1 starts; stepping off empties it. A click (mouse / LED step) still presses it at once.
        [Tooltip("Seconds a player must stand on SELECT before the 3-2-1 starts.")]
        [SerializeField, Min(.1f)] float bossSelectHoldSeconds = 1.5f;
        float bossSelectHold;

        void UpdateBossAutoConfirm()
        {
            FollowPlayersOutsideFight();   // บาส 2026-11-18: the players' characters walk on the boss select too
            BossFightAudio.PlayUi("debris_ambience", .25f, 4.9f, 0);   // 2026-11-25: pebbles trickling in the hall, every 5 s clip
            PickBossUnderPlayers();
            TickSelectHold();
            FocusWallOnStoodBoss();
            if (!bossCountdownRunning || selectedBossKind <= 0 || queuedNextBoss == null) return;
            bossAutoConfirmTimer -= Time.unscaledDeltaTime;
            int shown = Mathf.Max(1, Mathf.CeilToInt(bossAutoConfirmTimer));
            floorBossSelection.ShowAutoConfirm(selectedBossKind, shown);
            if (wallBossSelection != null) wallBossSelection.ShowAutoConfirm(selectedBossKind, shown);
            if (bossAutoConfirmTimer <= 0f) ConfirmBossSelection();
        }

        // Fills SELECT's bar while a player stands on it with a boss picked; full = the 3-2-1 starts.
        void TickSelectHold()
        {
            if (floorBossSelection == null) return;
            if (bossCountdownRunning) { floorBossSelection.ShowSelectHold(1f); return; }
            bool standing = false;
            foreach (int card in bossCardUnderPlayer) standing |= card == SelectButtonCard;
            bool ready = standing && queuedNextBoss != null && selectedBossKind > 0 && floorBossSelection.confirm != null && floorBossSelection.confirm.interactable;
            bossSelectHold = ready ? bossSelectHold + Time.unscaledDeltaTime : 0f;
            floorBossSelection.ShowSelectHold(bossSelectHold / bossSelectHoldSeconds);
            if (bossSelectHold >= bossSelectHoldSeconds) { bossSelectHold = 0f; StartBossCountdown(); }
        }

        // SELECT on the floor (LED step, a full hold of the walking cursor, or mouse): start the 3-2-1 for the picked boss.
        public void StartBossCountdown()
        {
            if (!selectingBoss || bossCountdownRunning || queuedNextBoss == null || selectedBossKind <= 0) return;
            bossCountdownRunning = true;
            bossAutoConfirmTimer = bossSelectCountdownSeconds;
            // 2026-11-25 "เสียงนับ 3 2 1 แบบ BountyHunter": one clip timed like Bounty Hunter's race start - the same short
            // bell + war drum on 3, 2 and 1 (0, 1, 2 s); no GO, the boss's roar takes that beat (made for the default
            // bossSelectCountdownSeconds of 3).
            BossFightAudio.PlayUi("select_countdown", .6f, .5f, 4);
        }

        void ApplySessionDifficulty(int round)
        {
            if (!sessionBalanceCaptured)
            {
                baseSessionHp = bossMaxHp; baseSessionDamage = bossHitDamage;
                baseSessionGap = attackGapSeconds; baseSessionFinalGap = finalPhaseAttackGapSeconds;
                sessionBalanceCaptured = true;
            }
            int tier = Mathf.Max(0, round - 1);
            float speedStep = IsRushActive ? rushSpeedStepPerBoss : .12f;
            bossMaxHp = IsRushActive ? RushBossHp(round) : Mathf.RoundToInt(baseSessionHp * (1f + .25f * tier));
            // บาส 2026-10-06 (Balance Note B1): no ATK growth per boss (was +15%) - players get no upgrades between bosses,
            // so a bigger number is a penalty, not a challenge. Difficulty grows through HP and attack speed only.
            bossHitDamage = baseSessionDamage;
            attackGapSeconds = baseSessionGap / (1f + speedStep * tier);
            finalPhaseAttackGapSeconds = baseSessionFinalGap / (1f + speedStep * tier);
        }
        void ShowBossSelection()
        {
            // บาส 2026-11-02: the players still pick on the floor; the kiosk gets a spare CONFIRM once a boss is picked
            Fortal.Arena.FortalArenaButtons.Add("Confirm Boss", () => "CONFIRM", ConfirmBossSelection, () => selectingBoss && queuedNextBoss != null, true);
            HideBossVictory();
            ClearCombatJuice();
            selectingBoss = true; awaitingPlayers = false; gameStarted = false;
            selectedBossKind = 0; queuedNextBoss = null; bossCountdownRunning = false; bossAutoConfirmTimer = bossSelectCountdownSeconds;
            RushOnShowSelection();
            SetGameplayHudVisible(false); SetBossVisible(false); SetMenuVisible(false);
            // บาส 2026-11-18: the floor keeps showing each player's character over the boss cards (RaisePlayersOverCards).
            if (characterPanel != null) characterPanel.gameObject.SetActive(true);
            System.Array.Fill(bossCardUnderPlayer, -1);
            if (readyScreens != null) foreach (var screen in readyScreens) if (screen != null) screen.gameObject.SetActive(false);
            if (resultPanel != null) resultPanel.gameObject.SetActive(false);
            HideRaidResult();
            if (gameplayResultPanel != null) gameplayResultPanel.gameObject.SetActive(false);
            if (floorBossSelection == null) floorBossSelection = BossSelectionPanel.Create(playArea, statusText != null ? statusText.font : null);
            if (wallBossSelection == null && cutsceneArea != null) wallBossSelection = BossSelectionPanel.Create(cutsceneArea, statusText != null ? statusText.font : null);
            floorBossSelection.Bind(SelectBossKind, StartBossCountdown, true);
            ShowFloorMagicBackdrop(floorBossSelection.transform as RectTransform);
            if (wallBossSelection != null) wallBossSelection.Bind(SelectBossKind, StartBossCountdown, false);
            RefreshBossSelection();
            RevealBossCards();   // 2026-11-19: one card at a time, SELECT last (.MenuIntro.cs / BossSelectionPanel.Reveal.cs)
            sfx?.Score?.Menu();
        }
        // บาส 2026-11-18: the floor boss select stands on the same looping rune floor as the player select (was a plain
        // night fill). First child, behind the cards and the border that Bind's skin puts first just before this.
        static void ShowFloorMagicBackdrop(RectTransform panel)
        {
            if (panel == null) return;
            var backdrop = UiThemeKit.Child(panel, "Magic Backdrop");
            UiThemeKit.Stretch(backdrop, 0);
            if (!backdrop.TryGetComponent(out RawImage image)) image = backdrop.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            backdrop.SetAsFirstSibling();
            BossFloorMagicVideo.Attach(image);
        }
        void RefreshBossSelection()
        {
            var available = new HashSet<int>(); var cleared = new HashSet<int>();
            foreach (var candidate in bossModelCandidates) if (candidate != null) available.Add((int)BossPassiveState.Identify(candidate.name));
            foreach (var candidate in sessionBossesPlayed) if (candidate != null) cleared.Add((int)BossPassiveState.Identify(candidate.name));
            int round = sessionActive ? sessionBossIndex + 1 : 1;
            int total = sessionActive ? sessionBossTargetCount : SelectedSessionBossCount;
            ThemeBossSelection();
            floorBossSelection.Display(round,total,selectedBossKind,available,cleared);
            if (wallBossSelection != null) wallBossSelection.Display(round,total,selectedBossKind,available,cleared);
            RaisePlayersOverCards();
        }

        // Display puts the boss select on top of the floor; the players' characters go back over it.
        void RaisePlayersOverCards()
        {
            if (characterPanel == null || floorBossSelection == null) return;
            if (characterPanel.parent == floorBossSelection.transform.parent) characterPanel.SetAsLastSibling();
        }

        // บาส 2026-11-18: walking onto a boss card picks it, like stepping on it with the LED floor. Only a step ONTO a
        // card counts (each player's last card is remembered), so two players standing on different cards do not keep
        // swapping the pick; whoever steps on a card last decides, and the countdown starts again (SelectBossKind).
        readonly int[] bossCardUnderPlayer = new int[6];

        // 2026-11-19: "อยากให้ทุกปุ่มที่ player ไปชนมีการเด้งตอบกลับ" - stepping onto any card or SELECT punches it (the same
        // punch a click / LED step gives), locked and cleared cards too, so every step gets an answer; only open ones act.
        void PickBossUnderPlayers()
        {
            if (floorBossSelection == null || floorBossSelection.slots == null) return;
            if (floorBossSelection.IsRevealing) return;   // nothing to pick until every card is in
            foreach (var player in players)
            {
                if (player.Index < 0 || player.Index >= bossCardUnderPlayer.Length) continue;
                int card = player.DownTimer <= 0f && player.Cursor.gameObject.activeInHierarchy ? BossCardAt(player.Cursor.position) : 0;
                if (card == bossCardUnderPlayer[player.Index]) continue;
                bossCardUnderPlayer[player.Index] = card;
                if (card == 0) continue;
                // 2026-11-25: SELECT already pressed (the 3-2-1 is running): stepping on it again does nothing - no sound, no bounce.
                if (card == SelectButtonCard && bossCountdownRunning) continue;
                var button = BossButton(card);
                Bounce(button);
                // 2026-11-25: a player stepping on a boss card or on SELECT gets its own sound (a locked card too).
                BossFightAudio.PlayUi(card == SelectButtonCard ? "select_press" : button.interactable ? "boss_card_touch" : "card_locked", .5f, .05f);
                if (!button.interactable) continue;
                if (card != SelectButtonCard) SelectBossKind(card);   // SELECT itself needs a hold (TickSelectHold)
            }
        }

        // 2026-11-19: the wall camera looks at the boss whose card a player stands on: the picked one if someone is on it,
        // else any boss card someone is on.
        // 2026-11-26: "เวลาซูมเลือกบอสตัวไหนแล้วไม่ต้องถอยออกมาแล้ว ถ้า select ก็นับเวลาเลยไม่ต้องถอยกล้อง" - once a boss is
        // picked the camera stays on it when players step off the cards, and the 3-2-1 after SELECT keeps it there too.
        // Only with no pick (and nobody on a boss card) does it show the whole hall.
        void FocusWallOnStoodBoss()
        {
            if (wallBossSelection == null || floorBossSelection == null) return;
            int focus = 0;
            if (!bossCountdownRunning && !floorBossSelection.IsRevealing)
                foreach (int card in bossCardUnderPlayer)
                {
                    if (card <= 0 || card == SelectButtonCard) continue;
                    if (card == selectedBossKind) { focus = card; break; }
                    if (focus == 0) focus = card;
                }
            if (focus == 0 && !floorBossSelection.IsRevealing) focus = selectedBossKind;
            wallBossSelection.FocusWallOn(focus);
        }

        static void Bounce(Button button)
        {
            if (!button.TryGetComponent(out ButtonPressFeedback feedback)) feedback = button.gameObject.AddComponent<ButtonPressFeedback>();
            feedback.Punch();
        }

        // Stand-in "card" number for the SELECT button under the cards.
        const int SelectButtonCard = 100;

        Button BossButton(int card) => card == SelectButtonCard ? floorBossSelection.confirm : floorBossSelection.slots[card - 1];

        // The boss kind (1..9) of the card whose frame holds this world point, SelectButtonCard on SELECT, or 0.
        // Locked cards count too (they bounce but do nothing); a cleared boss's card is 0, as if nobody stood there.
        int BossCardAt(Vector3 worldPoint)
        {
            if (Under(floorBossSelection.confirm, worldPoint)) return SelectButtonCard;
            var slots = floorBossSelection.slots;
            for (int i = 0; i < slots.Length; i++)
                if (Under(slots[i], worldPoint)) return floorBossSelection.IsCleared(i + 1) ? 0 : i + 1;   // cleared: no input
            return 0;
        }

        // Hit test on the button's resting size: its bounce scale must not move the edge under a player standing on it,
        // or they would step off and on again and bounce it forever.
        static bool Under(Button button, Vector3 worldPoint)
        {
            if (button == null || !button.isActiveAndEnabled) return false;
            var rect = (RectTransform)button.transform;
            var local = Vector2.Scale(rect.InverseTransformPoint(worldPoint), rect.localScale);
            return rect.rect.Contains(local);
        }
        public void SelectBossKind(int kind)
        {
            if (!selectingBoss || bossCountdownRunning) return;   // the 3-2-1 locks the pick
            foreach (var candidate in bossModelCandidates)
                if (candidate != null && (int)BossPassiveState.Identify(candidate.name) == kind && !sessionBossesPlayed.Contains(candidate))
                {
                    if (kind != selectedBossKind) BossFightAudio.PlayUi("statue_awaken", .55f, .3f, 3);   // 2026-11-25: the statue wakes
                    queuedNextBoss = candidate; selectedBossKind = kind; RefreshBossSelection(); return;
                }
        }
        public void ConfirmBossSelection()
        {
            if (!selectingBoss || queuedNextBoss == null || sessionBossesPlayed.Contains(queuedNextBoss)) return;
            selectingBoss = false; bossCountdownRunning = false;   // no more picks or countdown while the fade runs
            // 2026-11-24 "ตอนเปลี่ยนฉากจาก select boss ไปสู้บอส ให้มี fade": both displays fade to black, the boss select
            // closes and the fight is set up behind the black, then the fight fades in. Same fade as between bosses.
            StartCoroutine(FadeIntoBossFight());
        }

        // 2026-11-25 "เสียงคำรามตอนข้ามฉากเล่นต่อ ไม่ใช่เริ่มเล่นใหม่": every scene change uses this. On a UI voice (the
        // fight's StopCombat leaves it alone) and never restarted while it is still sounding - a second fade right after
        // the first (e.g. the rush starting behind the boss select's) lets the first one run on.
        void PlayTransitionSound()
        {
            if (sfx == null || sfx.IsPlaying("transition")) return;
            sfx.Play("transition", .55f, 0, .2f, 4, true);
        }

        IEnumerator FadeIntoBossFight()
        {
            // 2026-11-25 "ให้เสียงคำรามหลัง 321 ติดมาอีกฉากด้วย เล่นให้จบ": on a UI voice, which the fight's set-up
            // (StopCombat when the boss starts) leaves alone, so it plays to its end over the new scene.
            PlayTransitionSound();
            yield return TweenSessionTransitionAlpha(1f, sessionTransitionFadeSeconds);
            floorBossSelection.gameObject.SetActive(false);
            if (wallBossSelection != null) wallBossSelection.gameObject.SetActive(false);
            // บาส 2026-11-18: no READY screen any more ("รอผู้เล่น" / P1 รอ). The 5-4-3-2-1 on the wall is the only wait:
            // at zero the picked boss starts straight away.
            awaitingPlayers = false;
            StartSelectedBossAfterReady();
            // The first boss of a session goes through BeginBossSession, which calls StopAllCoroutines (this one stops at
            // its next yield) and clears the fade to 0. So: back to black at once, and fade in from a fresh coroutine.
            KillSessionTransitionTween();
            SetSessionTransitionAlpha(1f);
            StartCoroutine(TweenSessionTransitionAlpha(0f, sessionTransitionFadeSeconds));
        }
        void StartSelectedBossAfterReady()
        {
            if (queuedNextBoss == null) { ShowBossSelection(); return; }
            if (!sessionActive) { BeginBossSession(SelectedSessionBossCount); return; }
            awaitingPlayers = false;
            if (readyScreens != null) foreach (var screen in readyScreens) if (screen != null) screen.gameObject.SetActive(false);
            StartNextSessionBoss(false);
        }
    }
}
