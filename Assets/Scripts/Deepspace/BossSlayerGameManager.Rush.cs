using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
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
        [SerializeField, Min(15f)] float rushPerkSeconds = 40f;
        [SerializeField, Min(5f)] float rushPerkVoteSeconds = 28f;
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
                if (rushClock - rushBreakStart >= rushPerkSeconds) EnterRushStage(rushStage == RushStage.Perk1 ? RushStage.Rush2 : RushStage.Final);
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
                case RushStage.Rush2:
                    ClosePerkBreak(); rushHalfKills = 0;
                    StartCoroutine(RushFade(ShowBossSelection));
                    break;
                case RushStage.Final:
                    ClosePerkBreak();
                    StartCoroutine(RushFade(StartAbyssalKing, "THE ABYSSAL KING AWAKENS"));
                    break;
            }
        }

        IEnumerator RushFade(System.Action middle, string milestone = null)
        {
            sfx?.Play("transition", 0.45f, 0, 0.2f, 3);
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
        sealed class RushPerk
        {
            public string Id, Base, Name, Detail, Source; public int Tier; // 0 common, 1 rare, 2 epic, 3 evolved
            public RushPerk(string id, int tier, string name, string detail, string source, string evolvedFrom = null)
            { Id = id; Tier = tier; Name = name; Detail = detail; Source = source; Base = evolvedFrom; }
        }
        static readonly RushPerk[] RushPerks =
        {
            new("dmg", 0, "พลังโจมตี", "ดาเมจเจมทุกเม็ด +25%", "Hades · Pom of Power"),
            new("hp", 0, "เกราะเหล็ก", "เลือดสูงสุด +50% โดนตีเบาลง 25%", "Isaac · HP up"),
            new("stun", 0, "ทุบหนัก", "เจมเหลืองเติมเกจสตัน +27 (ครบ 3 ลูกสตัน)", "Hades · Stagger"),
            new("leech", 0, "ดูดพลัง", "คนที่เก็บเจมได้ +8 HP", "Dead Cells · lifesteal"),
            new("chain", 1, "สายฟ้าลูกโซ่", "เก็บเจม 25% ฟ้าผ่าเพิ่ม 250", "Risk of Rain 2 · Ukulele"),
            new("poison", 1, "พิษสะสม", "เจมแดงติดพิษ 15/วิ 6 วิ ซ้อนได้ 5", "Hades · Hangover"),
            new("doom", 1, "คำสาปมรณะ", "เจมเหลืองฝังคำสาป 3 วิแล้วระเบิด 150", "Hades · Doom"),
            new("chill", 1, "ลมหนาว", "เจมเหลืองทำให้บอสตีช้าลงครึ่งหนึ่ง 8 วิ ระหว่างนั้นดาเมจเจม +20%", "Hades · Chill"),
            new("duo", 1, "ผนึกกำลัง", "ยืนเจมเดียวกัน 2 คนขึ้นไป ชาร์จเร็ว 2 เท่า", "co-op · Ravenswatch"),
            new("dodge", 1, "หลบหวุดหวิด", "หนีออกจากวงแดงใน 0.6 วิสุดท้าย = อัลติ +1", "Hades · dash / Gungeon"),
            new("revive", 1, "ใจไม่ยอมแพ้", "ล้มแค่ 2.5 วิ ลุกขึ้นมาเลือดเต็ม", "Hades · Death Defiance"),
            new("chaos", 2, "สัญญาอเวจี", "60 วิแรกบอสตีแรง x1.5 จากนั้นดาเมจ +40% ทั้งเกม", "Hades · Chaos boon"),
            new("dmg2", 3, "พลังโจมตี II", "ดาเมจเจมรวม +50%", "วิวัฒน์จาก พลังโจมตี", "dmg"),
            new("hp2", 3, "ป้อมปราการ", "เลือดสูงสุด +100% โดนตีเบาลง 40%", "วิวัฒน์จาก เกราะเหล็ก", "hp"),
            new("stun2", 3, "ทุบแหลก", "ครบ 3 ลูกสตัน สตันนานขึ้น 2 วิ ช่วงสตันบอสรับดาเมจ +75%", "วิวัฒน์จาก ทุบหนัก", "stun"),
            new("leech2", 3, "ดูดวิญญาณ", "เก็บเจมแล้วทั้งทีม +10 HP", "วิวัฒน์จาก ดูดพลัง", "leech"),
            new("chain2", 3, "พายุสายฟ้า", "โอกาส 40% ฟ้าผ่า 350", "วิวัฒน์จาก สายฟ้าลูกโซ่", "chain"),
            new("poison2", 3, "พิษมรณะ", "ซ้อนได้ 10 บอสสตันแล้วพิษระเบิดชั้นละ 120", "วิวัฒน์จาก พิษสะสม", "poison"),
            new("doom2", 3, "คำสาปลูกโซ่", "คำสาประเบิด 300", "วิวัฒน์จาก คำสาปมรณะ", "doom"),
            new("chill2", 3, "ยุคน้ำแข็ง", "ช้าลง 12 วิ ดาเมจเจม +20% และสตันนานขึ้น 2 วิ", "วิวัฒน์จาก ลมหนาว", "chill"),
            new("duo2", 3, "หนึ่งใจเดียวกัน", "2 คนขึ้นไปชาร์จ 3 เท่า ดาเมจ +15%", "วิวัฒน์จาก ผนึกกำลัง", "duo"),
            new("dodge2", 3, "เงาวูบ", "หลบหวุดหวิด = อัลติ +1 และยิงบอส 300", "วิวัฒน์จาก หลบหวุดหวิด", "dodge"),
            new("revive2", 3, "อมตะ", "ล้มแค่ 1.5 วิ ลุกขึ้นมาเลือดเต็ม", "วิวัฒน์จาก ใจไม่ยอมแพ้", "revive"),
            new("chaos2", 3, "สัญญาอเวจี II", "ดาเมจรวม +70% ไม่มีคำสาปเพิ่ม", "วิวัฒน์จาก สัญญาอเวจี", "chaos"),
        };
        static readonly Color[] TierColors = { new Color(.79f, .76f, .84f), new Color(.73f, .55f, 1f), new Color(1f, .76f, .24f), new Color(.37f, .88f, 1f) };
        static readonly string[] TierNames = { "ธรรมดา", "หายาก", "ตำนาน", "วิวัฒน์" };
        static RushPerk PerkById(string id) { foreach (var p in RushPerks) if (p.Id == id) return p; return null; }

        readonly List<string> rushPerks = new();
        bool rushBaseCaptured; int rushBasePlayerHp, rushBaseStunNeed; float rushBaseStunSeconds, rushBaseDownSeconds;
        float rushChaosEnd, rushChill, rushPoisonTick;
        readonly List<List<PlayerState>> rushPoison = new();
        readonly List<float> rushPoisonLeft = new();
        readonly List<(float left, List<PlayerState> credit, Vector2 at)> rushDoom = new();
        readonly Dictionary<AttackState, HashSet<int>> rushNearMiss = new();

        bool HasPerk(string id) => rushPerks.Contains(id) || rushPerks.Contains(id + "2");
        int PerkLevel(string id) => rushPerks.Contains(id + "2") ? 2 : rushPerks.Contains(id) ? 1 : 0;
        bool RushChaosCurse => HasPerk("chaos") && rushClock < rushChaosEnd;

        void ResetRushPerks()
        {
            rushPerks.Clear(); rushChaosEnd = 0; rushChill = 0; rushPoisonTick = 0;
            rushPoison.Clear(); rushPoisonLeft.Clear(); rushDoom.Clear(); rushNearMiss.Clear(); rushAttackDimUntil.Clear();
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
            if (gem && rushChill > 0f && HasPerk("chill")) m *= 1.2f; // control line: not switched off by being hit
            if (!RushAttackLineLive(who)) return m;
            m *= new[] { 1f, 1.25f, 1.5f }[PerkLevel("dmg")];
            if (HasPerk("chaos") && !RushChaosCurse) m *= PerkLevel("chaos") == 2 ? 1.7f : 1.4f;
            if (PerkLevel("duo") == 2 && who != null && who.Count >= 2) m *= 1.15f;
            return m;
        }
        float RushIncomingMultiplier() => !IsRushActive ? 1f : (RushChaosCurse ? 1.5f : 1f) * new[] { 1f, .75f, .6f }[PerkLevel("hp")];
        float RushChargeRate(IReadOnlyList<PlayerState> who) => IsRushActive && who.Count >= 2 && RushAttackLineLive(who) ? new[] { 1f, 2f, 3f }[PerkLevel("duo")] : 1f;
        int RushStunGain(int gain) => IsRushActive && gain > 0 && HasPerk("stun") ? 27 : gain; // 3 gems reach the gauge of 80
        float RushAttackTimeScale() => IsRushActive && rushChill > 0f ? .5f : 1f;
        int RushReviveHp() => IsRushActive && HasPerk("revive") ? playerMaxHp : Mathf.RoundToInt(playerMaxHp * .5f);

        // HitBoss hook: gem side effects after its damage landed.
        void RushOnGemHit(SpotDef def, List<PlayerState> contributors, Vector2 at)
        {
            if (!IsRushActive || rushPerks.Count == 0 || bossHp <= 0) return;
            if (PerkLevel("leech") == 1) foreach (var p in contributors) { p.Hp = Mathf.Min(playerMaxHp, p.Hp + 8); UpdatePlayerHp(p); }
            if (PerkLevel("leech") == 2) foreach (var p in players) if (p.DownTimer <= 0f) { p.Hp = Mathf.Min(playerMaxHp, p.Hp + 10); UpdatePlayerHp(p); }
            bool attackLive = RushAttackLineLive(contributors);
            if (attackLive && HasPerk("chain") && Random.value < (PerkLevel("chain") == 2 ? .4f : .25f))
            {
                int zap = ApplyBossDamage(Mathf.RoundToInt((PerkLevel("chain") == 2 ? 350 : 250) * StunMultiplier() * RushDamageMultiplier(contributors)), "Chain");
                CreditBossDamage(zap, contributors);
                JuiceFloor(SpaceJuice.Pickup, WorldToLocal(at), new Color(.75f, .9f, 1f), 190);
            }
            if (attackLive && def.GemModelName == "Attack" && HasPerk("poison"))
            {
                rushPoison.Add(new List<PlayerState>(contributors)); rushPoisonLeft.Add(6f);
                int cap = PerkLevel("poison") == 2 ? 10 : 5;
                while (rushPoison.Count > cap) { rushPoison.RemoveAt(0); rushPoisonLeft.RemoveAt(0); }
            }
            if (def.Stun > 0 && HasPerk("doom")) rushDoom.Add((3f, new List<PlayerState>(contributors), at));
            if (def.Stun > 0 && HasPerk("chill")) rushChill = PerkLevel("chill") == 2 ? 12f : 8f;
        }

        // HitBoss hook: the stun just triggered.
        void RushOnStun(List<PlayerState> contributors)
        {
            if (!IsRushActive || PerkLevel("poison") != 2 || rushPoison.Count == 0) return;
            int burst = ApplyBossDamage(Mathf.RoundToInt(120 * rushPoison.Count * RushDamageMultiplier(contributors)), "Poison");
            CreditBossDamage(burst, contributors);
            JuiceMilestone("TOXIC BURST!", new Color(.6f, 1f, .4f));
            rushPoison.Clear(); rushPoisonLeft.Clear();
        }

        void TickRushPerks(float dt)
        {
            UpdateAttackLineIcons();
            if (rushPerks.Count == 0 || bossHp <= 0) return;
            rushChill = Mathf.Max(0f, rushChill - dt);
            // Poison: every stack ticks 15/s, applied in half-second chunks so the damage numbers stay readable.
            for (int i = rushPoisonLeft.Count - 1; i >= 0; i--) { rushPoisonLeft[i] -= dt; if (rushPoisonLeft[i] <= 0f) { rushPoisonLeft.RemoveAt(i); rushPoison.RemoveAt(i); } }
            rushPoisonTick += dt;
            if (rushPoisonTick >= .5f)
            {
                rushPoisonTick -= .5f;
                foreach (var credit in rushPoison)
                {
                    int tick = ApplyBossDamage(Mathf.RoundToInt(7.5f * RushDamageMultiplier(credit)), "Poison");
                    CreditBossDamage(tick, credit);
                }
            }
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
        void RushTrackNearMiss(AttackState attack)
        {
            if (!IsRushActive || !HasPerk("dodge") || attack.Shape == Shape.Wave || attack.WarningDelay > 0f) return;
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
                if (!inside.Contains(p.Index) || attack.HitPlayers.Contains(p.Index) || p.DownTimer > 0f || PerksDimmed(p)) continue;
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
            if (ultimateCharge < ultimateNeed) { UpdateHud(); return; }
            ultimateCharge = 0;
            var contributors = new List<PlayerState> { player };
            ReplayUltimateStart(contributors);
            int opener = ApplyBossDamage(Mathf.RoundToInt(ultimateOpenDamage * RushDamageMultiplier(contributors)));
            replayUltDamage += opener;
            CreditBossDamage(opener, contributors);
            if (CheckBossDefeated()) return;
            ClearAttacks();
            SpawnUltimateSpots();
        }

        // ---------------------------------------------------------------- perk vote screens (floor + wall)
        string[] rushOffer = new string[0];
        bool rushPicked, rushRerolled;
        float rushRerollHold;
        string rushLastPick;
        readonly int[] rushVotes = new int[6], rushStaleCard = new int[6];
        RectTransform perkFloorRoot, perkWallRoot;
        readonly List<(Image frame, TextMeshProUGUI tier, TextMeshProUGUI name, TextMeshProUGUI detail, TextMeshProUGUI source, TextMeshProUGUI votes)> perkFloorCards = new(), perkWallCards = new();
        float perkFontScale = 1f;
        readonly List<UiThemeKit.Plate> perkFloorPlates = new(), perkWallPlates = new();
        Image perkRerollPad, perkRerollFill; TextMeshProUGUI perkRerollLabel, perkFloorTitle, perkWallTitle, perkWallFooter;

        Rect PerkCardRect(int i)
        {
            var r = FloorRect; float w = r.width * .25f, h = r.height * .54f;
            float cx = r.xMin + r.width * (.2f + .3f * i), cy = r.yMin + r.height * .56f;
            return new Rect(cx - w * .5f, cy - h * .5f, w, h);
        }
        Rect RerollRect() { var r = FloorRect; float w = r.width * .2f, h = r.height * .15f; return new Rect(r.center.x - w * .5f, r.yMin + r.height * .06f, w, h); }

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
            for (int i = 0; i < rushStaleCard.Length; i++) rushStaleCard[i] = -1;
            foreach (var p in players) if (TryGetInputLocalPoint(p.Index, out var local)) rushStaleCard[p.Index] = PerkCardAt(local); // standing on a card as it appears is not a vote
            System.Array.Clear(rushVotes, 0, rushVotes.Length);
            for (int i = 0; i < rushVotes.Length; i++) rushVotes[i] = -1;
            EnsurePerkScreens();
            perkFloorRoot.gameObject.SetActive(true); perkWallRoot.gameObject.SetActive(true);
            perkFloorRoot.SetAsFirstSibling();
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
                if (rushPicked) continue;
                int card = PerkCardAt(local);
                if (rushStaleCard[p.Index] >= 0 && card == rushStaleCard[p.Index]) continue;
                rushStaleCard[p.Index] = -1;
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
                    rushOffer = next; rushRerolled = true; rushRerollHold = 0;
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
            if (id == "chaos") rushChaosEnd = rushBreakStart + rushPerkSeconds + ChaosCurseSeconds;
            ApplyRushPerkStats();
            foreach (var p in players) { p.Hp = playerMaxHp; UpdatePlayerHp(p); }
            sfx?.Play("stun", .55f, 0, .2f, 4);
            JuiceMilestone(PerkById(id).Name + "!", TierColors[PerkById(id).Tier]);
            ReplayLog.Event("rush_perk", 1, 3, null, ReplayLog.Data().Add("perk", id).Add("offer", string.Join(",", rushOffer)).Add("rerolled", rushRerolled ? 1 : 0));
        }
        const float ChaosCurseSeconds = 60f; // the Chaos curse lasts the first minute of the next segment

        void EnsurePerkScreens()
        {
            if (perkFloorRoot == null) perkFloorRoot = BuildPerkScreen(InputArea, true, perkFloorCards, out perkFloorTitle);
            if (perkWallRoot == null) perkWallRoot = BuildPerkScreen(cutsceneArea, false, perkWallCards, out perkWallTitle);
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
            var hall = Resources.Load<Texture2D>("BossSlayer/SelectBoss/SelectBossBG_v7");
            image.texture = hall;
            image.color = hall != null ? Color.white : Color.black;
            if (hall == null || size.x <= 0 || size.y <= 0) return;
            float picture = (float)hall.width / hall.height, screen = size.x / size.y;
            image.uvRect = picture > screen
                ? new Rect((1 - screen / picture) / 2, 0, screen / picture, 1)
                : new Rect(0, (1 - picture / screen) / 2, 1, picture / screen);
        }

        RectTransform BuildPerkScreen(RectTransform parent, bool floor, List<(Image, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI)> cards, out TextMeshProUGUI title)
        {
            var root = new GameObject(floor ? "Rush Perk Vote (floor)" : "Rush Perk Vote (wall)", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false); root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var area = parent.rect;
            AddPerkBackdrop(root, floor, area.size);
            // The wall canvas is several times taller than the floor's: scale the text so it reads at the same size.
            perkFontScale = floor ? 1f : Mathf.Max(1f, area.height * .06f / 56f);
            float u = floor ? area.height / FloorDisplaySettings.Height : area.height / 2160f;
            title = PerkText(root, "Title", floor ? new Rect(area.xMin + area.width * .2f, area.yMax - area.height * .17f, area.width * .6f, area.height * .13f)
                : new Rect(area.xMin + area.width * .25f, area.yMax - area.height * .2f, area.width * .5f, area.height * .14f), 64, FontStyles.Bold);
            CodexGold(title, false, 4); // Thai once the pick is in, so the body font (Cinzel has no Thai glyphs)
            float ruleY = floor ? .895f : .87f;
            BossFantasyTheme.Rule(root, "Title Rule Left", floor ? .08f : .14f, floor ? .19f : .24f, ruleY, (floor ? 3 : 5) * u, true);
            BossFantasyTheme.Rule(root, "Title Rule Right", floor ? .81f : .76f, floor ? .92f : .86f, ruleY, (floor ? 3 : 5) * u, false);
            for (int i = 0; i < 3; i++)
            {
                Rect r = floor ? PerkCardRect(i) : new Rect(area.xMin + area.width * (.2f + .2f * i), area.yMin + area.height * .14f, area.width * .18f, area.height * .62f);
                var frame = new GameObject("Perk Card " + (i + 1), typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                frame.rectTransform.SetParent(root, false); Place(frame.rectTransform, r, area);
                frame.color = Color.clear; frame.raycastTarget = false;
                // codex card: chamfered night stone, gilded frame, trim in the tier colour (recoloured in RefreshPerkScreens)
                var plate = UiThemeKit.MakePlate(frame.rectTransform, "Codex Plate", UiThemeKit.Alpha(BossFantasyTheme.Gold, .85f), UiThemeKit.Alpha(BossFantasyTheme.Night, .97f),
                    40f * u, 6f * u, true, BossFantasyTheme.GoldDeep, true, UiThemeKit.Shape.Chamfer);
                UiThemeKit.Stretch(plate.rect, 0); plate.rect.SetAsFirstSibling();
                (floor ? perkFloorPlates : perkWallPlates).Add(plate);
                var tier = PerkText(frame.rectTransform, "Tier", Sub(r, .82f, .14f), 30, FontStyles.Bold);
                var name = PerkText(frame.rectTransform, "Name", Sub(r, .6f, .2f), 56, FontStyles.Bold);
                var detail = PerkText(frame.rectTransform, "Detail", Sub(r, .32f, .28f), 30, FontStyles.Normal);
                var source = PerkText(frame.rectTransform, "Source", Sub(r, .2f, .1f), 22, FontStyles.Italic);
                var votes = PerkText(frame.rectTransform, "Votes", Sub(r, .03f, .16f), 48, FontStyles.Bold);
                CodexLetter(tier, BossFantasyTheme.Gold, 3); CodexGold(name, false, 2);
                CodexLetter(detail, BossFantasyTheme.Parchment, 1); CodexLetter(source, BossFantasyTheme.ParchmentDim, 1);
                CodexGold(votes, true, 4);
                cards.Add((frame, tier, name, detail, source, votes));
            }
            if (floor)
            {
                var pad = RerollRect();
                perkRerollPad = new GameObject("Reroll Pad", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                perkRerollPad.rectTransform.SetParent(root, false); Place(perkRerollPad.rectTransform, pad, area);
                perkRerollPad.color = Color.clear; perkRerollPad.raycastTarget = false;
                var padPlate = UiThemeKit.MakePlate(perkRerollPad.rectTransform, "Codex Plate", UiThemeKit.Alpha(new Color(.37f, .88f, 1f), .85f), UiThemeKit.Alpha(BossFantasyTheme.Night, .97f),
                    30f * u, 5f * u, true, UiThemeKit.Alpha(BossFantasyTheme.Gold, .6f), true, UiThemeKit.Shape.Chamfer);
                UiThemeKit.Stretch(padPlate.rect, 0); padPlate.rect.SetAsFirstSibling();
                if (padPlate.glow != null) padPlate.glow.color = UiThemeKit.Alpha(new Color(.37f, .88f, 1f), .35f);
                perkRerollFill = new GameObject("Reroll Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                perkRerollFill.rectTransform.SetParent(perkRerollPad.rectTransform, false);
                perkRerollFill.rectTransform.anchorMin = Vector2.zero; perkRerollFill.rectTransform.anchorMax = new Vector2(0, 1); perkRerollFill.rectTransform.offsetMin = perkRerollFill.rectTransform.offsetMax = Vector2.zero;
                perkRerollFill.color = new Color(.37f, .88f, 1f, .55f); perkRerollFill.raycastTarget = false;
                perkRerollLabel = PerkText(perkRerollPad.rectTransform, "Reroll Label", new Rect(0, 0, pad.width, pad.height), 34, FontStyles.Bold);
                perkRerollLabel.rectTransform.anchorMin = Vector2.zero; perkRerollLabel.rectTransform.anchorMax = Vector2.one; perkRerollLabel.rectTransform.offsetMin = perkRerollLabel.rectTransform.offsetMax = Vector2.zero;
                CodexLetter(perkRerollLabel, new Color(.75f, .95f, 1f), 2);
            }
            else
            {
                perkWallFooter = PerkText(root, "Footer", new Rect(area.xMin + area.width * .2f, area.yMin + area.height * .03f, area.width * .6f, area.height * .09f), 36, FontStyles.Normal);
                CodexLetter(perkWallFooter, BossFantasyTheme.ParchmentDim, 2);
            }
            return root;
        }

        // Card children are laid out in the card's own space: y fraction from the bottom, height fraction.
        static Rect Sub(Rect card, float y, float h) => new Rect(card.width * .06f, card.height * y, card.width * .88f, card.height * h);

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
            if (parent.GetComponent<Image>() != null && parent.name.StartsWith("Perk Card"))
            {
                // Inside a card: anchor to the card's bottom-left so Sub() rects are card-relative.
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.zero; label.rectTransform.pivot = Vector2.zero;
                label.rectTransform.sizeDelta = r.size; label.rectTransform.anchoredPosition = r.position;
            }
            else Place(label.rectTransform, r, parent.rect);
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
            int round = rushBreakStage == RushStage.Perk1 ? 1 : 2;
            var tally = new int[3];
            for (int i = 0; i < playerCount && i < rushVotes.Length; i++) if (rushVotes[i] >= 0 && rushVotes[i] < 3) tally[rushVotes[i]]++;
            string heading = rushPicked ? $"ได้พลังทีม: {PerkById(rushLastPick)?.Name}" : $"เลือกพลังทีม {round}/2  •  ปิดโหวตใน {Mathf.CeilToInt(voteLeft)} วิ";
            if (perkFloorTitle != null) perkFloorTitle.text = heading;
            if (perkWallTitle != null) perkWallTitle.text = rushPicked ? heading : $"TEAM PERK {round}/2  •  {Mathf.CeilToInt(voteLeft)}";
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
                    int perkLine = PerkLineOf(perk);
                    c.tier.text = PerkLineNames[perkLine] + "  •  " + TierNames[perk.Tier]; c.tier.color = PerkLineColors[perkLine];
                    c.name.text = perk.Name;
                    c.detail.text = perk.Detail; c.source.text = perk.Source;
                    c.votes.text = tally[i] + (pass == 1 ? " VOTES" : "");
                }
            }
            if (perkRerollPad != null)
            {
                bool open = !rushPicked && !rushRerolled && voteLeft > 6f;
                perkRerollPad.gameObject.SetActive(open);
                if (open)
                {
                    int present = 0, onPad = 0;
                    foreach (var p in players) if (TryGetInputLocalPoint(p.Index, out var local)) { present++; if (RerollRect().Contains(local)) onPad++; }
                    perkRerollLabel.text = $"สุ่มใหม่ ({onPad}/{present})";
                    perkRerollFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(rushRerollHold / RerollHoldSeconds), 1);
                }
            }
            if (perkWallFooter != null)
            {
                var owned = new List<string>(); foreach (var id in rushPerks) owned.Add(PerkById(id).Name);
                perkWallFooter.text = owned.Count > 0 ? "พลังทีม: " + string.Join("  •  ", owned) : rushRerolled ? "สุ่มใหม่แล้ว" : "ทุกคนยืนช่อง สุ่มใหม่ บนพื้นพร้อมกัน = สุ่มการ์ดใหม่ 1 ครั้ง";
            }
        }
    }
}
