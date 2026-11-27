using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-19: "ถ้า Player ไปชนป้ายให้กล้องซูมไปที่บอสตัวนั้นตรงจอกำแพง" - while a player stands on a boss card on the
    // floor, the wall's boss hall picture (SelectBossBG_v7) glides in on that boss's statue. 2026-11-26: it stays on the
    // picked boss (also through the 3-2-1 after SELECT); only with no pick and nobody on a card does it glide back out
    // to the whole hall (BossSlayerGameManager.FocusWallOnStoodBoss).
    // The "camera" is the RawImage's uvRect, so nothing else on the wall moves.
    public sealed partial class BossSelectionPanel
    {
        // How much closer the camera gets on a statue (2 = half the hall's width on screen).
        const float WallZoom = 2.4f;
        // 2026-11-19: "ตอนถอยกล้องอย่าถอยไวเกินไป" - walking from card to card left a moment on no card, so the camera
        // zoomed out and straight back in. Now it waits WallZoomOutDelay on the statue before pulling back (a new card in
        // that time pans straight across), pans between statues more gently, and pulls back slowly.
        const float WallPanSeconds = 1f;
        const float WallZoomOutDelay = 2.2f;   // 2026-11-19: 1.2 s pulled back while walking between cards, 3 s felt too slow
        const float WallZoomOutSeconds = 1.8f;
        // The statues near the picture's ends cannot be centred (the view would run off the picture), so the camera
        // gets closer on them instead, until the statue sits at least this far in from the screen edge (0..0.5).
        // Without it the Demon / Fish and Serpent / Hydra shared one view.
        const float EdgeStatueInset = .3f;
        // The camera floats: it drifts up and down (and a little sideways) inside a view this much tighter than the
        // aimed one, so it never shows past the picture. 0.035 = 3.5% closer, moving up to 1.75% each way.
        // 2026-11-19: was .06 with a 4.2 s bob / 7.3 s sway - "ดูละยังมึนๆหัว", so smaller and slower.
        const float FloatMargin = .035f;
        const float FloatBobSeconds = 5.5f, FloatSwaySeconds = 9.5f;

        // Centre of each statue in the picture, as 0..1 of its width and height (bottom = 0), by boss kind 1..9:
        // Demon, Fish, Dragon, Jellyfish, Golem, Wraith, Sentinel, Serpent, Hydra. Measured on SelectBossBG_v6 and moved
        // onto the wider v7 (x = (600 + x_v6 * 4128) / 5328); remeasure if the picture changes. The Serpent aims high
        // (.60, its body is low but its head is near the top) so the zoom shows its head.
        static readonly Vector2[] StatueCentres =
        {
            new Vector2(.175f, .65f), new Vector2(.260f, .50f), new Vector2(.502f, .50f),
            new Vector2(.323f, .56f), new Vector2(.394f, .46f), new Vector2(.604f, .46f),
            new Vector2(.679f, .48f), new Vector2(.747f, .60f), new Vector2(.829f, .67f),
        };

        RawImage wallHall;
        Rect wallHallFull = new Rect(0, 0, 1, 1);
        Rect wallView = new Rect(0, 0, 1, 1);   // where the camera aims; LateUpdate adds the float
        int wallFocusKind = -1;
        float wallZoomOutAt = -1f;   // when a pending pull-back to the whole hall starts, or -1
        Tween wallZoomTween;

        // Called by ApplyWallLogo once the picture is in place: remembers the whole-hall view.
        void SetWallHall(RawImage hall, Rect fullView)
        {
            wallZoomTween?.Kill();
            wallHall = hall; wallHallFull = wallView = fullView; wallFocusKind = -1; wallZoomOutAt = -1f;
        }

        // 2026-11-19: "ตอนรูปปั้นถล่มลงมาให้กล้องสั่นด้วย" - while a statue collapses (BossStatueShatter.ShakeStrength) the
        // camera shakes, up to this much of the view's width / height each way.
        // 2026-11-19: "กล้องก็สั่นแรงกว่านี้หน่อย" - was .012.
        const float ShakeAmount = .024f, ShakeSpeed = 22f;
        // 2026-11-19: "ให้ซูมเข้าไปหาตัวที่ถล่มนิดๆ ไม่ต้องซูมแบบตอนเลือกตัว" - while a statue breaks the camera eases a little
        // closer to it (BreakZoom, against the select's WallZoom), then eases back to wherever it was aiming.
        const float BreakZoom = 1.35f, BreakEaseIn = .9f, BreakEaseOut = 1.4f;
        float breakBlend;          // 0 = the normal aim, 1 = the gentle look at the breaking statue
        Rect breakView;

        // Update, not LateUpdate: the hall's layers (statue shatter, eyes, aura, falling stones) place themselves in their
        // LateUpdate from this frame's view, so they shake and float exactly with the picture.
        void Update()
        {
            if (wallHall == null || wallHall.texture == null) return;
            float t = Time.unscaledTime;
            if (wallZoomOutAt >= 0f && t >= wallZoomOutAt) { wallZoomOutAt = -1f; MoveWallTo(0, WallZoomOutSeconds); }
            // the looked-at statue and the picked one glow (eyes, under light, rising squares)
            int lookedAt = wallFocusKind > 0 ? wallFocusKind : 0;
            if (wallEyes != null) wallEyes.Focus(lookedAt, wallPickedKind);
            if (wallAura != null) wallAura.Focus(lookedAt, wallPickedKind);
            int breaking = wallShatter != null ? wallShatter.BreakingKind : 0;
            if (breaking >= 1 && breaking <= StatueCentres.Length) breakView = StatueView(StatueCentres[breaking - 1], BreakZoom);
            float dt = Time.unscaledDeltaTime;
            breakBlend = breaking > 0 ? Mathf.MoveTowards(breakBlend, 1f, dt / BreakEaseIn) : Mathf.MoveTowards(breakBlend, 0f, dt / BreakEaseOut);
            var aim = breakBlend > 0f ? Lerp(wallView, breakView, Mathf.SmoothStep(0f, 1f, breakBlend)) : wallView;
            float bob = Mathf.Sin(t * 2f * Mathf.PI / FloatBobSeconds), sway = Mathf.Sin(t * 2f * Mathf.PI / FloatSwaySeconds);
            float w = aim.width * (1f - FloatMargin), h = aim.height * (1f - FloatMargin);
            float spareX = aim.width - w, spareY = aim.height - h;
            float x = aim.x + spareX * (.5f + .3f * sway), y = aim.y + spareY * (.5f + .5f * bob);
            float shake = wallShatter != null ? wallShatter.ShakeStrength : 0f;
            if (shake > 0f)
            {
                x += (Mathf.PerlinNoise(t * ShakeSpeed, 0f) - .5f) * 2f * ShakeAmount * w * shake;
                y += (Mathf.PerlinNoise(0f, t * ShakeSpeed) - .5f) * 2f * ShakeAmount * h * shake;
                x = Mathf.Clamp(x, 0f, 1f - w); y = Mathf.Clamp(y, 0f, 1f - h);   // never past the picture's edge
            }
            wallHall.uvRect = new Rect(x, y, w, h);
        }

        // Zooms to the statue of this boss kind, or back out to the whole hall for 0 / unknown kinds: after
        // WallZoomOutDelay, or at once when `now` (the 3-2-1 after SELECT). Safe to call every frame.
        public void FocusWallOn(int kind, bool now = false)
        {
            if (wallHall == null || wallHall.texture == null) return;
            bool statue = kind >= 1 && kind <= StatueCentres.Length;
            if (statue) { wallZoomOutAt = -1f; if (kind != wallFocusKind) MoveWallTo(kind, WallPanSeconds); return; }
            if (wallFocusKind == 0) return;
            if (now || wallFocusKind < 0) { wallZoomOutAt = -1f; MoveWallTo(0, WallZoomOutSeconds); }
            else if (wallZoomOutAt < 0f) wallZoomOutAt = Time.unscaledTime + WallZoomOutDelay;
        }

        void MoveWallTo(int kind, float seconds)
        {
            BossFightAudio.PlayUi("wall_zoom", .35f, .4f);   // 2026-11-25
            wallFocusKind = kind;
            var target = kind >= 1 && kind <= StatueCentres.Length ? StatueView(StatueCentres[kind - 1], WallZoom) : wallHallFull;
            wallZoomTween?.Kill();
            var from = wallView;
            wallZoomTween = DOTween.To(() => 0f, t => wallView = Lerp(from, target, t), 1f, seconds)
                .SetEase(Ease.InOutSine).SetUpdate(true).SetTarget(wallHall);
        }

        // The zoomed view centred on a statue, kept inside the picture so the edges never show. A statue close to an
        // end gets a closer view, so it still sits EdgeStatueInset in from the screen edge.
        Rect StatueView(Vector2 centre, float zoom)
        {
            float w = wallHallFull.width / zoom;
            float room = Mathf.Min(centre.x, 1f - centre.x) / EdgeStatueInset;
            if (room < w) w = room;
            float h = w * wallHallFull.height / wallHallFull.width;
            float x = Mathf.Clamp(centre.x - w * .5f, 0f, 1f - w), y = Mathf.Clamp(centre.y - h * .5f, 0f, 1f - h);
            return new Rect(x, y, w, h);
        }

        static Rect Lerp(Rect a, Rect b, float t) =>
            new Rect(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.width, b.width, t), Mathf.Lerp(a.height, b.height, t));
    }
}
