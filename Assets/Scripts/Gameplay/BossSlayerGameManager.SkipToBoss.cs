using UnityEngine;

namespace BossSlayer
{
    // 2026-11-26 "ทำปุ่มกด M แล้วข้ามไป cutscene ของบอสเพื่อตีบอสเลย" / "กดแล้วไม่เห็นไป Final boss เลย": M jumps straight
    // to the FINAL boss - the Abyssal King rising out of the floor (FinalBossEntrance) and then his fight. From the player
    // pads it ends them first (whoever hasn't picked gets the free pads); from the boss select, a perk break or a normal
    // boss fight it goes there at once; from the result screen it starts over from the menu first. Nothing happens once
    // the king's entrance or fight is already running.
    // The king only exists in the boss rush: with rushMode off M starts the first boss not yet beaten instead.
    public sealed partial class BossSlayerGameManager
    {
        // How long a pressed M keeps pushing through screens that come up a frame or two later.
        const float SkipToBossSeconds = 5f;
        float skipToBossUntil = -1f;
        // 2026-11-27 "ทำให้กดปุ่ม N จะตัดไปหน้าเลือก perk เลย": N rides the same push as M but stops at the next perk break.
        bool skipToPerk;

        // Every frame from Update, before the per-screen early outs.
        void TickSkipToBoss()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame) BeginSkipToBoss();
            if (keyboard != null && keyboard.nKey.wasPressedThisFrame) BeginSkipToPerk();
            if (skipToBossUntil < 0f) return;
            if (Time.unscaledTime > skipToBossUntil) { skipToBossUntil = -1f; return; }
            AdvanceSkipToBoss();
        }

        void BeginSkipToBoss()
        {
            if (IsRushActive && rushStage == RushStage.Final && !resultShown)
            {
                // 2026-11-26 "หน้า final boss กด m อีกทีบอสตายเลย": during the king's fight a second M kills him at once
                // (straight to his death and RAID COMPLETE); during his entrance cutscene it still does nothing.
                if (gameStarted && IsAbyssalKingActive && bossHp > 0) KillBossForTest();
                return;
            }
            Debug.Log("[BossSlayer] M: skipping to the final boss", this);
            skipToPerk = false;
            skipToBossUntil = Time.unscaledTime + SkipToBossSeconds;
            if (resultShown && !sessionActive) RestartToMenu();   // final result: start over from the menu
        }

        // One step per frame until the final boss's entrance has started.
        void AdvanceSkipToBoss()
        {
            if (pickingPlayers) { FinishPlayerSelect(false); return; }
            bool fighting = gameStarted && !resultShown;
            if (!selectingBoss && !rushBreakOpen && !fighting) return;   // waiting for the next screen to come up
            skipToBossUntil = -1f;
            if (skipToPerk) JumpToPerkBreak();
            else if (rushMode) JumpToFinalBoss();
            else JumpToFirstBoss();
        }

        // N: the next team perk vote - the first one from the pads, boss select or the first half's fights, the second
        // one from the first perk break or the second half. Nothing during the king's entrance or fight, and nothing
        // with rushMode off (there are no perk breaks then). From the result screen it starts over from the menu first.
        void BeginSkipToPerk()
        {
            if (!rushMode) return;
            if (IsRushActive && rushStage == RushStage.Final && !resultShown) return;
            Debug.Log("[BossSlayer] N: skipping to the perk select", this);
            skipToPerk = true;
            skipToBossUntil = Time.unscaledTime + SkipToBossSeconds;
            if (resultShown && !sessionActive) RestartToMenu();
        }

        void JumpToPerkBreak()
        {
            skipToPerk = false;
            bool secondBreak = IsRushActive && (rushStage == RushStage.Perk1 || rushStage == RushStage.Rush2 || rushStage == RushStage.Perk2);
            if (secondBreak && rushStage == RushStage.Perk2 && rushBreakOpen) return;   // already on the last perk vote
            StopSessionForSkip();
            ClosePerkBreak();   // a first break still open resolves its vote before the second one
            // as if this half's bosses were played, so later bosses get their usual attack speed
            sessionBossIndex = Mathf.Max(sessionBossIndex, rushBossesPerHalf * (secondBreak ? 2 : 1));
            EnterRushStage(secondBreak ? RushStage.Perk2 : RushStage.Perk1);
        }

        void JumpToFinalBoss()
        {
            StopSessionForSkip();
            // as if both halves were played, so the king gets his usual attack speed
            sessionBossIndex = Mathf.Max(sessionBossIndex, rushBossesPerHalf * 2);
            EnterRushStage(RushStage.Final);   // closes any perk break, then the king's entrance cutscene
        }

        // Shared by M and N: drop the current screen and make sure a rush session is running.
        void StopSessionForSkip()
        {
            // drop whatever this screen was doing (boss select fade, perk break, a running fight and its death routine)
            StopAllCoroutines();
            HideNextBossCards();
            CancelBossEntrance();
            ClearAttackChargeVfx();
            ClearLightweightVoidAttackVfx();
            KillSessionTransitionTween();
            SetSessionTransitionAlpha(0f);
            // a boss fought now stops (no attacks while the screen fades to the king)
            if (gameStarted) { sfx?.StopCombat(); ClearAttacks(); ClearSpots(); SetBossVisible(false); }
            gameStarted = false; resultShown = false;
            selectingBoss = false; bossCountdownRunning = false; awaitingPlayers = false;
            if (floorBossSelection != null) floorBossSelection.gameObject.SetActive(false);
            if (wallBossSelection != null) wallBossSelection.gameObject.SetActive(false);
            if (!IsRushActive) StartRushClock();
            if (!sessionActive)
            {
                // the session set-up BeginBossSession would do, without starting a normal boss
                sessionBossTargetCount = RushBossCap;
                sessionBossesPlayed.Clear();
                sessionActive = true;
                replaySessionPending = true;
            }
        }

        void KillBossForTest()
        {
            Debug.Log("[BossSlayer] M: killing the final boss", this);
            bossHp = 0;
            CheckBossDefeated();
        }

        // rushMode off: the picked boss (or the first one not yet beaten) and its entrance.
        void JumpToFirstBoss()
        {
            if (!selectingBoss) return;
            if (queuedNextBoss == null) SelectBossKind(FirstUnplayedBossKind());
            if (queuedNextBoss != null) ConfirmBossSelection();
        }

        int FirstUnplayedBossKind()
        {
            foreach (var candidate in bossModelCandidates)
                if (candidate != null && !sessionBossesPlayed.Contains(candidate))
                    return (int)BossPassiveState.Identify(candidate.name);
            return 0;
        }
    }
}
