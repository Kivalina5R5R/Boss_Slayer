using System.Collections.Generic;
using UnityEngine;

namespace BossSlayer
{
    // 2026-11-19: "ทำไมตอนเปลี่ยนฉากต่างๆมันกระตุก ... ทำเป็น preload คอยแอบโหลดต่างๆก่อน". The screen changes stalled on
    // work done on that very frame: the boss select / Start / card art read from disk the first time it showed, and each
    // boss's voxel model (Resources/BossSlayer/Skins, the heaviest) was loaded and built the first time that boss was
    // fought, right at the portal. Now, from the start of the scene: every one of those assets loads in the background,
    // then the bosses' models are built one per frame while nobody is fighting or counting down. The first fight and the
    // first boss select find everything ready.
    public sealed partial class BossSlayerGameManager
    {
        [Header("Preload")]
        [Tooltip("Load the screens' art and build every boss's model in the background from the start, so screen changes and first fights do not stall.")]
        [SerializeField] bool preloadAssets = true;

        static readonly string[] PreloadPaths =
        {
            "Art/UI/BossSlayerLogo",
            "Art/UI/Start/BG", "Art/UI/Start/LineBG1", "Art/UI/Start/LineBG2", "Art/UI/Start/Logo",
            "Art/UI/Start/Particle", "Art/UI/Start/Shard",
            "Art/UI/PlayerSelectCards/Card", "Art/UI/PlayerSelectCards/CardReady", "Art/UI/PlayerSelectCards/Floor",
            "Art/Players/P1", "Art/Players/P2", "Art/Players/P3",
            "Art/Players/P4", "Art/Players/P5", "Art/Players/P6",
            "Art/UI/SelectBoss/CardRed", "Art/UI/SelectBoss/CardBlue", "Art/UI/SelectBoss/CardBrown",
            "Art/UI/SelectBoss/Clear", "Art/UI/SelectBoss/SelectButton", "Art/UI/SelectBoss/SelectBossBG_v7",
            "Art/UI/SelectBoss/Shatter/Demon", "Art/UI/SelectBoss/Shatter/Fish", "Art/UI/SelectBoss/Shatter/Dragon",
            "Art/UI/SelectBoss/Shatter/Jellyfish", "Art/UI/SelectBoss/Shatter/Golem", "Art/UI/SelectBoss/Shatter/Wraith",
            "Art/UI/SelectBoss/Shatter/Sentinel", "Art/UI/SelectBoss/Shatter/Serpent", "Art/UI/SelectBoss/Shatter/Hydra",
            "Art/UI/Hud/HpBack", "Art/UI/Hud/HpFill", "Art/UI/Hud/NamePlate",
            "Art/UI/Hud/StunBack", "Art/UI/Hud/StunFill", "Art/UI/Hud/StunPlate",
            "Art/UI/Hud/UltimateBack", "Art/UI/Hud/UltimateFill", "Art/UI/Hud/UltimatePlate",
            "Art/UI/Hud/PlayerHpBack", "Art/UI/Hud/PlayerPlate1", "Art/UI/Hud/PlayerPlate2", "Art/UI/Hud/PlayerPlate3",
            "Art/UI/Hud/PlayerPlate4", "Art/UI/Hud/PlayerPlate5", "Art/UI/Hud/PlayerPlate6",
            "Art/UI/Victory/BgLight", "Art/UI/Victory/BgLine", "Art/UI/Victory/Line",
            "Art/UI/Victory/Ring", "Art/UI/Victory/Title", "Art/UI/Victory/Particle",
            "Art/UI/Gems/Red", "Art/UI/Gems/Blue", "Art/UI/Gems/Green", "Art/UI/Gems/Yellow",
            "Art/UI/Gems/RedParticles", "Art/UI/Gems/BlueParticles", "Art/UI/Gems/GreenParticles", "Art/UI/Gems/YellowParticles",
            "Prefab/Bosses/Boss_Demon_RedBoy", "Prefab/Bosses/Boss_Demon_YakshaKing", "Prefab/Bosses/Boss_GiantFish",
            "Prefab/Bosses/Boss_Dragon", "Prefab/Bosses/Boss_Jellyfish", "Prefab/Bosses/Boss_CrystalGolem",
            "Prefab/Bosses/Boss_VoidWraith", "Prefab/Bosses/Boss_Sentinel", "Prefab/Bosses/Boss_SeaSerpent",
            "Prefab/Bosses/Boss_Hydra", "Prefab/Bosses/Boss_AbyssalKing",
            "BossEnvironmentMotion", "BossThemeSurface",
        };

        ResourcePreloader preloader;
        readonly Queue<Transform> bossSkinsToBuild = new Queue<Transform>();
        bool preloadQueued, preloadReported;
        float preloadStartedAt;

        // Every frame from Update, before its early outs.
        void TickPreload()
        {
            if (!preloadAssets || preloadReported) return;
            if (preloader == null)
            {
                preloadStartedAt = Time.realtimeSinceStartup;
                preloader = new ResourcePreloader(PreloadPaths);
                return;
            }
            if (!preloader.IsDone) return;
            if (!preloadQueued) QueueBossSkins();
            // Each model is one main-thread build: never during a fight or the 3-2-1.
            if (gameStarted || bossCountdownRunning) return;
            while (bossSkinsToBuild.Count > 0)
            {
                var model = bossSkinsToBuild.Dequeue();
                if (model == null || bossSkins.ContainsKey(model)) continue;
                BuildBossSkins(model);
                return;   // one per frame
            }
            preloadReported = true;
            Debug.Log($"[BossSlayer] Preload done in {Time.realtimeSinceStartup - preloadStartedAt:0.0} s: {preloader.LoadedCount}/{preloader.Count} assets, {bossSkins.Count} boss models built", this);
        }

        void QueueBossSkins()
        {
            preloadQueued = true;
            foreach (var model in bossModelCandidates) if (model != null) bossSkinsToBuild.Enqueue(model);
            if (abyssalKingModel != null) bossSkinsToBuild.Enqueue(abyssalKingModel);
        }
    }
}
