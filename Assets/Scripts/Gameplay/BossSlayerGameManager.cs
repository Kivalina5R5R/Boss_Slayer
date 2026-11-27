using System.Collections;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.VFX;
using FortalReplay;

namespace BossSlayer
{
    public sealed partial class BossSlayerGameManager : MonoBehaviour
    {
        enum Shape { Circle, Line, Zone, Wave, Beam }
        enum BossSessionPackage
        {
            ThreeBosses = 3,
            FiveBosses = 5,
            SevenBosses = 7,
        }

        // 2026-11-20 (BossSlayer project): players come from the project's UWBPlayerSpawner through BossFloorPlayers
        // (PlayerSelect.cs). Off = Editor mouse testing only (Player 1 follows the mouse, the pads are skipped).
        [Header("Input")]
        [SerializeField] bool useUwbInput = true;
        [SerializeField] bool useMouseInEditor = true;

        [Header("Scene")]
        [SerializeField] RectTransform playArea;
        [SerializeField] RectTransform cutsceneArea;
        [SerializeField] RectTransform cutsceneMenuPanel;
        [SerializeField] RectTransform gameplayMenuPanel;
        // บาส 2026-09-03: "ปิด UI Gameplay ในหน้าเมนู" — the boss HP bar/stun
        // gauge/ultimate gauge HUD ("Panel == Boss" and "Panel == UI" under
        // Cutscene) are siblings of Panel == Menu, not children of it, so
        // they kept showing THROUGH the title/menu screen before a fight
        // even starts. Hidden together with the menu in ShowMenu(), shown
        // again once ResetFight() actually starts a fight.
        [SerializeField] RectTransform cutsceneBossPanel;
        [SerializeField] RectTransform cutsceneUiPanel;
        [SerializeField] RectTransform playerHpPanel;
        [SerializeField] RectTransform characterPanel;
        [SerializeField] RectTransform bossImage;
        [SerializeField] TMP_Text bossHpText;
        [SerializeField] Slider bossHpSlider;
        [SerializeField] Scrollbar bossHpScrollbar;
        [SerializeField] Image bossHpFill;
        [SerializeField] Image stunGaugeFill;
        [SerializeField] Image ultimateGaugeFill;
        [SerializeField] TMP_Text stunGaugeText;
        [SerializeField] TMP_Text ultimateGaugeText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] RectTransform resultPanel;
        [SerializeField] RectTransform gameplayResultPanel;
        [SerializeField] RectTransform gameplayPlayerScorePanel;
        [Header("Session")]
        [Tooltip("Number of bosses per session: 3, 5 or 7. Configured here only; no menu selector.")]
        [SerializeField] BossSessionPackage sessionPackage = BossSessionPackage.ThreeBosses;
        [SerializeField] float sessionTransitionFadeSeconds = 0.45f;
        Transform queuedNextBoss;
        BossNextBossCard floorNextBossCard, wallNextBossCard;
        [SerializeField] Color sessionTransitionColor = Color.black;
        [SerializeField, Range(1, 6)] int playerCount = 4;
        public int PlayerCount
        {
            get => playerCount;
            set => playerCount = Mathf.Clamp(value, 1, 6);
        }
        [SerializeField] BossFightReadyScreen[] readyScreens;
        readonly BossFightReadyGate readyGate = new BossFightReadyGate();
        readonly bool[] readyPlayers = new bool[6];
        bool awaitingPlayers;
        public int RequiredPlayerCount => playerCount;
        // บาส 2026-10-02: the real player count comes from the tags on the floor (2026-11-18: Food Isekai's player system,
        // .PlayerSelect.cs); the Inspector value is only the fallback when there is no UWB receiver (Editor / LED-only),
        // so keep it apart from the live count.
        int inspectorPlayerCount;
        public bool IsAwaitingPlayers => awaitingPlayers;
        // Manually assign to force one specific boss every fight; leave empty to
        // auto-discover every match of bossModelNamePatterns in the scene and
        // pick a weighted-random one each ResetFight (see DiscoverBossModelCandidates).
        [SerializeField] Transform bossModel;
        [Header("Hydra Boss")]
        [SerializeField] bool includeHydraBoss = true;
        [SerializeField, HideInInspector] float hydraSelectionWeight = 1f; // Legacy value migrated into the shared weights list.
        Transform hydraModel;
        [Header("Boss Passives")]
        [SerializeField] bool enableBossPassives = true;
        readonly BossPassiveState passives = new BossPassiveState();
        BossPassiveFeedback passiveFeedback;
        int feedbackHeads = -1;

        void LateUpdate()
        {
            WriteReplayFrame();
            TickBossUiTheme(Time.deltaTime);
            if (!gameStarted || resultShown || bossHp <= 0 || !enableBossPassives || passives.Active == BossPassiveState.Kind.None)
            { if (passiveFeedback != null) passiveFeedback.Hide(); feedbackHeads = -1; return; }
            if (cutsceneArea == null) return;
            if (passiveFeedback == null)
            {
                var go = new GameObject("Boss Passive Feedback");
                go.transform.SetParent(transform, false);
                passiveFeedback = go.AddComponent<BossPassiveFeedback>();
                passiveFeedback.Initialize(cutsceneArea, statusText != null ? statusText.font : null);
            }
            var hydra = bossModel != null ? bossModel.GetComponent<BossHydra>() : null;
            int heads = hydra != null ? hydra.ActiveHeads : 0;
            if (feedbackHeads >= 0 && heads != feedbackHeads)
                passiveFeedback.Notify(heads > feedbackHeads ? "HEADS REGROWN!" : "HEAD SEVERED!");
            feedbackHeads = heads;
            bool active; Color color;
            string message = passives.Feedback(bossHp, bossMaxHp, stunTimer > 0f, heads,
                hydra != null ? hydra.PendingRegrowth : 0, out active, out color);
            if (passives.Active == BossPassiveState.Kind.Dragon)
            {
                active = false;
                float fireRemaining = 0f;
                foreach (var attack in attacks)
                    if (attack.Lingering && attack.TelegraphTimer <= -.45f && attack.ActiveTimer > 0f)
                        fireRemaining = Mathf.Max(fireRemaining, attack.ActiveTimer);
                active = fireRemaining > 0f || passives.DragonHeated;
                if (fireRemaining > 0f) message = (passives.DragonHeated ? "INFERNO PHASE | " : "") + "BURNING FLOOR " + fireRemaining.ToString("0.0") + "s\nOrange fire still hurts - stay out!";
            }
            if (passives.Active == BossPassiveState.Kind.Serpent)
            {
                active = attacks.Count > 0 && passives.AttacksLaunched > 0 && passives.AttacksLaunched % 4 == 0;
                if (active) message = "STORM TIDE ACTIVE\nDOUBLE WAVE | Move into SAFE";
            }
            if (passives.Active == BossPassiveState.Kind.Fish && ultimateActive)
            { active = false; message = "RECOVERY PAUSED\nUltimate in progress"; }
            passiveFeedback.Show(message, active, color, bossModel, bossDisplayCamera, passives.Active, stunTimer > 0f);
        }
        // บาส 2026-09-03: 6 new bosses placed in the scene (Assets/Scenes/Bosses/
        // *.fbx — the vertex-color .glb -> Blender -> .fbx round-trip, confirmed
        // working via the `Fortal/VertexColorLit` shader already in this
        // project's Assets/Shaders/, same path Dragon/Demon/Fish already went
        // through). The one existing "Dragon Boss" slot now points at the NEW
        // voxel dragon (DragonBoss.fbx — dragonWingsBossGenerator.js in the
        // Fortal3DS repo: legs + wings, no whiskers) — บาส retired the old
        // rigged "Dragon Boss 1.fbx"/DragonBossController and removed it from
        // the scene entirely, this isn't a second dragon alongside the first.
        // Patterns below match the exact names บาส actually gave each prefab
        // instance in the Editor, NOT the "<namePrefix>_Voxel" glTF default
        // names the generator scripts export — several got renamed/typo'd on
        // the way in (e.g. "JeelyFishBoss", not "JellyfishBoss"; "CrystalGolem",
        // not "CrystalGolemBoss") — Contains() matching means these have to be
        // exact substrings of whatever's actually in the scene, so if a name
        // changes again in the Editor, update the matching entry here too.
        [SerializeField] string[] bossModelNamePatterns =
        {
            "Demon Boss", "Giant Fish", "DragonBoss", "JeelyFishBoss", "CrystalGolem", "VoidWraithBoss", "SentinelBoss", "SeaSerpentBoss", "HydraBoss",
        };
        // Parallel to bossModelNamePatterns by index (pattern[i] <-> weight[i]) —
        // higher weight means more likely to be picked, not a guaranteed
        // fraction (ResetFight rolls against the total every fight, same as a
        // loot table). A pattern past the end of this array — or the whole
        // array left empty — defaults to weight 1 (uniform), so filling this
        // in is optional. บาส 2026-09-03: back to flat 1/1/1/1/1/1/1/1 (equal
        // odds for all 8) after debugging one boss at a time with the others
        // zeroed out — retune individual entries later once there's a feel
        // for whether any boss should show up more/less often than the rest.
        [SerializeField] float[] bossModelWeights = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        [SerializeField] Camera wallDisplayCamera;
        [SerializeField] Camera floorDisplayCamera;
        [SerializeField] Camera bossDisplayCamera;
        [SerializeField, Range(1f, 1.25f)] float bossPresentationScale = 1.15f;
        [SerializeField, Range(1f, 1.5f)] float bossWallSizeMultiplier = 1.22f;
        // Per-boss size on the wall (บาส 2026-09-29). Transform scale can't do this: FitBossHaloFraming
        // zooms the wall camera to each boss's bounding circle, so a wide silhouette (Giant Fish fins)
        // reads small and a tall one (Sentinel) fills the frame. >1 = bigger on screen, <1 = smaller.
        // Bosses not listed (and the runtime Hydra) use 1. Replaces the old hard-coded Dragon ×1.2.
        [System.Serializable] public struct BossWallFraming { public BossModelAnimation.Kind boss; [Range(0.5f, 2f)] public float multiplier; }
        [SerializeField] BossWallFraming[] bossWallFramingMultipliers =
        {
            new BossWallFraming { boss = BossModelAnimation.Kind.Dragon, multiplier = 1.2f },
            new BossWallFraming { boss = BossModelAnimation.Kind.Fish, multiplier = 1.4f },
            new BossWallFraming { boss = BossModelAnimation.Kind.Sentinel, multiplier = 0.9f },
        };
        [SerializeField] float bossModelBobAmplitude = 0.08f;
        [SerializeField] float bossModelBobSpeed = 2.2f;
        [SerializeField] float bossModelTurnDegrees = 18f;
        [SerializeField] float bossModelBankDegrees = 10f;
        [SerializeField] float bossModelIdleYawDegrees = 6f;
        [SerializeField] float bossModelIdleYawSpeed = 1.4f;
        [SerializeField] float bossModelRotationLerp = 6f;

        [Header("Boss Entrance")]
        // บาส 2026-09-03: "แสดงเอฟเฟค Portal แล้วบอสโผล่มาจาก portal ค่อยเริ่มเกม" —
        // found by exact name ("Portal1", the VisualEffect GameObject already
        // hand-placed in BossFight.unity), hidden at Start() like every other
        // template in this file (explosionVfx etc.), then activated + played by
        // BossEntranceRoutine each time StartQuickSession fires. Not cloned —
        // unlike attackChargeVfx/explosionVfx, this only ever plays once per
        // fight (the intro), never needs multiple instances live at once.
        [SerializeField] Transform bossEntrancePortal;
        // บาส 2026-11-18: the blue Portal1 ring is no longer shown (entrance or rush escape). Its position is still where
        // the boss flies in from; tick this to bring the ring back.
        [SerializeField] bool showBossPortal;
        // บาส 2026-09-03 (2nd pass): "มี portal โผล่แล้วบอสบินออกมาจาก Portal
        // แล้วค่อยปิด portal ด้วยการหดเล็กลง" — portal now scales 0 -> its own
        // rest scale (bossEntrancePortalBaseScale, captured once at discovery
        // time — NOT assumed to be Vector3.one, in case it's ever re-scaled in
        // the Editor) to "open," instead of just SetActive(true) with no
        // transition. How long that scale-up takes.
        [SerializeField] float bossEntrancePortalOpenSeconds = 0.5f;
        // Extra hold AFTER the portal finishes opening, before the boss starts
        // flying out — gives the open portal a beat to actually read before
        // something emerges from it, rather than the two overlapping.
        [SerializeField] float bossEntrancePortalLeadSeconds = 0.4f;
        // How long the boss's fly-out-and-grow takes: bossModel starts AT the
        // portal's world position (converted into bossModel's own local space
        // — the two don't share a parent) and Vector3.zero scale, then
        // DOLocalMove + DOScale together carry it to its real rest
        // position/scale (screen-center — see BossHorizontalRestPosition) —
        // a "flying out of the portal" motion, not just growing in place at
        // its final spot. บาส 2026-09-03 (3rd pass): 0.6 -> 1.6 — the original
        // short "punchy pop" duration (matching ButtonPressFeedback's
        // DOPunchScale/the Ready button's pop) read as too fast for the boss
        // to visibly cross the distance from the portal to center screen;
        // this move is a bigger, slower beat than those quick UI pops, not
        // the same kind of motion. gameStarted only flips true (and the fight
        // clock/attack timer starts) once this finishes, so the boss can't
        // get hit or attack mid-emerge — see ResetFight's own comment for why
        // that matters.
        [SerializeField] float bossEntranceRevealSeconds = 1.6f;
        // Portal closing (shrinks back to 0) happens AFTER gameStarted flips
        // true — the fight doesn't wait on this, it's just the portal
        // tidying itself away in the background while play has already begun.
        [SerializeField] float bossEntrancePortalCloseSeconds = 0.4f;
        // Captured once in ResolveSceneReferences, the moment the portal is
        // first found — its designed rest scale, so opening/closing always
        // animates toward/from the CORRECT size instead of an assumed
        // Vector3.one that would be wrong if it's ever resized in the Editor.
        Vector3 bossEntrancePortalBaseScale = Vector3.one;
        Tween entrancePortalTween, entranceMoveTween, entranceScaleTween;



        [Header("Boss Death Effect")]
        [SerializeField] Transform explosionVfx;
        // Rapid-fire sweep across the boss's silhouette (see the sweepX/jitterY/
        // depth math in BossDeathSequenceRoutine), then one final blast at the
        // same scale — no extra size-up on the last one (unlike
        // DeepspaceGameManager's own boss death sequence, which this was
        // otherwise copied from): a boss-sized finale read as too big at 2.2x,
        // so this stays at 1x.
        // bossDeathResultDelaySeconds holds the result screen back long enough
        // for the whole sweep to finish playing.
        [SerializeField] int bossDeathExplosionBursts = 24;
        [SerializeField] float bossDeathResultDelaySeconds = 2.2f;

        [Header("Boss Attack Effect")]
        // Each boss gets its own attack-charge VFX Graph object (dragon breath vs
        // demon magic vs fish splash read very differently) — index-matched to
        // bossModelNamePatterns by position, same parallel-array idiom as
        // bossModelWeights, NOT by any shared naming convention (the 3 objects
        // in-scene are named inconsistently: "FX_DragonAttack" vs
        // "FX_AttackChargeDemon"/"FX_AttackChargeFish", so matching by name
        // pattern would be fragile — explicit names here sidestep that). Author
        // each graph yourself in the Editor's Visual Effect Graph window; this
        // only needs a GameObject with a Visual Effect component per boss, found
        // once by exact name and hidden, same template pattern as explosionVfx.
        // The 5 all-new bosses' entries below don't exist in the scene yet —
        // same "author it yourself" requirement as the originals, nothing
        // this pipeline can generate. Until each one exists, FindTransformExact
        // just finds nothing and that boss fights with no attack-charge VFX
        // (safe no-op, not a crash). DragonBoss keeps reusing FX_DragonAttack
        // (same slot as the old dragon it replaced — that VFX object is a
        // generic breath effect, not tied to the old rig, so it still applies
        // here). บาส 2026-09-03: unlike the original 3 (hand-authored VFX
        // Graphs), these 5 don't need to be built from scratch — just drag a
        // premade effect into the scene and rename the instance to the string
        // below (PlayAttackChargeVfx clones the Transform wholesale, doesn't
        // care whether it's a VisualEffect or a plain ParticleSystem). NOTE:
        // GameVFX Collection is a mixed bag under URP — checked every prefab's
        // materials/shaders by hand; ones built on this pack's own custom
        // shaders (water_ramp, uvnoise, dissolve_c, SraeWaveAlphaBlended02 —
        // e.g. the original FX_UI_Waterball/FX_DarkShock picks) render pink
        // (Shader Forge-authored, Built-in-RP-only, not just a missing-material
        // fix). Ones built ONLY on Unity's own simple built-in Particles/
        // Additive or Particles/Alpha Blended shaders (fileID 200/203) render
        // fine — the 4 below were re-picked/verified to only use those:
        //   FX_AttackChargeJellyfish   <- "Magic Ball" (Cool Visual Effects -
        //     Part 1/Effects/Magic Ball/Magic Ball.vfx) — NOT a GameVFX
        //     Collection prefab, so setup differs: create an empty GameObject,
        //     add a Visual Effect component, set its Asset Template to
        //     "Magic Ball" (same setup Portal1 already uses), then name it.
        //   FX_AttackChargeCrystalGolem <- FX_Magic_Circle.prefab (glowing rune)
        //   FX_AttackChargeVoidWraith  <- FX_DarkDoor_Purple.prefab (dark rift/
        //     door opening) — swapped from FX_DarkShock.prefab, which turned
        //     out to lean heavily on this pack's broken custom shaders.
        //   FX_AttackChargeSentinel    <- FX_Lightningball.prefab (electric charge orb)
        //   FX_AttackChargeSeaSerpent  <- FX_Thunder.prefab (storm bolt)
        [SerializeField] string[] attackChargeVfxNames =
        {
            "FX_AttackChargeDemon", "FX_AttackChargeFish", "FX_DragonAttack",
            "FX_AttackChargeJellyfish", "FX_AttackChargeCrystalGolem", "FX_AttackChargeVoidWraith",
            "FX_AttackChargeSentinel", "FX_AttackChargeSeaSerpent",
        };
        // Index-matched the same way — how many clones spawn per SpawnAttack
        // cue. Demon/Fish default to 8, scattered across the WHOLE wall
        // display rather than clustered on the boss's own silhouette — that's
        // the "เต็มจิกำแพง" look บาสasked for. Tune per-boss in the Inspector
        // without touching code. DragonBoss keeps the old dragon's 1 (single
        // glow centered on the boss, see PlayAttackChargeVfx); the other 5 new
        // bosses default to 1 too until each one's VFX graph exists and บาส
        // has a feel for whether it reads better scattered instead.
        [SerializeField] int[] attackChargeVfxBurstCounts = { 8, 8, 1, 1, 1, 1, 1, 1 };
        // Extra multiplier on top of BossWorldBounds-driven sizing (see
        // PlayAttackChargeVfx), for tuning overall size without touching each
        // graph itself.
        [SerializeField] float attackChargeVfxScale = 1f;
        [SerializeField] bool poolAttackChargeVfx = true;
        [SerializeField] int maxPooledAttackChargeVfxPerTemplate = 12;
        [SerializeField] bool useLightweightVoidAttackVfx = true;
        [SerializeField] int lightweightVoidAttackVfxParticles = 48;
        [SerializeField] float lightweightVoidAttackVfxScale = 1.35f;

        [Header("Boss Glow")]
        // Applied via MaterialPropertyBlock (not a material asset edit) so it
        // works on whichever boss model gets picked (dragon/demon/fish) without
        // touching their ~8 sub-materials each, and the shared Fortal/VertexColorLit
        // shader's emission defaults to 0 everywhere else it's used (glass tile,
        // farm animals, coins...) - only the boss actually glows. Bloom is already
        // active in this scene's Volume profile (SampleSceneProfile, threshold 0.9),
        // so a decently bright emission here is what actually produces the glow.
        // Index-matched to bossModelNamePatterns, same parallel-array idiom as
        // bossModelWeights/attackChargeVfxNames above. Falls back to element 0
        // for the manual bossModel-override testing path, which has no pattern
        // index of its own. Each new boss's glow is the exact accent hex it was
        // designed with in Fortal3DS/palette.js (JELLYFISH_EYE/CRYSTAL_SHARD/
        // WRAITH_GLOW/MECHA_VISOR/SERPENT_FIN/DRAGON2_SCALES[0]) — picking the
        // in-game rim light from the same "art bible" the model itself uses,
        // instead of an unrelated color chosen separately here.
        [SerializeField] Color[] bossGlowColors =
        {
            BossThemePalette.Accent("Demon Boss"), BossThemePalette.Accent("Giant Fish"),
            BossThemePalette.Accent("DragonBoss"), BossThemePalette.Accent("JeelyFishBoss"),
            BossThemePalette.Accent("CrystalGolem"), BossThemePalette.Accent("VoidWraithBoss"),
            BossThemePalette.Accent("SentinelBoss"), BossThemePalette.Accent("SeaSerpentBoss"),
            BossThemePalette.Accent("HydraBoss"),
        };
        [SerializeField] float bossGlowIntensity = .28f;
        // Fresnel falloff - only faces near the silhouette edge glow, not the
        // whole body (บาส 2026-09-02: "edge only, not the whole thing"). Lower
        // = glow creeps further in from the edge; higher = hugs the outline tighter.
        [SerializeField, Range(0.5f, 8f)] float bossGlowRimPower = 5f;

        [Header("Boss Theme Background")]
        // บาส 2026-09-03: "เพิ่ม BG พื้นกับกำแพงมาใหม่อีกสองชุด...เปลี่ยนตามบอส" — two
        // World Space RawImages (one per display, floor + wall) that swap
        // texture to match whichever boss is currently active. บาสbuilt these
        // himself as RawImage (not Image/Sprite — matches the old result
        // screen's RawImage convention), named "Panel == BG
        // Floor" under Canvas == Gameplay (matching that canvas's own
        // Panel-prefix sibling convention) and "Image == BG Wall" under
        // Canvas == Cutscene — auto-found by those exact names below. Both
        // canvases are World Space (not Screen Space Overlay), so these
        // composite by real camera depth like any other 3D object; บาสalready
        // placed them at the right Z to read as "behind" the boss/VFX.
        [SerializeField] RawImage bossFloorBackgroundImage;
        [SerializeField] RawImage bossWallBackgroundImage;
        // Resources.Load paths (not direct Texture references) so no Inspector
        // drag-and-drop is needed per boss — same "code owns the wiring"
        // idiom as bossModelNamePatterns/attackChargeVfxNames. Every sprite
        // lives under Assets/Resources/BossBackgrounds/ for that reason.
        // Index-matched to bossModelNamePatterns/bossGlowColors (same 8
        // bosses, same order). บาสonly named 6 of the 8 bosses' groups
        // ("seaserpent/jellyfish = น้ำ, crystalgolem/dragon = ฟ้า, void/demon
        // = นรก") — Giant Fish went into น้ำ (it's a fish) and Sentinel into
        // ฟ้า (closest fit of the three, a mechanical sky guardian) as a
        // judgment call; flag it and this is a one-line swap either way.
        [SerializeField]
        string[] bossFloorBackgroundNames =
        {
            // 2026-11-26: the old Hell / Water / Grass pictures were deleted; these are today's element sets. The scene's
            // own copy of this list still holds the old names, which LoadBackgroundTexture maps to these.
            "Art/BG/Fire/BGFloorFire",   // Demon
            "Art/BG/Water/BGFloorWater", // Giant Fish
            "Art/BG/Fire/BGFloorFire",   // DragonBoss
            "Art/BG/Water/BGFloorWater", // Jellyfish
            "Art/BG/Land/BGFloorLand",   // Crystal Golem
            "Art/BG/Fire/BGFloorFire",   // Void Wraith
            "Art/BG/Land/BGFloorLand",   // Sentinel
            "Art/BG/Water/BGFloorWater", // Sea Serpent
        };
        [SerializeField]
        string[] bossWallBackgroundNames =
        {
            "Art/BG/Fire/BGFireWall",
            "Art/BG/Water/BGWaterScene",
            "Art/BG/Fire/BGFireWall",
            "Art/BG/Water/BGWaterScene",
            "Art/BG/Land/BGEarthScene",
            "Art/BG/Fire/BGFireWall",
            "Art/BG/Land/BGEarthScene",
            "Art/BG/Water/BGWaterScene",
        };
        [SerializeField] bool useBossBackgroundMovies = true;

        [Header("Boss Slayer Reference")]
        [SerializeField] float arenaWidthMeters = 11f;
        [SerializeField] float arenaDepthMeters = 5f;
        [SerializeField] int bossMaxHp = 18000; // บาส 2026-10-06 (Balance Note C): 9000 gave 21-45 s fights; target 60-90 s at 4 players
        [SerializeField] int playerMaxHp = 100;
        [SerializeField] int bossHitDamage = 18;
        [SerializeField] float downTimeSeconds = 5f;
        [SerializeField] float telegraphSeconds = 2f;
        [SerializeField] float phaseSpeedPercent = 10f;
        [SerializeField, Range(0f, 100f)] float phase2AtPercent = 70f;
        [SerializeField, Range(0f, 100f)] float phase3AtPercent = 35f;
        [SerializeField] float attackGapSeconds = 4f;
        [SerializeField, Min(0f)] float finalPhaseAttackGapSeconds = 4.4f;
        [SerializeField, Min(0f)] float finalPhaseRecoverySeconds = 2f;
        [SerializeField] float spotLifeSeconds = 10f;
        [SerializeField] float spotHoldSeconds = .75f;
        [SerializeField] int openingSpots = 4;
        [SerializeField] float spotMinGapMeters = 1.6f;
        // 2026-11-26 "Item ที่เก็บต้องไม่ชิดขอบแมพจนเกินไป และไม่เกินตรงที่ player ยืนอยู่": the space kept clear between a
        // whole gem (with its load bar) and the edge of where players can stand (RandomGemPosition).
        [SerializeField, Min(0f)] float spotEdgeMarginMeters = .35f;
        // Multiplies the old flat-circle diameters (110 / 96) used below — the
        // circle itself is invisible now (see AttachGemModel), but its size
        // still drives the square progress frame's size (CreateFrameOverlay
        // stretches the frame to fill it exactly), so this is really "how much
        // bigger than the 3D gem should the frame read." The gem's own size is
        // controlled separately, by each gem template's own Transform Scale.
        [SerializeField] float gemFrameSizeMultiplier = 1.5f;
        // บาส 2026-09-03: "เพิ่มเอฟเฟค Glow ให้เจมที่เก็บ" — same MaterialPropertyBlock
        // + Fortal/VertexColorLit emission technique ApplyBossGlow already
        // uses, applied to the gem's own 3D model (Item/Attack|Heal|Stun|
        // Ultimate) via ApplyGemGlow below. Always at LEAST
        // gemGlowIntensity * gemGlowIdleFraction (a gem should read as
        // glowing even before anyone stands on it, not just once charging
        // starts), ramping up to the full gemGlowIntensity as Load rises
        // toward spotHoldSeconds. gemGlowRimPower deliberately lower than
        // bossGlowIntensity's own rim power — a gem's silhouette is small
        // enough that a tight edge-only rim (right for a boss-sized model)
        // barely reads at all; a lower value spreads the glow further in
        // from the edge so it's actually visible at gem scale.
        [SerializeField] float gemGlowIntensity = 4.5f;
        [SerializeField, Range(0f, 1f)] float gemGlowIdleFraction = 0.55f;
        [SerializeField, Range(0.5f, 8f)] float gemGlowRimPower = 0.75f;
        [SerializeField, Range(0f, 1f)] float gemEdgeGlowAlpha = 0.9f;
        [SerializeField] float gemGlowPulseSpeed = 2.8f;
        // Each half of the cross-display flight lasts at least 0.42 seconds.
        [SerializeField] float gemCollectLaunchSeconds = 0.28f;
        [SerializeField] float gemCollectImpactScale = 0.4f;
        readonly HashSet<GameObject> activeGemFlights = new();
        int gemFlightGeneration;
        [SerializeField] int stunNeed = 80;
        [SerializeField] float stunSeconds = 6f;
        [SerializeField] float stunBonusPercent = 50f;
        [SerializeField] int ultimateNeed = 8;
        [SerializeField] int ultimateOpenDamage = 800;
        [SerializeField] float ultimateSeconds = 10f;
        [SerializeField] int ultimateBurstSpots = 18;
        [SerializeField] int ultimateTouchDamage = 220;
        [SerializeField] float playerMoveSmoothTime = 0.05f;
        [SerializeField] float playerMoveMaxSpeed = 900f;

        readonly List<PlayerState> players = new();
        readonly List<SpotState> spots = new();
        readonly List<AttackState> attacks = new();
        readonly List<PlayerState> playersInsideScratch = new();
        readonly List<Transform> bossModelCandidates = new();
        // 3D gem models for pickups (see SpotDef.GemModelName), keyed by their
        // exact GameObject name under "Item" in the scene — a plain
        // Dictionary, not a candidate-list like the bosses, since every gem
        // shows every fight (no random pick needed, SpawnSpot already rolls
        // which pickup TYPE spawns).
        readonly Dictionary<string, Transform> gemModelsByName = new();
        // Index-matched to bossModelCandidates (candidate[i] <-> weight[i]),
        // not to bossModelNamePatterns — built once in DiscoverBossModelCandidates
        // so PickWeightedBossCandidate doesn't need to re-derive a candidate's
        // weight from its name every fight.
        readonly List<float> bossModelCandidateWeights = new();
        // Each candidate keeps its own rest pose captured once at discovery time
        // (position/scale/rotation set by hand per-model in the Editor, e.g. the
        // 30x scale + axis-fix rotation the Blender/FBX round-trip needs) —
        // UpdateBossModel's flight sway is relative to whichever one is
        // currently active, not a single shared baseline.
        readonly Dictionary<Transform, BossBaseTransform> bossBaseTransforms = new();
        // Same idiom again - per-candidate glow color, index-matched off
        // bossGlowColors[i] at discovery time so ApplyBossGlow doesn't need to
        // re-derive which pattern a candidate matched every time it's shown.
        readonly Dictionary<Transform, Color> bossGlowColorByModel = new();
        // Same idiom again — per-candidate background texture pair, index-matched
        // off bossFloorBackgroundNames/bossWallBackgroundNames at discovery time.
        readonly Dictionary<Transform, Texture2D> bossFloorBackgroundByModel = new();
        readonly Dictionary<Transform, Texture2D> bossWallBackgroundByModel = new();
        // Resources.Load is a disk read the first time any given name is
        // requested — cached here (keyed by resource path, so the water texture
        // shared by 3 bosses only loads once) rather than per-boss-change.
        readonly Dictionary<string, Texture2D> backgroundTextureCache = new();
        VideoBackgroundRuntime floorBackgroundVideo;
        VideoBackgroundRuntime wallBackgroundVideo;
        // Built at discovery time (see DiscoverBossModelCandidates) from
        // attackChargeVfxNames/attackChargeVfxBurstCounts, keyed by boss so
        // PlayAttackChargeVfx can look up "whichever one is active right now"'s
        // template + burst count directly.
        readonly Dictionary<Transform, AttackChargeVfx> attackChargeVfxByBoss = new();
        readonly Dictionary<Transform, Queue<Transform>> attackChargeVfxPool = new();
        readonly Dictionary<Transform, Transform> attackChargeVfxTemplateByClone = new();
        readonly List<Transform> activeAttackChargeVfx = new();
        BossChargeVfx elementalChargeVfx;
        BossModelElementVfx modelElementVfx;
        BossDeathVfx deathVfx;
        BossFightAudio sfx;
        readonly List<BossFantasyExplosion> fantasyExplosions = new();
        int explosionCursor;
        int AudioTheme => bossModel != null ? BossAttackVfx.ThemeFor(bossModel.name) : 0;
        // บาส 2026-10-06 (Balance Note A4): Hydra (theme 8) now has its own synthesized clips (charge_8/release_8 +
        // HydraBoss music/roar, source in D:\WORK\Fortal\Hydra Audio Preview). If those clips are ever missing, it borrows
        // the Sea Serpent set (5: water, long necks) instead of going silent. Sound only - VFX keep AudioTheme.
        const int HydraTheme = 8, HydraFallbackSoundTheme = 5;
        int SoundTheme { get { int theme = AudioTheme; return theme == HydraTheme && sfx != null && !sfx.HasTheme(HydraTheme) ? HydraFallbackSoundTheme : theme; } }
        float SfxPan(float x) => Mathf.InverseLerp(0, arenaWidthMeters, x) * 0.6f - 0.3f;
        readonly SpotDef[] spotDefs =
        {
            new("โจมตี", new Color(1f, 0.30f, 0.30f, 0.9f), 300, 0, 0, 0, "Attack"),
            new("ดูดเลือด", new Color(0.31f, 0.88f, 0.54f, 0.9f), 120, 25, 0, 0, "Heal"),
            new("ทุบสตัน", new Color(1f, 0.82f, 0.24f, 0.9f), 150, 0, 20, 0, "Stun"), // บาส 2026-10-06 (B3): 22 -> 20, 4 gems = exactly stunNeed 80
            new("ชาร์จอัลติ", new Color(0.35f, 0.65f, 1f, 0.9f), 100, 0, 0, 1, "Ultimate"),
        };
        // How many of each gem (index-matched to spotDefs: red, green, yellow, blue) one bag of 20 holds (Balance Note v3 §4.1).
        [SerializeField] int[] spotBag = { 8, 3, 4, 5 };
        readonly List<int> spotBagQueue = new();
        // Balance Note v3 §4.2: a gem every 3.6 / players seconds (0.6-1.8) and players + 2 on the floor (4-8);
        // 4 players keep the old 0.9 s and 6. (They replace the old spotGapSeconds and maxSpots.)
        float SpotGapSeconds => Mathf.Clamp(3.6f / Mathf.Max(1, playerCount), .6f, 1.8f);
        int MaxSpots => Mathf.Clamp(playerCount + 2, 4, 8);

        int bossHp;
        float bossX;
        int bossDir = 1;
        float attackTimer;
        float spotTimer;
        float stunTimer;
        float stunGauge;
        int ultimateCharge;
        float ultimateTimer;
        bool ultimateActive;
        float bossImageBaseY;
        float bossModelCurrentFacing = 1f;
        bool gameStarted;
        bool resultShown;
        bool sessionActive;
        int sessionBossTargetCount = 3;
        int sessionBossIndex;
        readonly List<Transform> sessionBossesPlayed = new();
        Image gameplaySessionTransitionOverlay;
        Image cutsceneSessionTransitionOverlay;
        Tween sessionTransitionTween;
        Material lightweightVoidAttackVfxMaterial;
        readonly Queue<ParticleSystem> lightweightVoidAttackVfxPool = new();
        readonly List<ParticleSystem> activeLightweightVoidAttackVfx = new();
        Renderer[] activeBossRenderers = new Renderer[0];
        float hudRefreshTimer;
        static Sprite circleSprite;
        static Sprite ringSprite;
        static Sprite squareFrameSprite;
        const string InnerCircleName = "Cursor Inner Circle";
        // บาส 2026-10-06 (Balance Note A1): players watch the floor to read attacks, so HP also lives on the floor -
        // an arc around each cursor that runs down with HP (green -> yellow -> red). The wall cards stay for spectators.
        const string FloorHpRingName = "Floor HP Ring", FloorHpTrackName = "Floor HP Track";
        const float FloorHpRingScale = 1.5f;
        // The marker's height in the cursor's own units: unpickedPlayerMeters on the real floor, carried from the
        // player select's floor panel (playArea) into the cursor's parent, which may be scaled differently.
        float PlayerMarkerSize(RectTransform cursor)
        {
            float floorMeters = BossFloorPlayers.ActiveFloorWidthMeters;
            var floor = playArea != null ? playArea : cursor.parent as RectTransform;
            float size = unpickedPlayerMeters * (floor != null ? floor.rect.width : FloorDisplaySettings.Width) / floorMeters;
            if (floor != null && cursor.parent != null && cursor.parent.lossyScale.x > 0f)
                size *= floor.lossyScale.x / cursor.parent.lossyScale.x;
            return size;
        }

        [Header("Player Cursor")]
        [Tooltip("บาส 2026-08-28: player cursor was a plain square (the 'Player N' Image with no sprite assigned defaults to a solid rect) — now a hollow ring with a solid circle centered inside it. This is the outer ring's diameter; the original square was 100x100.")]
        [SerializeField] float playerCursorCircleDiameter = 90f;
        [Tooltip("Diameter of the solid circle sitting in the ring's center. Smaller than the ring diameter above so it reads as \"inside\" it, not touching/overflowing the ring.")]
        [SerializeField] float playerCursorInnerCircleDiameter = 36f;

        RectTransform InputArea => characterPanel != null ? characterPanel : playArea;
        Rect FloorRect => InputArea != null ? InputArea.rect : FloorDisplaySettings.LayoutRect;
        int SelectedSessionBossCount => NormalizeSessionBossCount((int)sessionPackage);
        public bool IsSessionActive => sessionActive;
        public int CurrentSessionBossIndex => sessionBossIndex;
        public int CurrentSessionBossCount => sessionBossTargetCount;

        void Start()
        {
            inspectorPlayerCount = Mathf.Clamp(playerCount, 1, 6);
            ResolveSceneReferences();
            sfx = GetComponent<BossFightAudio>() ?? gameObject.AddComponent<BossFightAudio>();
            sfx.Initialize();
            EnsureCombatJuice();
            sfx.WireButtons();
            BindInput();
            // 2026-11-25: the floor result shows the results only - no SKIP / Restart / Exit (ResultButtonsOff).
            // (Was บาส 2026-10-02: hold 10 s, then SKIP -> Restart / Exit, ResultChoiceFlow.)
            ResultButtonsOff.Install(gameplayResultPanel);
            // With no Restart on the floor, the staff start the next group from the web launcher's NEW ROUND button
            // (shown only while the final result is up), which does what the floor's Restart did.
            Fortal.Arena.FortalArenaButtons.Add("New Round", () => "NEW ROUND", RestartToMenu,
                () => gameplayResultPanel != null && gameplayResultPanel.gameObject.activeInHierarchy, true);
            WireResultExit();
            // บาส 2026-10-02: freeze the game when a player's tag drops out, resume when they are back - PlayerDropoutGuard.
            PlayerDropoutGuard.Install(this, cutsceneArea, playArea, FindFirstObjectByType<TMPro.TMP_Text>(FindObjectsInactive.Include)?.font, () => gameStarted && !resultShown, () => playerCount,
                slot => FloorPlayers() != null && floorPlayers.TryGetSlotPlayer(slot, out var player) && floorPlayers.IsOnline(player),
                () => useUwbInput && FloorPlayers() != null && !floorPlayers.IsSimulation);
            ShowMenu();
        }

        void Update()
        {
            TickPreload();   // 2026-11-19: art and boss models ahead of time (.Preload.cs)
            UpdateMenuIntro();   // menu video while the lobby waits / players pick their pads (.MenuIntro.cs); before the early outs so it never stays up
            if (Time.timeScale <= 0f) return;
            if (InputArea == null) return;
            TickArenaPlayers();   // launcher seats and customer names (.Arena.cs)
            TickSkipToBoss();   // M: straight to the final boss, N: straight to the next perk select (.SkipToBoss.cs)
            if (IsRushActive) TickRush(Time.deltaTime); // บาส 2026-10-20: boss rush perk breaks + perk effects (Rush.cs)
            if (pickingPlayers) { UpdatePlayerSelect(Time.deltaTime); return; }   // player pads before the boss select (.PlayerSelect.cs)
            if (selectingBoss) { UpdateBossAutoConfirm(); return; }
            if (awaitingPlayers) { UpdatePlayerReadiness(); return; }
            if (!gameStarted)
            {
                if (!rushBreakOpen) FollowPlayersOutsideFight();   // the perk break moves the cursors itself
                return;
            }
            if (resultShown)
            {
                FollowPlayersOutsideFight();
                UpdateHudThrottled(Time.deltaTime);
                return;
            }

            float dt = Time.deltaTime;
            UpdatePlayers(dt);
            int recovered = passives.Advance(dt, bossHp, bossMaxHp, stunTimer > 0f || ultimateActive);
            bossHp += recovered;
            if (recovered > 0) passiveFeedback?.Notify("HP RECOVERING +" + recovered);
            UpdateBoss(dt);

            UpdateJellyfishTentacleSway();
            UpdateAttacks(dt);
            UpdateSpots(dt);
            UpdateUltimate(dt);
            UpdateHudThrottled(dt);
        }

        void ResolveSceneReferences()
        {
            playArea = playArea != null ? playArea : FindRect("Canvas == Gameplay");
            cutsceneArea = cutsceneArea != null ? cutsceneArea : FindRect("Canvas == Cutscene");
            cutsceneMenuPanel = cutsceneMenuPanel != null ? cutsceneMenuPanel : FindDirectRectIn(cutsceneArea, "Panel == Menu") ?? FindRectIn(cutsceneArea, "Panel == Menu");
            gameplayMenuPanel = gameplayMenuPanel != null ? gameplayMenuPanel : FindDirectRectIn(playArea, "Panel == Menu") ?? FindRectIn(playArea, "Panel == Menu");
            cutsceneBossPanel = cutsceneBossPanel != null ? cutsceneBossPanel : FindDirectRectIn(cutsceneArea, "Panel == Boss") ?? FindRectIn(cutsceneArea, "Panel == Boss");
            cutsceneUiPanel = cutsceneUiPanel != null ? cutsceneUiPanel : FindDirectRectIn(cutsceneArea, "Panel == UI") ?? FindRectIn(cutsceneArea, "Panel == UI");
            playerHpPanel = playerHpPanel != null ? playerHpPanel : FindRectIn(cutsceneUiPanel, "Panel == Player HP") ?? FindRectIn(cutsceneArea, "Panel == Player HP") ?? FindRect("Panel == Player HP");
            characterPanel = characterPanel != null ? characterPanel : FindRectIn(playArea, "Panel == Character") ?? FindRect("Panel == Character");
            bossFloorBackgroundImage = bossFloorBackgroundImage != null ? bossFloorBackgroundImage : FindRawImageIn(playArea, "Panel == BG Floor") ?? FindRawImage("Panel == BG Floor");
            bossWallBackgroundImage = bossWallBackgroundImage != null ? bossWallBackgroundImage : FindRawImageIn(cutsceneArea, "Image == BG Wall") ?? FindRawImage("Image == BG Wall");
            bossImage = bossImage != null ? bossImage : FindRectIn(cutsceneArea, "Boss Image") ?? FindRect("Boss Image");
            if (bossImage != null) bossImageBaseY = bossImage.anchoredPosition.y;
            bossHpText = bossHpText != null ? bossHpText : FindTMP("Text (TMP) == Boss HP") ?? FindTMPIn(cutsceneArea, "Boss HP");
            var bossHpRoot = FindRectIn(cutsceneArea, "Boss HP") ?? FindRect("Boss HP");
            bossHpSlider = FindSliderIn(bossHpRoot);
            bossHpScrollbar = FindScrollbarIn(bossHpRoot);
            bossHpFill = CreateHpFillImage(bossHpRoot, "Runtime HP Fill", Color.red);
            var stunGaugeRoot = FindRectIn(cutsceneArea, "Stunning Gague") ?? FindRect("Stunning Gague");
            var ultimateGaugeRoot = FindRectIn(cutsceneArea, "Ultimate Gague") ?? FindRect("Ultimate Gague");
            stunGaugeFill = CreateHpFillImage(stunGaugeRoot, "Runtime Gauge Fill", new Color(1f, 0.82f, 0.16f, 1f));
            ultimateGaugeFill = CreateHpFillImage(ultimateGaugeRoot, "Runtime Gauge Fill", new Color(0.28f, 0.62f, 1f, 1f));
            stunGaugeText = stunGaugeText != null ? stunGaugeText : FindTMP("Text (TMP) == Stunning Persent") ?? FindTMPIn(cutsceneArea, "Stunning Persent");
            ultimateGaugeText = ultimateGaugeText != null ? ultimateGaugeText : FindTMP("Text (TMP) == Ultimate Persent") ?? FindTMPIn(cutsceneArea, "Ultimate Persent");
            statusText = statusText != null ? statusText : CreateText("Boss Slayer Status", cutsceneArea, new Vector2(0f, -260f), 76f);
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 24f;
            statusText.fontSizeMax = 76f;
            statusText.textWrappingMode = TextWrappingModes.NoWrap;
            statusText.raycastTarget = false;
            statusText.gameObject.SetActive(false); // Central wall status text removed; retain its font reference.
            resultPanel = resultPanel != null ? resultPanel : FindDirectRectIn(cutsceneArea, "Panel == Result") ?? FindRectIn(cutsceneArea, "Panel == Result") ?? FindRect("Panel == Result");
            gameplayResultPanel = gameplayResultPanel != null ? gameplayResultPanel : FindDirectRectIn(playArea, "Panel == Result") ?? FindRectIn(playArea, "Panel == Result");
            gameplayPlayerScorePanel = gameplayPlayerScorePanel != null ? gameplayPlayerScorePanel : FindRectIn(gameplayResultPanel, "Panel == Player Score");
            DiscoverBossModelCandidates();
            ConfigureBossDisplayCameras();
            DiscoverGemModels();
            explosionVfx = explosionVfx != null ? explosionVfx : FindTransformExact("FX_Explosion");
            if (explosionVfx != null) explosionVfx.gameObject.SetActive(false); // template only — clones opt back in when spawned
            bossEntrancePortal = bossEntrancePortal != null ? bossEntrancePortal : FindTransformExact("Portal1");
            if (bossEntrancePortal != null)
            {
                bossEntrancePortalBaseScale = bossEntrancePortal.localScale;
                bossEntrancePortal.gameObject.SetActive(false); // hidden until BossEntranceRoutine plays it
            }

            BuildPlayers();
            // The session player count is configured, not inferred from template cursors.
            WireMenuStartButtons(cutsceneMenuPanel);
            WireMenuStartButtons(gameplayMenuPanel);
            EnsureSessionTransitionOverlays();
        }

        // บาส 2026-10-02: split out of ResolveSceneReferences so ApplyPlayerCount can rebuild the players for the count the
        // lobby locked. Everything in here finds-or-reuses its objects, so running it again leaves no duplicates.
        void BuildPlayers()
        {
            playerCount = Mathf.Clamp(playerCount, 1, 6);
            players.Clear();
            for (int i = 0; i < 6; i++)
            {
                var cursor = FindDirectRectIn(characterPanel, $"Player {i + 1}");
                if (cursor == null) continue;

                var slotHp = FindRawImageIn(playerHpPanel, $"RawImage == Player {i + 1}");
                bool participates = i < playerCount;
                cursor.gameObject.SetActive(participates);
                if (slotHp != null) slotHp.gameObject.SetActive(participates);
                if (!participates) continue;
                SetupPlayerCursorVisual(cursor, i);

                var playerHpCard = FindRawImageIn(playerHpPanel, $"RawImage == Player {i + 1}");
                var hpRoot = FindDirectRectIn(playerHpCard != null ? playerHpCard.transform : null, "HP Bar")
                    ?? FindRectIn(playerHpCard != null ? playerHpCard.transform : null, "HP Bar")
                    ?? FindDirectRectIn(cursor, "HP Bar")
                    ?? FindRectIn(cursor, "HP Bar");
                var hpSlider = FindSliderIn(hpRoot);
                var hpScrollbar = FindScrollbarIn(hpRoot);
                var hpFill = CreateHpFillImage(hpRoot, "Runtime HP Fill", Color.green);
                players.Add(new PlayerState(i, cursor, hpSlider, hpScrollbar, hpFill, PlayerColor(i), playerMaxHp));
                // the art marker carries its own colour; PlayerState tints the cursor root for the old plain ring
                if (BossPlayerMarkers.For(i) != null && cursor.TryGetComponent(out Graphic markerGraphic)) markerGraphic.color = Color.white;
            }
            BuildPlayerHudCards(); // บาส 2026-09-29: wall player cards (WallHud.cs)
        }

        // บาส 2026-10-02: the lobby locked n players - resize everything that depends on the count. The result rows and the
        // replay session read players.Count when they are shown/started, so rebuilding the list covers them too.
        void ApplyPlayerCount(int n)
        {
            n = Mathf.Clamp(n, 1, 6);
            if (n == playerCount && players.Count > 0) return; // same count (e.g. no UWB: Inspector count) - nothing to redo
            playerCount = n;
            BuildPlayers();
            System.Array.Clear(readyPlayers, 0, readyPlayers.Length);
        }

        void BindInput()
        {
            if (useUwbInput) FloorPlayers();
        }

        // บาส 2026-09-03: gameStarted stays FALSE and the boss stays HIDDEN
        // through the portal intro (see BossEntranceRoutine below) — Update()
        // early-returns entirely while gameStarted is false, so nothing (boss
        // AI, attacks, spot spawns, HUD) ticks during the portal/reveal beat.
        // Everything that used to happen instantly here (picking the boss,
        // resetting HP/timers/players, opening spots) still happens up front
        // — only the actual reveal (SetBossVisible(true) + gameStarted = true)
        // moved into the coroutine, gated behind the portal's timing.
        void ResetFight()
        {
            ResetFight(true);
        }

        void ResetFight(bool resetPlayerDamage)
        {
            ResetDragonPhase();ResetDemonPhase();ResetGolemPhase();ResetAbyssalKingPhase();
            sfx?.StopCombat();
            ClearFantasyExplosions();
            ClearGemFlights();
            gameStarted = false;
            if (passiveFeedback != null) passiveFeedback.Hide();
            feedbackHeads = -1;
            ClearAttacks();
            ClearSpots();
            resultShown = false;
            SetMenuVisible(false);
            SetGameplayHudVisible(true);
            if (resultPanel != null) resultPanel.gameObject.SetActive(false);
            HideRaidResult();
            if (gameplayResultPanel != null) gameplayResultPanel.gameObject.SetActive(false);
            bossHp = bossMaxHp;
            damageCreditRemainder = 0;
            bossX = arenaWidthMeters * 0.5f;
            bossDir = 1;
            // Hide whichever boss was active before picking the next one — with
            // more than one candidate, ResetFight is what rolls the dice per fight.
            SetBossVisible(false);
            if (bossModelCandidates.Count > 0) bossModel = ConsumeNextBossCandidate();
            EnsureBossSkin();
            if (bossModel != null) bossModel.GetComponent<BossHydra>()?.ResetHeads();
            passives.Reset(bossModel != null ? bossModel.name : string.Empty, enableBossPassives);
            CacheActiveBossRenderers();
            jellyfishTentaclePivots = null;
            UpdateBossThemeBackground();
            BeginBossUiTheme();
            sfx?.Score?.Boss(SoundTheme, BossPassiveState.Identify(bossModel != null ? bossModel.name : null) == BossPassiveState.Kind.AbyssalKing);
            UpdateBossImage(); // positions the (still-hidden) boss before it's revealed
            attackTimer = attackGapSeconds;
            bossSignatureCastCount = 0;
            ResetBossVariety();
            lowestBossSignatureHp = 1f;
            spotTimer = 1f;
            stunTimer = 0f;
            stunGauge = 0f;
            ultimateCharge = 0;
            ultimateActive = false;
            ultimateTimer = 0f;

            foreach (var player in players)
            {
                player.Reset(playerMaxHp, ZoneCenterLocal(player.Index), resetPlayerDamage);
                UpdatePlayerHp(player);
            }
            // บาส 2026-11-18: the opening gems wait for the boss to land (BossEntranceRoutine): spawned here they sat on the
            // floor through the whole portal entrance, where they could not be collected and the game looked frozen
            UpdateHud();

            ReplayRoundStart();
            RushOnFightStarted();
            StartCoroutine(BossEntranceRoutine());
        }

        // 3 beats: (1) portal scales 0 -> rest size and plays, held open for a
        // moment; (2) boss flies OUT of the portal — starts at the portal's own
        // position/zero scale, DOLocalMove + DOScale carry it to its real rest
        // position/scale together, so it reads as emerging and growing at once,
        // not just popping in place; (3) once the boss has arrived, gameStarted
        // flips true (see ResetFight's own comment for why that's gated here)
        // and — NOT blocking that — the portal shrinks back to 0 and hides.
        IEnumerator BossEntranceRoutine()
        {
            ReplayLog.Event("boss_entrance", 0, 4, null, ReplayLog.Data().Add("boss", ReplayBoss()).Add("bossIndexInSession", sessionBossIndex));
            sfx?.Play("portal_open", 0.55f, 0, 0.1f, 3);
            if (bossEntrancePortal != null && showBossPortal)
            {
                bossEntrancePortal.localScale = Vector3.zero;
                bossEntrancePortal.gameObject.SetActive(true);
                var portalVfx = bossEntrancePortal.GetComponent<VisualEffect>();
                if (portalVfx != null) portalVfx.Play();
                entrancePortalTween = bossEntrancePortal.DOScale(bossEntrancePortalBaseScale, bossEntrancePortalOpenSeconds).SetEase(Ease.OutBack);
            }
            yield return new WaitForSeconds(bossEntrancePortalOpenSeconds + bossEntrancePortalLeadSeconds);

            SetBossVisible(true);
            ActiveBossSkin?.Play("intro");
            sfx?.Play("boss_enter", 0.6f, 0, 0.1f, 3);
            sfx?.Score?.Roar();
            if (bossModel != null && bossBaseTransforms.TryGetValue(bossModel, out var baseTransform))
            {
                // Portal's world position converted into bossModel's own local
                // space (the two don't share a parent) — this is where the
                // "flight" starts from.
                bossModel.localPosition = bossEntrancePortal != null
                    ? (bossModel.parent != null ? bossModel.parent.InverseTransformPoint(bossEntrancePortal.position) : bossEntrancePortal.position)
                    : baseTransform.Position;
                bossModel.localScale = Vector3.zero;
                // horizontalT = 0.5 — screen-center, matching BossHorizontalRestPosition
                // (see its own comment) and ResetFight's bossX = arenaWidthMeters * 0.5f,
                // NOT baseTransform.Position's raw hand-placed X (บาส 2026-09-03: bosses
                // were landing off-center when that per-boss Editor position wasn't
                // exactly centered under the wall camera).
                var restPosition = BossHorizontalRestPosition(baseTransform, 0.5f);
                entranceMoveTween = bossModel.DOLocalMove(restPosition, bossEntranceRevealSeconds).SetEase(Ease.OutQuad);
                entranceScaleTween = bossModel.DOScale(baseTransform.Scale, bossEntranceRevealSeconds).SetEase(Ease.OutBack);
            }
            yield return new WaitForSeconds(bossEntranceRevealSeconds);

            gameStarted = true;
            if (replaySessionPending)
            {
                replaySessionPending = false;
                ReplayBeginSession(sessionBossTargetCount);
                ReplayRoundStart();   // its first call (in the entrance) came before the round was open
            }
            for (int i = 0; i < openingSpots; i++) SpawnSpot();   // now collectable (moved from ResetFight)
            spotTimer = SpotGapSeconds;
            replayFightStart = ReplayLog.SessionTime;
            sfx?.Play("battle_start", 0.5f, 0, 0.1f, 4);

            if (bossEntrancePortal != null)
            {
                var portalVfx = bossEntrancePortal.GetComponent<VisualEffect>();
                if (portalVfx != null) portalVfx.Stop();
                entrancePortalTween = bossEntrancePortal.DOScale(Vector3.zero, bossEntrancePortalCloseSeconds).SetEase(Ease.InBack);
                yield return new WaitForSeconds(bossEntrancePortalCloseSeconds);
                bossEntrancePortal.gameObject.SetActive(false);
            }
        }

        void ShowMenu()
        {
            FloorPlayers()?.ClearRoster(); // back at the menu: the next group picks its pads again
            ResetRush();
            sfx?.StopCombat();
            sfx?.Score?.Menu();
            ClearFantasyExplosions();
            ClearGemFlights();
            gameStarted = false;
            if (passiveFeedback != null) passiveFeedback.Hide();
            feedbackHeads = -1;
            resultShown = false;
            sessionActive = false;
            sessionBossIndex = 0;
            sessionBossesPlayed.Clear();
            queuedNextBoss = null;
            ClearAttacks();
            ClearSpots();
            if (resultPanel != null) resultPanel.gameObject.SetActive(false);
            HideRaidResult();
            if (gameplayResultPanel != null) gameplayResultPanel.gameObject.SetActive(false);
            if (statusText != null) statusText.text = string.Empty;
            SetBossVisible(false);
            SetMenuVisible(false);
            awaitingPlayers = false;
            readyGate.Reset();
            System.Array.Clear(readyPlayers, 0, readyPlayers.Length);
            ApplySessionDifficulty(1);
            // บาส 2026-10-02: back at the menu = a new group. Count who stands on the floor first (once per session - the
            // next bosses of the same session keep it), so the boss select / READY gate / HP cards are sized for them.
            // Without a UWB receiver the lobby calls back at once with the Inspector count, so this is the old flow.
            // While counting nothing else may be on screen or take input: drop a half-done boss pick / READY screen.
            selectingBoss = false;
            HidePlayerSelect();
            if (floorBossSelection != null) floorBossSelection.gameObject.SetActive(false);
            if (wallBossSelection != null) wallBossSelection.gameObject.SetActive(false);
            if (readyScreens != null) foreach (var screen in readyScreens) if (screen != null) screen.gameObject.SetActive(false);
            // บาส 2026-10-22: each player picks their number on a floor pad before the boss select (.PlayerSelect.cs).
            // 2026-11-18: Food Isekai's player system - connected tags join, one pad each, start when all have picked.
            ShowPlayerSelect(() => { StartRushClock(); PlayWallSlashTransition(); ShowBossSelection(); });
            SetGameplayHudVisible(false);
            SetSessionTransitionAlpha(0f);
        }

        void SetMenuVisible(bool visible)
        {
            SetPanelVisible(cutsceneMenuPanel, visible);
            SetPanelVisible(gameplayMenuPanel, visible);
        }

        // Gameplay HUD (boss HP bar, stun/ultimate gauges) — opposite
        // visibility from the menu: hidden while the menu is showing, shown
        // once a fight actually starts. Kept separate from SetMenuVisible
        // (not just "!visible" folded in there) since it's a different
        // concern — the menu panels and this HUD are siblings, not related
        // to each other structurally, just both need to be right at the
        // same two call sites (ShowMenu/ResetFight).
        void SetGameplayHudVisible(bool visible)
        {
            if (cutsceneBossPanel != null) cutsceneBossPanel.gameObject.SetActive(visible);
            if (cutsceneUiPanel != null) cutsceneUiPanel.gameObject.SetActive(visible);
            // บาส 2026-09-03: "ปิด player ออกไปด้วยในหน้าเมนู" — same sibling-of-
            // Panel-==-Menu situation as cutsceneBossPanel/cutsceneUiPanel
            // above, just on the FLOOR side this time: characterPanel (Player
            // 1..6's cursors/HP bars) kept showing through the title/menu
            // screen too. Safe to deactivate during the menu even though
            // characterPanel also backs InputArea (see its own property) —
            // nothing reads player positions while gameStarted is false,
            // Update() early-returns before ever touching them.
            if (characterPanel != null) characterPanel.gameObject.SetActive(visible);
        }

        static void SetPanelVisible(RectTransform panel, bool visible)
        {
            if (panel == null) return;
            panel.gameObject.SetActive(visible);
            if (visible) panel.SetAsLastSibling();
        }

        // บาส 2026-10-02: the floor result Exit had no onClick at all (scene or code) - pressing it did nothing.
        void WireResultExit()
        {
            if (gameplayResultPanel == null) return;
            foreach (var button in gameplayResultPanel.GetComponentsInChildren<Button>(true))
                if (button.name == "Exit" && button.onClick.GetPersistentEventCount() == 0) button.onClick.AddListener(ResultChoiceFlow.QuitGame);
        }

        void WireMenuStartButtons(RectTransform menuPanel)
        {
            if (menuPanel == null) return;

            foreach (var button in menuPanel.GetComponentsInChildren<Button>(true))
            {
                if (button == null) continue;
                int sessionCount = SessionBossCountFromButtonName(button.name);
                if (sessionCount <= 0 && button.name != "Start Button" && button.name != "Start") continue;
                button.onClick = new Button.ButtonClickedEvent();
                if (button.TryGetComponent(out ButtonPressFeedback feedback)) feedback.Rehook(); // the reset above dropped its punch
                int capturedSessionCount = sessionCount > 0 ? sessionCount : SelectedSessionBossCount;
                button.onClick.AddListener(() => StartBossSession(capturedSessionCount));
            }
        }

        public void StartQuickSession()
        {
            StartBossSession(SelectedSessionBossCount);
        }

        static int NormalizeSessionBossCount(int count) => count <= 3 ? 3 : count <= 5 ? 5 : 7;

        public void StartFiveBossSession() { StartBossSession(5); }
        public void StartSevenBossSession() { StartBossSession(7); }

        // Legacy UnityEvent entry points retained for existing scene bindings.
        public void StartOneBossSession()
        {
            StartBossSession(3);
        }

        public void StartTwoBossSession()
        {
            StartBossSession(5);
        }

        public void StartThreeBossSession()
        {
            StartBossSession(3);
        }

        public void SetBossSessionCount(int bossCount)
        {
            sessionPackage = (BossSessionPackage)NormalizeSessionBossCount(bossCount);
        }

        public void StartBossSession(int bossCount)
        {
            SetBossSessionCount(bossCount);
            RestartToMenu();
        }

        void BeginBossSession(int bossCount)
        {
            awaitingPlayers = false;
            if (readyScreens != null) foreach (var screen in readyScreens) if (screen != null) screen.gameObject.SetActive(false);
            StopAllCoroutines();
            HideNextBossCards();
            CancelBossEntrance();
            ClearAttackChargeVfx();
            ClearLightweightVoidAttackVfx();
            KillSessionTransitionTween();
            SetSessionTransitionAlpha(0f);
            sessionBossTargetCount = IsRushActive ? RushBossCap : NormalizeSessionBossCount(bossCount);
            sessionBossIndex = 0;
            sessionBossesPlayed.Clear();
            sessionActive = true;
            // 2026-11-25 (Fortal Replay Signal README: "GameStarted ... after the menus and the countdown"): the replay
            // round now opens when the first boss fight really starts (below, at gameStarted), not here before the boss
            // select, so the highlight recording has no picking or waiting in it.
            replaySessionPending = true;
            StartNextSessionBoss(true);
            sfx?.Play("session_start", 0.5f, 0, 0.2f, 4);
        }

        public void RestartToMenu()
        {
            ReplayEnd("aborted");
            StopAllCoroutines();
            HideNextBossCards();
            CancelBossEntrance();
            ClearAttackChargeVfx();
            ClearLightweightVoidAttackVfx();
            KillSessionTransitionTween();
            ShowMenu();
        }

        void CancelBossEntrance()
        {
            entrancePortalTween?.Kill(); entrancePortalTween = null;
            entranceMoveTween?.Kill(); entranceMoveTween = null;
            entranceScaleTween?.Kill(); entranceScaleTween = null;
            if (bossEntrancePortal != null)
            {
                var portalVfx = bossEntrancePortal.GetComponent<VisualEffect>();
                if (portalVfx != null) portalVfx.Stop();
                bossEntrancePortal.gameObject.SetActive(false);
                bossEntrancePortal.localScale = bossEntrancePortalBaseScale;
            }
        }

        void StartNextSessionBoss(bool resetPlayerDamage)
        {
            sessionBossIndex = Mathf.Clamp(sessionBossIndex + 1, 1, Mathf.Max(1, sessionBossTargetCount));
            ApplySessionDifficulty(sessionBossIndex);
            ResetFight(resetPlayerDamage);
        }

        static int SessionBossCountFromButtonName(string buttonName)
        {
            if (string.IsNullOrEmpty(buttonName)) return 0;
            var lower = buttonName.ToLowerInvariant();
            bool sessionButtonName = lower.Contains("session") || lower.Contains("package") || lower.Contains("boss") || lower.Contains("start") || lower.Contains("button");
            if (!sessionButtonName) return 0;
            if (lower.Contains("7")) return 7;
            if (lower.Contains("5")) return 5;
            if (lower.Contains("3")) return 3;
            if (lower.Contains("2")) return 5;
            if (lower.Contains("1")) return 3;
            return 0;
        }

        int ReadyPlayerCount => ShouldUseDirectEditorMouse(0) ? 1 : playerCount;

        void UpdatePlayerReadiness()
        {
            for (int i = 0; i < playerCount; i++) readyPlayers[i] = IsPlayerInsideReadyArea(i);
            bool start = readyGate.Step(readyPlayers, ReadyPlayerCount, Time.deltaTime);
            UpdateReadyScreens();
            if (start) StartSelectedBossAfterReady();
        }

        bool IsPlayerInsideReadyArea(int index)
        {
            // Real, unclamped positions: a player standing off the floor cannot mark ready.
            if (useUwbInput) return FloorPlayers() != null && floorPlayers.IsSlotOnFloor(index);
            return TryGetInputLocalPoint(index, out var local) && FloorRect.Contains(local);
        }
        void UpdateReadyScreens()
        {
            if (readyScreens == null) return;
            foreach (var screen in readyScreens) if (screen != null)
                screen.Display(readyPlayers, ReadyPlayerCount, SelectedSessionBossCount, readyGate.AllPresent, readyGate.Countdown);
        }

        void UpdatePlayers(float dt)
        {
            if (Time.timeScale <= 0f) return;
            foreach (var player in players)
            {
                if (player.DownTimer > 0f)
                {
                    player.DownTimer -= dt;
                    if (player.DownTimer <= 0f)
                    {
                        player.Hp = RushReviveHp();
                        sfx?.Play("revive", 0.5f, SfxPan(LocalToWorld(player.Cursor.anchoredPosition).x), 0.15f, 3);
                        player.Cursor.gameObject.SetActive(TryGetInputLocalPoint(player.Index, out _));
                        if (player.Cursor.gameObject.activeSelf) JuiceFloor(SpaceJuice.Respawn, player.Cursor.anchoredPosition, new Color(.3f, 1f, .8f), 160);
                    }
                    UpdatePlayerHp(player);
                    continue;
                }

                if (!TryGetInputLocalPoint(player.Index, out var target))
                {
                    player.Velocity = Vector2.zero;
                    player.Cursor.gameObject.SetActive(false);
                    continue;
                }
                player.Cursor.gameObject.SetActive(true);
                target.x = Mathf.Clamp(target.x, FloorRect.xMin + 40f, FloorRect.xMax - 40f);
                target.y = Mathf.Clamp(target.y, FloorRect.yMin + 40f, FloorRect.yMax - 40f);

                if (useUwbInput || ShouldUseDirectEditorMouse(player.Index))
                {
                    player.Cursor.anchoredPosition = target;
                    player.Velocity = Vector2.zero;
                    continue;
                }

                player.Cursor.anchoredPosition = Vector2.SmoothDamp(
                    player.Cursor.anchoredPosition, target, ref player.Velocity,
                    playerMoveSmoothTime, playerMoveMaxSpeed, dt);
            }
        }

        // บาส 2026-11-17: the cursors froze while the boss came out of the portal and while a beaten boss died, because
        // UpdatePlayers only runs during the fight. Outside it they still follow UWB here: position only (no HP, revive
        // or damage), only while the floor's player panel is shown (hidden at the menu), and a downed player stays hidden
        // until the fight revives them. Same per-frame cost as the fight's own update.
        void FollowPlayersOutsideFight()
        {
            if (characterPanel == null || !characterPanel.gameObject.activeInHierarchy) return;
            var floor = FloorRect;
            foreach (var player in players)
            {
                if (player.DownTimer > 0f) continue;
                bool present = TryGetInputLocalPoint(player.Index, out var target);
                if (player.Cursor.gameObject.activeSelf != present) player.Cursor.gameObject.SetActive(present);
                if (!present) continue;
                player.Velocity = Vector2.zero;
                player.Cursor.anchoredPosition = new Vector2(
                    Mathf.Clamp(target.x, floor.xMin + 40f, floor.xMax - 40f),
                    Mathf.Clamp(target.y, floor.yMin + 40f, floor.yMax - 40f));
            }
        }

        void UpdateBoss(float dt)
        {
            if (bossHp <= 0)
            {
                ShowResult();
                return;
            }

            UpdateDragonStage();ObserveGolemHealth();ObserveAbyssalKingHealth();
            ObserveBossSignatureHealth();
            SkinTickPassives();
            // Balance Note v3 §3.1: a boss with a second form transforms first; a wind-up caught by 50% winds up again after.
            if (SignatureWindupActive && AwaitingPhaseTransform()) CancelSignatureWindup(true);
            if(AdvanceDragonTransformation(dt))return;
            if(AdvanceDemonTransformation(dt))return;
            if(AdvanceGolemTransformation(dt))return;
            if(AdvanceAbyssalKingAwakening(dt))return;
            sfx?.ObservePhase(BossPhase());
            if (stunTimer > 0f)
            {
                stunTimer -= dt;
                if (stunTimer <= 0) sfx?.Play("stun_end", 0.4f, 0, 0.2f, 2);
                UpdateBossImage();
                return;
            }
            if (ultimateActive) return;
            // The big move's wind-up: the boss stands still and makes no small attacks (Balance Note v3 §3.2).
            if (SignatureWindupActive)
            {
                if (rushKingCharging) CancelSignatureWindup(false);
                else
                {
                    TickSignatureWindup(dt);
                    UpdateBossImage();
                    return;
                }
            }

            AdvanceBossLocomotion(dt);
            SkinFollowLocomotion();
            float bossMinX = arenaWidthMeters * 0.125f;
            float bossMaxX = arenaWidthMeters * 0.875f;
            if (bossX < bossMinX || bossX > bossMaxX)
            {
                bossX = Mathf.Clamp(bossX, bossMinX, bossMaxX);
                bossDir *= -1;
            }
            UpdateBossImage();

            if (rushKingCharging) return; // Balance Note A2: no new moves while the king charges the arena blast

            attackTimer -= dt * RushAttackTimeScale() / passives.AttackGapMultiplier(bossHp / (float)Mathf.Max(1, bossMaxHp));
            // Do not stack a new pattern over the current escape gaps.
            if (attacks.Count > 0 || HydraComboPending)
            {
                attackTimer = Mathf.Max(attackTimer, BossPhase() >= 3 ? finalPhaseRecoverySeconds : 1.5f);
                return;
            }
            if (attackTimer > 0f) return;

            SpawnAttack();
            attackTimer = passives.Active == BossPassiveState.Kind.Dragon ? attackGapSeconds : BossPhase() >= 3 ? Mathf.Max(attackGapSeconds, finalPhaseAttackGapSeconds) :
                attackGapSeconds * Mathf.Pow(1f - phaseSpeedPercent / 100f, BossPhase() - 1);
            if (IsAbyssalKingActive) attackTimer += abyssalKingRestSeconds; // real players need a breather between the king's chains
        }

        void UpdateBossImage()
        {
            var parent = bossImage != null ? bossImage.parent as RectTransform : cutsceneArea;
            var parentRect = parent != null ? parent.rect : cutsceneArea != null ? cutsceneArea.rect : new Rect(-960f, -540f, 1920f, 1080f);
            float halfWidth = bossImage != null ? bossImage.rect.width * Mathf.Abs(bossImage.localScale.x) * 0.5f : 0f;
            float t = Mathf.InverseLerp(0f, arenaWidthMeters, bossX);
            float x = Mathf.Lerp(parentRect.xMin + halfWidth, parentRect.xMax - halfWidth, t);
            float y = bossImageBaseY + Mathf.Sin(Time.time * 2.2f) * 18f;

            if (bossImage != null)
            {
                bossImage.anchoredPosition = new Vector2(x, y);

                var scale = bossImage.localScale;
                scale.x = Mathf.Abs(scale.x) * (bossDir >= 0 ? 1f : -1f);
                bossImage.localScale = scale;
            }

            UpdateBossModel(t);
        }

        // Populates bossModelCandidates: either just the manually-assigned
        // bossModel (explicit override — one fixed boss every fight, weight
        // irrelevant since there's nothing else to weigh against), or every
        // scene Transform matching bossModelNamePatterns (auto mode —
        // ResetFight picks a weighted-random one from this list each fight).
        // Each candidate gets its own rest pose captured and gets put on
        // BossLayer and hidden here, once, rather than repeating that
        // per-fight for whichever one gets picked.
        void OnValidate() { EnsureHydraListEntry(); }

        void EnsureHydraListEntry()
        {
            if (bossModelNamePatterns == null) bossModelNamePatterns = new string[0];
            if (bossModelWeights == null) bossModelWeights = new float[0];
            int index = System.Array.IndexOf(bossModelNamePatterns, "HydraBoss");
            bool added = index < 0;
            if (added)
            {
                index = bossModelNamePatterns.Length;
                System.Array.Resize(ref bossModelNamePatterns, index + 1);
                bossModelNamePatterns[index] = "HydraBoss";
            }
            int oldLength = bossModelWeights.Length;
            if (oldLength <= index)
            {
                System.Array.Resize(ref bossModelWeights, index + 1);
                for (int i = oldLength; i < bossModelWeights.Length; i++) bossModelWeights[i] = 1f;
            }
            if (added || oldLength <= index) bossModelWeights[index] = Mathf.Max(0f, hydraSelectionWeight);
        }
        void DiscoverBossModelCandidates()
        {
            EnsureHydraListEntry();
            bossModelCandidates.Clear();
            bossModelCandidateWeights.Clear();
            attackChargeVfxByBoss.Clear();
            bossGlowColorByModel.Clear();
            bossFloorBackgroundByModel.Clear();
            bossWallBackgroundByModel.Clear();

            // บาส 2026-09-03: guards against exactly the mistake that already
            // happened once — dragging an FX_ object (e.g.
            // FX_AttackChargeCrystalGolem) into the Boss Model field instead
            // of an actual boss, which made the manual-override branch below
            // happily show the EFFECT as "the boss" with nothing ever
            // checking it was really one. Any object carrying a VisualEffect
            // or ParticleSystem is almost certainly an FX template, never a
            // boss model — reject it here and fall through to the normal
            // auto-discover path instead, so a stray Inspector drag self-
            // corrects at Play instead of needing to be caught by hand every
            // session.
            if (bossModel != null && (bossModel.GetComponent<VisualEffect>() != null || bossModel.GetComponent<ParticleSystem>() != null))
            {
                Debug.LogWarning(
                    $"Boss Slayer Game Manager: Boss Model was set to '{bossModel.name}', which looks like an FX object (has a VisualEffect/ParticleSystem), not a boss — ignoring it and auto-discovering instead. Clear the Boss Model field in the Inspector to stop seeing this warning.",
                    this);
                bossModel = null;
            }

            if (bossModel != null)
            {
                // No pattern index to key attackChargeVfxNames/glow color off in
                // the manual-override case (there's only ever this one candidate,
                // matched by explicit assignment rather than name pattern) — the
                // fixed boss simply plays no attack-charge VFX, and glow falls
                // back to bossGlowColors[0] in ApplyBossGlow. Acceptable: this
                // path only exists for pinning one boss during testing.
                bossModelCandidates.Add(bossModel);
                bossModelCandidateWeights.Add(1f);
            }
            else
            {
                foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (transform == null || !transform.gameObject.scene.IsValid()) continue;
                    if (transform.GetComponent<BossHydra>() != null || transform.name == "HydraBoss") continue;
                    // บาส 2026-09-03: found the real bug behind the earlier
                    // "effect shows up as the boss" report — "CrystalGolem" as
                    // a bossModelNamePatterns entry is ALSO a plain substring
                    // of "FX_AttackChargeCrystalGolem" (Contains() can't tell
                    // which one it's matching inside), so this loop was
                    // discovering the attack VFX as a SECOND "CrystalGolem"
                    // candidate alongside the real boss, and sometimes picking
                    // the VFX to display as the boss instead. Same
                    // VisualEffect/ParticleSystem check the manual bossModel
                    // override above already uses, generalized here to every
                    // candidate this loop finds — a real boss model never
                    // carries either component, only FX objects do.
                    if (transform.GetComponent<VisualEffect>() != null || transform.GetComponent<ParticleSystem>() != null) continue;
                    for (int i = 0; i < bossModelNamePatterns.Length; i++)
                    {
                        if (!transform.name.Contains(bossModelNamePatterns[i])) continue;
                        bossModelCandidates.Add(transform);
                        bossModelCandidateWeights.Add(i < bossModelWeights.Length ? Mathf.Max(0f, bossModelWeights[i]) : 1f);
                        if (i < bossGlowColors.Length) bossGlowColorByModel[transform] = bossGlowColors[i];
                        var elementArt = ElementArt(transform.name);
                        if (elementArt.HasValue || i < bossFloorBackgroundNames.Length)
                        {
                            var floorTexture = LoadBackgroundTexture(elementArt.HasValue ? elementArt.Value.floor : bossFloorBackgroundNames[i]);
                            if (floorTexture != null) bossFloorBackgroundByModel[transform] = floorTexture;
                        }
                        if (elementArt.HasValue || i < bossWallBackgroundNames.Length)
                        {
                            var wallTexture = LoadBackgroundTexture(elementArt.HasValue ? elementArt.Value.wall : bossWallBackgroundNames[i]);
                            if (wallTexture != null) bossWallBackgroundByModel[transform] = wallTexture;
                        }
                        if (i < attackChargeVfxNames.Length)
                        {
                            var vfx = FindTransformExact(attackChargeVfxNames[i]);
                            if (vfx != null)
                            {
                                int burstCount = i < attackChargeVfxBurstCounts.Length ? Mathf.Max(1, attackChargeVfxBurstCounts[i]) : 1;
                                attackChargeVfxByBoss[transform] = new AttackChargeVfx(vfx, burstCount);
                                vfx.gameObject.SetActive(false); // template only — clones opt back in when spawned
                            }
                        }
                        break;
                    }
                }
            }

            if (bossModel == null && includeHydraBoss)
            {
                if (hydraModel == null)
                    hydraModel = BossHydra.Create(bossModelCandidates.Count > 0 ? bossModelCandidates[0] : null).transform;
                bossModelCandidates.Add(hydraModel);
                bossModelCandidateWeights.Add(Mathf.Max(0f, bossModelWeights[System.Array.IndexOf(bossModelNamePatterns, "HydraBoss")]));
                bossGlowColorByModel[hydraModel] = BossThemePalette.Accent("HydraBoss");
                var floor = LoadBackgroundTexture(LandFloorArt);
                var wall = LoadBackgroundTexture(LandWallArt);
                if (floor != null) bossFloorBackgroundByModel[hydraModel] = floor;
                if (wall != null) bossWallBackgroundByModel[hydraModel] = wall;
            }

            if (bossModel == null) EnsureAbyssalKing(bossModelCandidates.Count > 0 ? bossModelCandidates[0] : null);

            foreach (var candidate in bossModelCandidates)
            {
                bossBaseTransforms[candidate] = new BossBaseTransform(candidate.localPosition, candidate.localScale, candidate.localRotation);
                Visual3DFollower.SetLayerRecursive(candidate.gameObject, Visual3DFollower.BossLayer);
                candidate.gameObject.SetActive(false);
            }
        }

        // Finds each pickup type's 3D gem template by exact name (SpotDef's own
        // GemModelName field is the single source of truth for which names
        // matter, so this list can't drift out of sync with spotDefs) and hides
        // it — same "find once, deactivate the template, clone it per use"
        // pattern as everything else (bosses, explosionVfx). SpawnSpot/
        // SpawnUltimateSpots below clone a fresh instance per spawned pickup via
        // Visual3DFollower.Attach; the templates themselves never render directly.
        void DiscoverGemModels()
        {
            gemModelsByName.Clear();
            foreach (var def in spotDefs)
            {
                if (string.IsNullOrEmpty(def.GemModelName) || gemModelsByName.ContainsKey(def.GemModelName)) continue;
                var model = FindTransformExact(def.GemModelName);
                if (model == null) continue;
                gemModelsByName[def.GemModelName] = model;
                model.gameObject.SetActive(false);
            }
        }

        // Weighted roll against bossModelCandidateWeights — same idea as a loot
        // table, not a guaranteed fraction: sum every candidate's weight, roll a
        // point in [0, total), walk the cumulative sum until it passes the roll.
        // Falls back to a uniform pick if every weight is 0 (misconfigured
        // rather than "nothing should ever be picked").
        Transform PickWeightedBossCandidate()
        {
            float totalWeight = 0f;
            for (int i = 0; i < bossModelCandidateWeights.Count; i++) totalWeight += bossModelCandidateWeights[i];

            if (totalWeight <= 0f) return bossModelCandidates[Random.Range(0, bossModelCandidates.Count)];

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < bossModelCandidates.Count; i++)
            {
                cumulative += bossModelCandidateWeights[i];
                if (roll <= cumulative) return bossModelCandidates[i];
            }
            return bossModelCandidates[bossModelCandidates.Count - 1]; // float rounding safety net
        }

        Transform ReserveNextBossCandidate()
        {
            if (queuedNextBoss == null) queuedNextBoss = PickSessionBossCandidate();
            return queuedNextBoss;
        }
        Transform ConsumeNextBossCandidate()
        {
            var selected = queuedNextBoss != null ? queuedNextBoss : PickSessionBossCandidate();
            queuedNextBoss = null;
            if (selected != null && !sessionBossesPlayed.Contains(selected)) sessionBossesPlayed.Add(selected);
            return selected;
        }
        void HideNextBossCards()
        {
            if (floorNextBossCard != null) floorNextBossCard.Hide();
            if (wallNextBossCard != null) wallNextBossCard.Hide();
        }

        Transform PickSessionBossCandidate()
        {
            if (bossModelCandidates.Count == 0) return null;
            if (!sessionActive)
            {
                var fallback = PickWeightedBossCandidate();
                sessionBossesPlayed.Add(fallback);
                return fallback;
            }

            bool canAvoidRepeats = sessionBossesPlayed.Count < bossModelCandidates.Count;
            float totalWeight = 0f;
            for (int i = 0; i < bossModelCandidates.Count; i++)
            {
                if (canAvoidRepeats && sessionBossesPlayed.Contains(bossModelCandidates[i])) continue;
                totalWeight += i < bossModelCandidateWeights.Count ? Mathf.Max(0f, bossModelCandidateWeights[i]) : 1f;
            }

            Transform picked = null;
            if (totalWeight > 0f)
            {
                float roll = Random.Range(0f, totalWeight);
                float cumulative = 0f;
                for (int i = 0; i < bossModelCandidates.Count; i++)
                {
                    var candidate = bossModelCandidates[i];
                    if (canAvoidRepeats && sessionBossesPlayed.Contains(candidate)) continue;
                    cumulative += i < bossModelCandidateWeights.Count ? Mathf.Max(0f, bossModelCandidateWeights[i]) : 1f;
                    if (roll <= cumulative)
                    {
                        picked = candidate;
                        break;
                    }
                }
            }

            if (picked == null) picked = PickWeightedBossCandidate();
            sessionBossesPlayed.Add(picked);
            return picked;
        }

        // Wall-display camera/culling-mask setup — independent of which specific
        // boss ends up active, so this runs once, not per candidate.
        void ConfigureBossDisplayCameras()
        {
            wallDisplayCamera = wallDisplayCamera != null ? wallDisplayCamera : FindCamera("Camera == Cutscene Display") ?? FindDisplayCamera(1);
            floorDisplayCamera = floorDisplayCamera != null ? floorDisplayCamera : Camera.main ?? FindCamera("Main Camera") ?? FindDisplayCamera(MultiDisplaySetup.FloorDisplay);

            int bossMask = 1 << Visual3DFollower.BossLayer;
            foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (camera != null && camera.gameObject.scene.IsValid())
                {
                    camera.cullingMask &= ~bossMask;
                }
            }

            bossDisplayCamera = bossDisplayCamera != null ? bossDisplayCamera : EnsureBossDisplayCamera();
            if (bossDisplayCamera != null) bossDisplayCamera.cullingMask = bossMask;
            else if (wallDisplayCamera != null) wallDisplayCamera.cullingMask |= bossMask;
            EnsureBossKeyLight(bossDisplayCamera != null ? bossDisplayCamera : wallDisplayCamera, bossMask);
            EnsureWallHudInFrontOfBoss();   // the wall HUD draws after the boss (WallHudLayer.cs)

            // Gem pickups render on FloorLayer, and every camera in this scene
            // defaults to culling mask "Everything" (unlike DeepspaceGameManager's
            // own scene, which explicitly separates Wall/FloorLayer per camera) —
            // Main Camera already includes FloorLayer by default, so the only
            // camera that needs an explicit exclusion is the wall one, or a gem
            // would render doubled on both displays the same way the boss
            // explosion VFX did before BossLayer got this same treatment.
            if (wallDisplayCamera != null) wallDisplayCamera.cullingMask &= ~(1 << Visual3DFollower.FloorLayer);
        }

        // The scene has no Light, so the boss skins (URP/Lit, textured) were lit only by the dim navy
        // ambient and showed up almost black. Add one directional light that touches only BossLayer,
        // so every other object in the scene looks exactly as before. Skipped if a light already does it.
        static void EnsureBossKeyLight(Camera view, int bossMask)
        {
            foreach (var existing in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (existing.type == LightType.Directional && existing.enabled && (existing.cullingMask & bossMask) != 0) return;

            var light = new GameObject("Boss Key Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, .96f, .9f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.None;
            light.cullingMask = bossMask;
            // From above and slightly to the side of the viewer, so the boss's front faces are lit.
            var forward = view != null ? view.transform.forward : Vector3.forward;
            var flat = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flat.sqrMagnitude < .001f) flat = Vector3.forward;
            light.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(40f, -25f, 0f);
        }

        Camera EnsureBossDisplayCamera()
        {
            if (wallDisplayCamera == null) return null;

            var existing = FindCamera("Camera == Wall Boss 3D");
            var cam = existing != null ? existing : new GameObject("Camera == Wall Boss 3D", typeof(Camera)).GetComponent<Camera>();
            cam.transform.SetPositionAndRotation(wallDisplayCamera.transform.position, wallDisplayCamera.transform.rotation);
            cam.transform.localScale = wallDisplayCamera.transform.localScale;
            cam.orthographic = wallDisplayCamera.orthographic;
            cam.orthographicSize = wallDisplayCamera.orthographicSize;
            cam.nearClipPlane = wallDisplayCamera.nearClipPlane;
            cam.farClipPlane = wallDisplayCamera.farClipPlane;
            cam.rect = wallDisplayCamera.rect;
            cam.targetDisplay = wallDisplayCamera.targetDisplay;
            cam.depth = wallDisplayCamera.depth + 10f;
            cam.clearFlags = CameraClearFlags.Depth;
            cam.backgroundColor = Color.clear;
            cam.allowHDR = true;
            cam.allowMSAA = wallDisplayCamera.allowMSAA;

            var cameraData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null) cameraData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderType = CameraRenderType.Overlay;
            cameraData.renderPostProcessing = true;

            var wallCameraData = wallDisplayCamera.GetUniversalAdditionalCameraData();
            wallCameraData.renderType = CameraRenderType.Base;
            // Grade the completed wall stack once, using its scene-specific profile.
            wallCameraData.renderPostProcessing = false;
            cameraData.volumeLayerMask = wallCameraData.volumeLayerMask;
            cameraData.dithering = wallCameraData.dithering;
            cameraData.stopNaN = wallCameraData.stopNaN;
            if (!wallCameraData.cameraStack.Contains(cam))
            {
                wallCameraData.cameraStack.Add(cam);
            }
            return cam;
        }

        // visible: false hides every candidate (not just the current bossModel) —
        // called before ResetFight picks the next random one, so a previous
        // fight's boss can never stay active alongside the newly chosen one.
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
        static readonly int RimPowerId = Shader.PropertyToID("_RimPower");

        void ApplyBossGlow(Transform model)
        {
            if (model == null) return;
            Color color = bossGlowColorByModel.TryGetValue(model, out var perBossColor)
                ? perBossColor
                : (bossGlowColors.Length > 0 ? bossGlowColors[0] : Color.white);
            if (BossPassiveState.Identify(model.name) != BossPassiveState.Kind.None) color = BossThemePalette.Accent(model.name);
            var block = new MaterialPropertyBlock();
            block.SetColor(EmissionColorId, color);
            block.SetFloat(EmissionIntensityId, bossGlowIntensity);
            block.SetFloat(RimPowerId, bossGlowRimPower);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.SetPropertyBlock(block);
            foreach (var skin in model.GetComponentsInChildren<BossVoxelSkin>(true)) if (skin.body != null) skin.body.SetPropertyBlock(null); // the new models carry their own emission
            // 2026-11-24 "เอาออร่าวงๆรอบตัวบอสออก ทุกตัว": the divine halo ring is no longer shown behind any boss.
            if (bossDivineHalo != null) bossDivineHalo.Hide();
            if (bossDisplayCamera != null && wallDisplayCamera != null)
                bossDisplayCamera.orthographicSize = wallDisplayCamera.orthographicSize / (bossPresentationScale * bossWallSizeMultiplier * BossWallFramingMultiplier(model));
            FitBossHaloFraming();
        }

        Texture2D LoadBackgroundTexture(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName)) return null;
            // BossFight.unity keeps its own copy of the name lists above, with the old pictures' names (some as
            // "BossBackgrounds/..."). 2026-11-19 hell -> the Fire set, 2026-11-25 water floor -> the Water set, and
            // 2026-11-26 "ย้ายกับลบรูปพื้นหลังที่ไม่ได้ใช้ ... แก้ที่อยู่": the deleted Hell / Water / Grass / Sky pictures map to
            // today's sets by file name, whatever folder the name carries.
            resourceName = CurrentBackgroundArt(resourceName);
            if (backgroundTextureCache.TryGetValue(resourceName, out var cached)) return cached;
            var texture = GameArt.Load<Texture2D>(resourceName);
            if (texture == null)
                Debug.LogWarning($"Boss Slayer Game Manager: no texture found at Assets/{resourceName} (GameArt) — check it is under Art/BG.", this);
            backgroundTextureCache[resourceName] = texture;
            return texture;
        }

        static string CurrentBackgroundArt(string name)
        {
            int slash = name.LastIndexOf('/');
            switch (slash < 0 ? name : name.Substring(slash + 1))
            {
                case "BGHellBoss": case "BGFireWall": return FireWallArt;
                case "BGFloorHellBoss": case "BGFloorFire": return FireFloorArt;
                case "BGWaterBoss": case "BGWaterWall": case "BGWaterScene": return WaterWallArt;
                case "BGFloorWaterBoss": case "BGFloorWater": return WaterFloorArt;
                case "BGBlueSkyBoss": case "BGLandWall": case "BGEarthScene": return LandWallArt;
                case "BGGrassFloorBoss": case "BGFloorLand": return LandFloorArt;
                case "BGFinalBossScene": return FinalWallArt;
                default: return name;
            }
        }

        // Swaps both displays' background to match whichever boss ResetFight
        // just picked — called right after bossModel is assigned (before the
        // portal even opens), so the scenery is already correct the moment the
        // boss reveals rather than popping mid-entrance.
        void UpdateBossThemeBackground()
        {
            if (bossModel == null) return;
            if (bossFloorBackgroundImage != null && bossFloorBackgroundByModel.TryGetValue(bossModel, out var floorTexture))
            {
                bossFloorBackgroundImage.texture = floorTexture;
                PlayBossBackgroundMovie(bossFloorBackgroundImage, ref floorBackgroundVideo, floorTexture);
            }
            if (bossWallBackgroundImage != null && bossWallBackgroundByModel.TryGetValue(bossModel, out var wallTexture))
            {
                bossWallBackgroundImage.texture = wallTexture;
                PlayBossBackgroundMovie(bossWallBackgroundImage, ref wallBackgroundVideo, wallTexture);
            }
            RefreshFireArena();
        }

        const string FireWallArt = "Art/BG/Fire/BGFireWall", FireFloorArt = "Art/BG/Fire/BGFloorFire";
        // 2026-11-26 "เอาพื้นหลังของจอกำแพงทั้ง3แมพใหม่อันนี้ไปทำ animation loop": the art team's new wall paintings (Earth,
        // Water, Final Boss) replace the 2026-11-25 Land and Water walls, and the Abyssal King gets its own. Each has a
        // centre loop made in Fortal Studio (Seedance 2.5, StreamingAssets/BossBackgroundMovies/<name>_Centre.mp4).
        // 2026-11-26: the old BGWaterWall / BGLandWall pictures were deleted; each set now lives in its own folder.
        const string WaterFloorArt = "Art/BG/Water/BGFloorWater", WaterWallArt = "Art/BG/Water/BGWaterScene";
        const string LandFloorArt = "Art/BG/Land/BGFloorLand", LandWallArt = "Art/BG/Land/BGEarthScene";
        const string FinalWallArt = "Art/BG/Final/BGFinalBossScene";
        // 2026-11-27 "เปลี่ยนพื้นของบอสเป็นฉากที่สร้างมาใหม่ใน fortal studio": the king's violet rift floor (Fortal Studio, Seedream 5.5).
        const string FinalFloorArt = "Art/BG/Final/BGFloorFinal", FinalFloorName = "BGFloorFinal";
        const string WaterWallName = "BGWaterScene", LandWallName = "BGEarthScene", FinalWallName = "BGFinalBossScene";
        BossFireWall fireWall;
        BossWallCentreVideo wallCentreVideo;
        BossPortalSpin portalSpin;
        BossPortalOrbit portalOrbit;
        BossWallRockfall wallRockfall;
        BossWallFloaters wallFloaters;
        BossWallShaderOverlay waterCaustics, finalShockwave;
        static readonly Color WaterGlow = new Color(.2f, .62f, 1f), LandGlow = new Color(1f, .58f, .16f), FinalGlow = new Color(.85f, .3f, 1f);
        Texture2D alphaLight, waterParticle, landParticle;
        // The share of the rising particles the Land wall shows (BossFireWall.ParticleCount 36 -> 8).
        const float LandRockShare = 8f / 36f;
        Texture2D AlphaLight() => alphaLight != null ? alphaLight : alphaLight = GameArt.Load<Texture2D>("Art/BG/AlphaLight");
        BossFireFloor fireFloor;

        // 2026-11-24 "เอาฉากไฟไปใส่แทนอันเดิม": every FIRE-element boss (the boss select's red row: Demon, Void Wraith,
        // Dragon) fights on the art team's BG Fire set, with its Red Alpha glow and rising Particle (BossFireWall) and the
        // floor flames (BossFireFloor). Chosen by boss here, not by the background name lists, because the scene keeps its
        // own copy of those lists and Dragon shared the sky picture with Golem and Sentinel.
        static bool IsFireElementBoss(string modelName)
        {
            var kind = BossPassiveState.Identify(modelName);
            return kind == BossPassiveState.Kind.Demon || kind == BossPassiveState.Kind.Wraith || kind == BossPassiveState.Kind.Dragon;
        }

        // 2026-11-25 "สร้างแมพทั้งจอกำแพงแล้วก็จอพื้นให้หน่อย จะเป็นของน้ำและดินที่เหลือ": each row of the boss select
        // (FIRE, WATER, LAND) fights on its own set made from the Fire set (Higgsfield Nano Banana, references BGFireWall /
        // BGFloorFire / BGFloorWater). Null for a boss outside the three rows, which keeps the scene's name lists.
        static (string floor, string wall)? ElementArt(string modelName)
        {
            if (IsFireElementBoss(modelName)) return (FireFloorArt, FireWallArt);
            switch (BossPassiveState.Identify(modelName))
            {
                case BossPassiveState.Kind.Fish:
                case BossPassiveState.Kind.Jellyfish:
                case BossPassiveState.Kind.Serpent:
                    return (WaterFloorArt, WaterWallArt);
                case BossPassiveState.Kind.Golem:
                case BossPassiveState.Kind.Sentinel:
                case BossPassiveState.Kind.Hydra:
                    return (LandFloorArt, LandWallArt);
                default:
                    return null;
            }
        }

        static bool IsFireArt(Texture texture) => texture != null && (texture.name == "BGFireWall" || texture.name == "BGFloorFire");

        // Art from the element sets (Fire, Water, Land) is shown exactly as painted.
        static bool IsPaintedArt(Texture texture) => IsFireArt(texture) || (texture != null && System.Array.IndexOf(PaintedArtNames, texture.name) >= 0);
        static readonly string[] PaintedArtNames = { "BGFloorWater", "BGWaterWall", "BGFloorLand", "BGLandWall", WaterWallName, LandWallName, FinalWallName, FinalFloorName };

        // The Fire set's extras (BossFireWall: red glow and rising particles; BossFireFloor: flickering flames) show
        // while the wall / floor show the Fire art.
        void RefreshFireArena()
        {
            if (fireWall == null) fireWall = BossFireWall.Attach(bossWallBackgroundImage);
            // 2026-11-25: the element walls' portal band loops (BossWallCentreVideo); other walls show no video.
            if (wallCentreVideo == null) wallCentreVideo = BossWallCentreVideo.Attach(bossWallBackgroundImage);
            if (wallCentreVideo != null) wallCentreVideo.Show(bossWallBackgroundImage.texture);
            // 2026-11-25: Land's portal turns clockwise on top of its video (BossPortalSpin), under the glow.
            if (portalSpin == null) portalSpin = BossPortalSpin.Attach(bossWallBackgroundImage);
            if (portalSpin != null)
            {
                if (wallCentreVideo != null) portalSpin.transform.SetSiblingIndex(wallCentreVideo.transform.GetSiblingIndex() + 1);
                portalSpin.Show(bossWallBackgroundImage.texture);
            }
            // 2026-11-26: the Final Boss wall's rune ring turns clockwise as light over its video (BossPortalOrbit).
            if (portalOrbit == null) portalOrbit = BossPortalOrbit.Attach(bossWallBackgroundImage);
            if (portalOrbit != null) portalOrbit.Show(bossWallBackgroundImage.texture);
            // 2026-11-26 (no new videos): Earth's rocks cut from its picture fall along the whole wall; Water's painted
            // sides get moving caustic light; the Final Boss wall's cut-out side rocks float and its shockwaves roll on
            // across the whole wall.
            if (wallRockfall == null) wallRockfall = BossWallRockfall.Attach(bossWallBackgroundImage);
            if (wallRockfall != null) wallRockfall.Show(bossWallBackgroundImage.texture);
            if (waterCaustics == null) waterCaustics = BossWallShaderOverlay.Attach(bossWallBackgroundImage, "Water Caustics", "Art/Shaders/BossWaterCaustics", WaterWallName);
            if (waterCaustics != null) waterCaustics.Show(bossWallBackgroundImage.texture);
            if (wallFloaters == null) wallFloaters = BossWallFloaters.Attach(bossWallBackgroundImage);
            if (wallFloaters != null) wallFloaters.Show(bossWallBackgroundImage.texture);
            if (finalShockwave == null) finalShockwave = BossWallShaderOverlay.Attach(bossWallBackgroundImage, "Final Shockwave", "Art/Shaders/BossShockwave", FinalWallName);
            if (finalShockwave != null) finalShockwave.Show(bossWallBackgroundImage.texture);
            BossShockwaveSync.Attach(finalShockwave, wallCentreVideo);   // rings born on the centre video's pulses
            // Drawn over the centre video in this order (bottom to top); the glow and particles (BossFireWall) stay above.
            if (wallCentreVideo != null)
            {
                int above = wallCentreVideo.transform.GetSiblingIndex();
                foreach (var layer in new Component[] { wallFloaters, portalSpin, wallRockfall, waterCaustics, portalOrbit, finalShockwave })
                    if (layer != null) layer.transform.SetSiblingIndex(++above);
            }
            if (fireWall != null)
            {
                // 2026-11-25: the Water and Land walls get the same glow as Fire's Red Alpha, in their own colour.
                var wallArt = bossWallBackgroundImage.texture;
                string wallName = wallArt != null ? wallArt.name : "";
                // 2026-11-26: the Final Boss wall gets a violet glow and no particles (its debris floats in the loop).
                // 2026-11-26 "particle หินลอยขึ้นอยู่ไหนของด่านดิน": the new Earth wall gets Land's rising rocks back too.
                if (wallName == "BGWaterWall" || wallName == WaterWallName) fireWall.SetElement(AlphaLight(), WaterGlow, waterParticle != null ? waterParticle : waterParticle = GameArt.Load<Texture2D>("Art/BG/Water/Particle"));
                // "หินลอยไม่ต้องเยอะ เพราะมีหินร่วงลงมาแล้ว แค่มีบางๆ": Land's rocks fall in the loop too, so only LandRockShare
                // of the usual count rise.
                else if (wallName == "BGLandWall" || wallName == LandWallName) fireWall.SetElement(AlphaLight(), LandGlow, landParticle != null ? landParticle : landParticle = GameArt.Load<Texture2D>("Art/BG/Land/Particle"), LandRockShare);
                else if (wallName == FinalWallName) fireWall.SetElement(AlphaLight(), FinalGlow, null);
                else fireWall.SetFire();
                fireWall.Show(IsFireArt(wallArt) || System.Array.IndexOf(PaintedArtNames, wallName) >= 0 && !wallName.StartsWith("BGFloor"));
            }
            // 2026-11-24 "ฉากพื้นเหมือนเดิม เอารูปที่ให้ไปใส่แทน": the floor shows the art team's Floor_Fire-BG exactly as
            // drawn. The animated flames (BossFireFloor, cut from the older "EXFloor Fire" example) are no longer added.
            if (fireFloor != null) fireFloor.Show(false);
        }

        Material floorGlowMaterial;

        // One shared material for every element floor; the RawImage supplies the texture.
        Material FloorGlowMaterial(Texture artwork)
        {
            if (floorGlowMaterial == null)
            {
                var shader = GameArt.Load<Shader>("Art/Shaders/BossFloorGlow");
                if (shader == null || !shader.isSupported) return null;
                floorGlowMaterial = new Material(shader) { name = "Boss Floor Glow", hideFlags = HideFlags.HideAndDontSave };
            }
            floorGlowMaterial.SetFloat("_Aspect", (float)artwork.width / Mathf.Max(1, artwork.height));
            return floorGlowMaterial;
        }

        void PlayBossBackgroundMovie(RawImage target, ref VideoBackgroundRuntime runtime, Texture2D fallbackTexture)
        {
            if (target == null || fallbackTexture == null) return;
            // The Fire art is shown as painted: no lava-motion shader or movie over it (BossFireWall / BossFireFloor
            // bring its own glow, particles and flames).
            if (IsPaintedArt(fallbackTexture))
            {
                if (target.TryGetComponent(out BossEnvironmentMotion motion)) motion.enabled = false;
                // 2026-11-24 bug fix ("ยังเหมือนเดิม"): disabling the motion only restores the plain material in its
                // OnDisable, and Unity skips OnDisable when the image is already inactive (the wall is hidden during the
                // boss select, when the background is picked). The wall then kept the motion shader - the old sky theme's
                // swirls and grain drawn over the Fire art. Take the material off here, active or not.
                if (target.material != null && target.material.shader != null && target.material.shader.name.Contains("BossEnvironmentMotion"))
                    target.material = null;
                if (target.material == floorGlowMaterial) target.material = null;
                target.uvRect = new Rect(0, 0, 1, 1);
                RefreshFireArena();
                // 2026-11-25 "เอาพื้นไปให้ higgsfield สร้าง animation": each element floor has a quiet 5 s loop made from its
                // own art (MiniMax H3, StreamingAssets/BossBackgroundMovies/<art name>.mp4). Without the movie the floor
                // falls back to the BossFloorGlow shader (lines breathe, a soft ring rolls out); walls stay as painted.
                bool floorArt = fallbackTexture.name.StartsWith("BGFloor");
                string floorMovie = Path.Combine(Application.streamingAssetsPath, "BossBackgroundMovies", $"{fallbackTexture.name}.mp4");
                if (floorArt && useBossBackgroundMovies && File.Exists(floorMovie))
                {
                    if (runtime == null) runtime = new VideoBackgroundRuntime($"{target.name} Video", transform);
                    runtime.Play(target, floorMovie, fallbackTexture);
                    return;
                }
                runtime?.Suspend();
                target.texture = fallbackTexture;
                if (floorArt) target.material = FloorGlowMaterial(fallbackTexture);
                return;
            }
            if (BossEnvironmentMotion.TryApply(target, fallbackTexture))
            {
                runtime?.Suspend();
                return;
            }
            if (!useBossBackgroundMovies) return;
            string moviePath = Path.Combine(Application.streamingAssetsPath, "BossBackgroundMovies", $"{fallbackTexture.name}.mp4");
            if (!File.Exists(moviePath)) return;
            if (runtime == null) runtime = new VideoBackgroundRuntime($"{target.name} Video", transform);
            runtime.Play(target, moviePath, fallbackTexture);
        }

        // Same technique as ApplyBossGlow (MaterialPropertyBlock on the shared
        // Fortal/VertexColorLit shader), reused here for the gem-charging glow
        // — own method rather than generalizing ApplyBossGlow itself, since
        // that one's color/intensity come from per-boss lookup tables this
        // call site has no use for.
        void ApplyGemGlow(Transform model, Color color, float intensity)
        {
            if (model == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor(EmissionColorId, color);
            block.SetFloat(EmissionIntensityId, intensity);
            block.SetFloat(RimPowerId, gemGlowRimPower);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.SetPropertyBlock(block);
        }

        BossDivineHalo bossDivineHalo;

        void ApplyGemGlow(Renderer[] renderers, MaterialPropertyBlock block, Color color, float intensity)
        {
            if (renderers == null || renderers.Length == 0 || block == null) return;
            block.SetColor(EmissionColorId, color);
            block.SetFloat(EmissionIntensityId, intensity);
            block.SetFloat(RimPowerId, gemGlowRimPower);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].SetPropertyBlock(block);
            }
        }



        // บาส 2026-09-03: "หนวด jellyfish ไม่เห็นขยับแอนิเมชั่นเลย" — the FBX
        // (Assets/Scenes/Bosses/JeelyFishBoss.fbx) does carry animation curves
        // for its 9 TentaclePivot_0..8 nodes, but Fortal3DS's own source
        // (jellyfishBossGenerator.js) explicitly flags this rig's Blender/FBX
        // animated round-trip as unverified (only the dragon's own tentacle
        // rig has been confirmed), and nothing in this scene has an
        // Animator+Controller wired up to actually PLAY whatever clip Unity
        // imported. Rather than debug that unverified import path, this
        // reproduces the exact same sway math directly in C# — mirrored 1:1
        // off jellyfishBossGenerator.js's own tentacleRotationAt() — driving
        // the pivots' local rotation every frame instead. Same "don't trust
        // the animation round-trip, redo it engine-side" call the portal
        // entrance already makes by tweening with DOTween instead of an
        // imported clip.
        // lengthT = this tentacle's length / the longest tentacle (8), same
        // JELLYFISH_BOSS_DEFAULTS.tentacleLengths = [[5,7,5],[6,8,6],[5,7,5]]
        // grid the generator uses, flattened in its xi/zi loop order (index =
        // xi*3+zi) so index i here lines up with TentaclePivot_i.
        static readonly float[] JellyfishTentacleLengthT = { 0.625f, 0.875f, 0.625f, 0.75f, 1f, 0.75f, 0.625f, 0.875f, 0.625f };
        Transform[] jellyfishTentaclePivots;

        void UpdateJellyfishTentacleSway()
        {
            bool isJellyfish = bossModel != null && bossModel.name.Contains("JeelyFishBoss");
            if (!isJellyfish) return;
            // The articulated controller owns idle, charge and release together.
            var jellyfishMotion = bossModel.GetComponent<BossJellyfishAnimation>();
            if (jellyfishMotion != null && jellyfishMotion.isActiveAndEnabled) return;

            // Found once and cached — same lazy-discovery idiom as
            // sentinelAntennaGlow, since these pivots never move in the
            // Hierarchy for the rest of the session. A pivot the search
            // doesn't find (name mismatch, or the mesh/pivot parenting broke
            // in the FBX round-trip) just stays null and is skipped below
            // rather than throwing.
            if (jellyfishTentaclePivots == null)
            {
                jellyfishTentaclePivots = new Transform[JellyfishTentacleLengthT.Length];
                foreach (var t in bossModel.GetComponentsInChildren<Transform>(true))
                    for (int i = 0; i < jellyfishTentaclePivots.Length; i++)
                        if (t.name == $"TentaclePivot_{i}") jellyfishTentaclePivots[i] = t;
            }

            float time = Time.time;
            for (int i = 0; i < jellyfishTentaclePivots.Length; i++)
            {
                var pivot = jellyfishTentaclePivots[i];
                if (pivot == null) continue;
                float lengthT = JellyfishTentacleLengthT[i];
                float amplitudeX = 0.06f + 0.16f * lengthT;
                float amplitudeZ = 0.05f + 0.11f * lengthT;
                float speed = 1.1f - 0.25f * lengthT;
                float phase = i * 0.9f;
                // tentacleRotationAt returns radians (three.js Euler) — Rad2Deg
                // before feeding Quaternion.Euler, which expects degrees.
                float rx = Mathf.Sin(time * speed + phase) * amplitudeX * Mathf.Rad2Deg;
                float rz = Mathf.Cos(time * speed * 0.7f + phase) * amplitudeZ * Mathf.Rad2Deg;
                // บาส 2026-09-03: "ส่ายซ้าย-ขวาด้วย" — the ported JS math only ever
                // swayed X/Z (front-back relative to the model), no Y term at
                // all, so there's no reliable way to know which of rx/rz would
                // even read as screen left-right from the wall camera's own
                // angle without seeing it live. A yaw (Y-axis) term sidesteps
                // that guesswork entirely — rotating a hanging pivot around its
                // own up axis always swings its tip sideways in a horizontal
                // arc, regardless of camera angle. Own amplitude/speed/phase
                // offset (not reusing rx/rz's) so the three axes don't fall
                // into a repeating lockstep pattern.
                float amplitudeY = 0.05f + 0.12f * lengthT;
                float ry = Mathf.Sin(time * speed * 0.85f + phase + 1.3f) * amplitudeY * Mathf.Rad2Deg;
                pivot.localRotation = Quaternion.Euler(rx, ry, rz);
            }
        }

        void SetBossVisible(bool visible)
        {
            if (!visible && elementalChargeVfx != null) elementalChargeVfx.Stop();
            if (!visible && modelElementVfx != null) modelElementVfx.Clear();
            if (!visible && bossDivineHalo != null) bossDivineHalo.Hide();
            if (!visible) ResetBossWallShift();
            if (visible)
            {
                if (bossModel != null)
                {
                    bossModel.gameObject.SetActive(true);
                    ApplyBossGlow(bossModel);
                    BossHoverVfx.Install(bossModel,bossDisplayCamera!=null?bossDisplayCamera:wallDisplayCamera);
                }
            }
            else
            {
                foreach (var candidate in bossModelCandidates)
                {
                    if (candidate != null) candidate.gameObject.SetActive(false);
                }
                if (abyssalKingModel != null) abyssalKingModel.gameObject.SetActive(false);
            }

            if (bossImage != null)
            {
                bossImage.gameObject.SetActive(visible && bossModel == null);
            }
        }

        // horizontalT's X placement only — split out of UpdateBossModel so
        // BossEntranceRoutine can fly the boss to the exact same "resting at
        // screen-center" spot (horizontalT = 0.5, same value ResetFight's
        // bossX = arenaWidthMeters * 0.5f settles to) instead of duplicating
        // this math or drifting from it. Y/scale/rotation stay UpdateBossModel's
        // own job — bob wiggling toward a moving target makes no sense as a
        // fixed fly-to destination for the entrance tween.
        Vector3 BossHorizontalRestPosition(BossBaseTransform baseTransform, float horizontalT)
        {
            var p = baseTransform.Position;
            if (wallDisplayCamera != null && wallDisplayCamera.orthographic)
            {
                float halfViewWidth = wallDisplayCamera.orthographicSize * wallDisplayCamera.aspect;
                p.x = Mathf.Lerp(
                    wallDisplayCamera.transform.position.x - halfViewWidth * bossWallRoamFraction,
                    wallDisplayCamera.transform.position.x + halfViewWidth * bossWallRoamFraction,
                    horizontalT);
            }
            else
            {
                p.x = baseTransform.Position.x + (horizontalT - 0.5f) * arenaWidthMeters * 0.35f;
            }
            return p;
        }

        void UpdateBossModel(float horizontalT)
        {
            if (bossModel == null || !bossBaseTransforms.TryGetValue(bossModel, out var baseTransform)) return;

            var p = BossHorizontalRestPosition(baseTransform, horizontalT);
            bool grounded=bossModel.GetComponent<BossGolemAnimation>()!=null||bossModel.GetComponent<BossSentinelAnimation>()!=null;
            bool hydraMotion=bossModel.GetComponent<BossHydra>()!=null;
            bool slithering=bossModel.GetComponent<BossSerpentAnimation>()!=null||hydraMotion;
            p.y = baseTransform.Position.y + (grounded||slithering?0:Mathf.Sin(Time.time * bossModelBobSpeed) * bossModelBobAmplitude);
            bossModel.localPosition = p;

            bossModel.localScale = baseTransform.Scale;

            bossModelCurrentFacing = Mathf.Lerp(
                bossModelCurrentFacing,
                bossDir >= 0 ? 1f : -1f,
                1f - Mathf.Exp(-bossModelRotationLerp * Time.deltaTime));

            float turnYaw = bossModelCurrentFacing * bossModelTurnDegrees;
            float idleYaw = Mathf.Sin(Time.time * bossModelIdleYawSpeed) * bossModelIdleYawDegrees;
            float bank = -bossModelCurrentFacing * bossModelBankDegrees;
            float bobPitch = Mathf.Sin(Time.time * bossModelBobSpeed + 0.7f) * 3f;
            var flightRotation = Quaternion.Euler(grounded||hydraMotion?0:bobPitch, turnYaw + idleYaw*(grounded||hydraMotion?.2f:1), grounded||hydraMotion?0:bank);
            if(bossModel.GetComponent<BossSentinelAnimation>()!=null)
            {
                var space=Quaternion.Euler(90,0,0);
                flightRotation=space*Quaternion.Euler(0,turnYaw+idleYaw*.2f,0)*Quaternion.Inverse(space);
            }
            bossModel.localRotation = baseTransform.Rotation * (slithering?Quaternion.identity:flightRotation);
            CenterBossOnWall();
            FitBossHaloFraming();
        }

        void FitBossHaloFraming()
        {
            if (bossModel == null || bossDisplayCamera == null || !bossDisplayCamera.orthographic) return;
            if(bossModel.TryGetComponent<BossHydra>(out var hydra) && hydra.SevenHeadStrikeActive)return;
            // Raised Golem arms change the silhouette, not its intended screen size.
            // Keep the established camera framing through the full attack pose.
            var motion = bossModel.GetComponent<BossModelAnimation>();
            if (motion != null && (motion.character == BossModelAnimation.Kind.Golem || motion.character == BossModelAnimation.Kind.Sentinel) && motion.IsAttackMotionActive) return;
            var bounds = BossWorldBounds();
            float radius = BossDivineHalo.RadiusForBounds(bounds, bossDisplayCamera, activeBossRenderers) * (0.995f / 0.72f);
            var offset = bounds.center - BossWallCameraBase; // the unshifted wall camera: CenterBossOnWall moves the picture, not its size
            float vertical = Mathf.Abs(Vector3.Dot(offset, bossDisplayCamera.transform.up)) + radius;
            float horizontal = (Mathf.Abs(Vector3.Dot(offset, bossDisplayCamera.transform.right)) + radius)
                / Mathf.Max(bossDisplayCamera.aspect, 0.1f);
            // Only expand within a fight, avoiding zoom oscillation as the boss banks and bobs.
            bossDisplayCamera.orthographicSize = Mathf.Max(bossDisplayCamera.orthographicSize,
                Mathf.Max(vertical, horizontal) / (0.92f * bossPresentationScale * bossWallSizeMultiplier
                    * BossWallFramingMultiplier(bossModel)));
        }

        float BossWallFramingMultiplier(Component model)
        {
            var motion = model != null ? model.GetComponent<BossModelAnimation>() : null;
            if (motion == null || bossWallFramingMultipliers == null) return 1f;
            foreach (var entry in bossWallFramingMultipliers)
                if (entry.boss == motion.character && entry.multiplier > 0f) return entry.multiplier;
            return 1f;
        }

        void UpdateAttacks(float dt)
        {
            if (Time.timeScale <= 0f) return;
            AdvanceHydraStrikeRounds(dt);
            UpdateSentinelArsenal();
            PrepareGolemUltimateShake();
            if(bossModel!=null)bossModel.GetComponent<BossGolemAnimation>()?.SetBeamBraced(false);
            if (modelElementVfx == null) modelElementVfx = gameObject.AddComponent<BossModelElementVfx>();
            modelElementVfx.BeginFrame(bossModel, bossDisplayCamera != null ? bossDisplayCamera : wallDisplayCamera, activeBossRenderers);
            for (int i = attacks.Count - 1; i >= 0; i--)
            {
                var attack = attacks[i];
                // Severing a head interrupts its pending breath, including its warning.
                // A breath already released remains dangerous for its normal lifetime.
                if (attack.IsHydraBreathInterrupted)
                {
                    DestroyAttackView(attack);
                    attacks.RemoveAt(i);
                    continue;
                }
                attack.TelegraphTimer -= dt;
                attack.ActiveTimer -= dt;
                attack.WarningDelay -= dt;
                if (attack.WarningDelay > 0f) continue;
                if (attack.View != null) attack.View.gameObject.SetActive(true);
                StartAttackStageMotion(attack);
                if (attack.TelegraphTimer <= 0 && !attack.SoundReleased)
                {
                    attack.SoundReleased = true;
                    PlayBossAttackImpact(attack);
                    if (bossSignatureTitle != null && bossUltimateAura != null) bossUltimateAura.Pulse();

                    if (attack.HydraOwner != null && !attack.IsHydraSevenHeadStrike) attack.HydraOwner.AttackHead(attack.HydraHeadIndex);
                    if (bossModel != null) bossModel.GetComponent<BossModelAnimation>()?.ReleaseAttack(attack.AnimationCastId);
                    OnAbyssalKingRelease(attack);
                    sfx?.Release(SoundTheme, SfxPan(bossX));
                    if (attack.IsBossUltimate) signatureLanded = true;
                }
                // 2026-11-26 "เอา UI ที่บังบอสเหมือนเด็กวาดๆออกจากบอสทุกตัว": the element voxels drawn on the boss's body
                // during an attack (BossModelElementVfx.AddAttack) covered the model, so no boss gets them any more.
                // BeginFrame/Clear still run, so nothing left over from before stays on screen.
                UpdateAttackView(attack);
                UpdateGolemAttack(attack);
                UpdateAbyssalKingAttack(attack);
                UpdateSentinelAttack(attack);
                UpdateDemonUltimate(attack);

                RushTrackNearMiss(attack);
                if (attack.TelegraphTimer <= 0f && (!attack.HasHit || attack.Shape == Shape.Wave || (attack.Lingering && attack.ActiveTimer > 0f)))
                {
                    foreach (var player in players)
                    {
                        if (player.DownTimer > 0f || !TryGetInputLocalPoint(player.Index, out _) || attack.HitPlayers.Contains(player.Index)) continue;
                        if (!IsInAttack(attack, LocalToWorld(player.Cursor.anchoredPosition))) continue;

                        attack.HitPlayers.Add(player.Index);
                        HurtPlayer(player, attack.IsBossUltimate);
                    }
                    ReplayAttackHits(attack);
                    RushResolveNearMiss(attack);
                    if (attack.Shape != Shape.Wave) attack.HasHit = true;
                }

                if (attack.ActiveTimer > -0.45f) continue;

                DestroyAttackView(attack);
                attacks.RemoveAt(i);
                if(attack.IsBossUltimate && attack.SoundReleased && attacks.Count==0 && passives.Active==BossPassiveState.Kind.Dragon) dragonFirstUltimateCompleted=true;
            }
            UpdateGolemUltimateShake();
            UpdateSentinelArsenal();
            AdvanceDemonUltimateFx(dt);
            AdvanceUltimateFx(dt);
            TickSignatureAftermath();
            if (attacks.Count == 0) replayMultiHitLogged.Clear();
        }

        // บาส 2026-11-18: "ถ้าบอส animation อยู่ยังไม่ต้องโชว์ gem ... ค่อยโผล่มาตอนบอสพร้อมให้ยิง". While the boss plays its
        // phase change it is immune (BossPhaseTransitionInvulnerable), so a gem players ran to and charged did nothing.
        // Now no gem spawns then, the ones on the floor are hidden with their charge and life frozen, and they all come
        // back the moment the boss can be hit again (a new one follows shortly if the floor is empty).
        bool spotsWaitingForBoss;

        void HoldSpotsForBoss(bool hold)
        {
            if (hold == spotsWaitingForBoss) return;
            spotsWaitingForBoss = hold;
            foreach (var spot in spots)
            {
                if (spot.View != null) spot.View.gameObject.SetActive(!hold);
                if (spot.GemFollower != null) spot.GemFollower.gameObject.SetActive(!hold);
            }
            if (!hold) spotTimer = Mathf.Min(spotTimer, .3f);
        }

        void UpdateSpots(float dt)
        {
            if (Time.timeScale <= 0f) return;
            HoldSpotsForBoss(!ultimateActive && BossPhaseTransitionInvulnerable());
            if (spotsWaitingForBoss) return;
            if (!ultimateActive)
            {
                spotTimer -= dt;
                if (spotTimer <= 0f)
                {
                    SpawnSpot();
                    spotTimer = SpotGapSeconds;
                }
            }

            // บาส 2026-08-28: was `for (int i = spots.Count - 1; i >= 0; i--)` indexing straight
            // into `spots`. HitBoss() below can synchronously call SpawnUltimateSpots(), which
            // ClearSpots()s (destroys + empties) this SAME list and then re-populates it with 18
            // new entries — all from INSIDE this very loop, the moment a fully-charged spot's
            // HitBoss call is the one that crosses the ultimate-charge threshold. Once that
            // happens mid-loop, index `i` no longer refers to the spot this iteration was
            // processing — it now points at whatever landed at that slot in the brand-new
            // 18-item list, and `Destroy(spot.View...); spots.RemoveAt(i);` would silently
            // detach ONE OF THE FRESH SPOTS from tracking without ever destroying its
            // GameObject: never touched by any update loop again, un-collectible, and invisible
            // to every later ClearSpots() call since it's no longer even in the list — exactly
            // the "จุดสีฟ้าเก็บไม่ได้ ไม่ถูกทำลาย ไม่มีวันหาย" bug (only a full scene reload,
            // which destroys every GameObject regardless of C# tracking, ever cleared it).
            // Iterating a snapshot and removing by reference instead of by index means a
            // mid-loop Clear()+re-populate can't corrupt this loop's bookkeeping — worst case it
            // finds a spot already gone, which the two null/Contains-style guards below treat as
            // a harmless no-op instead of operating on the wrong entry.
            for (int i = spots.Count - 1; i >= 0; i--)
            {
                var spot = spots[i];

                spot.Life -= dt;
                // บาส 2026-10-06 (Balance Note A3): during the ultimate every spot is a burst gem, collected by touch in
                // UpdateUltimate (220 dmg). Standing 0.65-0.7 m away used to also hold-charge it as a normal blue gem
                // (100 dmg, +1 ultimate charge) - no hold-charging while the ultimate runs.
                if (ultimateActive) { UpdateSpotView(spot); continue; }
                var contributors = PlayersInside(spot.WorldPosition, 0.7f, playersInsideScratch);
                spot.Load = contributors.Count > 0 ? spot.Load + dt : Mathf.Max(0f, spot.Load - dt * 0.8f);
                spot.Golden = RushDuoFactor(contributors) > 1f;
                UpdateSpotView(spot);
                // 2026-11-25 "ดังเมื่อเหยียบ gem เลย ไม่ต้องโหลดเสร็จ": each gem's own sound the moment a player steps on it.
                bool stepped = contributors.Count > 0;
                if (stepped && !spot.Stepped) PlayGemSound(spot);
                spot.Stepped = stepped;
                if (contributors.Count > 0) sfx?.Hold(spot.Load / Mathf.Max(0.01f, spotHoldSeconds), SfxPan(spot.WorldPosition.x));

                if (spot.Load >= spotHoldSeconds)
                {
                    // บาส 2026-09-03: HitBoss + cleanup moved into
                    // CollectSpotRoutine, timed to fire after the gem's
                    // launch animation plays instead of instantly — see its
                    // own comment. Removed from `spots` right away regardless
                    // (same as before), so this spot can't be re-collected or
                    // re-processed while its animation is still playing.
                    spots.RemoveAt(i);
                    if (contributors.Count >= 2) ReplayLog.Event("team_gem", 2, 2, ReplayIndexes(contributors),
                        ReplayLog.Data().Add("gem", spot.Def.GemModelName).Add("count", contributors.Count));
                    StartCoroutine(CollectSpotRoutine(spot, new List<PlayerState>(contributors)));
                }
                else if (spot.Life <= 0f)
                {
                    sfx?.Play("gem_expire", 0.16f, SfxPan(spot.WorldPosition.x), 0.2f, 0);
                    PopOutSpot(spot);
                    spots.RemoveAt(i);
                }
            }
        }

        void UpdateUltimate(float dt)
        {
            if (Time.timeScale <= 0f) return;
            if (!ultimateActive) return;

            int previousSecond = Mathf.CeilToInt(ultimateTimer);
            ultimateTimer -= dt;
            int second = Mathf.CeilToInt(ultimateTimer);
            if (second > 0 && second <= 3 && second != previousSecond) sfx?.Play("countdown", 0.45f, 0, 0.2f, 4);
            for (int i = spots.Count - 1; i >= 0; i--)
            {
                var contributors = PlayersInside(spots[i].WorldPosition, 0.65f, playersInsideScratch);
                if (contributors.Count == 0) continue;

                var spot = spots[i];
                spots.RemoveAt(i);
                replayUltGems++;
                StartCoroutine(CollectSpotRoutine(spot, new List<PlayerState>(contributors), true));
            }

            if (CheckBossDefeated()) return;
            if (ultimateTimer > 0f && spots.Count > 0) return;

            // บาส 2026-08-28: was relying entirely on each spot's own Life countdown (ticked
            // down separately, in parallel, over in UpdateSpots) to clean up anything left
            // un-popped once the burst window closes. If one of the 18 burst spots ever
            // spawns somewhere a real player physically can't reach (SpawnUltimateSpots'
            // RandomX(0.94f)/RandomZ(0.88f) push spots to within ~3-6% of the arena edge),
            // it never gets touched by the loop above, and nothing else was ever destroying
            // it — reported as "จุดสีฟ้าที่เก็บไม่ได้และไม่มีวันหาย". Explicitly clearing
            // whatever's left the instant this window closes (whether it closed because time
            // ran out or because every spot got popped early — spots.Count is already 0 in
            // that case, so this is a no-op then) means nothing from this phase can outlive
            // it, reachable or not, regardless of why the Life-timer path didn't catch it.
            ClearSpots();

            ultimateActive = false;
            ReplayTryEndUltimate();
            sfx?.Play("ultimate_end", 0.4f, 0, 0.2f, 3);
            spotTimer = 0.3f;
            attackTimer = Mathf.Max(attackTimer, 1.2f);
        }

        void SpawnSpot()
        {
            if (spots.Count >= MaxSpots || InputArea == null) return;

            var def = PickSpotDefinition();
            float diameter = 110f * gemFrameSizeMultiplier * RushGemScale(def);
            for (int tries = 0; tries < 40; tries++)
            {
                var pos = RandomGemPosition(diameter);
                bool close = false;
                foreach (var other in spots)
                {
                    if (Vector2.Distance(other.WorldPosition, pos) < spotMinGapMeters)
                    {
                        close = true;
                        break;
                    }
                }
                if (close) continue;

                spots.Add(CreateSpot(def, pos, $"Spot == {def.Name}", diameter, spotLifeSeconds));
                if (gameStarted) sfx?.Play("gem_spawn", 0.2f, SfxPan(pos.x), 0.18f, 0);
                return;
            }
        }

        // บาส 2026-11-18: the art team's 2D gem + particle cloud (BossGemArt), its charge bar a diamond outline that runs
        // round the gem and moves with it; a gem type without art keeps the old disc, square glow frames and 3D gem model.
        SpotState CreateSpot(SpotDef def, Vector2 pos, string name, float diameter, float life)
        {
            var view = CreateCircle(name, WorldToLocal(pos), diameter, def.Color);
            PutBelowPlayers(view);
            var charge = BossGemArt.Attach(view, def.GemModelName, diameter, def.Color);
            if (charge != null)
            {
                if (view.TryGetComponent(out Image disc)) disc.enabled = false;
                return PopInSpot(new SpotState(def, pos, view, charge.rectTransform, null, null, life, null));
            }
            var edgeGlow = CreateGemEdgeGlow(view, def.Color);
            var contrastGlow = CreateGemContrastGlow(view);
            var progressRing = CreateProgressRing("Hold Progress Ring", view, Color.white);
            var gemFollower = AttachGemModel(def, view);
            return PopInSpot(new SpotState(def, pos, view, progressRing, contrastGlow, edgeGlow, life, gemFollower));
        }

        // 2026-11-24 "gems ตอนโผล่มาให้มี animation ขยายเกินขนาดแล้วกลับมาปกติ ตอนจะหายให้ขยายแล้วหดลง":
        // a new gem grows from nothing past its size and settles back; a gem that goes away (runs out, or the floor is
        // cleared) swells a little and then shrinks to nothing before it is removed. Collected gems keep their flight.
        // 2026-11-24 "animation ไวไป ช้ากว่านี้หน่อย": about 1.6x slower than the first version (.2/.16 and .12/.22).
        const float GemPopOvershoot = 1.25f, GemPopGrowSeconds = .32f, GemPopSettleSeconds = .26f;
        const float GemOutSwell = 1.18f, GemOutSwellSeconds = .2f, GemOutShrinkSeconds = .36f;

        SpotState PopInSpot(SpotState s)
        {
            foreach (var t in GemPopTargets(s))
            {
                var rest = t.localScale;
                t.localScale = Vector3.zero;
                DOTween.Sequence().SetTarget(t).SetLink(t.gameObject).SetUpdate(true)   // real time: also runs while the game is paused
                    .Append(t.DOScale(rest * GemPopOvershoot, GemPopGrowSeconds).SetEase(Ease.OutQuad))
                    .Append(t.DOScale(rest, GemPopSettleSeconds).SetEase(Ease.InOutQuad));
            }
            return s;
        }

        // Swells, shrinks away, then destroys the gem's view and 3D model.
        void PopOutSpot(SpotState s)
        {
            foreach (var t in GemPopTargets(s))
            {
                DOTween.Kill(t);
                var go = t.gameObject;
                var now = t.localScale;
                DOTween.Sequence().SetTarget(t).SetLink(go).SetUpdate(true)
                    .Append(t.DOScale(now * GemOutSwell, GemOutSwellSeconds).SetEase(Ease.OutQuad))
                    .Append(t.DOScale(Vector3.zero, GemOutShrinkSeconds).SetEase(Ease.InBack))
                    .OnComplete(() => { if (go != null) Destroy(go); });
            }
        }

        static IEnumerable<Transform> GemPopTargets(SpotState s)
        {
            if (s.View != null) yield return s.View;
            if (s.GemFollower != null && (s.View == null || !s.GemFollower.transform.IsChildOf(s.View))) yield return s.GemFollower.transform;
        }

        // 2026-11-19: "ตอนเล่นให้ตัว player อยู่บน gems" - a new gem is made last under the player panel, which drew it over
        // the players' characters. It goes just under the lowest player cursor instead, so players stand on top of it.
        void PutBelowPlayers(RectTransform view)
        {
            int lowest = int.MaxValue;
            foreach (var player in players)
                if (player.Cursor != null && player.Cursor.parent == view.parent) lowest = Mathf.Min(lowest, player.Cursor.GetSiblingIndex());
            if (lowest != int.MaxValue && view.GetSiblingIndex() > lowest) view.SetSiblingIndex(lowest);
        }

        // Balance Note v3 §4.1: a shuffled bag of 20 (red 8, blue 5, yellow 4, green 3) drawn until empty, then refilled,
        // so every game gets the same mix. Gems from the big move (SpawnBonusSpot) never come out of the bag.
        SpotDef PickSpotDefinition()
        {
            if (spotBagQueue.Count == 0)
            {
                for (int i = 0; i < spotDefs.Length; i++)
                    for (int n = i < spotBag.Length ? spotBag[i] : 0; n > 0; n--) spotBagQueue.Add(i);
                if (spotBagQueue.Count == 0) return spotDefs[Random.Range(0, spotDefs.Length)];
                for (int i = spotBagQueue.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (spotBagQueue[i], spotBagQueue[j]) = (spotBagQueue[j], spotBagQueue[i]); }
            }
            int last = spotBagQueue.Count - 1;
            int pick = spotBagQueue[last];
            spotBagQueue.RemoveAt(last);
            return spotDefs[pick];
        }

        void SpawnUltimateSpots()
        {
            sfx?.Play("ultimate_ready", 0.65f, 0, 0.3f, 4);
            JuiceMilestone("ULTIMATE!", new Color(1f, .65f, .25f));
            JuiceFloor(SpaceJuice.Transition, FloorRect.center, new Color(1f, .65f, .25f, .45f), 350);
            ClearSpots();
            ultimateActive = true;
            ultimateTimer = ultimateSeconds;
            var def = spotDefs[3];
            // บาส 2026-08-28: was 0.94f/0.88f (only ~3-6% margin from the arena edge) — with 18
            // spots spawned at once, that was enough margin for one to occasionally land
            // somewhere too close to the physical boundary for a real player to actually reach
            // (see ClearSpots() call added above in UpdateUltimate for the guaranteed-cleanup
            // side of this same fix). Pulled in to match SpawnSpot's own regular-spot margins.
            for (int i = 0; i < ultimateBurstSpots; i++)
            {
                var pos = RandomGemPosition(96f * gemFrameSizeMultiplier);
                spots.Add(CreateSpot(def, pos, "Spot == Ultimate Burst", 96f * gemFrameSizeMultiplier, ultimateSeconds));
            }
        }

        // Clones def's 3D gem model tracking `view`'s position — the circle
        // itself is untouched (still what carries hit detection and the
        // fill/color load-progress feedback, see UpdateSpotView), this just
        // adds the real model as a visual on top of it. No cleanup call needed
        // here: Visual3DFollower's own LateUpdate destroys itself the moment
        // `view` does (Unity's overloaded == treats a destroyed object as
        // null), so every `Destroy(spot.View.gameObject)` already in the
        // codebase (spot collected, expired, cleared) takes the gem with it
        // for free.
        Visual3DFollower AttachGemModel(SpotDef def, RectTransform view)
        {
            if (string.IsNullOrEmpty(def.GemModelName)) return null;
            if (!gemModelsByName.TryGetValue(def.GemModelName, out var template) || template == null) return null;
            var follower = Visual3DFollower.Attach(template, InputArea, view, cutsceneArea);
            if (follower == null) return null;
            var edge = view.Find("Gem Edge Glow");
            var shine = follower.gameObject.AddComponent<BossGemShine>();
            if (!shine.Initialize(edge != null ? edge.GetComponent<Image>() : null, def.Color)) Destroy(shine);

            // The 3D gem is the primary visual now — hide the flat circle
            // underneath rather than deleting it: it still anchors the white
            // progress ring (a child of it, see CreateProgressRing) and is what
            // Visual3DFollower/the spot's own cleanup track (destroying view
            // takes both the ring and the gem clone with it). Only hides it
            // once a gem actually attached, so a missing/misnamed model falls
            // back to the circle instead of leaving nothing visible at all.
            var image = view.GetComponent<Image>();
            if (image != null) image.enabled = false;
            return follower;
        }

        const float AttackClearanceMeters = 1.2f;

        // Minimum edge-to-edge spacing; never changes radius or grants immunity.
        bool AttacksTooClose(AttackState a, AttackState b)
        {
            if (a.Shape == Shape.Wave || b.Shape == Shape.Wave) return false;
            if (a.Shape == Shape.Circle && b.Shape == Shape.Circle)
                return Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z))
                    < a.Radius + b.Radius + AttackClearanceMeters;
            if (a.Shape == Shape.Zone || b.Shape == Shape.Zone) return true;
            float aHalf = a.Shape == Shape.Circle ? a.Radius : a.Width * .5f;
            float bHalf = b.Shape == Shape.Circle ? b.Radius : b.Width * .5f;
            return Mathf.Abs(a.X - b.X) < aHalf + bHalf + AttackClearanceMeters;
        }

        void EnsureAttackSpacing(int firstNewAttack)
        {
            for (int i = firstNewAttack; i < attacks.Count; i++)
            {
                var attack = attacks[i];
                bool tooClose = false;
                for (int j = 0; j < i; j++)
                    tooClose |= AttacksTooClose(attack, attacks[j]);
                if (!tooClose) continue;
                // Reject overcrowded placements before warning, keeping the accepted shapes intact.
                DestroyAttackView(attack);
                attacks.RemoveAt(i--);
            }
        }

        void SpawnAttack()
        {
            int firstNewAttack = attacks.Count;
            PlayAttackChargeVfx();
            if (IsAbyssalKingActive)
            {
                if (!TrySpawnBossSignature()) SpawnAbyssalKingTechnique();
                for (int i = firstNewAttack; i < attacks.Count; i++)
                    if (attacks[i].WarningDelay <= 0) StartAttackStageMotion(attacks[i]);
                return;
            }

            int phase = BossPhase();
            var pool = new List<int> { 0, 1, 2, 3 };
            if (phase >= 2) pool.AddRange(new[] { 4, 5, 6, 7, 8 });
            if (phase >= 3) pool.AddRange(new[] { 9, 10, 11, 12 });

            var hydra = bossModel != null ? bossModel.GetComponent<BossHydra>() : null;
            bool signature = TrySpawnBossSignature();
            if (!signature)
            {
                SpawnVariedBossAttack(pool[Random.Range(0, pool.Count)]);

            }
            if (!signature && passives.Active == BossPassiveState.Kind.Serpent && passives.AttacksLaunched % 4 == 0)
                passiveFeedback?.Notify("STORM TIDE! DOUBLE WAVE");
            for (int i = firstNewAttack; i < attacks.Count; i++)
                if (attacks[i].WarningDelay <= 0) StartAttackStageMotion(attacks[i]);
        }

        void SpawnHydraVolley(BossHydra hydra)
        {
            // Schedule one independently warned shot per fully regrown head.
            // Spatial overlaps are allowed because the shots never overlap in time.
            float warning = Mathf.Max(.8f, telegraphSeconds);
            float interval = warning + .45f + .45f + .35f;
            int shot = 0;
            for (int head = 0; head < BossHydra.MaximumHeads; head++)
            {
                if (!hydra.IsHeadReady(head)) continue;
                float x = Mathf.Lerp(arenaWidthMeters * .15f, arenaWidthMeters * .85f, head / 6f);
                float delay = warning + shot * interval;
                AddCircle("Hydra breath " + (head + 1), x, RandomZ(.7f), 1.2f, delay);
                var attack = attacks[attacks.Count - 1];
                attack.HydraOwner = hydra;
                attack.HydraHeadIndex = head;
                attack.AnimationVariant = shot % 2 == 0 ? 2 : 3;
                attack.WarningDelay = shot * interval;
                attack.TelegraphTotal = warning;
                attack.View.gameObject.SetActive(attack.WarningDelay <= 0f);
                shot++;
            }
        }

        void SpawnAttackPattern(int pattern)
        {
            int firstNewAttack = attacks.Count;
            switch (pattern)
            {
                case 0:
                    AddCircle("ฟาดจุดเดียว", RandomX(0.8f), RandomZ(0.7f), 1.8f, telegraphSeconds);
                    break;
                case 1:
                    AddLine("ลำแสงตรง", bossX, 1.7f, telegraphSeconds);
                    break;
                case 2:
                    AddZone("ถล่มครึ่งสนาม", Random.value < 0.5f ? -1 : 1, telegraphSeconds);
                    break;
                case 3:
                    SpawnWave(1, telegraphSeconds);
                    break;
                case 4:
                    float off = Random.Range(-0.8f, 0.8f);
                    for (int i = -1; i <= 1; i++) AddLine("ลำแสงหลายเส้น", arenaWidthMeters * .5f + off + i * Mathf.Min(3.4f, arenaWidthMeters * .3f), 1.5f, telegraphSeconds);
                    break;
                case 5:
                    for (int i = 0, n = Random.Range(7, 11); i < n; i++) AddCircle("ฝนอุกกาบาต", RandomX(0.92f), RandomZ(0.88f), 0.95f, telegraphSeconds + i * 0.2f);
                    break;
                case 6:
                    AddCircle("ระเบิดวงกว้าง", RandomX(0.5f), RandomZ(0.35f), 3.4f, telegraphSeconds * 1.35f);
                    break;
                case 7:
                    for (int i = 0; i < 5; i++) AddCircle("ระเบิดไล่ต่อเนื่อง", RandomX(0.85f), RandomZ(0.8f), 1.5f, telegraphSeconds + i * 0.8f);
                    break;
                case 8:
                    SpawnWave(2, telegraphSeconds);
                    break;
                case 9:
                    AddCircle("ระเบิดกลาง + สะเก็ดรอบทิศ", arenaWidthMeters * .5f, arenaDepthMeters * .5f, 2.9f, telegraphSeconds);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2f;
                        AddCircle("สะเก็ดรอบทิศ", arenaWidthMeters * .5f + Mathf.Cos(a) * Mathf.Min(4.3f, arenaWidthMeters * .4f), arenaDepthMeters * .5f + Mathf.Sin(a) * Mathf.Min(2f, arenaDepthMeters * .4f), 1.05f, telegraphSeconds + 0.55f);
                    }
                    break;
                case 10:
                    SpawnWave(3, telegraphSeconds);
                    break;
                case 11:
                    for (int i = 0, n = Random.Range(12, 17); i < n; i++) AddCircle("พายุอุกกาบาต", RandomX(0.95f), RandomZ(0.92f), 0.8f, telegraphSeconds + i * 0.13f);
                    break;
                default:
                    AddLine("ลำแสงคู่ + คลื่นตาม", bossX - 2.6f, 1.5f, telegraphSeconds);
                    AddLine("ลำแสงคู่ + คลื่นตาม", bossX + 2.6f, 1.5f, telegraphSeconds);
                    SpawnWave(1, telegraphSeconds + 1f);
                    break;
            }
            EnsureAttackSpacing(firstNewAttack);
        }

        void AddCircle(string name, float x, float z, float radius, float delay)
        {
            var view = CreateCircle($"Attack == {name}", WorldToLocal(new Vector2(x, z)), radius * 2f * PixelsPerMeter(), new Color(1f, 0.15f, 0.12f, 0.36f));
            var warningRing = CreateCircleRing("Warning Ring", view, new Color(0.72f, 0.015f, 0.035f, 1f));
            attacks.Add(new AttackState(Shape.Circle, view, delay, 0.45f + passives.ScorchSeconds) { X = x, Z = z, Radius = radius, WarningRing = warningRing, Lingering = passives.ScorchSeconds > 0f });
            ConfigureFloorEffect(attacks[attacks.Count - 1], name);
        }

        void AddLine(string name, float x, float width, float delay)
        {
            var local = WorldToLocal(new Vector2(x, 0f));
            var view = CreateRect($"Attack == {name}", new Vector2(local.x, 0f), new Vector2(width * PixelsPerMeter(), FloorRect.height), new Color(1f, 0.15f, 0.12f, 0.36f));
            // บาส 2026-08-28: same charge-warning frame circle attacks use (see
            // UpdateAttackWarningRing — it already runs for every shape, this just gives Line/
            // Zone something to animate), just a rectangle sprite instead of a ring.
            var warningFrame = CreateSquareFrame("Warning Frame", view, new Color(0.72f, 0.015f, 0.035f, 1f));
            attacks.Add(new AttackState(Shape.Line, view, delay, 0.45f) { X = x, Width = width, WarningRing = warningFrame });
            ConfigureFloorEffect(attacks[attacks.Count - 1], name);
        }

        void AddZone(string name, int side, float delay)
        {
            float half = FloorRect.width * 0.5f;
            var view = CreateRect($"Attack == {name}", new Vector2(side < 0 ? -half * 0.5f : half * 0.5f, 0f), new Vector2(half, FloorRect.height), new Color(1f, 0.15f, 0.12f, 0.34f));
            var warningFrame = CreateSquareFrame("Warning Frame", view, new Color(0.72f, 0.015f, 0.035f, 1f));
            attacks.Add(new AttackState(Shape.Zone, view, delay, 0.45f) { Side = side, WarningRing = warningFrame });
            ConfigureFloorEffect(attacks[attacks.Count - 1], name);
        }

        void SpawnWave(int count, float startDelay)
        {
            bool horizontal = Random.value < 0.5f;
            int direction = Random.value < 0.5f ? -1 : 1;
            foreach (var existing in attacks)
                startDelay = Mathf.Max(startDelay, existing.ActiveTimer + .45f + .8f + telegraphSeconds);
            for (int i = 0; i < count; i++)
            {
                var size = horizontal ? new Vector2(FloorRect.width, 1.3f * FloorRect.height / Mathf.Max(.01f, arenaDepthMeters)) : new Vector2(1.3f * PixelsPerMeter(), FloorRect.height);
                var view = CreateRect("Attack == คลื่นกวาดสนาม", Vector2.zero, size, new Color(1f, 0.35f, 0.12f, 0.45f));
                var attack = new AttackState(Shape.Wave, view, startDelay + i * (3.2f + .45f + .8f + telegraphSeconds), 3.2f)
                {
                    WaveHorizontal = horizontal,
                    WaveDir = direction,
                    Width = 1.3f,
                    MoveSeconds = 3.2f,
                    WarningDelay = Mathf.Max(0f, startDelay + i * (3.2f + .45f + .8f + telegraphSeconds) - telegraphSeconds),
                    TelegraphTotal = telegraphSeconds,
                };
                attack.View.gameObject.SetActive(attack.WarningDelay <= 0f);
                CreateWaveGaps(attack);
                attacks.Add(attack);
            }
        }

        void CreateWaveGaps(AttackState a)
        {
            float span = a.WaveHorizontal ? arenaWidthMeters : arenaDepthMeters;
            float roll = Random.value;
            float width = roll < .4f ? 2f : roll < .72f ? 1.35f : .85f;
            int count = roll < .4f ? 1 : roll < .72f ? 2 : Random.Range(3, 5);
            width = Mathf.Min(width, Mathf.Max(.1f, span - .4f));
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    float center = Random.Range(width * .5f + .2f, span - width * .5f - .2f);
                    bool crowded = false;
                    foreach (var gap in a.WaveGaps)
                        crowded |= Mathf.Abs((gap.x + gap.y) * .5f - center) < width * 1.5f;
                    if (crowded) continue;
                    a.WaveGaps.Add(new Vector2(center - width * .5f, center + width * .5f));
                    break;
                }
            }
            a.WaveGaps.Sort((x, y) => x.x.CompareTo(y.x));
            a.ViewImage.enabled = false;
            float start = 0f;
            foreach (var gap in a.WaveGaps)
            {
                CreateWaveSection(a, start, gap.x, false);
                CreateWaveSection(a, gap.x, gap.y, true);
                start = gap.y;
            }
            CreateWaveSection(a, start, span, false);
            PlaceHazardBelowPlayers(a.View);
            UpdateWaveView(a);
        }

        void CreateWaveSection(AttackState a, float start, float end, bool safe)
        {
            if (end - start <= .001f) return;
            float center = (start + end) * .5f;
            float pixelsX = FloorRect.width / Mathf.Max(.01f, arenaWidthMeters);
            float pixelsY = FloorRect.height / Mathf.Max(.01f, arenaDepthMeters);
            Vector2 position = a.WaveHorizontal ? new Vector2((center - arenaWidthMeters * .5f) * pixelsX, 0f)
                : new Vector2(0f, (arenaDepthMeters * .5f - center) * pixelsY);
            Vector2 size = a.WaveHorizontal ? new Vector2((end - start) * pixelsX, a.Width * pixelsY)
                : new Vector2(a.Width * pixelsX, (end - start) * pixelsY);
            var section = CreateRect(safe ? "Wave Safe Gap" : "Wave Danger Segment", position, size,
                safe ? new Color(.1f, 1f, .55f, .3f) : new Color(1f, .2f, .1f, .6f));
            section.SetParent(a.View, false);
            section.anchoredPosition = position;
            if (safe)
            {
                var vfx = section.gameObject.AddComponent<BossSafeZoneVfx>();
                vfx.Initialize(section, a.WaveSafeVfx.Count); a.WaveSafeVfx.Add(vfx);
                CreateSquareFrame("Gap Border", section, new Color(.35f, 1f, .65f, 1f));
            }
            else
            {
                var vfx = section.gameObject.AddComponent<BossAttackVfx>();
                var color = bossModel != null && bossGlowColorByModel.TryGetValue(bossModel, out var glow) ? glow : Color.red;
                vfx.Initialize(section, floorDisplayCamera, BossAttackVfx.ThemeFor(bossModel != null ? bossModel.name : string.Empty), color, false, true);
                vfx.Configure(BossAttackVfx.AttackForm.Wave, 0, 1, a.WaveHorizontal, a.WaveDir);
                a.WaveDangerVfx.Add(vfx);
            }
        }

        void UpdateWaveView(AttackState a)
        {
            float k = 1f - Mathf.Clamp01(a.ActiveTimer / Mathf.Max(.01f, a.MoveSeconds));
            float span = a.WaveHorizontal ? arenaDepthMeters : arenaWidthMeters;
            a.CurrentWavePosition = a.WaveDir > 0 ? Mathf.Lerp(0f, span + a.Width * .5f, k)
                : Mathf.Lerp(span, -a.Width * .5f, k);
            // Unlike player input, the wave must travel beyond the arena edge without clamping.
            var floor = FloorRect;
            a.View.anchoredPosition = a.WaveHorizontal
                ? new Vector2(floor.center.x, floor.yMax - a.CurrentWavePosition * floor.height / arenaDepthMeters)
                : new Vector2(floor.xMin + a.CurrentWavePosition * floor.width / arenaWidthMeters, floor.center.y);
            foreach (var vfx in a.WaveDangerVfx) vfx.Tick(a.TelegraphTimer, a.TelegraphTotal, a.ActiveTimer);
            foreach (var vfx in a.WaveSafeVfx) vfx.SetState(a.ActiveTimer, a.TelegraphTimer <= 0f);
            UpdateSerpentWave(a);
        }

        bool IsInWave(AttackState a, Vector2 p)
        {
            float along = a.WaveHorizontal ? p.y : p.x;
            if (Mathf.Abs(along - a.CurrentWavePosition) >= a.Width * .5f) return false;
            float across = a.WaveHorizontal ? p.x : p.y;
            foreach (var gap in a.WaveGaps)
                if (across > gap.x && across < gap.y) return false;
            return true;
        }
        bool IsInAttack(AttackState a, Vector2 p)
        {
            if (!IsBehindFireFront(a, p)) return false;
            return a.Shape switch
            {
                Shape.Circle => Vector2.Distance(p, new Vector2(a.X, a.Z)) < a.Radius,
                Shape.Line => Mathf.Abs(p.x - a.X) < a.Width * 0.5f,
                Shape.Zone => a.Side < 0 ? p.x < arenaWidthMeters * .5f : p.x >= arenaWidthMeters * .5f,
                Shape.Wave => IsInWave(a, p),
                Shape.Beam => a.ActiveTimer>0 && InGolemBeam(p,a.BeamStart,a.BeamEnd,a.Width),
                _ => false,
            };
        }

        // 2026-11-25 "ใช้เสียงเมื่อชน gem เป็นอันเดียวกันหมด": one pickup sound for every gem colour.
        void PlayGemSound(SpotState spot)
        {
            sfx?.Play("gem_pickup", 0.5f, SfxPan(spot.WorldPosition.x), 0.075f, 3);
        }

        IEnumerator CollectSpotRoutine(SpotState spot, List<PlayerState> contributors, bool ultimate = false)
        {
            if (ultimate) PlayGemSound(spot);   // ultimate burst gems are taken by touch, with no step before
            JuiceFloor(SpaceJuice.Pickup, WorldToLocal(spot.WorldPosition), Color.white, ultimate ? 165 : 125);   // 2026-11-26: gem particles are white
            int generation = gemFlightGeneration;
            var target = bossModel;
            Vector3 origin = spot.GemFollower != null ? spot.GemFollower.transform.position
                : (spot.View != null ? spot.View.position : Vector3.zero);
            var go = new GameObject("VFX == Collected Gem Flight");
            go.transform.SetParent(transform, false);
            var flight = go.AddComponent<BossGemFlight>();
            bool ready = flight.Initialize(floorDisplayCamera,
                bossDisplayCamera != null ? bossDisplayCamera : wallDisplayCamera, origin, target, spot.Def.Color);
            activeGemFlights.Add(go);
            // 2026-11-25: the gem's whoosh as it flies from the floor up to the boss (gem_impact plays when it lands).
            if (ready) sfx?.Play("gem_flight", .4f, SfxPan(spot.WorldPosition.x), .06f, 2);
            if (spot.View != null) Destroy(spot.View.gameObject);
            if (spot.GemFollower != null) Destroy(spot.GemFollower.gameObject);
            if (ultimate) replayUltPending++;
            try
            {
                float duration = Mathf.Max(0.42f, gemCollectLaunchSeconds) * 2;
                for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
                {
                    if (generation != gemFlightGeneration || !gameStarted || resultShown || target != bossModel) yield break;
                    if (ready && flight != null) flight.Tick(elapsed / duration, Time.deltaTime);
                    yield return null;
                }
                while (Time.timeScale <= 0f) yield return null;
                if (generation != gemFlightGeneration || !gameStarted || resultShown || target != bossModel) yield break;
                if (ready && flight != null) flight.Tick(1, 0);
                replayLastCollectors = ReplayIndexes(contributors);
                // 2026-11-26 "Effect ควันตอน gems โดนบอส": the toon smoke puff (BossFantasyExplosion) is replaced by voxel
                // chips knocked off the boss (white since 2026-11-26); a boss without a voxel skin keeps the puff.
                if (!BossHitChips(Color.white, 16, 1.1f)) SpawnBossExplosionVfx(BossWorldBounds().center, gemCollectImpactScale);
                sfx?.Play("gem_impact", ultimate ? 0.5f : 0.4f, SfxPan(bossX), 0.075f, 2);
                if (ultimate)
                {
                    int damage = Mathf.RoundToInt(ultimateTouchDamage * StunMultiplier() * RushDamageMultiplier(contributors, true));
                    damage = ApplyBossDamage(damage);
                    if (replayUltOpen) replayUltDamage += damage;
                    CreditBossDamage(damage, contributors);
                    CheckBossDefeated();
                }
                else HitBoss(spot.Def, contributors, spot.WorldPosition);
            }
            finally
            {
                activeGemFlights.Remove(go);
                if (go != null) Destroy(go);
                if (ultimate) { replayUltPending--; ReplayTryEndUltimate(); }
            }
        }

        void ClearGemFlights()
        {
            gemFlightGeneration++;
            foreach (var flight in activeGemFlights) if (flight != null) Destroy(flight);
            activeGemFlights.Clear();
        }

        void OnDisable()
        {
            ReplayEnd("unloaded");
            if (ultimateImpactFrame != null) ultimateImpactFrame.Clear();
            ClearGolemUltimateShake();
            if (passiveFeedback != null) passiveFeedback.Hide();
            sfx?.StopCombat();
            sfx?.Score?.StopMusic();
            ClearFantasyExplosions();
            ClearGemFlights();
            if (elementalChargeVfx != null) elementalChargeVfx.Stop();
            if (modelElementVfx != null) modelElementVfx.Clear();
        }

        // Phase 2 lands at exactly 50% HP (บาส 2026-09-29): a hit that would cross the halfway mark
        // stops at it, and the boss takes no damage from then until its transformation has finished
        // (Demon → Yaksha King, Dragon → Inferno incl. its pre-transform signature, Golem → Crimson).
        // Exception (บาส): a stun or team ultimate the players already earned plays out in full — damage
        // keeps landing past 50% during it (never below 1 HP, so phase 2 can't be skipped), and the
        // immunity + transformation start once it ends.
        bool BossHasUnreachedPhaseTwo() =>
            (passives.Active == BossPassiveState.Kind.Demon && !passives.DemonAdult)
            || (passives.Active == BossPassiveState.Kind.Dragon && !passives.DragonHeated)
            || (bossModel != null && !golemRedPhase && bossModel.TryGetComponent<BossGolemAnimation>(out _))
            || (IsAbyssalKingActive && !passives.KingAwakened);

        bool PlayerDamageWindowActive => stunTimer > 0f || ultimateActive;

        bool BossPhaseTransitionInvulnerable() =>
            bossHp > 0 && !PlayerDamageWindowActive && (passives.DemonPhasePending || passives.DragonPhasePending || golemPhasePending || kingPhasePending);

        int BossPhaseTwoHp => Mathf.RoundToInt(bossMaxHp * .5f);
        float phaseShiftImmuneNoticeTime = -10f;

        int ApplyBossDamage(int damage, string source = "Ultimate")
        {
            if (damage <= 0 || bossHp <= 0) return 0;
            if (BossPhaseTransitionInvulnerable())
            {
                if (Time.time - phaseShiftImmuneNoticeTime > 1.5f) { phaseShiftImmuneNoticeTime = Time.time; passiveFeedback?.Notify("PHASE SHIFT - IMMUNE"); }
                return 0;
            }
            int previousHp = bossHp;
            if (juicePhase < 0) juicePhase = BossPhase();
            float multiplier = passives.IncomingMultiplier(source, stunTimer > 0f);
            var hydra = bossModel != null && enableBossPassives ? bossModel.GetComponent<BossHydra>() : null;
            if (hydra != null) multiplier *= BossPassiveState.HydraVulnerability(hydra.ActiveHeads);
            bossHp = Mathf.Max(0, bossHp - Mathf.Max(1, Mathf.RoundToInt(damage * multiplier)));
            if (BossHasUnreachedPhaseTwo())
            {
                if (PlayerDamageWindowActive) bossHp = Mathf.Max(1, bossHp);
                else if (previousHp > BossPhaseTwoHp && bossHp < BossPhaseTwoHp) bossHp = BossPhaseTwoHp;
            }
            UpdateDragonStage();ObserveGolemHealth();ObserveAbyssalKingHealth();
            if (bossModel != null && enableBossPassives)
                bossModel.GetComponent<BossHydra>()?.ReceiveDamage(previousHp - bossHp, bossHp, bossMaxHp);
            if (multiplier < 1f) passiveFeedback?.Notify("DAMAGE BLOCKED " + Mathf.RoundToInt((1f - multiplier) * 100f) + "%");
            else if (multiplier > 1f) passiveFeedback?.Notify("WEAK POINT! +" + Mathf.RoundToInt((multiplier - 1f) * 100f) + "% DAMAGE");
            JuiceBossDamage(previousHp - bossHp, multiplier > 1f, source);
            return previousHp - bossHp;
        }

        int damageCreditRemainder;
        void CreditBossDamage(int damage, List<PlayerState> contributors)
        {
            if (damage <= 0 || contributors.Count == 0) return;
            int share = damage / contributors.Count;
            int remainder = damage % contributors.Count;
            for (int i = 0; i < contributors.Count; i++)
                contributors[i].Damage += share;
            // Rotate indivisible points so list order does not always favor P1.
            for (int i = 0; i < remainder; i++)
                contributors[(damageCreditRemainder + i) % contributors.Count].Damage++;
            damageCreditRemainder = (damageCreditRemainder + remainder) % contributors.Count;
        }

        void HitBoss(SpotDef def, List<PlayerState> contributors, Vector2 at = default)
        {
            // Join Forces (Balance Note v3 §5.2): a gem shared by 2+ players gives 2.5x (evolved 3x); a blue one counts as 2 (3).
            float duo = RushDuoFactor(contributors);
            float duoEffect = def.Ultimate > 0 ? 1f : duo;
            int damage = Mathf.RoundToInt(def.Damage * duoEffect * StunMultiplier() * RushDamageMultiplier(contributors, true));
            damage = ApplyBossDamage(damage, def.GemModelName);
            CreditBossDamage(damage, contributors);
            foreach (var p in contributors) UpdatePlayerHp(p);

            if (def.Heal > 0)
            {
                ReplayHeal(contributors);
                sfx?.Play("heal", 0.42f, 0, 0.15f, 2);
                foreach (var p in players)
                {
                    if (p.DownTimer > 0f) continue;
                    int beforeHeal = p.Hp;
                    p.Hp = Mathf.Min(playerMaxHp, p.Hp + Mathf.RoundToInt(def.Heal * duo));
                    if (p.Hp > beforeHeal && p.Cursor != null && p.Cursor.gameObject.activeInHierarchy) JuiceFloor(SpaceJuice.Pickup, p.Cursor.anchoredPosition, new Color(.25f, 1f, .55f));
                    UpdatePlayerHp(p);
                }
            }

            if (def.Stun > 0 && stunTimer <= 0f && !BossPhaseTransitionInvulnerable()) // no stun build-up while immune: a stun here would only delay the transformation
            {
                stunGauge += RushStunGain(def.Stun) * duo;
                if (stunGauge >= stunNeed)
                {
                    stunGauge = 0f;
                    stunTimer = stunSeconds;
                    bool broke = BreakSignatureWindup("stun");
                    int cancelled = CancelPendingAttacksOnStun(out bool cancelledUltimate);
                    ReplayLog.Event("stun", 3, 3, ReplayIndexes(contributors), ReplayLog.Data().Add("boss", ReplayBoss())
                        .Add("cancelledAttacks", cancelled).Add("cancelledUltimate", cancelledUltimate));
                    if (ultimateImpactFrame != null) ultimateImpactFrame.Clear();
            ClearGolemUltimateShake();
                    passives.OnStun();
                    if (bossModel != null) { bossModel.GetComponent<BossModelAnimation>()?.Stun(stunSeconds); bossModel.GetComponent<BossHydra>()?.Stun(stunSeconds); bossModel.GetComponent<BossAbyssalKing>()?.Stun(stunSeconds); }
                    sfx?.Play("stun", 0.6f, 0, 0.2f, 4);
                    if (!broke) JuiceMilestone("STUN!", new Color(.4f, .85f, 1f));
                    ActiveBossSkin?.Stun(stunSeconds);
                }
            }

            RushOnGemHit(def, contributors, at);
            if (CheckBossDefeated()) return;
            if (def.Ultimate <= 0) return;
            ultimateCharge += Mathf.FloorToInt(duo);
            if (ultimateCharge < UltimateNeed) return;

            ultimateCharge = 0;
            ReplayUltimateStart(contributors);
            // Credit the collectors who triggered the ultimate, using actual HP removed.
            int replayOpener = ApplyBossDamage(Mathf.RoundToInt(ultimateOpenDamage * RushDamageMultiplier(contributors)));
            replayUltDamage += replayOpener;
            CreditBossDamage(replayOpener, contributors);
            if (CheckBossDefeated()) return;
            BreakSignatureWindup("ultimate");
            ClearAttacks();
            SpawnUltimateSpots();
        }

        // bigMove: the hit came from a big move (IsBossUltimate), 35 per beat in Balance Note v3 §3.3 instead of 18.
        void HurtPlayer(PlayerState p, bool bigMove = false)
        {
            if (Time.timeScale <= 0f || p.DownTimer > 0f || !TryGetInputLocalPoint(p.Index, out _)) return;
            int baseDamage = bigMove ? signatureHitDamage : bossHitDamage;
            p.Hp -= Mathf.RoundToInt(baseDamage * passives.OutgoingDamageMultiplier(bossHp / (float)Mathf.Max(1, bossMaxHp)) * RushIncomingMultiplier());
            JuiceFloor(SpaceJuice.Hurt, p.Cursor.anchoredPosition, new Color(1f, .18f, .16f, .9f), p.Hp <= 0 ? 190 : 125);
            sfx?.Play(p.Hp <= 0 ? "player_down" : "player_hit", 0.6f,
                SfxPan(LocalToWorld(p.Cursor.anchoredPosition).x), 0.12f, 4);
            if (p.Hp > 0 && p.Hp <= playerMaxHp * 0.25f) sfx?.Play("low_health", 0.4f, 0, 2f, 3);
            if (p.Hp <= 0)
            {
                p.Hp = 0;
                p.DownTimer = downTimeSeconds;
                p.Cursor.gameObject.SetActive(false);
                ReplayPlayerDown(p);
            }
            UpdatePlayerHp(p);
        }

        List<PlayerState> PlayersInside(Vector2 worldPosition, float radius, List<PlayerState> result)
        {
            result.Clear();
            float radiusSq = radius * radius;
            foreach (var p in players)
            {
                if (Time.timeScale > 0f && p.DownTimer <= 0f && TryGetInputLocalPoint(p.Index, out _) && (LocalToWorld(p.Cursor.anchoredPosition) - worldPosition).sqrMagnitude < radiusSq) result.Add(p);
            }
            return result;
        }

        void UpdateHudThrottled(float dt)
        {
            hudRefreshTimer -= dt;
            if (hudRefreshTimer > 0f) return;

            hudRefreshTimer = 0.08f;
            UpdateHud();
        }

        void UpdateHud()
        {
            UpdateBossSignatureBanner();
            SetHpBar(bossHpSlider, bossHpScrollbar, bossHpFill, bossHp / (float)Mathf.Max(1, bossMaxHp));
            SetHpBar(null, null, stunGaugeFill, stunGauge / Mathf.Max(1, stunNeed));
            SetHpBar(null, null, ultimateGaugeFill, ultimateCharge / (float)Mathf.Max(1, UltimateNeed));
            if (bossHpText != null) bossHpText.text = $"{bossHp:n0} / {bossMaxHp:n0}";
            if (stunGaugeText != null) stunGaugeText.text = $"{Mathf.RoundToInt(stunGauge)} / {stunNeed}";
            if (ultimateGaugeText != null) ultimateGaugeText.text = $"{ultimateCharge} / {UltimateNeed}";
            UpdateBossWallHud();   // บาส 2026-11-18: the art team's wall HUD (WallBars.cs)
            if (statusText == null || bossHp <= 0) return;

            string sessionPrefix = sessionActive && sessionBossTargetCount > 1 ? $"Boss {sessionBossIndex}/{sessionBossTargetCount} | " : string.Empty;
            // บาส 2026-11-04: in the rush sessionBossTargetCount is the 30-boss cap ("Boss 1/30"): count within the half instead
            if (IsRushActive) sessionPrefix = IsAbyssalKingActive ? "Final Boss | " : $"Boss {Mathf.Clamp(rushHalfKills + 1, 1, Mathf.Max(1, rushBossesPerHalf))}/{Mathf.Max(1, rushBossesPerHalf)} | ";
            statusText.text = stunTimer > 0f
                ? $"{sessionPrefix}บอสสตัน! ดาเมจ +{stunBonusPercent:0}% ({Mathf.CeilToInt(stunTimer)} วิ)"
                : ultimateActive
                    ? $"{sessionPrefix}โหมดอัลติ! {Mathf.CeilToInt(ultimateTimer)} วิ · เหลือ {spots.Count} จุด"
                    : $"{sessionPrefix}เกจสตัน {Mathf.RoundToInt(stunGauge)}/{stunNeed} · อัลติ {ultimateCharge}/{UltimateNeed} · เฟส {BossPhase()}";

        }

        bool CheckBossDefeated()
        {
            if (bossHp > 0) return false;
            ShowResult();
            return true;
        }

        void ShowResult()
        {
            if (resultShown) return;

            sfx?.StopCombat();
            ReplayBossKilled();
            ClearGemFlights();
            resultShown = true;
            RushOnBossKilled();
            if (bossModel != null) bossModel.GetComponent<BossModelAnimation>()?.Defeat();
            if (bossModel != null) bossModel.GetComponent<BossWraithAnimation>()?.StopMist();
            if (bossModel != null) bossModel.GetComponent<BossSerpentAnimation>()?.StopWater();
            ultimateActive = false;
            ReplayTryEndUltimate();
            ClearAttacks();
            ClearSpots();
            StartCoroutine(BossDeathSequenceRoutine());
        }

        // Replace the defeated silhouette with elemental voxel fragments before advancing the session.
        IEnumerator BossDeathSequenceRoutine()
        {
            sfx?.Play("boss_defeat", 0.65f, 0, 0.3f, 5);
            float duration=Mathf.Max(0,bossDeathResultDelaySeconds);
            var dragon=bossModel!=null?bossModel.GetComponent<BossDragonAnimation>():null;
            if(ActiveBossSkin!=null){yield return SkinDeathRoutine(ActiveBossSkin);duration=0;}
            else if(bossModel!=null && bossModel.TryGetComponent<BossHydra>(out var hydra))
            {
                hydra.BeginDeathPose();
                hydra.ManualDeathPlayback=true;
                try
                {
                    while(hydra.DeathAge<BossHydra.DeathPoseSeconds)
                    {hydra.Advance(Mathf.Min(Time.deltaTime,BossHydra.DeathPoseSeconds-hydra.DeathAge));yield return null;}
                    PlayBossDefeatBurst();
                    yield return new WaitForSeconds(BossDeathVfx.Duration);
                }
                finally{hydra.ManualDeathPlayback=false;}
                duration=Mathf.Max(0,duration-BossHydra.DeathPoseSeconds-BossDeathVfx.Duration);
            }
            else if(dragon!=null)
            {
                dragon.BeginDeathPose();
                for(float age=0;age<BossDragonAnimation.DeathPoseSeconds;age+=Time.deltaTime)
                { dragon.SampleDeathPose(age);yield return null; }
                dragon.SampleDeathPose(BossDragonAnimation.DeathPoseSeconds);
                PlayBossDefeatBurst();
                for(float age=0;age<BossDeathVfx.Duration;age+=Time.deltaTime)
                { dragon.SampleDeathDissolve(age/BossDeathVfx.Duration);yield return null; }
                dragon.SampleDeathDissolve(1);
                duration=Mathf.Max(0,duration-BossDragonAnimation.DeathPoseSeconds-BossDeathVfx.Duration);
            }

            else if(bossModel!=null && bossModel.TryGetComponent<BossSerpentAnimation>(out var serpent))
            {
                serpent.BeginDeathPose();
                for(float age=0;age<BossSerpentAnimation.DeathPoseSeconds;age+=Time.deltaTime){serpent.SampleDeathPose(age);yield return null;}
                serpent.SampleDeathPose(BossSerpentAnimation.DeathPoseSeconds);
                PlayBossDefeatBurst();
                for(float age=0;age<BossDeathVfx.Duration;age+=Time.deltaTime){serpent.SampleDeathDissolve(age/BossDeathVfx.Duration);yield return null;}
                serpent.SampleDeathDissolve(1);
                duration=Mathf.Max(0,duration-BossSerpentAnimation.DeathPoseSeconds-BossDeathVfx.Duration);
            }
            else if(bossModel!=null && bossModel.TryGetComponent<BossSentinelAnimation>(out var sentinel))
            {
                sentinel.BeginDeathPose();
                for(float age=0;age<BossSentinelAnimation.DeathPoseSeconds;age+=Time.deltaTime){sentinel.SampleDeathPose(age);yield return null;}
                sentinel.SampleDeathPose(BossSentinelAnimation.DeathPoseSeconds);
                PlayBossDefeatBurst();
                for(float age=0;age<BossDeathVfx.Duration;age+=Time.deltaTime){sentinel.SampleDeathDissolve(age/BossDeathVfx.Duration);yield return null;}
                sentinel.SampleDeathDissolve(1);
                duration=Mathf.Max(0,duration-BossSentinelAnimation.DeathPoseSeconds-BossDeathVfx.Duration);
            }
            else if(bossModel!=null && bossModel.TryGetComponent<BossGolemAnimation>(out var golem))
            {
                golem.BeginDeathPose();
                for(float age=0;age<BossGolemAnimation.DeathPoseSeconds;age+=Time.deltaTime)
                { golem.SampleDeathPose(age);yield return null; }
                golem.SampleDeathPose(BossGolemAnimation.DeathPoseSeconds);
                PlayBossDefeatBurst();
                for(float age=0;age<BossDeathVfx.Duration;age+=Time.deltaTime)
                { golem.SampleDeathDissolve(age/BossDeathVfx.Duration);yield return null; }
                golem.SampleDeathDissolve(1);
                duration=Mathf.Max(0,duration-BossGolemAnimation.DeathPoseSeconds-BossDeathVfx.Duration);
            }
            else if(bossModel!=null && bossModel.TryGetComponent<BossDemonAnimation>(out var demon))
            {
                demon.BeginDeathPose();
                for(float age=0;age<BossDemonAnimation.DeathPoseSeconds;age+=Time.deltaTime)
                { demon.SampleDeathPose(age);yield return null; }
                demon.SampleDeathPose(BossDemonAnimation.DeathPoseSeconds);
                PlayBossDefeatBurst();
                for(float age=0;age<BossDeathVfx.Duration;age+=Time.deltaTime)
                { demon.SampleDeathDissolve(age/BossDeathVfx.Duration);yield return null; }
                demon.SampleDeathDissolve(1);
                duration=Mathf.Max(0,duration-BossDemonAnimation.DeathPoseSeconds-BossDeathVfx.Duration);
            }
            else if(bossModel!=null && bossModel.TryGetComponent<BossJellyfishAnimation>(out var jelly))
            {
                jelly.BeginDeathPose();
                for(float age=0;age<BossJellyfishAnimation.DeathPoseSeconds;age+=Time.deltaTime)
                { jelly.SampleDeathPose(age);yield return null; }
                jelly.SampleDeathPose(BossJellyfishAnimation.DeathPoseSeconds);
                PlayBossDefeatBurst();
                for(float age=0;age<BossDeathVfx.Duration;age+=Time.deltaTime)
                { jelly.SampleDeathDissolve(age/BossDeathVfx.Duration);yield return null; }
                jelly.SampleDeathDissolve(1);
                duration=Mathf.Max(0,duration-BossJellyfishAnimation.DeathPoseSeconds-BossDeathVfx.Duration);
            }
            else if(bossModel!=null && bossModel.TryGetComponent<BossPlesiothAnimation>(out var fish))
            {
                fish.BeginDeathPose();
                for(float age=0;age<BossPlesiothAnimation.DeathPoseSeconds;age+=Time.deltaTime)
                { fish.SampleDeathPose(age);yield return null; }
                fish.SampleDeathPose(BossPlesiothAnimation.DeathPoseSeconds);
                PlayBossDefeatBurst();
                for(float age=0;age<BossDeathVfx.Duration;age+=Time.deltaTime)
                { fish.SampleDeathDissolve(age/BossDeathVfx.Duration);yield return null; }
                fish.SampleDeathDissolve(1);
                duration=Mathf.Max(0,duration-BossPlesiothAnimation.DeathPoseSeconds-BossDeathVfx.Duration);
            }
            else if(bossModel!=null && bossModel.TryGetComponent<BossAbyssalKing>(out var abyssalKing))
            {
                yield return AbyssalKingDeathRoutine(abyssalKing);
                duration=Mathf.Max(0,duration-BossAbyssalKing.DeathPoseSeconds-BossDeathVfx.Duration);
            }
            else if(bossModel!=null && bossDeathExplosionBursts>0)
            {
                PlayBossDefeatBurst();
                duration=Mathf.Max(duration,BossDeathVfx.Duration);
            }
            if(duration>0)yield return new WaitForSeconds(duration);
            while(deathVfx!=null&&deathVfx.IsPlaying)yield return null;
            SetBossVisible(false);
            yield return ShowBossVictory();
            if (IsRushActive) { yield return RushAfterBossDefeated(); yield break; }
            if (HasNextSessionBoss())
            {
                sfx?.Play("round_clear", 0.55f, 0, 0.2f, 4);
                if (statusText != null) statusText.text = $"BOSS {sessionBossIndex} DOWN! NEXT BOSS...";
                UpdateHud();
                yield return PlayNextBossTransition();
                yield break;
            }

            ShowSessionResultsScreen();
        }

        void ShowSessionResultsScreen()
        {
            sessionActive = false;
            // Victory fanfare already played on the per-boss victory card.
            sfx?.Score?.Result();
            if (statusText != null) statusText.text = "BOSS DOWN!";
            UpdateHud();

            if (gameplayResultPanel != null)
            {
                gameplayResultPanel.gameObject.SetActive(true);
                gameplayResultPanel.SetAsLastSibling();
            }

            PlayerState mvp = null;
            foreach (var player in players)
            {
                if (mvp == null || player.Damage > mvp.Damage) mvp = player;
            }

            UpdateResultScoreboard(gameplayPlayerScorePanel);
            // 2026-11-26 "Result ... ลบ UI อันเก่าทิ้งเลย": the wall's old result (Panel == Result: MVP plate + player plates)
            // stays hidden; RAID COMPLETE, then the BOSS DEFEATED panel from the art team's mockups shows instead (RaidResult.cs).
            ShowRaidResult();
            ShowRushResultLayout(mvp); // 2026-11-26: every session hides the floor result UI; the wall shows the result (RushResult.cs)
            ReplaySessionCompleted(mvp);
            ReportArenaScores(mvp);   // scores to the Fortal launcher (.Arena.cs)
        }

        bool HasNextSessionBoss()
        {
            return sessionActive && sessionBossIndex < sessionBossTargetCount;
        }

        IEnumerator PlayNextBossTransition()
        {
            PlayTransitionSound();
            yield return TweenSessionTransitionAlpha(1f, sessionTransitionFadeSeconds);
            HideNextBossCards();
            ShowBossSelection();
            yield return TweenSessionTransitionAlpha(0f, sessionTransitionFadeSeconds);
        }

        void EnsureSessionTransitionOverlays()
        {
            gameplaySessionTransitionOverlay = EnsureSessionTransitionOverlay(gameplaySessionTransitionOverlay, playArea, "Gameplay Session Transition Fade");
            cutsceneSessionTransitionOverlay = EnsureSessionTransitionOverlay(cutsceneSessionTransitionOverlay, cutsceneArea, "Cutscene Session Transition Fade");
            var font = statusText != null ? statusText.font : null;
            if (floorNextBossCard == null && gameplaySessionTransitionOverlay != null)
                floorNextBossCard = BossNextBossCard.Create(gameplaySessionTransitionOverlay.rectTransform, font);
            if (wallNextBossCard == null && cutsceneSessionTransitionOverlay != null)
                wallNextBossCard = BossNextBossCard.Create(cutsceneSessionTransitionOverlay.rectTransform, font);
        }

        Image EnsureSessionTransitionOverlay(Image current, RectTransform parent, string name)
        {
            if (current != null) return current;
            if (parent == null) return null;

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();

            var image = go.GetComponent<Image>();
            image.color = TransitionColorWithAlpha(0f);
            image.raycastTarget = false;
            return image;
        }

        IEnumerator TweenSessionTransitionAlpha(float targetAlpha, float seconds)
        {
            EnsureSessionTransitionOverlays();
            if (gameplaySessionTransitionOverlay != null) gameplaySessionTransitionOverlay.rectTransform.SetAsLastSibling();
            if (cutsceneSessionTransitionOverlay != null) cutsceneSessionTransitionOverlay.rectTransform.SetAsLastSibling();

            float startAlpha = CurrentSessionTransitionAlpha();
            float duration = Mathf.Max(0.01f, seconds);
            bool completed = false;
            KillSessionTransitionTween();
            sessionTransitionTween = DOTween.To(
                    () => startAlpha,
                    value => SetSessionTransitionAlpha(value),
                    Mathf.Clamp01(targetAlpha),
                    duration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => completed = true);

            while (!completed) yield return null;
            sessionTransitionTween = null;
        }

        float CurrentSessionTransitionAlpha()
        {
            if (cutsceneSessionTransitionOverlay != null) return cutsceneSessionTransitionOverlay.color.a;
            if (gameplaySessionTransitionOverlay != null) return gameplaySessionTransitionOverlay.color.a;
            return 0f;
        }

        void SetSessionTransitionAlpha(float alpha)
        {
            if (alpha <= 0f) HideNextBossCards();
            var color = TransitionColorWithAlpha(alpha);
            if (gameplaySessionTransitionOverlay != null) gameplaySessionTransitionOverlay.color = color;
            if (cutsceneSessionTransitionOverlay != null) cutsceneSessionTransitionOverlay.color = color;
        }

        void KillSessionTransitionTween()
        {
            if (sessionTransitionTween == null) return;
            if (sessionTransitionTween.IsActive()) sessionTransitionTween.Kill();
            sessionTransitionTween = null;
        }

        Color TransitionColorWithAlpha(float alpha)
        {
            var color = sessionTransitionColor;
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        // Encapsulates every Renderer under bossModel into one world-space Bounds
        // — the only reliable way to know "how big is the boss, right now, in
        // real world units" given different bosses (dragon, demon, ...) can have
        // wildly different geometry/import scale. Falls back to a small sphere
        // around the model's own position if it somehow has no renderers yet.
        Bounds BossWorldBounds()
        {
            if (bossModel == null) return new Bounds(Vector3.zero, Vector3.one);
            var renderers = activeBossRenderers;
            if (renderers == null || renderers.Length == 0)
            {
                CacheActiveBossRenderers();
                renderers = activeBossRenderers;
            }
            if (renderers.Length == 0) return new Bounds(bossModel.position, Vector3.one);

            int first = -1;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                first = i;
                break;
            }
            if (first < 0) return new Bounds(bossModel.position, Vector3.one);

            var bounds = renderers[first].bounds;
            for (int i = first + 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null) bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        void CacheActiveBossRenderers()
        {
            activeBossRenderers = bossModel != null
                ? (ActiveBossSkin != null ? new Renderer[] { ActiveBossSkin.body } : bossModel.GetComponentsInChildren<Renderer>(true)) // framing must ignore the hidden old model and the skin's effects
                : new Renderer[0];
        }

        // Spawns directly in world space, deliberately NOT through
        // Visual3DVfx.SpawnOneShot's RectTransform/anchoredPosition API — that
        // API assumes the UI-canvas coordinate space DeepspaceGameManager's own
        // 3D-via-2D-rect followers share, which bossModel does not (see
        // BossDeathSequenceRoutine's comment). No parent, so it's plain world
        // position, matching how bossModel itself is positioned.
        void SpawnBossExplosionVfx(Vector3 worldPosition, float extraScale = 1f)
        {
            var camera = bossDisplayCamera != null ? bossDisplayCamera : wallDisplayCamera;
            if (bossModel != null && camera != null)
            {
                BossFantasyExplosion burst = fantasyExplosions.Find(item => item != null && !item.IsPlaying);
                if (burst == null && fantasyExplosions.Count < 20)
                {
                    var go = new GameObject("VFX == Toon boss explosion");
                    go.transform.SetParent(transform, false);
                    burst = go.AddComponent<BossFantasyExplosion>();
                    fantasyExplosions.Add(burst);
                }
                if (burst == null) burst = fantasyExplosions[explosionCursor++ % fantasyExplosions.Count];
                var bounds = BossWorldBounds();
                float radius = BossDivineHalo.RadiusForBounds(bounds, camera, activeBossRenderers);
                var accent = bossGlowColorByModel.TryGetValue(bossModel, out var tint) ? tint : Color.white;
                // Keep impacts in front of the body, using camera depth without changing screen position.
                float depth = Vector3.Dot(worldPosition - bounds.center, camera.transform.forward);
                worldPosition -= camera.transform.forward * Mathf.Max(0, depth + bounds.extents.magnitude + .05f);
                if (burst.Play(worldPosition, camera, accent, AudioTheme, radius * Mathf.Clamp(extraScale, .15f, 3f), extraScale > 1.5f)) return;
            }
            if (explosionVfx == null) return;

            var clone = Instantiate(explosionVfx);
            clone.gameObject.SetActive(true);
            Visual3DFollower.SetLayerRecursive(clone.gameObject, Visual3DFollower.BossLayer);
            clone.SetPositionAndRotation(worldPosition, explosionVfx.rotation);
            clone.localScale = explosionVfx.localScale * extraScale;
            Destroy(clone.gameObject, 2f);
        }

        void ClearFantasyExplosions()
        {
            ClearCombatJuice();
            HideBossVictory();
            if(deathVfx!=null)deathVfx.Stop();
            foreach (var burst in fantasyExplosions) if (burst != null) burst.Stop();
        }

        // One elemental cast cue per attack, independent of the number of floor hazards.
        void PlayAttackChargeVfx()
        {
            if (bossModel == null) return;
            sfx?.StartCharge(SoundTheme, telegraphSeconds);
            if (elementalChargeVfx == null)
            {
                var go = new GameObject("VFX == Boss elemental charge");
                go.layer = Visual3DFollower.BossLayer;
                go.transform.SetParent(transform, false);
                elementalChargeVfx = go.AddComponent<BossChargeVfx>();
            }
            var accent = bossGlowColorByModel.TryGetValue(bossModel, out var chargeColor) ? chargeColor : Color.white;
            if (elementalChargeVfx.Play(bossModel, bossDisplayCamera != null ? bossDisplayCamera : wallDisplayCamera, activeBossRenderers,
                accent, telegraphSeconds, attackChargeVfxScale)) return;
            // Preserve the authored cue as a fallback if a display or shader is unavailable.
            if (useLightweightVoidAttackVfx && IsVoidWraithBoss())
            {
                SpawnLightweightVoidAttackVfx(BossWorldBounds().center);
                return;
            }

            if (!attackChargeVfxByBoss.TryGetValue(bossModel, out var config) || config.Template == null) return;

            if (config.BurstCount <= 1)
            {
                SpawnAttackChargeVfxInstance(config.Template, BossWorldBounds().center);
            }
            else
            {
                ScatterAttackChargeVfx(config);
            }
        }

        // Random position per clone across the wall camera's full view
        // (orthographic half-width/half-height projected through its own
        // right/up axes), all sitting at the boss's own depth along the
        // camera's forward axis — same "wall-relative" trick
        // BossDeathSequenceRoutine uses for its burst sweep, just spread over
        // the whole display instead of one boss-sized cluster. 0.92 keeps
        // clones just inside the visible edge rather than clipping off it.
        // Falls back to a wide spread around the boss itself if there's no
        // wall camera to measure against (shouldn't normally happen —
        // ConfigureBossDisplayCameras always tries to find one).
        void ScatterAttackChargeVfx(AttackChargeVfx config)
        {
            Bounds bounds = BossWorldBounds();
            Vector3 right = Vector3.right;
            Vector3 up = Vector3.up;
            Vector3 wallCenter = bounds.center;
            float halfWidth = bounds.extents.magnitude * 3f;
            float halfHeight = bounds.extents.magnitude * 2f;

            var scatterCamera = bossDisplayCamera != null ? bossDisplayCamera : wallDisplayCamera; // the one the boss is drawn by (it may be shifted)
            if (scatterCamera != null)
            {
                Transform camT = scatterCamera.transform;
                right = camT.right;
                up = camT.up;
                float depth = Vector3.Dot(bounds.center - camT.position, camT.forward);
                wallCenter = camT.position + camT.forward * depth;
                if (scatterCamera.orthographic)
                {
                    halfHeight = scatterCamera.orthographicSize;
                    halfWidth = halfHeight * scatterCamera.aspect;
                }
            }

            for (int i = 0; i < config.BurstCount; i++)
            {
                Vector3 position = wallCenter
                    + right * Random.Range(-halfWidth, halfWidth) * 0.92f
                    + up * Random.Range(-halfHeight, halfHeight) * 0.92f;
                SpawnAttackChargeVfxInstance(config.Template, position);
            }
        }

        // Same world-space, unparented, template-scale approach as
        // SpawnBossExplosionVfx (see its own comment for why: parenting to
        // bossModel would inherit whatever oddball import scale that particular
        // boss has, see DiscoverBossModelCandidates). Lives for exactly
        // telegraphSeconds so it clears right as the attack actually lands —
        // if a specific case overrides its own delay longer than that (e.g.
        // "ระเบิดวงกว้าง" at telegraphSeconds * 1.35f), the charge cue still
        // ends on the base telegraph; that's intentional, it reads as "boss
        // finished casting," not "attack is now safe."
        void SpawnAttackChargeVfxInstance(Transform template, Vector3 worldPosition)
        {
            var clone = poolAttackChargeVfx ? GetAttackChargeVfxInstance(template) : Instantiate(template);
            if (clone == null) return;
            clone.gameObject.SetActive(true);
            Visual3DFollower.SetLayerRecursive(clone.gameObject, Visual3DFollower.BossLayer);
            clone.SetPositionAndRotation(worldPosition, template.rotation);
            clone.localScale = template.localScale * attackChargeVfxScale;

            PlayVfxGraphAndParticles(clone);

            if (poolAttackChargeVfx)
            {
                if (!activeAttackChargeVfx.Contains(clone)) activeAttackChargeVfx.Add(clone);
                StartCoroutine(ReleaseAttackChargeVfx(clone, telegraphSeconds));
            }
            else
            {
                Destroy(clone.gameObject, telegraphSeconds);
            }
        }

        Transform GetAttackChargeVfxInstance(Transform template)
        {
            if (template == null) return null;
            if (!attackChargeVfxPool.TryGetValue(template, out var pool))
            {
                pool = new Queue<Transform>();
                attackChargeVfxPool[template] = pool;
            }

            while (pool.Count > 0)
            {
                var pooled = pool.Dequeue();
                if (pooled != null) return pooled;
            }

            var clone = Instantiate(template);
            clone.gameObject.SetActive(false);
            attackChargeVfxTemplateByClone[clone] = template;
            return clone;
        }

        IEnumerator ReleaseAttackChargeVfx(Transform clone, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnAttackChargeVfx(clone);
        }

        void ReturnAttackChargeVfx(Transform clone)
        {
            if (clone == null) return;

            StopVfxGraphAndParticles(clone);
            clone.gameObject.SetActive(false);
            activeAttackChargeVfx.Remove(clone);

            if (!attackChargeVfxTemplateByClone.TryGetValue(clone, out var template) || template == null)
            {
                Destroy(clone.gameObject);
                return;
            }

            if (!attackChargeVfxPool.TryGetValue(template, out var pool))
            {
                pool = new Queue<Transform>();
                attackChargeVfxPool[template] = pool;
            }

            if (pool.Count < Mathf.Max(0, maxPooledAttackChargeVfxPerTemplate))
            {
                pool.Enqueue(clone);
            }
            else
            {
                attackChargeVfxTemplateByClone.Remove(clone);
                Destroy(clone.gameObject);
            }
        }

        void ClearAttackChargeVfx()
        {
            sfx?.StopCharge();
            if (elementalChargeVfx != null) elementalChargeVfx.Stop();
            if (modelElementVfx != null) modelElementVfx.Clear();
            for (int i = activeAttackChargeVfx.Count - 1; i >= 0; i--)
            {
                ReturnAttackChargeVfx(activeAttackChargeVfx[i]);
            }
            activeAttackChargeVfx.Clear();
        }

        static void PlayVfxGraphAndParticles(Transform root)
        {
            if (root == null) return;
            foreach (var vfx in root.GetComponentsInChildren<VisualEffect>(true)) vfx.Play();
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }

        static void StopVfxGraphAndParticles(Transform root)
        {
            if (root == null) return;
            foreach (var vfx in root.GetComponentsInChildren<VisualEffect>(true)) vfx.Stop();
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        bool IsVoidWraithBoss()
        {
            return bossModel != null && bossModel.name.Contains("VoidWraithBoss");
        }

        void SpawnLightweightVoidAttackVfx(Vector3 worldPosition)
        {
            var ps = GetLightweightVoidAttackVfx();
            var go = ps.gameObject;
            go.SetActive(true);
            go.layer = Visual3DFollower.BossLayer;
            go.transform.position = worldPosition;
            go.transform.localScale = Vector3.one * lightweightVoidAttackVfxScale;
            if (!activeLightweightVoidAttackVfx.Contains(ps)) activeLightweightVoidAttackVfx.Add(ps);

            var main = ps.main;
            main.duration = Mathf.Max(0.25f, telegraphSeconds);
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.55f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.48f, 0.25f, 1f, 0.9f), new Color(0.05f, 0f, 0.16f, 0.78f));
            main.maxParticles = Mathf.Clamp(lightweightVoidAttackVfxParticles, 8, 128);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(lightweightVoidAttackVfxParticles, 8, 128)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.25f;
            shape.radiusThickness = 0.35f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.radial = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.7f, 0.45f, 1f), 0f),
                    new GradientColorKey(new Color(0.2f, 0.02f, 0.48f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.9f, 0.15f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.65f, 1f, 1.6f));

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var material = GetLightweightVoidAttackVfxMaterial();
            if (material != null) renderer.material = material;

            ps.Play();
            StartCoroutine(ReleaseLightweightVoidAttackVfx(ps, telegraphSeconds + 1f));
        }

        ParticleSystem GetLightweightVoidAttackVfx()
        {
            while (lightweightVoidAttackVfxPool.Count > 0)
            {
                var pooled = lightweightVoidAttackVfxPool.Dequeue();
                if (pooled != null) return pooled;
            }

            var go = new GameObject("FX_AttackChargeVoidWraith_Lightweight(runtime)", typeof(ParticleSystem));
            go.SetActive(false);
            return go.GetComponent<ParticleSystem>();
        }

        IEnumerator ReleaseLightweightVoidAttackVfx(ParticleSystem ps, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (ps == null) yield break;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);
            activeLightweightVoidAttackVfx.Remove(ps);
            lightweightVoidAttackVfxPool.Enqueue(ps);
        }

        void ClearLightweightVoidAttackVfx()
        {
            for (int i = activeLightweightVoidAttackVfx.Count - 1; i >= 0; i--)
            {
                var ps = activeLightweightVoidAttackVfx[i];
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
                lightweightVoidAttackVfxPool.Enqueue(ps);
            }
            activeLightweightVoidAttackVfx.Clear();
        }

        Material GetLightweightVoidAttackVfxMaterial()
        {
            if (lightweightVoidAttackVfxMaterial != null) return lightweightVoidAttackVfxMaterial;
            var shader = Shader.Find("Particles/Additive")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard");
            if (shader == null) return null;
            lightweightVoidAttackVfxMaterial = new Material(shader)
            {
                name = "Boss Slayer Lightweight Void Attack VFX Material (runtime)",
            };
            return lightweightVoidAttackVfxMaterial;
        }

        void UpdateResultScoreboard(RectTransform scorePanel)
        {
            if (scorePanel == null) return;

            var entries = new List<ResultScoreEntry>();
            foreach (var rawImage in scorePanel.GetComponentsInChildren<RawImage>(true))
            {
                int playerIndex = ParseResultPlayerIndex(rawImage.name);
                if (playerIndex < 0) continue;

                bool activePlayer = playerIndex < players.Count;
                rawImage.gameObject.SetActive(activePlayer);
                if (!activePlayer) continue;

                int score = PlayerScore(playerIndex);
                var scoreText = FindTMPIn(rawImage.transform, "Score");
                if (scoreText != null) scoreText.text = score.ToString("n0");
                entries.Add(new ResultScoreEntry(rawImage.rectTransform, score, playerIndex));
            }

            entries.Sort((a, b) =>
            {
                int scoreCompare = b.Score.CompareTo(a.Score);
                return scoreCompare != 0 ? scoreCompare : a.PlayerIndex.CompareTo(b.PlayerIndex);
            });

            for (int i = 0; i < entries.Count; i++) entries[i].Rect.SetSiblingIndex(i);
        }

        int PlayerScore(int playerIndex)
        {
            foreach (var player in players)
            {
                if (player.Index == playerIndex) return player.Damage;
            }
            return 0;
        }

        static int ParseResultPlayerIndex(string objectName)
        {
            const string prefix = "RawImage == Player";
            if (string.IsNullOrEmpty(objectName) || !objectName.StartsWith(prefix)) return -1;
            return int.TryParse(objectName.Substring(prefix.Length).Trim(), out int number) ? number - 1 : -1;
        }

        void UpdatePlayerHp(PlayerState p)
        {
                        SetHpBar(p.HpSlider, p.HpScrollbar, p.HpFill, p.Hp / (float)Mathf.Max(1, playerMaxHp));
            UpdateFloorHpRing(p);
            StylePlayerCard(p);
        }

        static void SetHpBar(Slider slider, Scrollbar scrollbar, Image fillImage, float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (scrollbar != null)
            {
                PrepareScrollbarHpBar(scrollbar);
                SetHpBarRect(fillImage != null ? fillImage.rectTransform : scrollbar.handleRect, ratio);
                if (fillImage != null && fillImage.type == Image.Type.Filled) fillImage.fillAmount = ratio;
                return;
            }

            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = ratio;
            }

            if (slider != null && slider.fillRect != null)
            {
                SetHpBarRect(slider.fillRect, ratio);
            }

            if (fillImage == null) return;

            if (fillImage.type == Image.Type.Filled)
            {
                fillImage.fillAmount = ratio;
                return;
            }

            SetHpBarRect(fillImage.rectTransform, ratio);
        }

        static void SetHpBarRect(RectTransform rect, float ratio)
        {
            if (rect == null) return;
            EnsureLeftPivot(rect);
            var scale = rect.localScale;
            scale.x = ratio;
            rect.localScale = scale;
        }

        static void PrepareScrollbarHpBar(Scrollbar scrollbar)
        {
            if (scrollbar == null) return;
            scrollbar.enabled = false;
            if (scrollbar.handleRect == null) return;

            var handle = scrollbar.handleRect;
            var handleImage = handle.GetComponent<Image>();
            if (handleImage != null)
            {
                var color = handleImage.color;
                color.a = 0f;
                handleImage.color = color;
                handleImage.raycastTarget = false;
            }
        }

        static void EnsureLeftPivot(RectTransform rect)
        {
            if (rect == null || Mathf.Abs(rect.pivot.x) < 0.001f) return;

            rect.pivot = new Vector2(0f, rect.pivot.y);
        }

        void UpdateSpotView(SpotState s)
        {
            if (s.View == null) return;
            var image = s.ViewImage;
            if (image == null) return;

            var c = s.Def.Color;
            float progress0 = Mathf.Clamp01(s.Load / spotHoldSeconds);
            c.a = Mathf.Lerp(0.25f, 0.9f, progress0);
            image.color = c;
            image.fillAmount = 1f;
            UpdateGemHighlight(s, progress0);
            bool art = BossGemArt.Animate(s.View, progress0);
            BossGemArt.SetGolden(s.View, s.Golden);

            if (s.GemFollower != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * gemGlowPulseSpeed + s.WorldPosition.x * 0.47f + s.WorldPosition.y * 0.31f);
                float glowIntensity = Mathf.Lerp(gemGlowIntensity * gemGlowIdleFraction, gemGlowIntensity, progress0) + pulse * 0.8f;
                ApplyGemGlow(s.GemRenderers, s.GemGlowBlock, s.Def.Color, glowIntensity);
            }

            if (s.ProgressRing == null) return;
            var ringImage = s.ProgressRingImage;
            if (ringImage == null) return;

            float progress = Mathf.Clamp01(s.Load / spotHoldSeconds);
            ringImage.fillAmount = progress;
            if (!art) s.ProgressRing.localRotation = Quaternion.Euler(0f, 0f, -Time.time * 120f);   // the gem's diamond fills from the top
        }

        void UpdateGemHighlight(SpotState s, float progress)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * gemGlowPulseSpeed + s.WorldPosition.x * 0.47f + s.WorldPosition.y * 0.31f);
            float charged = Mathf.Clamp01(progress);

            if (s.ContrastGlow != null)
            {
                var contrastImage = s.ContrastGlowImage;
                if (contrastImage != null)
                    contrastImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.42f, 0.56f, charged) + pulse * 0.04f);

                s.ContrastGlow.localScale = Vector3.one * (1f + pulse * 0.035f + charged * 0.04f);
            }

            if (s.EdgeGlow != null)
            {
                var edgeImage = s.EdgeGlowImage;
                if (edgeImage != null)
                {
                    var edgeColor = Color.white;
                    edgeColor.a = Mathf.Clamp01(Mathf.Max(gemEdgeGlowAlpha, 0.92f) * Mathf.Lerp(0.85f, 1f, charged) + pulse * 0.08f);
                    edgeImage.color = edgeColor;
                }

                s.EdgeGlow.localScale = Vector3.one * (1f + pulse * 0.045f);
                s.EdgeGlow.localRotation = Quaternion.Euler(0f, 0f, -Time.time * 36f);
            }
        }

        void UpdateAttackView(AttackState a)
        {
            if (a.View == null) return;
            if (a.DemonCut) return; // drawn by BossDemonUltimateGraphic
            if (a.IsMeteor) { if (a.MeteorVfx == null) { a.MeteorVfx = a.View.gameObject.AddComponent<BossMeteorVfx>(); a.MeteorVfx.Initialize(a.View); } a.MeteorVfx.Tick(a.TelegraphTimer, a.TelegraphTotal); }
            if (a.Shape == Shape.Wave) { UpdateWaveView(a); return; }
            if (a.ElementalVfx == null)
            {
                a.ElementalVfx = a.View.gameObject.AddComponent<BossAttackVfx>();
                var color = bossModel != null && bossGlowColorByModel.TryGetValue(bossModel,out var glow) ? glow : Color.red;
                a.ElementalVfx.Initialize(a.View,floorDisplayCamera,
                    BossAttackVfx.ThemeFor(bossModel != null ? bossModel.name : string.Empty),color,a.Shape == Shape.Circle,a.Shape == Shape.Wave);
                PlaceHazardBelowPlayers(a.View);
            }
            var image = a.ViewImage;
            if (image != null && !a.ElementalVfx.IsReady)
            {
                // บาส 2026-08-28: was reversed (telegraph=red, HasHit=orange) — swapped so the
                // charging warning reads as orange (caution) and the moment it's actually
                // attacking reads as red (danger now), the more standard direction. Only the two
                // colors traded places; each branch's own alpha behavior (fixed 0.62 once hit,
                // fading 0.62->0.22 while still charging) is untouched.
                //
                // Wave (the sweeping lane hazard) never sets HasHit at all (see the Shape.Wave
                // exclusion where HasHit gets set below) — it re-checks every frame instead so
                // it can keep hurting players the whole time it's moving, per บาส's ask, so it
                // would otherwise sit in the orange "still charging" branch forever. It's
                // dangerous continuously, not just once it "fires," so it stays solid red the
                // whole time instead of following the orange->red telegraph->hit convention.
                if (a.Shape == Shape.Wave)
                {
                    image.color = new Color(1f, 0.08f, 0.08f, 0.62f);
                }
                else
                {
                    float warn = Mathf.Clamp01(a.TelegraphTimer / Mathf.Max(0.01f, telegraphSeconds));
                    image.color = a.HasHit ? new Color(1f, 0.08f, 0.08f, 0.62f) : new Color(1f, 0.55f, 0.25f, Mathf.Lerp(0.62f, 0.22f, warn));
                }
            }
            if (a.ElementalVfx.IsReady)
            {
                if (a.WarningRing != null) a.WarningRing.gameObject.SetActive(false);
            }
            else UpdateAttackWarningRing(a);
            if (a.Lingering)
            {
                a.ElementalVfx.SetScorch(a.TelegraphTimer <= -Mathf.Max(.45f, a.FireSpreadSeconds), a.ActiveTimer);
            }
            a.ElementalVfx.Configure(a.FloorForm, a.FireSpreadSeconds > 0 ? (a.Shape == Shape.Circle ? 1 : 2) : 0, FireFront(a));
            a.ElementalVfx.Tick(a.TelegraphTimer,a.TelegraphTotal,a.ActiveTimer);
        }

        void UpdateAttackWarningRing(AttackState a)
        {
            if (a.WarningRing == null) return;

            bool warning = a.TelegraphTimer > 0f && !a.HasHit;
            a.WarningRing.gameObject.SetActive(warning);
            if (!warning) return;

            float ratio = Mathf.Clamp01(a.TelegraphTimer / Mathf.Max(0.01f, a.TelegraphTotal));
            // บาส 2026-08-28: square/rectangle attacks (Line/Zone) want their frame growing
            // small -> full-size as the charge completes ("จากเล็กไปใหญ่", full size = the
            // instant it fires) — the opposite direction from circle's shrink-in warning ring,
            // which stays as it was. `elapsed` (0 at spawn -> 1 at firing) drives the grow
            // direction; `ratio` (1 at spawn -> 0 at firing) still drives circle's shrink.
            float elapsed = 1f - ratio;
            float t = a.Shape == Shape.Circle ? ratio : elapsed;
            float scale = Mathf.Lerp(0.16f, 1.12f, t);
            a.WarningRing.localScale = new Vector3(scale, scale, 1f);

            if (a.WarningRingImage == null) a.WarningRingImage = a.WarningRing.GetComponent<Image>();
            var image = a.WarningRingImage;
            if (image == null) return;

            var color = image.color;
            color.a = Mathf.Lerp(1f, 0.88f, t);
            image.color = color;
        }

        void PlaceHazardBelowPlayers(RectTransform view)
        {
            int index = view.GetSiblingIndex();
            foreach (var player in players)
                if (player.Cursor != null && player.Cursor.parent == view.parent)
                    index = Mathf.Min(index,player.Cursor.GetSiblingIndex());
            view.SetSiblingIndex(index);
        }

        float StunMultiplier() => stunTimer > 0f ? 1f + (IsRushActive && PerkLevel("stun") == 2 ? 75f : stunBonusPercent) / 100f : 1f;
        int BossPhase()
        {
            float hpPercent = bossHp * 100f / Mathf.Max(1, bossMaxHp);
            if(passives.Active == BossPassiveState.Kind.Dragon) return passives.DragonHeated ? 2 : 1;
            return hpPercent > phase2AtPercent ? 1 : hpPercent > phase3AtPercent ? 2 : 3;
        }
        // Players are kept this far (floor units) inside the floor's edge (UpdatePlayers), and a gem's load bar reaches
        // this far above its centre (spot diameters, BossGemArt).
        const float PlayerEdgeInset = 40f, GemTopReach = .75f;

        // A random gem centre (metres) where the whole gem, its bar included, lies inside the area players can stand
        // in, spotEdgeMarginMeters clear of its edge. diameter is the spot's size in floor units.
        Vector2 RandomGemPosition(float diameter)
        {
            var r = FloorRect;
            float perX = r.width / Mathf.Max(.01f, arenaWidthMeters), perZ = r.height / Mathf.Max(.01f, arenaDepthMeters);
            float side = (PlayerEdgeInset + diameter * .5f) / perX + spotEdgeMarginMeters;
            float far = (PlayerEdgeInset + diameter * .5f) / perZ + spotEdgeMarginMeters;          // bottom edge of the floor
            float near = (PlayerEdgeInset + diameter * GemTopReach) / perZ + spotEdgeMarginMeters;  // top edge (z = 0), where the bar points
            side = Mathf.Min(side, arenaWidthMeters * .45f);
            if (near + far > arenaDepthMeters * .9f) { float k = arenaDepthMeters * .9f / (near + far); near *= k; far *= k; }
            return new Vector2(Random.Range(side, arenaWidthMeters - side), Random.Range(near, arenaDepthMeters - far));
        }

        float RandomX(float scale)
        {
            float margin = arenaWidthMeters * (1f - Mathf.Clamp01(scale)) * 0.5f;
            return Random.Range(margin, arenaWidthMeters - margin);
        }

        float RandomZ(float scale)
        {
            float margin = arenaDepthMeters * (1f - Mathf.Clamp01(scale)) * 0.5f;
            return Random.Range(margin, arenaDepthMeters - margin);
        }

        bool ShouldUseDirectEditorMouse(int zoneIndex)
        {
#if UNITY_EDITOR
            return useMouseInEditor && !useUwbInput && zoneIndex == 0;
#else
            return false;
#endif
        }

        // Game player zoneIndex's point on the floor, local to InputArea. UWB: the UWBPlayerController the pads gave that
        // slot (BossFloorPlayers). Without UWB in the Editor: Player 1 follows the mouse.
        bool TryGetInputLocalPoint(int zoneIndex, out Vector2 localPoint)
        {
            localPoint = default;
            if (useUwbInput) return FloorPlayers() != null && floorPlayers.TryGetSlotFloorLocal(zoneIndex, FloorRect, out localPoint);
#if UNITY_EDITOR
            if (useMouseInEditor && zoneIndex == 0)
            {
                var inputArea = InputArea;
                var canvas = inputArea.GetComponentInParent<Canvas>();
                var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                return RectTransformUtility.ScreenPointToLocalPointInRectangle(inputArea, (UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero), camera, out localPoint)
                    && FloorRect.Contains(localPoint);
            }
#endif
            return false;
        }

        int GetZoneIndexForScreenPoint(Vector2 point)
        {
            if (players.Count == 0) return 0;

            var canvas = InputArea.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(InputArea, point, camera, out var local);

            int nearestIndex = 0;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < players.Count; i++)
            {
                float distance = (players[i].Cursor.anchoredPosition - local).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = players[i].Index;
                }
            }
            return nearestIndex;
        }

        Vector2 ZoneCenterLocal(int zoneIndex)
        {
            var r = FloorRect;
            int count = Mathf.Max(1, players.Count);
            float t = count == 1 ? 0.5f : (zoneIndex + 1f) / (count + 1f);
            return new Vector2(Mathf.Lerp(r.xMin + 120f, r.xMax - 120f, t), r.yMin + r.height * 0.25f);
        }

        Vector2 WorldToLocal(Vector2 meters)
        {
            var r = FloorRect;
            float x = Mathf.InverseLerp(0f, arenaWidthMeters, meters.x);
            float y = Mathf.InverseLerp(0f, arenaDepthMeters, meters.y);
            return new Vector2(Mathf.Lerp(r.xMin, r.xMax, x), Mathf.Lerp(r.yMax, r.yMin, y));
        }

        Vector2 LocalToWorld(Vector2 local)
        {
            var r = FloorRect;
            float x = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
            float y = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
            return new Vector2(Mathf.Lerp(0f, arenaWidthMeters, x), Mathf.Lerp(arenaDepthMeters, 0f, y));
        }

        float PixelsPerMeter() => FloorRect.width / Mathf.Max(0.01f, arenaWidthMeters);

        // บาส 2026-08-28: turns the hand-placed "Player N" square (an Image with no sprite,
        // defaulting to a solid rect at whatever size the RectTransform was set to) into a
        // hollow ring with a solid circle centered inside it — the classic "graze ring + real
        // hitbox" bullet-hell convention, not just two circles side by side (first pass got
        // that wrong; this replaces it). Reuses GetRingSprite()/GetCircleSprite(), the same
        // textures the attack telegraphs already use, no new art asset needed.
        // The root Image becomes the OUTER RING in place — it keeps tracking the player's real
        // position/size exactly as before (gameplay code moves `cursor.anchoredPosition`
        // directly; distance checks like IsInAttack read the same anchoredPosition), so hit
        // detection is unaffected. The inner solid circle is a plain child centered at (0,0) —
        // named and found by Find() (not GetComponentsInChildren) so PlayerState's per-player
        // coloring can target it specifically without also recoloring "HP Bar"'s own images.
        // Re-applies size/position every call (cheap) but only creates the child once, so
        // Setup() re-running mid-session still lets Inspector tweaks to the two diameters above
        // take effect without leaving a duplicate child behind.
        void SetupPlayerCursorVisual(RectTransform cursor, int index)
        {
            var rootImage = cursor.GetComponent<Image>();
            // บาส 2026-11-18: the art team's P1..P6 marker replaces the ring + centre dot (BossPlayerMarkers)
            var marker = BossPlayerMarkers.For(index);
            if (rootImage != null && marker != null)
            {
                rootImage.sprite = marker;
                rootImage.type = Image.Type.Simple;
                rootImage.preserveAspect = true;
                rootImage.color = Color.white;
                // บาส 2026-11-18: "ขนาดเท่าเดิมทุกฉาก" - the same real size as on the player select (its unpicked circle,
                // unpickedPlayerMeters), on the boss select and in the fight alike
                float size = PlayerMarkerSize(cursor);
                cursor.sizeDelta = new Vector2(size * BossPlayerMarkers.Aspect(index), size);
                if (cursor.Find(InnerCircleName) is Transform oldDot) oldDot.gameObject.SetActive(false);
                // บาส 2026-11-18: "เอาวงสีเขียวออก" - no floor HP ring around the marker (HP stays on the wall cards)
                foreach (var ringName in new[] { FloorHpTrackName, FloorHpRingName })
                    if (cursor.Find(ringName) is Transform hpRing) hpRing.gameObject.SetActive(false);
                return;
            }
            if (rootImage != null)
            {
                rootImage.sprite = GetRingSprite();
                rootImage.type = Image.Type.Simple;
            }
            cursor.sizeDelta = new Vector2(playerCursorCircleDiameter, playerCursorCircleDiameter);

            var inner = cursor.Find(InnerCircleName) as RectTransform;
            if (inner == null)
            {
                var go = new GameObject(InnerCircleName, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(cursor, false);
                inner = go.GetComponent<RectTransform>();
                inner.anchorMin = inner.anchorMax = new Vector2(0.5f, 0.5f);
                inner.pivot = new Vector2(0.5f, 0.5f);
                var innerImage = inner.GetComponent<Image>();
                innerImage.sprite = GetCircleSprite();
                innerImage.type = Image.Type.Simple;
                innerImage.raycastTarget = false;
            }
            inner.anchoredPosition = Vector2.zero;
            inner.sizeDelta = new Vector2(playerCursorInnerCircleDiameter, playerCursorInnerCircleDiameter);

            float ringDiameter = playerCursorCircleDiameter * FloorHpRingScale;
            EnsureFloorHpRing(cursor, FloorHpTrackName, ringDiameter, new Color(0f, 0f, 0f, .45f), false);
            EnsureFloorHpRing(cursor, FloorHpRingName, ringDiameter, new Color(.3f, 1f, .45f, .95f), true);
        }

        static void EnsureFloorHpRing(RectTransform cursor, string name, float diameter, Color color, bool filled)
        {
            var ring = cursor.Find(name) as RectTransform;
            if (ring == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(cursor, false);
                ring = go.GetComponent<RectTransform>();
                ring.anchorMin = ring.anchorMax = ring.pivot = new Vector2(0.5f, 0.5f);
                var image = go.GetComponent<Image>();
                image.sprite = GetRingSprite();
                image.raycastTarget = false;
                image.color = color;
                if (filled)
                {
                    image.type = Image.Type.Filled;
                    image.fillMethod = Image.FillMethod.Radial360;
                    image.fillOrigin = (int)Image.Origin360.Top;
                    image.fillClockwise = true;
                    image.fillAmount = 1f;
                }
                ring.SetAsFirstSibling(); // under the cursor ring and inner circle
            }
            ring.anchoredPosition = Vector2.zero;
            ring.sizeDelta = new Vector2(diameter, diameter);
        }

        void UpdateFloorHpRing(PlayerState p)
        {
            if (p.Cursor == null || !(p.Cursor.Find(FloorHpRingName) is RectTransform ring)) return;
            var image = ring.GetComponent<Image>();
            if (image == null) return;
            float ratio = Mathf.Clamp01(p.Hp / (float)Mathf.Max(1, playerMaxHp));
            image.fillAmount = ratio;
            var color = ratio > .5f ? Color.Lerp(new Color(1f, .85f, .2f), new Color(.3f, 1f, .45f), (ratio - .5f) * 2f)
                : Color.Lerp(new Color(1f, .2f, .15f), new Color(1f, .85f, .2f), ratio * 2f);
            color.a = .95f;
            image.color = color;
        }

        RectTransform CreateCircle(string name, Vector2 localPosition, float diameter, Color color)
        {
            var rect = CreateRect(name, localPosition, new Vector2(diameter, diameter), color);
            var image = rect.GetComponent<Image>();
            image.sprite = GetCircleSprite();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillAmount = 1f;
            return rect;
        }

        // บาส 2026-08-28: extracted from what used to be CreateCircleRing's own body so
        // AddLine/AddZone's square/rectangle attacks can get the exact same charge-telegraph
        // treatment (see UpdateAttackWarningRing) as circle attacks already had — same stretch-
        // to-fill-parent setup, just a different sprite. Stretching to the parent's own rect
        // (anchorMin 0 / anchorMax 1) means this naturally traces whatever aspect ratio the
        // parent attack view actually is — a tall narrow Line's frame reads as a tall narrow
        // rectangle, not a circle squashed to fit.
        RectTransform CreateFrameOverlay(string name, RectTransform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
            return rect;
        }

        RectTransform CreateCircleRing(string name, RectTransform parent, Color color) =>
            CreateFrameOverlay(name, parent, color, GetRingSprite());

        RectTransform CreateSquareFrame(string name, RectTransform parent, Color color) =>
            CreateFrameOverlay(name, parent, color, GetSquareFrameSprite());

        // "Ring" is legacy naming (this used to be a literal circular ring) —
        // now a square frame reusing GetSquareFrameSprite, the same asset the
        // attack telegraph's "Warning Frame" already uses, for a squared-off
        // "you can collect this" indicator instead of a circle now that the
        // circle underneath it is hidden (see AttachGemModel) and a 3D gem
        // model, not a disc, is what it's actually surrounding.
        RectTransform CreateProgressRing(string name, RectTransform parent, Color color)
        {
            var rect = CreateSquareFrame(name, parent, color);
            var image = rect.GetComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = 0f;
            return rect;
        }

        RectTransform CreateGemEdgeGlow(RectTransform parent, Color color)
        {
            color = Color.white;
            color.a = Mathf.Max(gemEdgeGlowAlpha, 0.92f);
            var rect = CreateSquareFrame("Gem Edge Glow", parent, color);
            GrowFrame(rect, 0.32f);
            return rect;
        }

        RectTransform CreateGemContrastGlow(RectTransform parent)
        {
            var rect = CreateFrameOverlay("Gem Contrast Backplate", parent, new Color(0f, 0f, 0f, 0.42f), GetCircleSprite());
            GrowFrame(rect, 0.46f);
            rect.SetAsFirstSibling();
            return rect;
        }

        static void GrowFrame(RectTransform rect, float fraction)
        {
            if (rect == null) return;
            float width = rect.rect.width;
            float height = rect.rect.height;
            if ((width <= 0f || height <= 0f) && rect.parent is RectTransform parent)
            {
                width = parent.rect.width;
                height = parent.rect.height;
            }

            float x = width * fraction * 0.5f;
            float y = height * fraction * 0.5f;
            rect.offsetMin = new Vector2(-x, -y);
            rect.offsetMax = new Vector2(x, y);
        }

        RectTransform CreateRect(string name, Vector2 localPosition, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(InputArea, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = localPosition;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static Sprite GetCircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            circleSprite = CreateDiscSprite("BossSlayer Circle Sprite", false);
            return circleSprite;
        }

        static Sprite GetRingSprite()
        {
            if (ringSprite != null) return ringSprite;
            ringSprite = CreateDiscSprite("BossSlayer Ring Sprite", true);
            return ringSprite;
        }

        // บาส 2026-08-28: square/rectangle equivalent of GetRingSprite() — a hollow-bordered
        // frame instead of a hollow-bordered disc — so AddLine/AddZone's attacks can get the
        // same charge-warning frame treatment circle attacks already had (see
        // CreateFrameOverlay/UpdateAttackWarningRing).
        static Sprite GetSquareFrameSprite()
        {
            if (squareFrameSprite != null) return squareFrameSprite;
            squareFrameSprite = CreateSquareFrameSprite("BossSlayer Square Frame Sprite");
            return squareFrameSprite;
        }

        static Sprite CreateDiscSprite(string name, bool ring)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            var clear = new Color32(255, 255, 255, 0);
            var solid = new Color32(255, 255, 255, 255);
            float center = (size - 1) * 0.5f;
            float outer = center - 2f;
            float inner = ring ? outer - 10f : 0f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    bool inside = ring ? distance <= outer && distance >= inner : distance <= outer;
                    pixels[y * size + x] = inside ? solid : clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // Same border-thickness convention as CreateDiscSprite's ring (10px on a 128px texture)
        // so the two frame styles read as the same visual "weight" next to each other.
        static Sprite CreateSquareFrameSprite(string name)
        {
            const int size = 128;
            const int border = 10;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            var clear = new Color32(255, 255, 255, 0);
            var solid = new Color32(255, 255, 255, 255);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inBorder = x < border || x >= size - border || y < border || y >= size - border;
                    pixels[y * size + x] = inBorder ? solid : clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        TMP_Text CreateText(string name, RectTransform parent, Vector2 localPosition, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent != null ? parent : InputArea, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1800f, 160f);
            rect.anchoredPosition = localPosition;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            return text;
        }

        void ClearAttacks()
        {
            CancelSignatureWindup(false);
            signatureLanded = false;
            rushNearMiss.Clear();
            ClearHydraSevenHeadStrike();
            if (ultimateImpactFrame != null) ultimateImpactFrame.Clear();
            ClearGolemUltimateShake();
            bossSignatureTitle = null;
            UpdateBossSignatureBanner();
            sfx?.StopCharge();
            if (elementalChargeVfx != null) elementalChargeVfx.Stop();
            if (modelElementVfx != null) modelElementVfx.Clear();
            foreach (var a in attacks) DestroyAttackView(a);
            attacks.Clear();
            ClearUltimateGraphics();
            UpdateSentinelArsenal();
        }

        // บาส 2026-10-06 (Balance Note A2): a stun cancels every attack that has not landed yet (the Hydra already worked
        // this way), so a successful stun never "still hits". Attacks already released keep their normal short lifetime.
        // Returns how many were cancelled and whether a boss ultimate was among them (both logged on the replay stun event).
        int CancelPendingAttacksOnStun(out bool ultimateCancelled)
        {
            int cancelled = 0; ultimateCancelled = false;
            for (int i = attacks.Count - 1; i >= 0; i--)
            {
                var attack = attacks[i];
                if (attack.SoundReleased || attack.TelegraphTimer <= 0f) continue;
                ultimateCancelled |= attack.IsBossUltimate;
                DestroyAttackView(attack);
                attacks.RemoveAt(i);
                cancelled++;
            }
            if (cancelled == 0) return 0;
            // An interrupted ultimate ends as a whole: its shared floor/wall graphics (Demon cuts, Sentinel arsenal) would
            // otherwise keep drawing warnings that can no longer hit. ClearAttacks also resets banner, charge audio/VFX.
            if (attacks.Count == 0 || ultimateCancelled) ClearAttacks();
            else
            {
                sfx?.StopCharge();
                if (elementalChargeVfx != null) elementalChargeVfx.Stop();
                if (modelElementVfx != null) modelElementVfx.Clear();
                UpdateBossSignatureBanner();
            }
            return cancelled;
        }

        void DestroyAttackView(AttackState attack)
        {
            if (attack == null) return;
            if(attack.MissileVfx!=null)Destroy(attack.MissileVfx.gameObject);
            if(attack.FloorMissileVfx!=null)Destroy(attack.FloorMissileVfx.gameObject);
            if(attack.BeamVfx!=null)Destroy(attack.BeamVfx.gameObject);
            if(attack.FloorBeamVfx!=null)Destroy(attack.FloorBeamVfx.gameObject);
            if (attack.View != null) Destroy(attack.View.gameObject);
        }

        void ClearSpots()
        {
            foreach (var s in spots) PopOutSpot(s);
            spots.Clear();
        }

        // บาส 2026-11-18: the colours of the art team's P1..P6 markers (were cyan, green, yellow, magenta, pink, violet)
        static Color PlayerColor(int index) => BossPlayerMarkers.ColorOf(index);

        static RectTransform FindRect(string name)
        {
            foreach (var rect in Resources.FindObjectsOfTypeAll<RectTransform>())
                if (rect != null && rect.gameObject.scene.IsValid() && rect.name == name) return rect;
            return null;
        }

        // Exact-match only — deliberately NOT a Contains fallback: for names with
        // sibling objects that share a prefix (e.g. "FX_Explosion" vs.
        // "FX_Explosion_Smoke"/"_Debris"/"_Trail"), a loose match can resolve to
        // any of them depending on Resources.FindObjectsOfTypeAll's unspecified
        // enumeration order. That's exactly how explosionVfx once ended up
        // resolving to FX_Explosion_Smoke instead of the actual explosion — a
        // much weaker sub-effect, spawned over and over.
        static Transform FindTransformExact(string name)
        {
            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
                if (transform != null && transform.gameObject.scene.IsValid() && transform.name == name) return transform;
            return null;
        }

        static Camera FindCamera(string name)
        {
            foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>())
                if (camera != null && camera.gameObject.scene.IsValid() && (camera.name == name || camera.name.Contains(name))) return camera;
            return null;
        }

        static Camera FindDisplayCamera(int targetDisplay)
        {
            foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>())
                if (camera != null && camera.gameObject.scene.IsValid() && camera.targetDisplay == targetDisplay) return camera;
            return null;
        }

        static RectTransform FindRectIn(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name) return rect;
            return null;
        }

        static RectTransform FindDirectRectIn(Transform root, string name)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name && child.TryGetComponent(out RectTransform rect)) return rect;
            }
            return null;
        }

        static TMP_Text FindTMP(string name)
        {
            foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
                if (text != null && text.gameObject.scene.IsValid() && text.name == name) return text;
            return null;
        }

        static TMP_Text FindTMPIn(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.name == name || text.name.Contains(name)) return text;
            return null;
        }

        static RawImage FindRawImage(string name)
        {
            foreach (var rawImage in Resources.FindObjectsOfTypeAll<RawImage>())
                if (rawImage != null && rawImage.gameObject.scene.IsValid() && rawImage.name == name) return rawImage;
            return null;
        }

        static RawImage FindRawImageIn(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var rawImage in root.GetComponentsInChildren<RawImage>(true))
                if (rawImage.name == name || rawImage.name.Contains(name)) return rawImage;
            return null;
        }

        static Image FindImageIn(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.name == name || image.name.Contains(name)) return image;
            return null;
        }

        static Image FindHpBarImageIn(Transform root)
        {
            if (root == null) return null;

            var fill = FindImageIn(root, "Fill");
            if (fill != null) return fill;

            var handle = FindImageIn(root, "Handle");
            if (handle != null) return handle;

            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.transform == root) continue;
                return image;
            }

            return root.GetComponent<Image>();
        }

        static Image CreateHpFillImage(Transform root, string name, Color color)
        {
            if (root == null) return null;

            var existing = FindDirectRectIn(root, name);
            Image image;
            RectTransform rect;
            if (existing != null && existing.TryGetComponent(out image))
            {
                rect = existing;
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root, false);
                rect = go.GetComponent<RectTransform>();
                image = go.GetComponent<Image>();
            }

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.SetAsFirstSibling();

            image.color = color;
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
            image.fillAmount = 1f;
            return image;
        }

        static Slider FindSliderIn(Transform root)
        {
            return root != null ? root.GetComponentInChildren<Slider>(true) : null;
        }

        static Scrollbar FindScrollbarIn(Transform root)
        {
            return root != null ? root.GetComponentInChildren<Scrollbar>(true) : null;
        }

        readonly struct SpotDef
        {
            public readonly string Name;
            public readonly Color Color;
            public readonly int Damage;
            public readonly int Heal;
            public readonly int Stun;
            public readonly int Ultimate;
            // Which 3D gem model (GameObject name under "Item" in the scene) this
            // pickup type shows — separate from Name, which is the Thai display
            // label ("โจมตี" etc.), not the model's actual name in the Hierarchy.
            public readonly string GemModelName;

            public SpotDef(string name, Color color, int damage, int heal, int stun, int ultimate, string gemModelName)
            {
                Name = name;
                Color = color;
                Damage = damage;
                Heal = heal;
                Stun = stun;
                Ultimate = ultimate;
                GemModelName = gemModelName;
            }
        }

        readonly struct BossBaseTransform
        {
            public readonly Vector3 Position;
            public readonly Vector3 Scale;
            public readonly Quaternion Rotation;

            public BossBaseTransform(Vector3 position, Vector3 scale, Quaternion rotation)
            {
                Position = position;
                Scale = scale;
                Rotation = rotation;
            }
        }

        readonly struct AttackChargeVfx
        {
            public readonly Transform Template;
            public readonly int BurstCount;

            public AttackChargeVfx(Transform template, int burstCount)
            {
                Template = template;
                BurstCount = burstCount;
            }
        }

        readonly struct ResultScoreEntry
        {
            public readonly RectTransform Rect;
            public readonly int Score;
            public readonly int PlayerIndex;

            public ResultScoreEntry(RectTransform rect, int score, int playerIndex)
            {
                Rect = rect;
                Score = score;
                PlayerIndex = playerIndex;
            }
        }

        sealed class VideoBackgroundRuntime
        {
            readonly VideoPlayer player;
            RawImage target;
            Texture fallbackTexture;
            string requestedUrl;

            public VideoBackgroundRuntime(string name, Transform parent)
            {
                var go = new GameObject(name, typeof(VideoPlayer));
                go.transform.SetParent(parent, false);
                player = go.GetComponent<VideoPlayer>();
                player.playOnAwake = false;
                player.isLooping = true;
                player.renderMode = VideoRenderMode.APIOnly;
                player.source = VideoSource.Url;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.skipOnDrop = true;
                player.prepareCompleted += OnPrepared;
                player.errorReceived += OnError;
            }

            public void Play(RawImage target, string moviePath, Texture fallbackTexture)
            {
                this.target = target;
                this.fallbackTexture = fallbackTexture;
                requestedUrl = new System.Uri(moviePath).AbsoluteUri;
                if (target != null)
                {
                    target.texture = fallbackTexture;
                    target.uvRect = new Rect(0f, 0f, 1f, 1f);
                }

                if (player.url == requestedUrl && player.isPrepared)
                {
                    ApplyPreparedTexture();
                    if (!player.isPlaying) player.Play();
                    return;
                }

                player.Stop();
                player.url = requestedUrl;
                player.Prepare();
            }

            public void Suspend()
            {
                requestedUrl = null;
                player.Stop();
                target = null;
                fallbackTexture = null;
            }

            void OnPrepared(VideoPlayer preparedPlayer)
            {
                if (preparedPlayer != player || player.url != requestedUrl) return;
                ApplyPreparedTexture();
                player.Play();
            }

            void OnError(VideoPlayer erroredPlayer, string message)
            {
                if (erroredPlayer != player || target == null) return;
                target.texture = fallbackTexture;
                target.uvRect = new Rect(0f, 0f, 1f, 1f);
            }

            void ApplyPreparedTexture()
            {
                if (target == null || player.texture == null) return;
                target.texture = player.texture;
                target.uvRect = new Rect(0f, 0f, 1f, 1f);
            }
        }

        sealed class PlayerState
        {
            public readonly int Index;
            public readonly RectTransform Cursor;
            public readonly Slider HpSlider;
            public readonly Scrollbar HpScrollbar;
            public readonly Image HpFill;
            public Vector2 Velocity;
            public int Hp;
            public int Damage;
            public float DownTimer;

            public PlayerState(int index, RectTransform cursor, Slider hpSlider, Scrollbar hpScrollbar, Image hpFill, Color color, int hp)
            {
                Index = index;
                Cursor = cursor;
                HpSlider = hpSlider;
                HpScrollbar = hpScrollbar;
                HpFill = hpFill;
                Hp = hp;
                var graphic = cursor.GetComponent<Graphic>();
                if (graphic != null) graphic.color = color;
                // บาส 2026-08-28: cursor is now a ring (root) + solid inner circle (see
                // SetupPlayerCursorVisual) — the inner circle is a plain named child, found
                // directly rather than via GetComponentsInChildren, so this can't accidentally
                // also recolor "HP Bar"'s own fill/background images (also children of cursor).
                var innerCircle = cursor.Find(InnerCircleName);
                if (innerCircle != null)
                {
                    var innerGraphic = innerCircle.GetComponent<Graphic>();
                    if (innerGraphic != null) innerGraphic.color = color;
                }
            }

            public void Reset(int hp, Vector2 localPosition, bool resetDamage = true)
            {
                Hp = hp;
                if (resetDamage) Damage = 0;
                DownTimer = 0f;
                Velocity = Vector2.zero;
                Cursor.anchoredPosition = localPosition;
                Cursor.gameObject.SetActive(true);
            }
        }

        sealed class SpotState
        {
            public readonly SpotDef Def;
            public readonly Vector2 WorldPosition;
            public readonly RectTransform View;
            public readonly Image ViewImage;
            public readonly RectTransform ProgressRing;
            public readonly Image ProgressRingImage;
            public readonly RectTransform ContrastGlow;
            public readonly Image ContrastGlowImage;
            public readonly RectTransform EdgeGlow;
            public readonly Image EdgeGlowImage;
            // บาส 2026-09-03: the 3D gem clone's own follower (AttachGemModel used
            // to discard this) — needed so UpdateSpotView can glow the actual gem
            // MODEL (not just the flat circle underneath it, which gets hidden
            // once a gem attaches) and so the collect animation can shrink the
            // gem itself away. Null whenever a spot's def has no GemModelName or
            // the model failed to attach — every user checks for that.
            public readonly Visual3DFollower GemFollower;
            public readonly Renderer[] GemRenderers;
            public readonly MaterialPropertyBlock GemGlowBlock;
            public float Life;
            public float Load;
            // 2026-11-25: someone was standing on this gem last frame (its pickup sound plays on the first step).
            public bool Stepped;
            // Balance Note v3 §5.2: 2+ players with Join Forces stand on it, so it wears a gold frame.
            public bool Golden;

            public SpotState(SpotDef def, Vector2 worldPosition, RectTransform view, RectTransform progressRing, RectTransform contrastGlow, RectTransform edgeGlow, float life, Visual3DFollower gemFollower)
            {
                Def = def;
                WorldPosition = worldPosition;
                View = view;
                ViewImage = view != null ? view.GetComponent<Image>() : null;
                ProgressRing = progressRing;
                ProgressRingImage = progressRing != null ? progressRing.GetComponent<Image>() : null;
                ContrastGlow = contrastGlow;
                ContrastGlowImage = contrastGlow != null ? contrastGlow.GetComponent<Image>() : null;
                EdgeGlow = edgeGlow;
                EdgeGlowImage = edgeGlow != null ? edgeGlow.GetComponent<Image>() : null;
                Life = life;
                GemFollower = gemFollower;
                GemRenderers = gemFollower != null ? gemFollower.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
                GemGlowBlock = GemRenderers.Length > 0 ? new MaterialPropertyBlock() : null;
            }
        }

        sealed class AttackState
        {
            public readonly Shape Shape;
            public readonly RectTransform View;
            public readonly Image ViewImage;
            public readonly HashSet<int> HitPlayers = new();
            public RectTransform WarningRing;
            public Image WarningRingImage;
            public BossAttackVfx ElementalVfx;
            public readonly List<Vector2> WaveGaps = new();
            public readonly List<BossSafeZoneVfx> WaveSafeVfx = new();
            public readonly List<BossAttackVfx> WaveDangerVfx = new();
            public float WarningDelay;
            public float TelegraphTimer;
            public float TelegraphTotal;
            public float ActiveTimer;
            public bool HasHit;
            public bool Lingering;

            public bool IsHydraSevenHeadStrike;
            public BossHydra HydraOwner;
            public int HydraHeadIndex = -1;
            public bool IsHydraBreathInterrupted => HydraHeadIndex >= 0 && TelegraphTimer > 0f &&
                (HydraOwner == null || !HydraOwner.IsHeadReady(HydraHeadIndex) || (IsHydraSevenHeadStrike && !HydraOwner.SevenHeadStrikeActive));
            public bool SoundReleased;
            public bool IsBossUltimate;
            public int AnimationCastId = -1;
            public int AnimationVariant;
            public bool MotionStarted;
            public bool IsMeteor;
            public BossMeteorVfx MeteorVfx;
            public Vector2 BeamStart,BeamEnd;
            // Demon SEVERING JUDGEMENT (DemonUltimate.cs): a straight cut, or the blast that carries the thrown spear.
            public bool DemonCut,DemonSpearHost;public RectTransform DemonCutCore,DemonSpear;
            public int BeamIndex;
            public BossCrystalBeamVfx BeamVfx,FloorBeamVfx;
            public bool SentinelMissile,SentinelLaser;
            public bool IsSerpentSurge;
            // บาส 2026-10-20: Abyssal King (AbyssalKing.cs) - a void beam drawn wall->floor, and slams that re-aim at a player
            // the moment their warning appears.
            public bool KingBeam, KingHunt, KingAimed; public int KingHuntPlayer;
            public readonly List<BossSerpentWaveVfx> SerpentCrests=new();
            public BossSentinelMissileVfx MissileVfx,FloorMissileVfx;

            public BossCrystalFloorGraphic FloorCrystals;
            public int FloorTheme;
            public BossAttackVfx.AttackForm FloorForm;
            public float FireSpreadSeconds;
            public float X;
            public float Z;
            public float Radius;
            public float Width;
            public int Side;
            public bool WaveHorizontal;
            public int WaveDir;
            public float CurrentWavePosition;
            public float MoveSeconds;

            public AttackState(Shape shape, RectTransform view, float telegraphTimer, float activeTimer)
            {
                Shape = shape;
                View = view;
                ViewImage = view != null ? view.GetComponent<Image>() : null;
                TelegraphTimer = telegraphTimer;
                TelegraphTotal = telegraphTimer;
                ActiveTimer = telegraphTimer + activeTimer;
            }
        }

    }
}
