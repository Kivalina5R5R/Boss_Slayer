using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // Demon BOSS ULTIMATE "SEVERING JUDGEMENT" (บาส 2026-09-30, Judgement Cut reference): the Demon fights with blades and
    // a spear now, so the old INFERNAL CROWN fire circles gave way to
    //   1. two sets of five straight cuts across the whole floor at mixed angles; each set flickers as thin scratches for
    //      1 s, then every line cuts at once (0.3 s danger). Every set is re-rolled until it leaves room to stand:
    //      at most 40% of the floor cut and at least three 1.2 m clear spots;
    //   2. a spear thrown at the players' average position (1.2 s target warning), which then explodes three times,
    //      growing r 1.2 -> 2.0 -> 2.8 m, 0.55 s apart: running outward early is the answer.
    // Damage per hit is the normal boss hit, like every other ultimate. All stages live in the normal attack queue,
    // so stun, death and menu cancel them together.
    public sealed partial class BossSlayerGameManager
    {
        public const int DemonJudgementMotion = 12, DemonSpearMotion = 13;
        const int JudgementCuts = 5;
        const float JudgementWarning = SignatureWarning /* บาส 2026-10-06 B2: was 1.0 */, JudgementWindow = .3f, JudgementSetGap = .6f, JudgementWidth = .45f;
        const float JudgementMaxCover = .40f, JudgementClearRadius = .6f;
        const float SpearWarning = SignatureWarning /* บาส 2026-10-06 B2: was 1.2 */, SpearBlastStep = .55f, SpearBlastWindow = .35f;
        static readonly float[] SpearBlastRadius = { 1.2f, 2.0f, 2.8f };

        BossDemonUltimateGraphic demonFloorFx, demonWallFx;
        bool demonFxActive;

        void SpawnDemonJudgement(string title)
        {
            EnsureDemonFx();
            float setA = SignatureWarning;
            float setB = setA + JudgementWarning + JudgementWindow + JudgementSetGap;
            SpawnJudgementSet(title, setA);
            SpawnJudgementSet(title, setB);
            SpawnDemonSpear(title, setB + JudgementWarning + JudgementWindow + .4f);
        }

        void SpawnJudgementSet(string title, float start)
        {
            var cuts = RollJudgementCuts();
            foreach (var (a, b) in cuts)
            {
                var la = WorldToLocal(a); var lb = WorldToLocal(b); var delta = lb - la;
                var view = CreateRect("ULTIMATE " + title + " CUT", (la + lb) * .5f,
                    new Vector2(JudgementWidth * PixelsPerMeter(), delta.magnitude), new Color(1, .1f, .15f, .4f));
                view.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90);
                var core = BossUiRect.Make(view, "Cut Core");
                core.anchorMin = new Vector2(.5f, 0); core.anchorMax = new Vector2(.5f, 1); core.pivot = new Vector2(.5f, .5f);
                core.sizeDelta = new Vector2(JudgementWidth * PixelsPerMeter() * .22f, 0); core.anchoredPosition = Vector2.zero;
                var coreImage = core.gameObject.AddComponent<Image>(); coreImage.raycastTarget = false; coreImage.color = Color.clear;
                var attack = new AttackState(Shape.Beam, view, start + JudgementWarning, JudgementWindow)
                {
                    BeamStart = a, BeamEnd = b, Width = JudgementWidth, WarningDelay = start, TelegraphTotal = JudgementWarning,
                    Lingering = true, AnimationVariant = DemonJudgementMotion, DemonCut = true, DemonCutCore = core
                };
                attack.FloorForm = BossAttackVfx.AttackForm.Beam;
                view.gameObject.AddComponent<CanvasGroup>().alpha = 0; // drawn by BossDemonUltimateGraphic
                view.gameObject.SetActive(start <= 0);
                attacks.Add(attack);
                demonFloorFx.Cuts.Add(new BossDemonUltimateGraphic.Cut { A = la, B = lb, Width = JudgementWidth * PixelsPerMeter(),
                    WarnAt = start, ReleaseAt = start + JudgementWarning, Seed = Random.value * 100 });
                demonWallFx.Cuts.Add(new BossDemonUltimateGraphic.Cut { WarnAt = start, ReleaseAt = start + JudgementWarning });
            }
        }

        // Five long cuts at 25..155 degrees through random points, re-rolled until the floor keeps safe ground.
        List<(Vector2, Vector2)> RollJudgementCuts()
        {
            float w = arenaWidthMeters, d = arenaDepthMeters, reach = w + d;
            List<(Vector2, Vector2)> best = null; float bestCover = float.MaxValue;
            for (int attempt = 0; attempt < 80; attempt++)
            {
                var cuts = new List<(Vector2, Vector2)>(); var angles = new List<float>();
                for (int i = 0; i < JudgementCuts; i++)
                {
                    float angle = 0;
                    for (int tries = 0; tries < 20; tries++)
                    {
                        angle = Random.Range(25f, 155f);
                        bool apart = true;
                        foreach (var other in angles) if (Mathf.Abs(Mathf.DeltaAngle(angle, other)) < 18f) { apart = false; break; }
                        if (apart) break;
                    }
                    angles.Add(angle);
                    var p = new Vector2(Random.Range(w * .1f, w * .9f), Random.Range(d * .15f, d * .85f));
                    var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                    cuts.Add((p - dir * reach, p + dir * reach));
                }
                float cover = JudgementCover(cuts, out int clearSpots);
                if (cover <= JudgementMaxCover && clearSpots >= 3) return cuts;
                if (cover < bestCover) { bestCover = cover; best = cuts; }
            }
            return best;
        }

        // Share of the floor inside any cut, and how many separate clear spots (a 1.2 m circle touching no cut, 2 m apart) remain.
        float JudgementCover(List<(Vector2, Vector2)> cuts, out int clearSpots)
        {
            const int nx = 44, nz = 20;
            int covered = 0; var spots = new List<Vector2>();
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    var p = new Vector2((ix + .5f) / nx * arenaWidthMeters, (iz + .5f) / nz * arenaDepthMeters);
                    float nearest = float.MaxValue;
                    foreach (var (a, b) in cuts) nearest = Mathf.Min(nearest, DistanceToSegment(p, a, b));
                    if (nearest < JudgementWidth * .5f) covered++;
                    if (nearest < JudgementWidth * .5f + JudgementClearRadius) continue;
                    bool far = true;
                    foreach (var s in spots) if ((s - p).sqrMagnitude < 4f) { far = false; break; }
                    if (far) spots.Add(p);
                }
            clearSpots = spots.Count;
            return covered / (float)(nx * nz);
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var d = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, d) / Mathf.Max(.0001f, d.sqrMagnitude));
            return Vector2.Distance(p, a + d * t);
        }

        // Spear lands on the players' average position, then three growing blasts. Largest added first so it draws beneath;
        // it also carries the thrown spear graphic because it lives longest.
        void SpawnDemonSpear(string title, float start)
        {
            var target = new Vector2(arenaWidthMeters * .5f, arenaDepthMeters * .5f); int count = 0; var sum = Vector2.zero;
            foreach (var player in players)
            {
                if (player.DownTimer > 0f || !TryGetInputLocalPoint(player.Index, out _)) continue;
                sum += LocalToWorld(player.Cursor.anchoredPosition); count++;
            }
            if (count > 0) target = sum / count;
            target.x = Mathf.Clamp(target.x, 1f, arenaWidthMeters - 1f);
            target.y = Mathf.Clamp(target.y, 1f, arenaDepthMeters - 1f);
            demonFloorFx.HasSpear = demonWallFx.HasSpear = true;
            demonFloorFx.SpearTarget = WorldToLocal(target); demonFloorFx.SpearAimAt = demonWallFx.SpearAimAt = start;
            for (int i = 0; i < SpearBlastRadius.Length; i++)
                foreach (var fx in new[] { demonFloorFx, demonWallFx })
                { fx.BlastAt.Add(start + SpearWarning + i * SpearBlastStep); fx.BlastRadius.Add(SpearBlastRadius[i] * PixelsPerMeter()); }
            for (int i = SpearBlastRadius.Length - 1; i >= 0; i--)
            {
                float release = start + SpearWarning + i * SpearBlastStep;
                AddCircle("ULTIMATE " + title + " SPEAR", target.x, target.y, SpearBlastRadius[i], release);
                var blast = attacks[attacks.Count - 1];
                blast.WarningDelay = start; blast.TelegraphTimer = release; blast.TelegraphTotal = release - start;
                blast.ActiveTimer = release + SpearBlastWindow; blast.Lingering = false;
                blast.AnimationVariant = DemonSpearMotion;
                // บาส 2026-10-05: drawn only by BossDemonUltimateGraphic (red). Without this the generic ultimate overlay also
                // drew these rings, and Demon has no element there so it fell back to Venom = a green warning.
                blast.DemonCut = true;
                blast.View.gameObject.AddComponent<CanvasGroup>().alpha = 0; // drawn by BossDemonUltimateGraphic
                blast.View.gameObject.SetActive(start <= 0);
                continue;
            }
        }

        void UpdateDemonUltimate(AttackState attack) { }

        void EnsureDemonFx()
        {
            if (demonFloorFx == null)
            {
                demonFloorFx = MakeDemonFx(InputArea, "VFX == Demon Judgement (floor)", false);
                demonWallFx = MakeDemonFx(cutsceneArea, "VFX == Demon Judgement (wall)", true);
            }
            demonFloorFx.Clear(); demonWallFx.Clear();
            demonFloorFx.gameObject.SetActive(true); demonWallFx.gameObject.SetActive(true);
            PlaceHazardBelowPlayers((RectTransform)demonFloorFx.transform);
            demonWallFx.transform.SetAsLastSibling();
            demonFxActive = true;
        }

        static BossDemonUltimateGraphic MakeDemonFx(RectTransform parent, string name, bool wall)
        {
            var rect = BossUiRect.Make(parent, name);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (!rect.TryGetComponent(out CanvasRenderer _)) rect.gameObject.AddComponent<CanvasRenderer>();
            var fx = rect.gameObject.AddComponent<BossDemonUltimateGraphic>();
            fx.raycastTarget = false; fx.Wall = wall;
            rect.gameObject.SetActive(false);
            return fx;
        }

        // Scaled game time, like the attacks; a stun / death / menu that clears the attacks early also ends the show.
        void AdvanceDemonUltimateFx(float dt)
        {
            if (!demonFxActive || demonFloorFx == null) return;
            demonFloorFx.Clock += dt; demonWallFx.Clock = demonFloorFx.Clock;
            float clock = demonFloorFx.Clock, end = demonFloorFx.LastEvent;
            bool cancelled = attacks.Count == 0 && clock < end - .05f;
            if (cancelled || clock > end + 1.6f)
            {
                demonFxActive = false;
                demonFloorFx.Clear(); demonWallFx.Clear();
                demonFloorFx.gameObject.SetActive(false); demonWallFx.gameObject.SetActive(false);
                return;
            }
            demonFloorFx.SetVerticesDirty(); demonWallFx.SetVerticesDirty();
        }
    }
}
