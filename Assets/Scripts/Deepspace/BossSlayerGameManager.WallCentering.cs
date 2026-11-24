using UnityEngine;

namespace Deepspace
{
    // บาส 2026-11-11: "ปรับตำแหน่งบอสให้อยู่ตรงกลางๆจอหน่อย".
    // The new voxel bodies sit off their old pivots (most float high into the HP bar, the Serpent sits low right), and the
    // boss walked across ±35% of the wall. Two visual-only fixes; bossX, attacks and the floor are unchanged:
    //  * the wall walk is narrowed (bossWallRoamFraction)
    //  * the boss camera (an overlay above the wall camera) slides so the body's centre sits on bossWallCenterViewportY
    //    and over the boss's walk position. Halo, element VFX, gem flights and wall-to-floor seams all project through
    //    this same camera, so they move with the body. Its zoom still uses the wall camera's position (unchanged size).
    public sealed partial class BossSlayerGameManager
    {
        [Header("Wall framing (2026-11-11)")]
        [Tooltip("How far the boss walks across the wall, as a fraction of half the wall width (was 0.35).")]
        [SerializeField, Range(0f, 0.35f)] float bossWallRoamFraction = 0.15f;
        [Tooltip("Where the centre of the boss body sits on the wall: 0 = bottom, 1 = top. The HP bar is at the top.")]
        [SerializeField, Range(0.3f, 0.7f)] float bossWallCenterViewportY = 0.47f;
        [Tooltip("How quickly the framing follows the body (per second). Low = no visible chasing of the bob.")]
        [SerializeField, Range(0.5f, 8f)] float bossWallCenterFollow = 2.5f;

        Vector2 bossWallShift;      // camera offset from the wall camera, along its right / up
        Transform bossWallShiftFor;

        Vector3 BossWallCameraBase => wallDisplayCamera != null ? wallDisplayCamera.transform.position
            : bossDisplayCamera != null ? bossDisplayCamera.transform.position : Vector3.zero;

        // Called every fight frame from UpdateBossModel, after the boss has been placed.
        void CenterBossOnWall()
        {
            if (bossModel == null || bossDisplayCamera == null || wallDisplayCamera == null || !bossDisplayCamera.orthographic) return;
            if (bossWallShiftFor != bossModel)
            {
                bossWallShiftFor = bossModel;
                CacheActiveBossRenderers();          // the skin body, not the hidden old model
            }
            // Attack poses (raised arms, seven heads) change the silhouette, not where the boss stands: hold the framing.
            bool hold = bossModel.TryGetComponent<BossHydra>(out var hydra) && hydra.SevenHeadStrikeActive;
            var motion = bossModel.GetComponent<BossModelAnimation>();
            if (motion != null && motion.IsAttackMotionActive) hold = true;
            if (!hold)
            {
                var cam = bossDisplayCamera.transform;
                var body = BossWorldBounds().center;
                var basePos = BossWallCameraBase;
                // Horizontal: cancel only the body's own offset from its pivot, so the (narrowed) walk still shows.
                float wantX = Vector3.Dot(body - bossModel.position, cam.right);
                // Vertical: camera centre such that the body centre lands on the wanted height of the screen.
                float bodyY = Vector3.Dot(body - basePos, cam.up);
                float wantY = bodyY - (bossWallCenterViewportY - 0.5f) * 2f * bossDisplayCamera.orthographicSize;
                bossWallShift = Vector2.Lerp(bossWallShift, new Vector2(wantX, wantY), 1f - Mathf.Exp(-Time.deltaTime * bossWallCenterFollow));
            }
            ApplyBossWallShift();
        }

        void ApplyBossWallShift()
        {
            if (bossDisplayCamera == null || wallDisplayCamera == null) return;
            var w = wallDisplayCamera.transform;
            bossDisplayCamera.transform.position = w.position + w.right * bossWallShift.x + w.up * bossWallShift.y;
        }

        // A hidden boss (between fights) gets a straight camera, so the next one enters exactly as before.
        void ResetBossWallShift()
        {
            bossWallShift = Vector2.zero;
            bossWallShiftFor = null;
            ApplyBossWallShift();
        }
    }
}
