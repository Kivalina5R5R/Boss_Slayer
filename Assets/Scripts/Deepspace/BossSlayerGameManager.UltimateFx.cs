using System.Collections.Generic;
using UnityEngine;

namespace Deepspace
{
    // Feeds BossUltimateOverlayGraphic from the live BOSS ULTIMATE attacks of every boss except the Demon (its own
    // BossDemonUltimateGraphic). Footprints are read from each attack's telegraph view, so the overlay always matches
    // what the attack really covers; released footprints are kept ~1.4 s as "ghosts" for the aftermath after the
    // attack itself is gone. Runs on scaled game time like the attacks.
    public sealed partial class BossSlayerGameManager
    {
        BossUltimateOverlayGraphic ultimateFloorFx, ultimateWallFx;
        readonly Dictionary<AttackState, float> ultimateSeeds = new();
        readonly HashSet<AttackState> ultimateReleased = new();
        readonly List<(BossUltimateOverlayGraphic.Item item, float releasedAt)> ultimateGhosts = new();
        readonly Vector3[] ultimateCorners = new Vector3[4];
        float ultimateFxClock, lastUltimateBurst = -9;

        static BossUltimateOverlayGraphic.Element UltimateElement(BossPassiveState.Kind kind) => kind switch
        {
            BossPassiveState.Kind.Dragon => BossUltimateOverlayGraphic.Element.Fire,
            BossPassiveState.Kind.Fish or BossPassiveState.Kind.Serpent => BossUltimateOverlayGraphic.Element.Water,
            BossPassiveState.Kind.Jellyfish => BossUltimateOverlayGraphic.Element.Lightning,
            BossPassiveState.Kind.Golem => BossUltimateOverlayGraphic.Element.Crystal,
            BossPassiveState.Kind.Wraith or BossPassiveState.Kind.AbyssalKing => BossUltimateOverlayGraphic.Element.Void,
            BossPassiveState.Kind.Sentinel => BossUltimateOverlayGraphic.Element.Tech,
            _ => BossUltimateOverlayGraphic.Element.Venom,
        };

        // บาส 2026-10-05 bug fix: these overlays are only hidden from AdvanceUltimateFx/AdvanceDemonUltimateFx, which
        // stop running once the boss dies or the game goes back to the menu, so an ultimate cut short by death/restart
        // left its cut lines, spear target and footprints frozen on the floor and wall. ClearAttacks now hides them.
        void ClearUltimateGraphics()
        {
            if (ultimateFloorFx != null) { ultimateFloorFx.Clear(); ultimateFloorFx.gameObject.SetActive(false); }
            if (ultimateWallFx != null) { ultimateWallFx.Clear(); ultimateWallFx.gameObject.SetActive(false); }
            ultimateSeeds.Clear(); ultimateReleased.Clear(); ultimateGhosts.Clear();
            if (demonFloorFx != null) { demonFloorFx.Clear(); demonFloorFx.gameObject.SetActive(false); }
            if (demonWallFx != null) { demonWallFx.Clear(); demonWallFx.gameObject.SetActive(false); }
            demonFxActive = false;
        }

        BossUltimateBanner ultimateBanner;
        bool bossUltimateShowing;

        // 2026-11-25: the "ULTIMATE SKILL" banner on the wall each time a boss starts an ultimate (BossUltimateBanner),
        // in its stage colour: fire 1, water 2, land 3, the Abyssal King 4.
        void TickUltimateBanner()
        {
            bool now = false;
            foreach (var a in attacks) if (a.IsBossUltimate) { now = true; break; }
            if (now && !bossUltimateShowing)
            {
                if (ultimateBanner == null) ultimateBanner = BossUltimateBanner.Create(cutsceneArea, transform);
                ultimateBanner?.Play(UltimateBannerStage(BossPassiveState.Identify(bossModel != null ? bossModel.name : "")));
            }
            bossUltimateShowing = now;
        }

        static int UltimateBannerStage(BossPassiveState.Kind kind) => kind switch
        {
            BossPassiveState.Kind.Fish or BossPassiveState.Kind.Jellyfish or BossPassiveState.Kind.Serpent => 2,
            BossPassiveState.Kind.Golem or BossPassiveState.Kind.Sentinel or BossPassiveState.Kind.Hydra => 3,
            BossPassiveState.Kind.AbyssalKing => 4,
            _ => 1,
        };

        void AdvanceUltimateFx(float dt)
        {
            TickUltimateBanner();
            bool any = ultimateGhosts.Count > 0;
            foreach (var a in attacks) if (a.IsBossUltimate && !a.DemonCut) { any = true; break; }
            if (!any)
            {
                if (ultimateFloorFx != null && ultimateFloorFx.gameObject.activeSelf)
                {
                    ultimateFloorFx.Clear(); ultimateWallFx.Clear();
                    ultimateFloorFx.gameObject.SetActive(false); ultimateWallFx.gameObject.SetActive(false);
                    ultimateSeeds.Clear(); ultimateReleased.Clear();
                }
                return;
            }
            EnsureUltimateFx();
            ultimateFxClock += dt;
            var kind = BossPassiveState.Identify(bossModel != null ? bossModel.name : "");
            ultimateFloorFx.Style = ultimateWallFx.Style = UltimateElement(kind);
            ultimateFloorFx.Crimson = ultimateWallFx.Crimson = golemRedPhase;
            ultimateFloorFx.Clock = ultimateWallFx.Clock = ultimateFxClock;
            var items = ultimateFloorFx.Items; items.Clear();
            foreach (var a in attacks)
            {
                if (!a.IsBossUltimate || a.DemonCut || a.View == null || a.WarningDelay > 0f || !a.View.gameObject.activeInHierarchy) continue;
                if (!ultimateSeeds.TryGetValue(a, out float seed)) ultimateSeeds[a] = seed = Random.value * 100;
                var item = Footprint(a, seed);
                if (a.TelegraphTimer > 0f)
                {
                    item.Warn = 1f - Mathf.Clamp01(a.TelegraphTimer / Mathf.Max(.01f, a.TelegraphTotal)); item.Age = -1;
                    items.Add(item);
                }
                else if (ultimateReleased.Add(a))
                {
                    item.Warn = -1;
                    ultimateGhosts.Add((item, ultimateFxClock));
                    // Releases within 0.12 s share one wall burst.
                    if (ultimateFxClock - lastUltimateBurst > .12f) { ultimateWallFx.Bursts.Add(ultimateFxClock); lastUltimateBurst = ultimateFxClock; }
                }
            }
            for (int i = ultimateGhosts.Count - 1; i >= 0; i--)
            {
                var (item, at) = ultimateGhosts[i];
                item.Age = ultimateFxClock - at;
                if (item.Age > 1.4f) { ultimateGhosts.RemoveAt(i); continue; }
                items.Add(item);
            }
            ultimateWallFx.Bursts.RemoveAll(b => ultimateFxClock - b > 1f);
            ultimateReleased.RemoveWhere(a => !attacks.Contains(a));
            var keys = new List<AttackState>(ultimateSeeds.Keys); foreach (var k in keys) if (!attacks.Contains(k)) ultimateSeeds.Remove(k);
            // Above every attack view (they are created later and would cover it) but still under the player cursors.
            var fxTransform = ultimateFloorFx.transform; int below = fxTransform.parent.childCount;
            foreach (var player in players) if (player.Cursor != null && player.Cursor.parent == fxTransform.parent) below = Mathf.Min(below, player.Cursor.GetSiblingIndex());
            if (fxTransform.GetSiblingIndex() != below - 1) fxTransform.SetSiblingIndex(fxTransform.GetSiblingIndex() < below ? below - 1 : below);
            ultimateFloorFx.SetVerticesDirty(); ultimateWallFx.SetVerticesDirty();
        }

        BossUltimateOverlayGraphic.Item Footprint(AttackState a, float seed)
        {
            a.View.GetWorldCorners(ultimateCorners);
            var fx = ultimateFloorFx.rectTransform;
            Vector2 P(int i) => fx.InverseTransformPoint(ultimateCorners[i]);
            var item = new BossUltimateOverlayGraphic.Item { Circle = a.Shape == Shape.Circle, C0 = P(0), C1 = P(1), C2 = P(2), C3 = P(3), Seed = seed };
            item.Center = (item.C0 + item.C2) * .5f;
            item.Radius = (item.C3 - item.C0).magnitude * .5f;
            return item;
        }

        void EnsureUltimateFx()
        {
            if (ultimateFloorFx == null)
            {
                ultimateFloorFx = MakeUltimateFx(InputArea, "VFX == Boss Ultimate Overlay (floor)", false);
                ultimateWallFx = MakeUltimateFx(cutsceneArea, "VFX == Boss Ultimate Overlay (wall)", true);
            }
            if (!ultimateFloorFx.gameObject.activeSelf)
            {
                ultimateFloorFx.gameObject.SetActive(true); ultimateWallFx.gameObject.SetActive(true);
                ultimateWallFx.transform.SetAsLastSibling();
            }
        }

        static BossUltimateOverlayGraphic MakeUltimateFx(RectTransform parent, string name, bool wall)
        {
            var rect = BossUiRect.Make(parent, name);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (!rect.TryGetComponent(out CanvasRenderer _)) rect.gameObject.AddComponent<CanvasRenderer>();
            var fx = rect.gameObject.AddComponent<BossUltimateOverlayGraphic>();
            fx.raycastTarget = false; fx.Wall = wall;
            rect.gameObject.SetActive(false);
            return fx;
        }
    }
}
