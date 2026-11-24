using System.Collections.Generic;
using UnityEngine;

namespace Deepspace
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
            "BossSlayer/BossSlayerLogo",
            "BossSlayer/Start/BG", "BossSlayer/Start/LineBG1", "BossSlayer/Start/LineBG2", "BossSlayer/Start/Logo",
            "BossSlayer/Start/Particle", "BossSlayer/Start/Shard",
            "BossSlayer/PlayerSelectCards/Card", "BossSlayer/PlayerSelectCards/CardReady", "BossSlayer/PlayerSelectCards/Floor",
            "BossSlayer/Players/P1", "BossSlayer/Players/P2", "BossSlayer/Players/P3",
            "BossSlayer/Players/P4", "BossSlayer/Players/P5", "BossSlayer/Players/P6",
            "BossSlayer/SelectBoss/CardRed", "BossSlayer/SelectBoss/CardBlue", "BossSlayer/SelectBoss/CardBrown",
            "BossSlayer/SelectBoss/Clear", "BossSlayer/SelectBoss/SelectButton", "BossSlayer/SelectBoss/SelectBossBG_v7",
            "BossSlayer/SelectBoss/Shatter/Demon", "BossSlayer/SelectBoss/Shatter/Fish", "BossSlayer/SelectBoss/Shatter/Dragon",
            "BossSlayer/SelectBoss/Shatter/Jellyfish", "BossSlayer/SelectBoss/Shatter/Golem", "BossSlayer/SelectBoss/Shatter/Wraith",
            "BossSlayer/SelectBoss/Shatter/Sentinel", "BossSlayer/SelectBoss/Shatter/Serpent", "BossSlayer/SelectBoss/Shatter/Hydra",
            "BossSlayer/Hud/HpBack", "BossSlayer/Hud/HpFill", "BossSlayer/Hud/NamePlate",
            "BossSlayer/Hud/StunBack", "BossSlayer/Hud/StunFill", "BossSlayer/Hud/StunPlate",
            "BossSlayer/Hud/UltimateBack", "BossSlayer/Hud/UltimateFill", "BossSlayer/Hud/UltimatePlate",
            "BossSlayer/Gems/Red", "BossSlayer/Gems/Blue", "BossSlayer/Gems/Green", "BossSlayer/Gems/Yellow",
            "BossSlayer/Gems/RedParticles", "BossSlayer/Gems/BlueParticles", "BossSlayer/Gems/GreenParticles", "BossSlayer/Gems/YellowParticles",
            "BossSlayer/Skins/Boss_Demon_RedBoy", "BossSlayer/Skins/Boss_Demon_YakshaKing", "BossSlayer/Skins/Boss_GiantFish",
            "BossSlayer/Skins/Boss_Dragon", "BossSlayer/Skins/Boss_Jellyfish", "BossSlayer/Skins/Boss_CrystalGolem",
            "BossSlayer/Skins/Boss_VoidWraith", "BossSlayer/Skins/Boss_Sentinel", "BossSlayer/Skins/Boss_SeaSerpent",
            "BossSlayer/Skins/Boss_Hydra", "BossSlayer/Skins/Boss_AbyssalKing",
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
