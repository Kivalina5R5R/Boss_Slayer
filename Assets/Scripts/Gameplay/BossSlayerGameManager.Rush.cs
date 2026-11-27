using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FortalReplay;

namespace BossSlayer
{
    // บาส 2026-10-20: the BOSS RUSH session (web prototype "boss-rush-15" approved first), about 15 minutes long.
    // No master clock (บาส: "เอานาฬิกาจับเวลา 15 นาทีออก ขอแค่เล่นแล้วใช้เวลาประมาณ 15 นาที"): the session is counted in bosses -
    //   3 bosses -> team perk vote -> 3 bosses -> team perk vote -> ABYSSAL KING -> results
    // At 4 players a boss is ~80-110 s with its death / victory card / select / entrance, so the whole run is ~12.5-16 min.
    // Bosses come one after another (select with a 12 s random fallback, no READY gate after the first boss). Perks are
    // team picks by majority vote (1 of 3 cards, a reroll needs the whole team on the pad, the 2nd pick always offers the
    // evolved form of the 1st).
    public sealed partial class BossSlayerGameManager
    {
        [Header("Boss Rush (about 15 minutes)")]
        [Tooltip("On: 3 bosses -> perk -> 3 bosses -> perk -> Abyssal King. Off: the old 3/5/7-boss session.")]
        [SerializeField] bool rushMode = true;
        [Tooltip("Bosses before each team perk vote (two halves, then the king). 3 = about 15 minutes at 4 players.")]
        [SerializeField, Min(1)] int rushBossesPerHalf = 3;
        // 2026-11-27 "รอเวลาครบ 25วิ แล้ว พอเล่น animation เลือกการ์ดเสร็จก็ตัดเปลี่ยนฉากเลย": the break is the vote
        // (reveal included) plus the pick's animation; the old fixed 40 s break left 12 s of nothing after the pick.
        [SerializeField, Min(5f)] float rushPerkVoteSeconds = 25f;
        float rushPickedAt;
        [Tooltip("Attack speed added per boss in the rush (the 3/5/7 session uses 12%).")]
        [SerializeField, Range(0f, 1f)] float rushSpeedStepPerBoss = .06f;
        const int RushBossCap = 30;
        const float RerollHoldSeconds = 1.5f;

        enum RushStage { Off, Rush1, Perk1, Rush2, Perk2, Final, Results }
        RushStage rushStage;
        // rushClock only times the perk breaks and the Chaos curse; nothing in the session waits for a total time.
        float rushClock, rushBreakStart;
        int rushHalfKills;
        bool rushFighting, rushKingDefeated, rushBreakOpen;
        RushStage rushBreakStage = RushStage.Perk1;
        public bool IsRushActive => rushMode && rushStage != RushStage.Off;

        // ---------------------------------------------------------------- lifecycle
        void ResetRush()
        {
            rushStage = RushStage.Off; rushClock = 0; rushHalfKills = 0; rushKills = 0;
            rushFighting = rushKingDefeated = rushBreakOpen = false;
            HideRushResultLayout();
            HideFinalIntroFloor();   // the king's intro cut short (back to the menu)
            ResetRushPerks();
            HidePerkScreens();
            BossSelectionPanel.OpenEndedRounds = false;
            BossSelectionPanel.HpPercentOfRound = null; BossSelectionPanel.SpeedStepPercent = 12f;
            ResetBossClock();
        }

        // Called when the lobby locks the player count (BossSlayerGameManager.ShowMenu).
        void StartRushClock()
        {
            ResetRush();
            if (!rushMode) return;
            rushStage = RushStage.Rush1;
            ReplayLog.Event("rush_start", 0, 0, null, ReplayLog.Data().Add("bossesPerHalf", rushBossesPerHalf));
            BossSelectionPanel.OpenEndedRounds = true;
            BossSelectionPanel.HpPercentOfRound = RushHpPercentOfRound; BossSelectionPanel.SpeedStepPercent = rushSpeedStepPerBoss * 100f;
        }

        void TickRush(float dt)
        {
            rushClock += dt;
            if ((rushStage == RushStage.Perk1 || rushStage == RushStage.Perk2) && rushBreakOpen)
            {
                UpdatePerkBreak(dt);
                if (rushPicked && rushClock - rushPickedAt >= PerkPickShowSeconds) EnterRushStage(rushStage == RushStage.Perk1 ? RushStage.Rush2 : RushStage.Final);
            }
            if (gameStarted && !resultShown) { TickRushPerks(dt); TickBossClock(dt); }
        }

        void EnterRushStage(RushStage next)
        {
            rushStage = next;
            ReplayLog.Event("rush_stage", 0, 0, null, ReplayLog.Data().Add("stage", next.ToString()).Add("clock", rushClock));
            switch (next)
            {
                case RushStage.Perk1:
                case RushStage.Perk2:
                    StartCoroutine(RushFade(OpenPerkBreak));
                    break;
                // The perk screens go once the fade is black, so the pick's last frame never cuts to the empty arena.
                case RushStage.Rush2:
                    rushHalfKills = 0;
                    StartCoroutine(RushFade(() => { ClosePerkBreak(); ShowBossSelection(); }));
                    break;
                case RushStage.Final:
                    StartCoroutine(FinalBossEntrance());
                    break;
            }
        }

        BossFinalBossIntro finalBossIntro;

        // 2026-11-26: after the last perk break, fade to black, play the king rising out of the floor
        // (BossFinalBossIntro), start the fight under its closing flash. With no sprite it is the old straight fade.
        IEnumerator FinalBossEntrance()
        {
            if (!BossFinalBossIntro.HasArt) { yield return RushFade(() => { ClosePerkBreak(); StartAbyssalKing(); }, "THE ABYSSAL KING AWAKENS"); yield break; }
            PlayTransitionSound();
            yield return TweenSessionTransitionAlpha(1f, sessionTransitionFadeSeconds);
            ClosePerkBreak();
            if (finalBossIntro == null) finalBossIntro = BossFinalBossIntro.Create(cutsceneArea, transform);
            ShowFinalIntroFloor();
            bool done = false;
            void Risen()
            {
                HideFinalIntroFloor();
                StartAbyssalKing();
                SetSessionTransitionAlpha(0f);   // the intro's white flash covers the cut
                JuiceMilestone("THE ABYSSAL KING AWAKENS", new Color(.75f, .4f, 1f));
            }
            if (finalBossIntro == null || !finalBossIntro.Play(Risen, () => done = true, ShakeFinalIntroFloor)) { Risen(); yield break; }
            while (!done) yield return null;
        }

        // 2026-11-26 "ตอน cutscene finalboss ใช้พื้น select boss เลย แค่ไม่ต้องมี UI มี แค่ animation ของพื้นเล่นอยู่":
        // while the king rises on the wall, the floor shows the boss select's rune floor video on its own (no cards, no
        // players), above the black session fade. It fades in like the wall's intro and goes when the fight starts.
        // 2026-11-26 "ตอน cutscene ตัว player หายไปไหน ให้จอพื้นสั่นด้วย": the players' layer is lifted above that floor
        // for the cutscene (then put back), and the floor shakes with the wall while the king rises and slams down.
        RawImage finalIntroFloor;
        Tween finalIntroFloorFade, finalIntroFloorShake;
        Transform finalIntroPlayers;
        int finalIntroPlayersIndex;
        bool finalIntroPlayersWasActive;
        const float FinalIntroFloorFadeSeconds = .7f;
        // How far the floor shakes at strength 1, as a fraction of the floor's height; the picture reaches this far
        // past the floor's edges so the shake never shows its border.
        const float FinalIntroFloorShake = .012f;

        void ShowFinalIntroFloor()
        {
            if (playArea == null) return;
            if (finalIntroFloor == null)
            {
                var rect = NewLayer(playArea, "Final Boss Intro Floor");
                finalIntroFloor = rect.gameObject.AddComponent<RawImage>();
                finalIntroFloor.raycastTarget = false;
                float pad = playArea.rect.height * FinalIntroFloorShake * 2f;
                rect.offsetMin = new Vector2(-pad, -pad); rect.offsetMax = new Vector2(pad, pad);
            }
            finalIntroFloor.gameObject.SetActive(true);
            finalIntroFloor.rectTransform.SetAsLastSibling();   // over the session fade and everything else on the floor
            LiftPlayersOverIntroFloor();
            BossFloorMagicVideo.Attach(finalIntroFloor);
            finalIntroFloorFade?.Kill();
            finalIntroFloor.canvasRenderer.SetAlpha(0f);
            finalIntroFloorFade = DOVirtual.Float(0f, 1f, FinalIntroFloorFadeSeconds, a => finalIntroFloor.canvasRenderer.SetAlpha(a))
                .SetEase(Ease.InOutSine).SetUpdate(true).SetLink(finalIntroFloor.gameObject);
        }

        void HideFinalIntroFloor()
        {
            finalIntroFloorFade?.Kill(); finalIntroFloorFade = null;
            finalIntroFloorShake?.Kill(true); finalIntroFloorShake = null;
            if (finalIntroFloor != null) finalIntroFloor.gameObject.SetActive(false);
            PutBackIntroPlayers();
        }

        // The players' layer (the floor child holding the cursors) goes on top for the cutscene, shown even if hidden.
        void LiftPlayersOverIntroFloor()
        {
            PutBackIntroPlayers();
            Transform layer = players.Count > 0 && players[0].Cursor != null ? players[0].Cursor.parent : null;
            while (layer != null && layer.parent != playArea) layer = layer.parent;
            if (layer == null) return;
            finalIntroPlayers = layer;
            finalIntroPlayersIndex = layer.GetSiblingIndex();
            finalIntroPlayersWasActive = layer.gameObject.activeSelf;
            layer.gameObject.SetActive(true);
            layer.SetAsLastSibling();
        }

        void PutBackIntroPlayers()
        {
            if (finalIntroPlayers == null) return;
            finalIntroPlayers.SetSiblingIndex(finalIntroPlayersIndex);
            finalIntroPlayers.gameObject.SetActive(finalIntroPlayersWasActive);
            finalIntroPlayers = null;
        }

        // Shakes the cutscene's floor picture (only the picture: the players stay where the people stand).
        void ShakeFinalIntroFloor(float seconds, float strength)
        {
            if (finalIntroFloor == null || !finalIntroFloor.gameObject.activeInHierarchy) return;
            finalIntroFloorShake?.Kill(true);
            float pixels = playArea.rect.height * FinalIntroFloorShake * strength;
            finalIntroFloorShake = finalIntroFloor.rectTransform.DOShakeAnchorPos(seconds, pixels, 28, 90f, false, true)
                .SetUpdate(true).SetLink(finalIntroFloor.gameObject);
        }

        IEnumerator RushFade(System.Action middle, string milestone = null)
        {
            PlayTransitionSound();
            yield return TweenSessionTransitionAlpha(1f, sessionTransitionFadeSeconds);
            middle();
            if (!string.IsNullOrEmpty(milestone)) JuiceMilestone(milestone, new Color(.75f, .4f, 1f));
            yield return TweenSessionTransitionAlpha(0f, sessionTransitionFadeSeconds);
        }

        // Fight hooks (BossSlayerGameManager.cs): ResetFight -> started, ShowResult -> killed.
        void RushOnFightStarted() { rushFighting = true; rushKingDefeated = false; CloseEscapePortal(); ResetBossClock(); }
        void RushOnBossKilled()
        {
            rushFighting = false; rushKingDefeated = IsAbyssalKingActive; rushKingCharging = false; CloseEscapePortal();
            if (IsRushActive && !rushKingDefeated) { rushKills++; rushHalfKills++; }
        }

        // BossDeathSequenceRoutine ends here in rush mode instead of HasNextSessionBoss.
        IEnumerator RushAfterBossDefeated()
        {
            if (rushKingDefeated)
            {
                rushStage = RushStage.Results;
                ReplayLog.Event("rush_stage", 0, 0, null, ReplayLog.Data().Add("stage", "Results").Add("clock", rushClock));
                ShowSessionResultsScreen();
                yield break;
            }
            sfx?.Play("round_clear", 0.55f, 0, 0.2f, 4);
            if (rushHalfKills >= rushBossesPerHalf) { EnterRushStage(rushStage == RushStage.Rush1 ? RushStage.Perk1 : RushStage.Perk2); yield break; }
            yield return PlayNextBossTransition();
        }

        void StartAbyssalKing()
        {
            if (abyssalKingModel == null) EnsureAbyssalKing(bossModelCandidates.Count > 0 ? bossModelCandidates[0] : null);
            HideBossVictory(); selectingBoss = false; awaitingPlayers = false;
            if (floorBossSelection != null) floorBossSelection.gameObject.SetActive(false);
            if (wallBossSelection != null) wallBossSelection.gameObject.SetActive(false);
            sessionBossIndex = Mathf.Clamp(sessionBossIndex + 1, 1, Mathf.Max(1, sessionBossTargetCount));
            ApplySessionDifficulty(sessionBossIndex);
            bossMaxHp = RushBossHp(sessionBossIndex, true);
            queuedNextBoss = abyssalKingModel;
            ResetFight(false);
        }

        // Boss select hooks (Selection.cs).
        void RushOnShowSelection()
        {
            if (!IsRushActive) return;
            // A fast team can clear all nine: the roster opens again (the king is never on it).
            bool any = false;
            foreach (var candidate in bossModelCandidates) if (candidate != null && !sessionBossesPlayed.Contains(candidate)) { any = true; break; }
            if (!any) sessionBossesPlayed.RemoveAll(model => model != abyssalKingModel);
        }

        // 2026-11-19: "ทำไมหน้าบอสเริ่มเอง ยังไม่มีใครไปเหยียบปุ่ม Select" - the rush's 12 s select timeout (random boss /
        // auto start) is gone: the boss select only moves on when a player steps on SELECT (or the launcher's CONFIRM).

        // ---------------------------------------------------------------- perks
        // 2026-11-27 "เอา UI Skill ต่างๆไปใส่ base ภาษาไว้เป็นอังกฤษ": every perk is written in English with its Thai twin,
        // and shows Thai only after the launcher's language button (Lang.T). The English names are the skill art's own
        // names (Art/UI/Perk/Icons/<Art>.png).
        sealed class RushPerk
        {
            readonly string nameEn, nameTh, detailEn, detailTh, source;

            // tier: 0 common, 1 rare, 2 legendary, 3 evolved (from the perk with id evolvedFrom)
            public RushPerk(string id, int tier, string art, string nameEn, string nameTh, string detailEn, string detailTh, string source, string evolvedFrom = null)
            {
                Id = id; Tier = tier; Art = art; Base = evolvedFrom;
                this.nameEn = nameEn; this.nameTh = nameTh; this.detailEn = detailEn; this.detailTh = detailTh; this.source = source;
            }

            public string Id { get; }
            public string Base { get; }
            public string Art { get; }
            public int Tier { get; }
            public string Name => Lang.T(nameEn, nameTh);
            public string Detail => Lang.T(detailEn, detailTh);

            // the game the idea came from, or for an evolved perk the perk it grew from
            public string Source
            {
                get
                {
                    var from = Base != null ? PerkById(Base) : null;
                    return from == null ? source : Lang.T("Evolved from " + from.nameEn, "วิวัฒน์จาก " + from.nameTh);
                }
            }
        }
        static readonly RushPerk[] RushPerks =
        {
            new("dmg", 0, "Power Up", "POWER UP", "พลังโจมตี", "All gem damage +25%", "ดาเมจเจมทุกเม็ด +25%", "Hades · Pom of Power"),
            new("hp", 0, "Iron Armor", "IRON ARMOR", "เกราะเหล็ก", "Max HP +50%, take 25% less damage", "เลือดสูงสุด +50% โดนตีเบาลง 25%", "Isaac · HP up"),
            new("stun", 0, "Heavy Smash", "HEAVY SMASH", "ทุบหนัก", "Yellow gems fill the stun gauge +27 (3 gems stun)", "เจมเหลืองเติมเกจสตัน +27 (ครบ 3 ลูกสตัน)", "Hades · Stagger"),
            new("leech", 0, "Life Drain", "LIFE DRAIN", "ดูดพลัง", "Gem collectors +8 HP", "คนที่เก็บเจมได้ +8 HP", "Dead Cells · lifesteal"),
            new("chain", 1, "Lightning", "LIGHTNING", "สายฟ้า", "3 red gems: lightning hits the boss for 400", "เก็บแดงครบ 3 เม็ด ฟ้าผ่าบอส 400", "Risk of Rain 2 · Ukulele"),
            new("ult", 1, "Quick Ultimate", "QUICK ULTIMATE", "ท่าไม้ตายไว", "6 blue gems open the ultimate", "เก็บฟ้า 6 เม็ด เปิดท่าไม้ตาย", "Hades · Call gauge"),
            new("doom", 1, "Death Curse", "DEATH CURSE", "คำสาปมรณะ", "Yellow gems curse the boss: 150 blast after 3s", "เจมเหลืองฝังคำสาป 3 วิแล้วระเบิด 150", "Hades · Doom"),
            new("chill", 1, "Frost Wind", "FROST WIND", "ลมหนาว", "Yellow halves boss attack speed for 8s and slows big-move charge; gem damage +20% meanwhile", "เหลืองทำให้บอสตีช้าลงครึ่งหนึ่ง 8 วิ ท่าใหญ่ชาร์จช้าลงด้วย ระหว่างนั้นดาเมจไอคอน +20%", "Hades · Chill"),
            new("duo", 1, "Team Up", "TEAM UP", "ผนึกกำลัง", "2 players on one gem: 2.5x effect", "ยืนไอคอนเดียวกัน 2 คน ได้ผล 2.5 เท่า", "co-op · Ravenswatch"),
            new("dodge", 1, "Close Call", "CLOSE CALL", "หลบหวุดหวิด", "Dodge a big move at the last second: Ult +1", "หลบท่าใหญ่ได้ในวินาทีสุดท้าย ท่าไม้ตาย +1", "Hades · dash / Gungeon"),
            new("revive", 1, "Never Give Up", "NEVER GIVE UP", "ใจไม่ยอมแพ้", "Down only 2.5s, get up at full HP", "ล้มแค่ 2.5 วิ ลุกขึ้นมาเลือดเต็ม", "Hades · Death Defiance"),
            new("chaos", 2, "Abyss Pact", "ABYSS PACT", "สัญญาอเวจี", "Boss hits x1.5 for 60s, then +40% damage all game", "60 วิแรกบอสตีแรง x1.5 จากนั้นดาเมจ +40% ทั้งเกม", "Hades · Chaos boon"),
            new("dmg2", 3, "Power Up2", "POWER UP II", "พลังโจมตี II", "Total gem damage +50%", "ดาเมจเจมรวม +50%", null, "dmg"),
            new("hp2", 3, "Fortress", "FORTRESS", "ป้อมปราการ", "Max HP +100%, take 40% less damage", "เลือดสูงสุด +100% โดนตีเบาลง 40%", null, "hp"),
            new("stun2", 3, "Shatter", "SHATTER", "ทุบแหลก", "3 gems stun 2s longer; a stunned boss takes +75% damage", "ครบ 3 ลูกสตัน สตันนานขึ้น 2 วิ ช่วงสตันบอสรับดาเมจ +75%", null, "stun"),
            new("leech2", 3, "Soul Drain", "SOUL DRAIN", "ดูดวิญญาณ", "Each gem heals the whole team +10 HP", "เก็บเจมแล้วทั้งทีม +10 HP", null, "leech"),
            new("chain2", 3, "Thunderstorm", "THUNDERSTORM", "พายุสายฟ้า", "2 red gems: lightning hits the boss for 400", "เก็บแดงครบ 2 เม็ด ฟ้าผ่าบอส 400", null, "chain"),
            new("ult2", 3, "Quick Ultimate II", "QUICK ULTIMATE II", "ท่าไม้ตายไว II", "5 blue gems open the ultimate", "เก็บฟ้า 5 เม็ด เปิดท่าไม้ตาย", null, "ult"),
            new("doom2", 3, "Chain Curse", "CHAIN CURSE", "คำสาปลูกโซ่", "Curse blast 300", "คำสาประเบิด 300", null, "doom"),
            new("chill2", 3, "Ice Age", "ICE AGE", "ยุคน้ำแข็ง", "Slow lasts 12s, gem damage +20%, stun +2s", "ช้าลง 12 วิ ดาเมจเจม +20% และสตันนานขึ้น 2 วิ", null, "chill"),
            new("duo2", 3, "One Heart", "ONE HEART", "หนึ่งใจเดียวกัน", "2 players on one gem: 3x effect", "ยืนไอคอนเดียวกัน 2 คน ได้ผล 3 เท่า", null, "duo"),
            new("dodge2", 3, "Shadow Step", "SHADOW STEP", "เงาวูบ", "Dodge a big move: Ult +1 and hits boss for 300", "หลบท่าใหญ่ได้ ท่าไม้ตาย +1 และยิงบอส 300", null, "dodge"),
            new("revive2", 3, "Immortal", "IMMORTAL", "อมตะ", "Down only 1.5s, get up at full HP", "ล้มแค่ 1.5 วิ ลุกขึ้นมาเลือดเต็ม", null, "revive"),
            new("chaos2", 3, "Abyss Pact II", "ABYSS PACT II", "สัญญาอเวจี II", "Total damage +70%, no extra curse", "ดาเมจรวม +70% ไม่มีคำสาปเพิ่ม", null, "chaos"),
        };
        static readonly Color[] TierColors = { new Color(.79f, .76f, .84f), new Color(.73f, .55f, 1f), new Color(1f, .76f, .24f), new Color(.37f, .88f, 1f) };
        static string TierName(int tier) => tier switch
        {
            0 => Lang.T("COMMON", "ธรรมดา"),
            1 => Lang.T("RARE", "หายาก"),
            2 => Lang.T("LEGENDARY", "ตำนาน"),
            _ => Lang.T("EVOLVED", "วิวัฒน์"),
        };
        static RushPerk PerkById(string id) { foreach (var p in RushPerks) if (p.Id == id) return p; return null; }

        readonly List<string> rushPerks = new();
        bool rushBaseCaptured; int rushBasePlayerHp, rushBaseStunNeed; float rushBaseStunSeconds, rushBaseDownSeconds;
        float rushChaosEnd, rushChill;
        // Lightning (Balance Note v3 §5.2): red gems the team has gathered toward the next strike.
        int rushChainCount;
        readonly List<(float left, List<PlayerState> credit, Vector2 at)> rushDoom = new();
        readonly Dictionary<AttackState, HashSet<int>> rushNearMiss = new();

        bool HasPerk(string id) => rushPerks.Contains(id) || rushPerks.Contains(id + "2");
        int PerkLevel(string id) => rushPerks.Contains(id + "2") ? 2 : rushPerks.Contains(id) ? 1 : 0;
        bool RushChaosCurse => HasPerk("chaos") && rushClock < rushChaosEnd;

        void ResetRushPerks()
        {
            rushPerks.Clear(); rushChaosEnd = 0; rushChill = 0; rushChainCount = 0;
            rushDoom.Clear(); rushNearMiss.Clear();
            if (rushBaseCaptured) { playerMaxHp = rushBasePlayerHp; stunNeed = rushBaseStunNeed; stunSeconds = rushBaseStunSeconds; downTimeSeconds = rushBaseDownSeconds; }
        }

        void ApplyRushPerkStats()
        {
            if (!rushBaseCaptured)
            {
                rushBaseCaptured = true;
                rushBasePlayerHp = playerMaxHp; rushBaseStunNeed = stunNeed; rushBaseStunSeconds = stunSeconds; rushBaseDownSeconds = downTimeSeconds;
            }
            playerMaxHp = Mathf.RoundToInt(rushBasePlayerHp * new[] { 1f, 1.5f, 2f }[PerkLevel("hp")]);
            stunNeed = rushBaseStunNeed; // Balance Note B5: the perk raises the yellow gem to 27 instead (RushStunGain)
            stunSeconds = rushBaseStunSeconds + (PerkLevel("stun") == 2 ? 2f : 0f) + (PerkLevel("chill") == 2 ? 2f : 0f);
            downTimeSeconds = new[] { rushBaseDownSeconds, 2.5f, 1.5f }[PerkLevel("revive")];
        }

        // Multipliers used by the fight code.
        float RushDamageMultiplier(IReadOnlyList<PlayerState> who = null, bool gem = false)
        {
            if (!IsRushActive) return 1f;
            float m = 1f;
            if (gem && rushChill > 0f && HasPerk("chill")) m *= 1.2f;
            m *= new[] { 1f, 1.25f, 1.5f }[PerkLevel("dmg")];
            if (HasPerk("chaos") && !RushChaosCurse) m *= PerkLevel("chaos") == 2 ? 1.7f : 1.4f;
            return m;
        }
        float RushIncomingMultiplier() => !IsRushActive ? 1f : (RushChaosCurse ? 1.5f : 1f) * new[] { 1f, .75f, .6f }[PerkLevel("hp")];
        // Join Forces: how much a gem shared by 2+ players is worth (2.5x, evolved 3x); 1 otherwise.
        float RushDuoFactor(IReadOnlyList<PlayerState> who) => IsRushActive && who != null && who.Count >= 2 ? new[] { 1f, 2.5f, 3f }[PerkLevel("duo")] : 1f;
        // Quick Ultimate: blue gems needed to open the ultimate (8, the perk 6, evolved 5).
        int UltimateNeed => IsRushActive && HasPerk("ult") ? (PerkLevel("ult") == 2 ? 5 : 6) : ultimateNeed;
        int RushChainNeed => PerkLevel("chain") == 2 ? 2 : 3;
        // Attack Up (Balance Note v3 §5.3): the red gem grows so the team can see the card at work.
        float RushGemScale(SpotDef def) => IsRushActive && def.GemModelName == "Attack" ? new[] { 1f, 1.2f, 1.35f }[PerkLevel("dmg")] : 1f;
        // Attack Up: the red gem's damage number pops bigger on the wall.
        float RushDamageNumberScale(string source) => IsRushActive && source == "Attack" ? new[] { 1f, 1.3f, 1.5f }[PerkLevel("dmg")] : 1f;
        int RushStunGain(int gain) => IsRushActive && gain > 0 && HasPerk("stun") ? 27 : gain; // 3 gems reach the gauge of 80
        float RushAttackTimeScale() => IsRushActive && rushChill > 0f ? .5f : 1f;
        int RushReviveHp() => IsRushActive && HasPerk("revive") ? playerMaxHp : Mathf.RoundToInt(playerMaxHp * .5f);

        // HitBoss hook: gem side effects after its damage landed.
        void RushOnGemHit(SpotDef def, List<PlayerState> contributors, Vector2 at)
        {
            if (!IsRushActive || rushPerks.Count == 0 || bossHp <= 0) return;
            if (PerkLevel("leech") == 1) foreach (var p in contributors) { p.Hp = Mathf.Min(playerMaxHp, p.Hp + 8); UpdatePlayerHp(p); }
            if (PerkLevel("leech") == 2) foreach (var p in players) if (p.DownTimer <= 0f) { p.Hp = Mathf.Min(playerMaxHp, p.Hp + 10); UpdatePlayerHp(p); }
            if (def.GemModelName == "Attack" && HasPerk("chain") && ++rushChainCount >= RushChainNeed)
            {
                rushChainCount = 0;
                int zap = ApplyBossDamage(Mathf.RoundToInt(400 * StunMultiplier() * RushDamageMultiplier(contributors)), "Chain");
                CreditBossDamage(zap, contributors);
                JuiceFloor(SpaceJuice.Pickup, WorldToLocal(at), new Color(.75f, .9f, 1f), 190);
                JuiceBossLightning();
                sfx?.Play("stun", .45f, 0, .2f, 3);
            }
            if (def.Stun > 0 && HasPerk("doom")) rushDoom.Add((3f, new List<PlayerState>(contributors), at));
            if (def.Stun > 0 && HasPerk("chill")) rushChill = PerkLevel("chill") == 2 ? 12f : 8f;
        }

        void TickRushPerks(float dt)
        {
            if (rushPerks.Count == 0 || bossHp <= 0) return;
            rushChill = Mathf.Max(0f, rushChill - dt);
            for (int i = rushDoom.Count - 1; i >= 0; i--)
            {
                var doom = rushDoom[i]; doom.left -= dt; rushDoom[i] = doom;
                if (doom.left > 0f) continue;
                rushDoom.RemoveAt(i);
                int blast = ApplyBossDamage(Mathf.RoundToInt((PerkLevel("doom") == 2 ? 300 : 150) * StunMultiplier() * RushDamageMultiplier(doom.credit)), "Doom");
                CreditBossDamage(blast, doom.credit);
                JuiceFloor(SpaceJuice.Hurt, WorldToLocal(doom.at), new Color(1f, .35f, .6f), 230);
            }
            CheckBossDefeated();
        }

        // UpdateAttacks hooks: remember who was still inside 0.6 s before impact; anyone not hit when it lands dodged.
        // Balance Note v3 §5.2: big moves only (IsBossUltimate), or 6 players would chain ultimates off small attacks.
        void RushTrackNearMiss(AttackState attack)
        {
            if (!IsRushActive || !HasPerk("dodge") || !attack.IsBossUltimate || attack.Shape == Shape.Wave || attack.WarningDelay > 0f) return;
            if (attack.TelegraphTimer > .6f || attack.TelegraphTimer <= 0f || rushNearMiss.ContainsKey(attack)) return;
            var inside = new HashSet<int>();
            foreach (var p in players)
                if (p.DownTimer <= 0f && TryGetInputLocalPoint(p.Index, out _) && IsInAttack(attack, LocalToWorld(p.Cursor.anchoredPosition))) inside.Add(p.Index);
            rushNearMiss[attack] = inside;
        }

        void RushResolveNearMiss(AttackState attack)
        {
            if (!rushNearMiss.TryGetValue(attack, out var inside)) return;
            rushNearMiss.Remove(attack);
            if (ultimateActive || bossHp <= 0) return;
            foreach (var p in players)
            {
                if (!inside.Contains(p.Index) || attack.HitPlayers.Contains(p.Index) || p.DownTimer > 0f) continue;
                JuiceFloor(SpaceJuice.Pickup, p.Cursor.anchoredPosition, new Color(.35f, .65f, 1f), 160);
                if (PerkLevel("dodge") == 2) CreditBossDamage(ApplyBossDamage(Mathf.RoundToInt(300 * StunMultiplier() * RushDamageMultiplier(new List<PlayerState> { p })), "Dodge"), new List<PlayerState> { p });
                RushAddUltimateCharge(p);
                break; // one charge per attack
            }
        }

        void RushForgetAttack(AttackState attack) => rushNearMiss.Remove(attack);

        void RushAddUltimateCharge(PlayerState player)
        {
            if (ultimateActive || CheckBossDefeated()) return;
            ultimateCharge++;
            if (ultimateCharge < UltimateNeed) { UpdateHud(); return; }
            ultimateCharge = 0;
            var contributors = new List<PlayerState> { player };
            ReplayUltimateStart(contributors);
            int opener = ApplyBossDamage(Mathf.RoundToInt(ultimateOpenDamage * RushDamageMultiplier(contributors)));
            replayUltDamage += opener;
            CreditBossDamage(opener, contributors);
            if (CheckBossDefeated()) return;
            BreakSignatureWindup("ultimate");
            ClearAttacks();
            SpawnUltimateSpots();
        }

        // ---------------------------------------------------------------- perk vote screens (floor + wall)
        string[] rushOffer = new string[0];
        bool rushPicked, rushRerolled;
        float rushRerollHold;
        string rushLastPick;
        readonly int[] rushVotes = new int[6];
        RectTransform perkFloorRoot, perkWallRoot;
        readonly List<(Image frame, TextMeshProUGUI tier, TextMeshProUGUI name, TextMeshProUGUI detail, TextMeshProUGUI source, TextMeshProUGUI votes)> perkFloorCards = new(), perkWallCards = new();
        float perkFontScale = 1f;
        readonly List<UiThemeKit.Plate> perkFloorPlates = new(), perkWallPlates = new();
        Image perkRerollPad, perkRerollFill;
        float perkRerollClosedAt = -1f; TextMeshProUGUI perkRerollLabel, perkWallFooter;

        Rect PerkCardRect(int i)
        {
            // 2026-11-27 "มันใหญ่ไป": an arched stepping pad (FloorPad), smaller than the old plates.
            var r = FloorRect; float w = Mathf.Min(r.width * .17f, r.height * .52f / FloorPadTall), h = w * FloorPadTall;
            float cx = r.xMin + r.width * (.2f + .3f * i), cy = r.yMin + r.height * .6f;
            return new Rect(cx - w * .5f, cy - h * .5f, w, h);
        }
        // the drawn button (RerollPad) is 3.5 : 1
        Rect RerollRect() { var r = FloorRect; float w = Mathf.Min(r.width * .22f, r.height * .17f * 3.5f), h = w / 3.5f; return new Rect(r.center.x - w * .5f, r.yMin + r.height * .06f, w, h); }

        string[] MakePerkOffer()
        {
            var pool = new List<RushPerk>();
            foreach (var p in RushPerks) if (p.Tier < 3 && !HasPerk(p.Id)) pool.Add(p);
            string evolve = null;
            foreach (var p in RushPerks) if (p.Tier == 3 && rushPerks.Contains(p.Base) && !rushPerks.Contains(p.Id)) { evolve = p.Id; break; }
            for (int tries = 0; tries < 60; tries++)
            {
                var shuffled = new List<RushPerk>(pool);
                for (int i = shuffled.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]); }
                var offer = new List<string>();
                int evolveLine = evolve != null ? PerkLineOf(PerkById(evolve)) : -1;
                if (evolve != null) offer.Add(evolve);
                for (int line = LineAttack; line <= LineControl; line++) // one card per line (Balance Note A6)
                {
                    if (line == evolveLine) continue;
                    var pick = shuffled.Find(p => PerkLineOf(p) == line);
                    if (pick != null) offer.Add(pick.Id);
                }
                bool rare = evolve != null || offer.Exists(id => PerkById(id).Tier > 0); // always at least one rare or better
                if (rare || tries == 59) return offer.ToArray();
            }
            return new string[0];
        }

        void OpenPerkBreak()
        {
            HideBossVictory(); SetBossVisible(false);
            gameStarted = false; selectingBoss = false; awaitingPlayers = false;
            SetGameplayHudVisible(false);
            if (characterPanel != null) characterPanel.gameObject.SetActive(true); // players see their own cursor to vote
            foreach (var p in players) { p.Hp = playerMaxHp; p.DownTimer = 0f; UpdatePlayerHp(p); }
            rushOffer = MakePerkOffer(); rushPicked = false; rushRerolled = false; rushRerollHold = 0; rushLastPick = null;
            System.Array.Clear(rushVotes, 0, rushVotes.Length);
            for (int i = 0; i < rushVotes.Length; i++) rushVotes[i] = -1;
            EnsurePerkScreens();
            perkFloorRoot.gameObject.SetActive(true); perkWallRoot.gameObject.SetActive(true);
            perkFloorRoot.SetAsFirstSibling();
            StartPerkReveal();
            sfx?.Score?.Menu();
            ReplayLog.Event("rush_perk_vote", 0, 0, null, ReplayLog.Data().Add("offer", string.Join(",", rushOffer)).Add("clock", rushClock));
            rushBreakOpen = true; rushBreakStart = rushClock; rushBreakStage = rushStage == RushStage.Perk2 ? RushStage.Perk2 : RushStage.Perk1;
            RefreshPerkScreens();
        }

        void ClosePerkBreak()
        {
            if (rushBreakOpen && !rushPicked) TallyPerkVote(); // the vote always resolves, even if the clock jumped
            rushBreakOpen = false;
            HidePerkScreens();
        }

        void HidePerkScreens()
        {
            if (perkFloorRoot != null) perkFloorRoot.gameObject.SetActive(false);
            if (perkWallRoot != null) perkWallRoot.gameObject.SetActive(false);
        }

        int PerkCardAt(Vector2 local) { for (int i = 0; i < rushOffer.Length; i++) if (PerkCardRect(i).Contains(local)) return i; return -1; }

        void UpdatePerkBreak(float dt)
        {
            float voteLeft = rushBreakStart + rushPerkVoteSeconds - rushClock;
            int present = 0, onPad = 0;
            foreach (var p in players)
            {
                if (!TryGetInputLocalPoint(p.Index, out var local)) { p.Cursor.gameObject.SetActive(false); continue; }
                p.Cursor.gameObject.SetActive(true);
                p.Cursor.anchoredPosition = new Vector2(Mathf.Clamp(local.x, FloorRect.xMin + 40f, FloorRect.xMax - 40f), Mathf.Clamp(local.y, FloorRect.yMin + 40f, FloorRect.yMax - 40f));
                present++;
                if (RerollRect().Contains(local)) onPad++;
                if (rushPicked || !PerkRevealDone) continue;   // the cards' faces are not showing yet
                // 2026-11-27 "ถ้า player ยืนบนการ์ดก่อนการ์ดจะโผล่ก็คือเลือกใบนั้นเลย": standing on a card counts as soon as
                // the faces show, even if the player was already there when the cards appeared.
                int card = PerkCardAt(local);
                if (card >= 0) rushVotes[p.Index] = card;
            }
            if (!rushPicked && !rushRerolled && voteLeft > 6f)
            {
                // Reroll once (Hades' Fated Authority): the whole team stands on the pad together for 1.5 s.
                rushRerollHold = present > 0 && onPad == present ? rushRerollHold + dt : 0f;
                if (rushRerollHold >= RerollHoldSeconds)
                {
                    var keep = new List<string>(); foreach (var id in rushOffer) if (PerkById(id).Tier == 3) keep.Add(id);
                    var before = new HashSet<string>(rushOffer);
                    var next = MakePerkOffer();
                    for (int i = 0; i < 20; i++) { bool fresh = false; foreach (var id in next) if (!keep.Contains(id) && !before.Contains(id)) fresh = true; if (fresh) break; next = MakePerkOffer(); }
                    rushOffer = next; rushRerolled = true; rushRerollHold = 0; StartPerkReveal();
                    for (int i = 0; i < rushVotes.Length; i++) rushVotes[i] = -1;
                    sfx?.Play("session_start", .5f, 0, .2f, 4); JuiceMilestone("REROLL!", new Color(.37f, .88f, 1f));
                }
            }
            if (!rushPicked && voteLeft <= 0f) TallyPerkVote();
            RefreshPerkScreens();
        }

        void TallyPerkVote()
        {
            if (rushOffer.Length == 0) { rushPicked = true; return; }
            var tally = new int[rushOffer.Length];
            for (int i = 0; i < playerCount && i < rushVotes.Length; i++) if (rushVotes[i] >= 0 && rushVotes[i] < tally.Length) tally[rushVotes[i]]++;
            int best = 0; foreach (var v in tally) best = Mathf.Max(best, v);
            var top = new List<int>(); for (int i = 0; i < tally.Length; i++) if (tally[i] == best) top.Add(i); // a tie is settled at random
            string id = rushOffer[top[Random.Range(0, top.Count)]];
            rushPerks.Add(id); rushLastPick = id; rushPicked = true;
            rushPickedAt = rushClock; StartPerkPick();
            if (id == "chaos") rushChaosEnd = rushClock + PerkPickShowSeconds + ChaosCurseSeconds;
            ApplyRushPerkStats();
            foreach (var p in players) { p.Hp = playerMaxHp; UpdatePlayerHp(p); }
            sfx?.Play("stun", .55f, 0, .2f, 4);
            JuiceMilestone(PerkById(id).Name + "!", TierColors[PerkById(id).Tier]);
            ReplayLog.Event("rush_perk", 1, 3, null, ReplayLog.Data().Add("perk", id).Add("offer", string.Join(",", rushOffer)).Add("rerolled", rushRerolled ? 1 : 0));
        }
        const float ChaosCurseSeconds = 60f; // the Chaos curse lasts the first minute of the next segment

        void EnsurePerkScreens()
        {
            if (perkFloorRoot == null) perkFloorRoot = BuildPerkScreen(InputArea, true, perkFloorCards);
            if (perkWallRoot == null) perkWallRoot = BuildPerkScreen(cutsceneArea, false, perkWallCards);
        }

        // 2026-11-25 "หน้าเลือก perk ให้เป็นพื้นหลัง select boss ทั้งจอกำแพงและจอพื้น": the perk vote sits on the boss
        // select's own backgrounds instead of the arena - the boss hall picture on the wall (cropped to fill, like
        // BossSelectionPanel does) and the looping rune floor (BossFloorMagicVideo, the same player the selects share).
        static void AddPerkBackdrop(RectTransform root, bool floor, Vector2 size)
        {
            var image = new GameObject("Perk Backdrop", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            var rect = image.rectTransform;
            rect.SetParent(root, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            image.raycastTarget = false;
            if (floor) { BossFloorMagicVideo.Attach(image); return; }
            var hall = GameArt.Load<Texture2D>("Art/UI/SelectBoss/SelectBossBG_v7");
            image.texture = hall;
            image.color = hall != null ? Color.white : Color.black;
            if (hall == null || size.x <= 0 || size.y <= 0) return;
            float picture = (float)hall.width / hall.height, screen = size.x / size.y;
            image.uvRect = picture > screen
                ? new Rect((1 - screen / picture) / 2, 0, screen / picture, 1)
                : new Rect(0, (1 - picture / screen) / 2, 1, picture / screen);
        }

        RectTransform BuildPerkScreen(RectTransform parent, bool floor, List<(Image, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI)> cards)
        {
            var root = new GameObject(floor ? "Rush Perk Vote (floor)" : "Rush Perk Vote (wall)", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false); root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var area = parent.rect;
            AddPerkBackdrop(root, floor, area.size);
            // The wall canvas is several times taller than the floor's: scale the text so it reads at the same size.
            perkFontScale = floor ? 1f : Mathf.Max(1f, area.height * .06f / 56f);
            float u = floor ? area.height / FloorDisplaySettings.Height : area.height / 2160f;
            // 2026-11-27 "ลบป้ายอันนี้ตรงพื้นออก ... Title ด้านบนลบทิ้งเลย": no title on either screen; the vote's clock is in
            // the wall's footer, and the floor cards are bare plates to stand on (the wall shows what each one is).
            for (int i = 0; i < 3; i++)
            {
                if (!floor) { cards.Add(BuildWallPerkCard(root, i, area)); continue; }
                Rect r = PerkCardRect(i);
                var frame = new GameObject("Perk Card " + (i + 1), typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                frame.rectTransform.SetParent(root, false); Place(frame.rectTransform, r, area);
                frame.color = Color.clear; frame.raycastTarget = false;
                BuildFloorPerkArt(frame.rectTransform);
                cards.Add((frame, null, null, null, null, null));
            }
            if (floor)
            {
                var pad = RerollRect();
                perkRerollPad = new GameObject("Reroll Pad", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                perkRerollPad.rectTransform.SetParent(root, false); Place(perkRerollPad.rectTransform, pad, area);
                perkRerollPad.color = Color.clear; perkRerollPad.raycastTarget = false;
                perkFloorPads.Add((perkRerollPad.rectTransform, 0, -9f)); // springs like the cards when stepped on
                // 2026-11-27 "ปุ่ม Reroll ก็ขอใหม่ด้วยให้เข้ากับธีม": the drawn pixel button (RerollPad); the code plate only if it is missing
                var padArt = PerkTexture(PerkArtFolder + "RerollPad");
                if (padArt != null)
                {
                    var art = new GameObject("Reroll Art", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                    art.rectTransform.SetParent(perkRerollPad.rectTransform, false); UiThemeKit.Stretch(art.rectTransform, 0);
                    art.texture = padArt; art.raycastTarget = false;
                }
                else
                {
                    var padPlate = UiThemeKit.MakePlate(perkRerollPad.rectTransform, "Codex Plate", UiThemeKit.Alpha(new Color(.37f, .88f, 1f), .85f), UiThemeKit.Alpha(BossFantasyTheme.Night, .97f),
                        30f * u, 5f * u, true, UiThemeKit.Alpha(BossFantasyTheme.Gold, .6f), true, UiThemeKit.Shape.Chamfer);
                    UiThemeKit.Stretch(padPlate.rect, 0); padPlate.rect.SetAsFirstSibling();
                    if (padPlate.glow != null) padPlate.glow.color = UiThemeKit.Alpha(new Color(.37f, .88f, 1f), .35f);
                }
                perkRerollFill = new GameObject("Reroll Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                perkRerollFill.rectTransform.SetParent(perkRerollPad.rectTransform, false);
                // the hold bar runs inside the button's border
                perkRerollFill.rectTransform.anchorMin = Vector2.zero; perkRerollFill.rectTransform.anchorMax = new Vector2(0, 1);
                perkRerollFill.rectTransform.offsetMin = new Vector2(pad.width * .05f, pad.height * .16f); perkRerollFill.rectTransform.offsetMax = new Vector2(-pad.width * .05f, -pad.height * .16f);
                perkRerollFill.color = new Color(.37f, .88f, 1f, .4f); perkRerollFill.raycastTarget = false;
                perkRerollLabel = PerkText(perkRerollPad.rectTransform, "Reroll Label", new Rect(0, 0, pad.width, pad.height), 40, FontStyles.Normal);
                // right of the drawn reroll arrows at the button's left end
                perkRerollLabel.rectTransform.anchorMin = new Vector2(padArt != null ? .28f : 0f, .15f); perkRerollLabel.rectTransform.anchorMax = new Vector2(.94f, .85f); perkRerollLabel.rectTransform.offsetMin = perkRerollLabel.rectTransform.offsetMax = Vector2.zero;
                // the pixel font the player cards and wall perk cards use (Jersey 15)
                BossFantasyTheme.Outlined(perkRerollLabel, PerkCardFont, new Color(.75f, .95f, 1f), .14f, 0);
            }
            else perkWallFooter = BuildWallPerkFooter(root, area);
            return root;
        }

        // r is in the area's rect space; children are anchored at the area's centre.
        static void Place(RectTransform rect, Rect r, Rect area)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = r.size; rect.anchoredPosition = r.center - area.center;
        }

        TextMeshProUGUI PerkText(RectTransform parent, string name, Rect r, float size, FontStyles style)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false);
            Place(label.rectTransform, r, parent.rect);
            BossFantasyTheme.UseFont(label, BossUiFont);
            label.fontStyle = style; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.enableAutoSizing = true; label.fontSizeMin = 12 * perkFontScale; label.fontSizeMax = size * perkFontScale;
            label.textWrappingMode = TextWrappingModes.Normal; label.color = new Color(.95f, .9f, 1f);
            return label;
        }

        void RefreshPerkScreens()
        {
            if (perkFloorRoot == null) return;
            float voteLeft = Mathf.Max(0, rushBreakStart + rushPerkVoteSeconds - rushClock);
            var tally = new int[3];
            for (int i = 0; i < playerCount && i < rushVotes.Length; i++) if (rushVotes[i] >= 0 && rushVotes[i] < 3) tally[rushVotes[i]]++;
            int secondsLeft = Mathf.CeilToInt(voteLeft);
            // 2026-11-27: the footer only counts the vote down ("TEAM PERK 1/2" is gone); once the pick is in, the band
            // and its text go too ("ตอนเลือก perk แล้ว เอาพื้นหลังและ text นี้ออก ให้มีแค่การ์ดพอ").
            if (perkWallFooter != null)
            {
                var band = perkWallFooter.transform.parent.gameObject;
                if (band.activeSelf == rushPicked) band.SetActive(!rushPicked);
                if (!rushPicked) perkWallFooter.text = Lang.T($"VOTE ENDS IN {secondsLeft}s", $"ปิดโหวตใน {secondsLeft} วิ");
            }
            for (int pass = 0; pass < 2; pass++)
            {
                var cards = pass == 0 ? perkFloorCards : perkWallCards;
                for (int i = 0; i < cards.Count; i++)
                {
                    var c = cards[i];
                    bool show = i < rushOffer.Length; c.frame.gameObject.SetActive(show);
                    if (!show) continue;
                    var perk = PerkById(rushOffer[i]); var tint = TierColors[perk.Tier];
                    bool chosen = rushPicked && rushLastPick == perk.Id;
                    var plates = pass == 0 ? perkFloorPlates : perkWallPlates;
                    if (i < plates.Count)
                    {
                        var plate = plates[i];
                        // chosen card: frame and glow take the tier colour; the others fade once the pick is in
                        UiThemeKit.Recolor(plate, chosen ? tint : UiThemeKit.Alpha(BossFantasyTheme.Gold, rushPicked ? .45f : .85f),
                            chosen ? UiThemeKit.Alpha(BossFantasyTheme.StoneLight, .95f) : UiThemeKit.Alpha(BossFantasyTheme.Night, rushPicked ? .75f : .97f));
                        if (plate.trim != null) plate.trim.color = UiThemeKit.Alpha(PerkLineColors[PerkLineOf(perk)], .8f);
                        if (plate.glow != null) plate.glow.color = UiThemeKit.Alpha(tint, chosen ? .6f : rushPicked ? 0f : .22f);
                    }
                    if (pass == 0) StyleFloorPerkCard(i, perk, chosen, tally[i]);
                    if (c.name == null) continue;   // the floor's cards carry no text
                    int perkLine = PerkLineOf(perk);
                    c.tier.text = PerkLineName(perkLine) + " : " + TierName(perk.Tier); c.tier.color = PerkLineColors[perkLine];
                    c.name.text = perk.Name;
                    c.detail.text = perk.Detail;
                    if (c.source != null) c.source.text = perk.Source;
                    c.votes.text = VotesLabel(tally[i]);
                    StyleWallPerkCard(i, perk, chosen);
                }
            }
            if (perkRerollPad != null)
            {
                bool open = !rushPicked && !rushRerolled && voteLeft > 6f;
                // 2026-11-27 "ตัวปุ่ม reroll ตอนจะหายจะขยายแล้วหด": closing, it grows then shrinks away before it goes
                if (open) perkRerollClosedAt = -1f;
                else if (perkRerollPad.gameObject.activeSelf && perkRerollClosedAt < 0f) perkRerollClosedAt = Time.unscaledTime;
                bool closing = !open && perkRerollClosedAt >= 0f && Time.unscaledTime - perkRerollClosedAt < PopAwaySeconds;
                perkRerollPad.gameObject.SetActive(open || closing);
                if (open)
                {
                    int present = 0, onPad = 0;
                    foreach (var p in players) if (TryGetInputLocalPoint(p.Index, out var local)) { present++; if (RerollRect().Contains(local)) onPad++; }
                    perkRerollLabel.text = Lang.T($"REROLL ({onPad}/{present})", $"สุ่มใหม่ ({onPad}/{present})");
                    perkRerollFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(rushRerollHold / RerollHoldSeconds), 1);
                }
            }
            AnimateFloorPerkPads();
        }
    }
}
