using System.Collections.Generic;
using FortalReplay;
using UnityEngine;

namespace BossSlayer
{
    // Balance Note v3 §3.2-3.3 (2026-11-26): every big move (the boss's signature ultimate) now winds up for 6 s first.
    // The ULTIMATE SKILL banner shows the charge bar (2026-11-27, no name), the boss stands still and makes no small attacks, and the
    // yellow gems the team still needs for a stun appear at once, spread apart. A stun or the team's ultimate in that
    // window breaks the move (BREAK!); otherwise it lands as before, each beat hits for 35, and 2 green gems follow it.
    public sealed partial class BossSlayerGameManager
    {
        [Header("Big move (Balance Note v3)")]
        [SerializeField, Min(.5f)] float signatureWindupSeconds = 6f;
        [SerializeField, Min(0f)] float windupStunGemGapMeters = 2.5f;
        [SerializeField, Min(0)] int signatureHitDamage = 35;
        [SerializeField, Min(0)] int healGemsAfterSignature = 2;

        static readonly Color BreakColor = new Color(1f, .82f, .24f);
        const int HealSpotIndex = 1, StunSpotIndex = 2;

        // Seconds wound up so far, or -1 when no wind-up runs.
        float signatureWindup = -1f;
        // Which threshold the current or last big move belongs to (the king alternates his two moves by it).
        int signatureCastIndex;
        bool signatureWindupLooping, signatureLanded;

        bool SignatureWindupActive => signatureWindup >= 0f;

        bool BeginSignatureWindup()
        {
            var kind = BossPassiveState.Identify(bossModel != null ? bossModel.name : string.Empty);
            string title = SignatureTitle(kind, bossSignatureCastCount, golemRedPhase);
            if (title == null) return false;
            signatureCastIndex = bossSignatureCastCount;
            signatureWindup = 0f;
            bossSignatureTitle = title;
            PlaySignatureChargeGlow(signatureWindupSeconds);
            // Only the Abyssal King has a charge clip; the other bosses keep their pose and glow (aura + charge VFX).
            var skin = ActiveBossSkin;
            signatureWindupLooping = skin != null && skin.Has("charge");
            if (signatureWindupLooping) skin.PlayLoop("charge");
            // SFX guide §4: boss_big_charge stands in for charge_<theme> (SpawnAttack already started that one).
            sfx?.StopCharge();
            sfx?.StartBigCharge();
            ultimateBanner?.SetCharge(0f);
            passiveFeedback?.Notify("CHARGING: " + title);
            SpawnWindupStunGems();
            ReplayLog.Event("boss_ultimate_charge", 1, 4, null, ReplayLog.Data().Add("boss", ReplayBoss()).Add("title", title)
                .Add("cast", signatureCastIndex + 1).Add("casts", SignatureThresholds.Length)
                .Add("bossHpPct", Mathf.RoundToInt(100f * bossHp / Mathf.Max(1, bossMaxHp))));
            return true;
        }

        // UpdateBoss, while a wind-up runs. The Chill perk slows the bar with the boss's attacks (up to 12 s).
        void TickSignatureWindup(float dt)
        {
            signatureWindup += dt * RushAttackTimeScale();
            float progress = Mathf.Clamp01(signatureWindup / signatureWindupSeconds);
            ultimateBanner?.SetCharge(progress);
            if (progress < 1f) return;
            EndSignatureWindup();
            int first = attacks.Count;
            if (!SpawnBossSignature())
            {
                bossSignatureTitle = null;
                UpdateBossSignatureBanner();
                return;
            }
            for (int i = first; i < attacks.Count; i++)
                if (attacks[i].WarningDelay <= 0) StartAttackStageMotion(attacks[i]);
            ReplayLog.Event("boss_ultimate_landed", 1, 4, null, ReplayLog.Data().Add("boss", ReplayBoss()).Add("title", bossSignatureTitle ?? "")
                .Add("cast", signatureCastIndex + 1));
        }

        // A stun or the team's ultimate during the wind-up stops the move. Returns false when no wind-up ran.
        // by: what broke it, for the replay log ("stun" or "ultimate").
        bool BreakSignatureWindup(string by)
        {
            if (!SignatureWindupActive) return false;
            string title = bossSignatureTitle;
            EndSignatureWindup();
            bossSignatureTitle = null;
            UpdateBossSignatureBanner();
            sfx?.Play("boss_break", .75f, 0, .2f, 5);
            JuiceMilestone("BREAK!", BreakColor, 1.5f);
            ReplayLog.Event("boss_ultimate_break", 3, 5, null, ReplayLog.Data().Add("boss", ReplayBoss()).Add("title", title ?? "")
                .Add("by", by).Add("cast", signatureCastIndex + 1));
            return true;
        }

        // A wind-up that can no longer finish (fight over, the king's finale, a transformation first) ends quietly.
        // refund: the same threshold winds up again later (after a transformation).
        void CancelSignatureWindup(bool refund)
        {
            if (!SignatureWindupActive) return;
            EndSignatureWindup();
            bossSignatureTitle = null;
            if (refund) bossSignatureCastCount = Mathf.Max(0, bossSignatureCastCount - 1);
        }

        // Takes down the wind-up's bar, sound, glow and charge pose.
        void EndSignatureWindup()
        {
            signatureWindup = -1f;
            sfx?.StopBigCharge();
            if (bossUltimateAura != null) bossUltimateAura.Stop();
            if (elementalChargeVfx != null) elementalChargeVfx.Stop();
            if (!signatureWindupLooping) return;
            signatureWindupLooping = false;
            ActiveBossSkin?.EndTransform();
        }

        // Exactly as many yellow gems as the stun gauge still lacks, no spare (RushStunGain counts the Heavy Hit perk).
        void SpawnWindupStunGems()
        {
            var def = spotDefs[StunSpotIndex];
            int gain = Mathf.Max(1, RushStunGain(def.Stun));
            int count = Mathf.CeilToInt(Mathf.Max(0f, stunNeed - stunGauge) / gain);
            var group = new List<Vector2>();
            for (int i = 0; i < count; i++) SpawnBonusSpot(def, windupStunGemGapMeters, group);
        }

        // UpdateAttacks: once a big move that reached the floor is over, the team gets its green gems.
        void TickSignatureAftermath()
        {
            if (!signatureLanded || HydraComboPending) return;
            foreach (var attack in attacks) if (attack.IsBossUltimate) return;
            signatureLanded = false;
            if (bossHp <= 0 || ultimateActive) return;
            var group = new List<Vector2>();
            for (int i = 0; i < healGemsAfterSignature; i++) SpawnBonusSpot(spotDefs[HealSpotIndex], spotMinGapMeters, group);
        }

        // A gem outside the floor cap and the colour bag. It keeps gap metres from the other gems of its group and the
        // usual spacing from every gem; on a crowded floor the gap shrinks rather than the gem being dropped.
        void SpawnBonusSpot(SpotDef def, float gap, List<Vector2> group)
        {
            if (InputArea == null) return;
            const int Tries = 80;
            for (int attempt = 0; attempt < Tries; attempt++)
            {
                var pos = RandomGemPosition(110f * gemFrameSizeMultiplier);
                float need = attempt < Tries / 2 ? gap : gap * .6f;
                bool close = false;
                foreach (var other in group) close |= Vector2.Distance(other, pos) < need;
                foreach (var other in spots) close |= Vector2.Distance(other.WorldPosition, pos) < spotMinGapMeters;
                if (close && attempt < Tries - 1) continue;
                group.Add(pos);
                spots.Add(CreateSpot(def, pos, $"Spot == {def.Name}", 110f * gemFrameSizeMultiplier, spotLifeSeconds));
                sfx?.Play("gem_spawn", 0.2f, SfxPan(pos.x), 0.05f, 0);
                return;
            }
        }

        // The ultimate aura and the large elemental charge round the boss, for the wind-up and for the move's own warning.
        void PlaySignatureChargeGlow(float seconds)
        {
            if (bossModel == null) return;
            var camera = bossDisplayCamera != null ? bossDisplayCamera : wallDisplayCamera;
            if (bossUltimateAura == null)
            {
                var aura = new GameObject("VFX == Boss Ultimate Aura"); aura.transform.SetParent(transform, false);
                bossUltimateAura = aura.AddComponent<BossUltimateAura>();
            }
            var ultimateColor = bossGlowColorByModel.TryGetValue(bossModel, out var glow) ? glow : new Color(1, .25f, .1f);
            bossUltimateAura.Play(bossModel, camera, BossWorldBounds(), ultimateColor, seconds);
            // Larger elemental charge distinguishes the ultimate from regular casts.
            if (elementalChargeVfx == null) return;
            var accent = bossGlowColorByModel.TryGetValue(bossModel, out var color) ? color : Color.white;
            elementalChargeVfx.Play(bossModel, camera, activeBossRenderers, accent, seconds, attackChargeVfxScale * 1.6f);
        }
    }
}
