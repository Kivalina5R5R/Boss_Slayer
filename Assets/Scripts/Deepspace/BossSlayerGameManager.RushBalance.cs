using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VFX;
using FortalReplay;

namespace Deepspace
{
    // Balance Note v2 / GDD v3.1 (2026-11-08): the rush is capped in time without a losing state.
    // Bosses 1-6 escape at 95 s, the Abyssal King detonates the arena at 120 s, boss HP comes from a per-slot table scaled by
    // the player count locked at the lobby, and team perks belong to three lines with their own rule.
    public sealed partial class BossSlayerGameManager
    {
        [Header("Boss Rush balance v2")]
        [Tooltip("HP at 4 players: bosses 1-6, then the Abyssal King.")]
        [SerializeField] int[] rushBossHpTable = { 6000, 6500, 7000, 13000, 13500, 14000, 18000 };
        [Tooltip("HP multiplier for 1 to 6 players.")]
        [SerializeField] float[] playerCountHpMultiplier = { .18f, .4f, .7f, 1f, 1.25f, 1.55f };
        [SerializeField, Min(1f)] float bossEscapeSeconds = 95f;
        [SerializeField, Min(1f)] float bossEscapeWarnSeconds = 80f;
        [SerializeField, Min(1f)] float abyssalKingChargeSeconds = 105f;
        [SerializeField, Min(1f)] float abyssalKingEndSeconds = 120f;
        [Tooltip("Seconds a player's attack-line perks stay off after being hit.")]
        [SerializeField, Min(0f)] float perkLineAttackDimSeconds = 4f;

        const float EscapeWarnRepeatSeconds = 5f, BossEscapeMoveSeconds = 1.2f, KingDetonationSeconds = 2.4f;
        const string AttackLineIconName = "Attack Perk Icon";
        static readonly Color EscapeColor = new Color(1f, .55f, .2f), AbyssColor = new Color(.75f, .25f, 1f);

        float rushFightClock, escapeWarnRepeat, kingChargeBurst;
        bool rushEscapeWarned, rushBossEscaped, rushKingCharging;
        readonly Dictionary<int, float> rushAttackDimUntil = new();

        int RushTotalBosses => rushBossesPerHalf * 2 + 1;

        // ---------------------------------------------------------------- boss HP
        int RushBossHp(int round, bool king = false)
        {
            if (rushBossHpTable == null || rushBossHpTable.Length == 0) return bossMaxHp;
            int last = rushBossHpTable.Length - 1;
            int slot = king ? last : Mathf.Clamp(round - 1, 0, Mathf.Max(0, last - 1));
            return Mathf.Max(1, Mathf.RoundToInt(rushBossHpTable[slot] * RushPlayerCountMultiplier()));
        }

        float RushPlayerCountMultiplier()
        {
            if (playerCountHpMultiplier == null || playerCountHpMultiplier.Length == 0) return 1f;
            return playerCountHpMultiplier[Mathf.Clamp(playerCount - 1, 0, playerCountHpMultiplier.Length - 1)];
        }

        // The boss select shows how much tougher this round is than the first boss.
        int RushHpPercentOfRound(int round)
        {
            if (rushBossHpTable == null || rushBossHpTable.Length < 2 || rushBossHpTable[0] <= 0) return 0;
            int slot = Mathf.Clamp(round - 1, 0, rushBossHpTable.Length - 2);
            return Mathf.RoundToInt((rushBossHpTable[slot] / (float)rushBossHpTable[0] - 1f) * 100f);
        }

        // ---------------------------------------------------------------- fight clock
        void ResetBossClock()
        {
            rushFightClock = 0f; escapeWarnRepeat = 0f; kingChargeBurst = 0f;
            rushEscapeWarned = rushBossEscaped = rushKingCharging = false;
        }

        // Runs from TickRush, which Update skips while the game is frozen for PLAYER LOST, so the clock stops with it.
        void TickBossClock(float dt)
        {
            if (!rushFighting || bossHp <= 0) return;
            rushFightClock += dt;
            if (IsAbyssalKingActive) TickKingFinale(dt);
            else TickBossEscape(dt);
        }

        void TickBossEscape(float dt)
        {
            if (!rushEscapeWarned && rushFightClock >= bossEscapeWarnSeconds) BeginBossEscapeWarning();
            if (rushEscapeWarned)
            {
                escapeWarnRepeat -= dt;
                if (escapeWarnRepeat <= 0f) AnnounceBossEscaping();
            }
            if (rushFightClock >= bossEscapeSeconds) StartBossEscape();
        }

        void BeginBossEscapeWarning()
        {
            rushEscapeWarned = true;
            ReplayLog.Event("boss_escape_warning", 2, 3, null, ReplayLog.Data().Add("boss", ReplayBoss()).Add("bossHp", bossHp).Add("clock", rushFightClock));
            if (bossEntrancePortal != null && showBossPortal)
            {
                entrancePortalTween?.Kill();
                bossEntrancePortal.localScale = Vector3.zero;
                bossEntrancePortal.gameObject.SetActive(true);
                var portalVfx = bossEntrancePortal.GetComponent<VisualEffect>();
                if (portalVfx != null) portalVfx.Play();
                entrancePortalTween = bossEntrancePortal.DOScale(bossEntrancePortalBaseScale, Mathf.Max(.1f, bossEscapeSeconds - bossEscapeWarnSeconds)).SetEase(Ease.Linear);
            }
            AnnounceBossEscaping();
        }

        // Placeholder for the announcer: the voice lines are delivered separately (Balance Note A1).
        void AnnounceBossEscaping()
        {
            escapeWarnRepeat = EscapeWarnRepeatSeconds;
            JuiceMilestone("THE BOSS IS ESCAPING!", EscapeColor);
            sfx?.Play("portal_open", .5f, 0, .2f, 4);
        }

        void CloseEscapePortal()
        {
            if (!rushEscapeWarned || bossEntrancePortal == null) return;
            entrancePortalTween?.Kill();
            var portalVfx = bossEntrancePortal.GetComponent<VisualEffect>();
            if (portalVfx != null) portalVfx.Stop();
            bossEntrancePortal.gameObject.SetActive(false);
        }

        void StopFightForRushEnd()
        {
            rushFighting = false; rushKingCharging = false;
            resultShown = true;
            sfx?.StopCombat();
            ClearGemFlights();
            ultimateActive = false;
            ReplayTryEndUltimate();
            if (bossModel != null) bossModel.GetComponent<BossWraithAnimation>()?.StopMist();
            if (bossModel != null) bossModel.GetComponent<BossSerpentAnimation>()?.StopWater();
            ClearAttacks();
            ClearSpots();
        }

        void StartBossEscape()
        {
            if (resultShown) return;
            ReplayLog.Event("boss_escape", 3, 3, null, ReplayLog.Data().Add("boss", ReplayBoss()).Add("bossHp", bossHp).Add("bossMaxHp", bossMaxHp));
            StopFightForRushEnd();
            rushBossEscaped = true;
            rushHalfKills++; // the half still moves on; rushKills, which the results show, does not count an escape
            StartCoroutine(BossEscapeRoutine());
        }

        IEnumerator BossEscapeRoutine()
        {
            sfx?.Play("portal_open", .6f, 0, .1f, 4);
            var escapeSkin = ActiveBossSkin;
            if (escapeSkin != null && escapeSkin.Has("escape"))
            {
                escapeSkin.PlayHeld("escape");
                yield return new WaitForSeconds(escapeSkin.Length("escape"));
                SetBossVisible(false);
                CloseEscapePortal();
                yield return ShowBossVictory();
                yield return RushAfterBossDefeated();
                yield break;
            }
            if (bossModel != null && bossEntrancePortal != null)
            {
                var portal = bossModel.parent != null ? bossModel.parent.InverseTransformPoint(bossEntrancePortal.position) : bossEntrancePortal.position;
                entranceMoveTween = bossModel.DOLocalMove(portal, BossEscapeMoveSeconds).SetEase(Ease.InQuad);
                entranceScaleTween = bossModel.DOScale(Vector3.zero, BossEscapeMoveSeconds).SetEase(Ease.InBack);
            }
            yield return new WaitForSeconds(BossEscapeMoveSeconds);
            SetBossVisible(false);
            CloseEscapePortal();
            yield return ShowBossVictory();
            yield return RushAfterBossDefeated();
        }

        // ---------------------------------------------------------------- Abyssal King finale
        void TickKingFinale(float dt)
        {
            if (!rushKingCharging && rushFightClock >= abyssalKingChargeSeconds) BeginKingFinalCharge();
            if (rushKingCharging) AnimateKingFinalCharge(dt);
            if (rushFightClock >= abyssalKingEndSeconds) StartKingDetonation();
        }

        void BeginKingFinalCharge()
        {
            rushKingCharging = true;
            ReplayLog.Event("king_final_charge", 3, 4, null, ReplayLog.Data().Add("bossHp", bossHp).Add("clock", rushFightClock));
            ActiveBossSkin?.PlayLoop("charge");
            JuiceMilestone("THE ABYSS IS GATHERING", AbyssColor);
            sfx?.Play("portal_open", .6f, 0, .2f, 5);
        }

        // Power visibly builds on the wall and floor, faster as the end nears. Attacks already in the air still land.
        void AnimateKingFinalCharge(float dt)
        {
            var king = ActiveAbyssalKing;
            float span = Mathf.Max(.1f, abyssalKingEndSeconds - abyssalKingChargeSeconds);
            float t = Mathf.Clamp01((rushFightClock - abyssalKingChargeSeconds) / span);
            ActiveBossSkin?.SetChargeProgress(t);
            if (king != null && !king.IsCasting && stunTimer <= 0f) king.BeginCast(0, Mathf.Max(.5f, abyssalKingEndSeconds - rushFightClock));
            kingChargeBurst -= dt;
            if (kingChargeBurst > 0f) return;
            kingChargeBurst = Mathf.Lerp(1f, .2f, t);
            if (king != null) SpawnBossExplosionVfx(king.CoreWorld, Mathf.Lerp(.7f, 1.8f, t));
            var floor = FloorRect;
            JuiceFloor(SpaceJuice.Hurt, new Vector2(Random.Range(floor.xMin, floor.xMax), Random.Range(floor.yMin, floor.yMax)), AbyssColor, Mathf.Lerp(120f, 300f, t));
        }

        void StartKingDetonation()
        {
            if (resultShown) return;
            ReplayLog.Event("king_detonation", 4, 4, null, ReplayLog.Data().Add("bossHp", bossHp).Add("bossMaxHp", bossMaxHp));
            StopFightForRushEnd();
            StartCoroutine(KingDetonationRoutine());
        }

        // A closing scene, not a move to dodge: no safe gaps and no score lost.
        IEnumerator KingDetonationRoutine()
        {
            ActiveBossSkin?.PlayHeld("blast");
            JuiceMilestone("ABYSSAL ANNIHILATION", AbyssColor);
            sfx?.Play("boss_defeat", .8f, 0, .3f, 5);
            var bounds = BossWorldBounds();
            var floor = FloorRect;
            for (float age = 0f; age < KingDetonationSeconds; age += .15f)
            {
                SpawnBossExplosionVfx(bounds.center + Vector3.Scale(bounds.extents, new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-1f, 1f), 0f)), Random.Range(1.2f, 2.4f));
                for (int i = 0; i < 4; i++)
                    JuiceFloor(SpaceJuice.Hurt, new Vector2(Random.Range(floor.xMin, floor.xMax), Random.Range(floor.yMin, floor.yMax)), AbyssColor, 320f);
                yield return new WaitForSeconds(.15f);
            }
            yield return TweenSessionTransitionAlpha(1f, sessionTransitionFadeSeconds);
            SetBossVisible(false);
            rushStage = RushStage.Results;
            ReplayLog.Event("rush_stage", 0, 0, null, ReplayLog.Data().Add("stage", "Results").Add("clock", rushClock));
            ShowSessionResultsScreen();
            yield return TweenSessionTransitionAlpha(0f, sessionTransitionFadeSeconds);
        }

        // ---------------------------------------------------------------- perk lines
        const int LineAttack = 0, LineDefense = 1, LineControl = 2;
        static readonly string[] PerkLineNames = { "สายโจมตี", "สายป้องกัน", "สายควบคุม" };
        static readonly Color[] PerkLineColors = { new Color(1f, .36f, .3f), new Color(.36f, .68f, 1f), new Color(.45f, .95f, .95f) };

        static int PerkLineOf(RushPerk perk)
        {
            switch (perk.Base ?? perk.Id)
            {
                case "hp": case "leech": case "revive": return LineDefense;
                case "stun": case "chill": case "doom": return LineControl;
                default: return LineAttack;
            }
        }

        bool TeamHasAttackLine()
        {
            foreach (var id in rushPerks) if (PerkLineOf(PerkById(id)) == LineAttack) return true;
            return false;
        }

        bool PerksDimmed(PlayerState player) => rushAttackDimUntil.TryGetValue(player.Index, out float until) && rushClock < until;

        // A gem shared by several players pays out in full while at least one of them still has their perks.
        bool RushAttackLineLive(IReadOnlyList<PlayerState> who)
        {
            if (who == null || who.Count == 0) return true;
            foreach (var player in who) if (!PerksDimmed(player)) return true;
            return false;
        }

        void RushOnPlayerHit(PlayerState player)
        {
            if (!IsRushActive || !TeamHasAttackLine()) return;
            rushAttackDimUntil[player.Index] = rushClock + perkLineAttackDimSeconds;
        }

        // A small marker on each player's floor HP ring shows whether their attack-line perks are on.
        void UpdateAttackLineIcons()
        {
            bool show = TeamHasAttackLine();
            foreach (var player in players)
            {
                if (player.Cursor == null) continue;
                var icon = player.Cursor.Find(AttackLineIconName) as RectTransform;
                if (icon == null)
                {
                    if (!show) continue;
                    icon = new GameObject(AttackLineIconName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    icon.SetParent(player.Cursor, false);
                    icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(.5f, .5f);
                    float ring = playerCursorCircleDiameter * FloorHpRingScale;
                    icon.anchoredPosition = new Vector2(0f, ring * .5f);
                    icon.sizeDelta = Vector2.one * (ring * .2f);
                    var created = icon.GetComponent<Image>();
                    created.sprite = GetCircleSprite();
                    created.raycastTarget = false;
                }
                icon.gameObject.SetActive(show);
                if (show) icon.GetComponent<Image>().color = PerksDimmed(player) ? new Color(.2f, .2f, .22f, .9f) : PerkLineColors[LineAttack];
            }
        }
    }
}
