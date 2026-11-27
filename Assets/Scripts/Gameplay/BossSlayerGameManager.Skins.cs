using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BossSlayer
{
    // Connects the fight to the Design team's new boss models (BossVoxelSkin). The hooks only pick which clip to show.
    public sealed partial class BossSlayerGameManager
    {
        readonly Dictionary<Transform, BossVoxelSkin[]> bossSkins = new();
        bool demonSkinAdult;
        float skinLastBossX;

        static string[] SkinPrefabsFor(BossPassiveState.Kind kind)
        {
            switch (kind)
            {
                case BossPassiveState.Kind.Demon: return new[] { "Boss_Demon_RedBoy", "Boss_Demon_YakshaKing" };
                case BossPassiveState.Kind.Fish: return new[] { "Boss_GiantFish" };
                case BossPassiveState.Kind.Dragon: return new[] { "Boss_Dragon" };
                case BossPassiveState.Kind.Jellyfish: return new[] { "Boss_Jellyfish" };
                case BossPassiveState.Kind.Golem: return new[] { "Boss_CrystalGolem" };
                case BossPassiveState.Kind.Wraith: return new[] { "Boss_VoidWraith" };
                case BossPassiveState.Kind.Sentinel: return new[] { "Boss_Sentinel" };
                case BossPassiveState.Kind.Serpent: return new[] { "Boss_SeaSerpent" };
                case BossPassiveState.Kind.Hydra: return new[] { "Boss_Hydra" };
                case BossPassiveState.Kind.AbyssalKing: return new[] { "Boss_AbyssalKing" };
                default: return new string[0];
            }
        }

        BossVoxelSkin ActiveBossSkin
        {
            get
            {
                if (bossModel == null || !bossSkins.TryGetValue(bossModel, out var skins) || skins.Length == 0) return null;
                return skins[demonSkinAdult && skins.Length > 1 ? 1 : 0];
            }
        }

        // Each boss wears its model in the fight (built ahead by the preload, BossSlayerGameManager.Preload.cs).
        void EnsureBossSkin()
        {
            demonSkinAdult = false; skinShieldUp = skinMistUp = false;
            if (bossModel == null) return;
            var skins = BuildBossSkins(bossModel);
            for (int i = 0; i < skins.Length; i++) skins[i].gameObject.SetActive(i == 0);
            ActiveBossSkin?.ResetForFight();
        }

        // Builds a boss's voxel model(s) the first time they are asked for and returns them (built ones are kept).
        BossVoxelSkin[] BuildBossSkins(Transform model)
        {
            if (bossSkins.TryGetValue(model, out var built)) return built;
            var made = new List<BossVoxelSkin>();
            var camera = bossDisplayCamera != null ? bossDisplayCamera : wallDisplayCamera;
            foreach (var prefab in SkinPrefabsFor(BossPassiveState.Identify(model.name)))
            {
                var skin = BossVoxelSkin.Attach(model, prefab, camera, prefab == "Boss_Demon_YakshaKing" ? 1.7f : 1f); // the adult form is the giant
                if (skin != null) { MapSkinAnchors(model, skin); made.Add(skin); }
            }
            return bossSkins[model] = made.ToArray();
        }

        bool skinShieldUp, skinMistUp;

        // Jellyfish shield and Wraith mist follow the passive timers. Their clips only play when no attack is using the body.
        void SkinTickPassives()
        {
            var skin = ActiveBossSkin;
            if (skin == null) return;
            bool shield = passives.ShieldUp && stunTimer <= 0f, mist = passives.MistUp && stunTimer <= 0f;
            if (shield != skinShieldUp)
            {
                skinShieldUp = shield;
                skin.ShowShield(shield);
                if (shield && !skin.IsBusy) skin.Play("shield");
            }
            if (mist != skinMistUp)
            {
                skinMistUp = mist;
                skin.WipeTo(mist ? "mist" : "void");
                if (mist && !skin.IsBusy) skin.Play("mist");
            }
        }

        // Old sockets -> new bones, so attack effects gather at the new mouth, hands, tentacles and heads.
        static void MapSkinAnchors(Transform owner, BossVoxelSkin skin)
        {
            var motion = owner.GetComponent<BossModelAnimation>();
            if (motion != null && motion.parts != null)
            {
                string[][] roles =
                {
                    new[] { "chest", "body", "bell" },
                    new[] { "fore_L", "arm_L", "pec_L", "fin_L", "wing_L", "glove_L" },
                    new[] { "fore_R", "arm_R", "pec_R", "fin_R", "wing_R", "glove_R" },
                    new[] { "head", "bell" },
                    new[] { "jaw", "head", "bell" },
                    new[] { "tail", "chest", "body" },
                };
                for (int i = 0; i < motion.parts.Length && i < roles.Length; i++) skin.MapAnchor(motion.parts[i], roles[i]);
            }
            if (motion is BossDemonAnimation demon && demon.joints != null)
            {
                skin.MapAnchor(demon.joints[14], "spear");
                skin.MapAnchor(demon.joints[6], "fore_L"); skin.MapAnchor(demon.joints[7], "fore_R");
                skin.MapAnchor(demon.joints[12], "foot_L"); skin.MapAnchor(demon.joints[13], "foot_R");
            }
            if (motion is BossSentinelAnimation sentinel && sentinel.parts != null && sentinel.parts.Length > 5)
            {
                skin.MapAnchor(sentinel.parts[4], "foot_L"); skin.MapAnchor(sentinel.parts[5], "foot_R");
            }
            var jelly = owner.GetComponent<BossJellyfishAnimation>();
            if (jelly != null && jelly.tentacles != null)
                for (int i = 0; i < jelly.tentacles.Length; i++) skin.MapAnchor(jelly.tentacles[i], "tent" + (i % 8) + "_b");
            var hydra = owner.GetComponent<BossHydra>();
            if (hydra != null)
                for (int i = 0; i < BossHydra.MaximumHeads; i++) skin.MapAnchor(hydra.Skull(i), "head" + BossVoxelSkin.HydraOldToNew[i]);
        }

        void SkinFollowLocomotion()
        {
            var skin = ActiveBossSkin;
            if (skin == null) return;
            float delta = bossX - skinLastBossX;
            skinLastBossX = bossX;
            skin.SetMoving(Mathf.Abs(delta) > .0002f, delta >= 0f);
        }

        void SkinOnAttackStart(AttackState attack)
        {
            var skin = ActiveBossSkin;
            if (skin == null) return;
            // The ultimate goes before the busy check: it takes over a normal attack clip, or waits out a transformation
            // (BossVoxelSkin.PlayAttack queues it); its several hazards start it once, not once each.
            if (attack.IsBossUltimate) { if (!skin.IsPlayingClip("ultimate")) skin.PlayAttack("ultimate", 1f); return; }
            if (skin.IsBusy) return;
            if (attack.AnimationVariant == 3 && skin.Has("fire")) { skin.PlayAttack("fire", 2f / Mathf.Max(.1f, attack.TelegraphTimer)); return; } // the Dragon's breath
            bool heavy = attack.AnimationVariant % 2 == 1;
            string clip = heavy ? "atk_heavy" : skin.Has("atk_light_l") && Random.value < .5f ? "atk_light_l" : "atk_light";
            // Regular moves land at 2.0 s in their clip, which has to meet the end of the floor warning.
            skin.PlayAttack(clip, 2f / Mathf.Max(.1f, attack.TelegraphTimer));
        }

        void SkinTransform(float seconds, float themeTime, string theme) => ActiveBossSkin?.Transform2("transform", seconds, themeTime, theme);

        // Red Boy and Yaksha King are separate models: the swap is hidden in the flames of the transformation.
        void SkinDemonAdult()
        {
            if (bossModel == null || !bossSkins.TryGetValue(bossModel, out var skins) || skins.Length < 2) return;
            demonSkinAdult = true;
            skins[0].gameObject.SetActive(false);
            skins[1].gameObject.SetActive(true);
            skins[1].ResetForFight();
            skins[1].SetTheme("ember");
            skins[1].Play("roar");
            skins[1].CueTheme(.65f, "lava");
            CacheActiveBossRenderers();
            // 2026-11-27: no toon smoke burst on the swap any more ("มัน 2d การ์ตูนเกินไป"); the skin's own flames cover it.
        }

        IEnumerator SkinDeathRoutine(BossVoxelSkin skin)
        {
            skin.PlayHeld("death");
            yield return new WaitForSeconds(skin.Length("death"));
            PlayBossDefeatBurst(false);
            yield return skin.Shatter();
        }
    }
}
