using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BossSlayer
{
    // บาส 2026-10-20: ABYSSAL KING, the final boss of the 15-minute boss rush (BossSlayerGameManager.Rush.cs).
    // Built in code (BossAbyssalKing.cs), kept OUT of bossModelCandidates so it never appears on the 9-card select and
    // the editor verifications that expect exactly 9 bosses stay valid. Its moves are chains of the ordinary floor
    // primitives (AddCircle / AddLine / AddZone / Beam), so stun, ultimate, death and menu cancel them like any attack.
    // Every hit is still telegraphed for at least 1.5 s (Balance Note B2) - "hard to dodge" comes from chaining,
    // hunting the players' current positions and shifting safe lanes, never from short warnings.
    public sealed partial class BossSlayerGameManager
    {
        [Header("Abyssal King (final boss)")]
        [Tooltip("Warning time for the king's regular moves (never below 1.5 s, Balance Note B2). 2 s = the game's normal telegraph.")]
        [SerializeField, Min(1.5f)] float abyssalKingWarningSeconds = 2f;
        [Tooltip("Warning time for each beat of the king's two BOSS ULTIMATE casts.")]
        [SerializeField, Min(1.5f)] float abyssalKingUltimateWarningSeconds = 1.9f;
        [Tooltip("Extra rest after each king move before the next one starts.")]
        [SerializeField, Min(0f)] float abyssalKingRestSeconds = 1.2f;
        Transform abyssalKingModel;
        bool kingPhasePending;
        float kingAwakening = -1, lastKingReleaseFx = -10;
        readonly List<int> kingMoveBag = new();
        const string KingTitle = "ABYSS / ";

        bool IsAbyssalKingActive => abyssalKingModel != null && bossModel == abyssalKingModel;
        BossAbyssalKing ActiveAbyssalKing => IsAbyssalKingActive ? abyssalKingModel.GetComponent<BossAbyssalKing>() : null;

        void EnsureAbyssalKing(Transform reference)
        {
            if (abyssalKingModel != null) return;
            var king = BossAbyssalKing.Create(reference);
            abyssalKingModel = king.transform;
            bossGlowColorByModel[abyssalKingModel] = BossThemePalette.Accent(BossAbyssalKing.ModelName);
            var floor = LoadBackgroundTexture(FinalFloorArt); // the violet rift floor, not the Fire floor
            // 2026-11-26: the king fights in front of the art team's Final Boss wall (the eye portal), not the Fire wall.
            var wall = LoadBackgroundTexture(FinalWallArt);
            if (floor != null) bossFloorBackgroundByModel[abyssalKingModel] = floor;
            if (wall != null) bossWallBackgroundByModel[abyssalKingModel] = wall;
            bossBaseTransforms[abyssalKingModel] = new BossBaseTransform(abyssalKingModel.localPosition, abyssalKingModel.localScale, abyssalKingModel.localRotation);
            Visual3DFollower.SetLayerRecursive(abyssalKingModel.gameObject, Visual3DFollower.BossLayer);
            abyssalKingModel.gameObject.SetActive(false);
        }

        void ResetAbyssalKingPhase()
        {
            kingPhasePending = false; kingAwakening = -1; kingMoveBag.Clear();
            if (abyssalKingModel != null) abyssalKingModel.GetComponent<BossAbyssalKing>()?.ResetMotion();
        }

        // ---------------------------------------------------------------- phase 2: THE CROWN IGNITES (50% HP)
        void ObserveAbyssalKingHealth()
        {
            if (!IsAbyssalKingActive || passives.KingAwakened || bossHp <= 0) return;
            if (bossHp <= bossMaxHp * .5f) kingPhasePending = true;
        }

        bool AdvanceAbyssalKingAwakening(float dt)
        {
            var king = ActiveAbyssalKing;
            if (!kingPhasePending || bossHp <= 0 || king == null) return false;
            if (dt <= 0 || Time.timeScale <= 0) return kingAwakening >= 0;
            if (stunTimer > 0 || ultimateActive || attacks.Count > 0) return false;
            if (kingAwakening < 0)
            {
                kingAwakening = 0; king.Awaken(); sfx?.Score?.Roar(); sfx?.Play("phase", .7f, 0, .3f, 5);
                SkinTransform(BossAbyssalKing.AwakenSeconds, 1.6f, "ignite");
                passiveFeedback?.Notify("THE CROWN IGNITES");
                JuiceMilestone("THE CROWN IGNITES", new Color(1f, .35f, .85f));
                ReplayBossTransform();
                // 2026-11-27: the toon smoke bursts on the awakening are gone ("มัน 2d การ์ตูนเกินไป").
            }
            kingAwakening = Mathf.Min(BossAbyssalKing.AwakenSeconds, kingAwakening + dt);
            if (kingAwakening >= BossAbyssalKing.AwakenSeconds)
            {
                passives.EnterKingAwakened(); kingPhasePending = false; kingAwakening = -1; attackTimer = .4f;
                passiveFeedback?.Notify("PHASE 2: THE CROWN IGNITES");
            }
            return true;
        }

        // ---------------------------------------------------------------- regular moves
        // บาส 2026-10-20 balance for real walking players (~1.2 m/s on the LED floor, ~0.5 s to read a warning):
        // every forced move gets at least  read time + distance / walk speed  between the warning appearing and the hit,
        // and no two sets of warnings that ask for opposite moves are on the floor at the same moment. The king stays
        // hard through chains and hunting, not through speed.
        const float KingWalkSpeed = 1.2f, KingReadSeconds = .5f;
        float KingWarn => Mathf.Max(SignatureWarning, abyssalKingWarningSeconds);
        float KingUltWarn => Mathf.Max(SignatureWarning, abyssalKingUltimateWarningSeconds);
        static float KingTravel(float meters) => KingReadSeconds + meters / KingWalkSpeed;

        void SpawnAbyssalKingTechnique()
        {
            if (kingMoveBag.Count == 0)
            {
                for (int i = 0; i < 6; i++) kingMoveBag.Add(i);
                for (int i = kingMoveBag.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (kingMoveBag[i], kingMoveBag[j]) = (kingMoveBag[j], kingMoveBag[i]); }
            }
            int move = kingMoveBag[kingMoveBag.Count - 1]; kingMoveBag.RemoveAt(kingMoveBag.Count - 1);
            bool fury = passives.KingAwakened;
            float w = KingWarn;
            switch (move)
            {
                case 0: // ABYSSAL CLAP: walls of force close in from both edges; the last one crushes the middle.
                {
                    // Each wall is 2 m wide: stepping out of the next one is ~1.2 m, so walls land >= 1.5 s apart.
                    float step = Mathf.Max(1.5f, KingTravel(1.2f));
                    KingLine("ABYSSAL CLAP", 1.0f, 2.0f, 0f, w, 5);
                    KingLine("ABYSSAL CLAP", arenaWidthMeters - 1.0f, 2.0f, 0f, w, 5);
                    KingLine("ABYSSAL CLAP", 3.1f, 2.0f, step, w, 5);
                    KingLine("ABYSSAL CLAP", arenaWidthMeters - 3.1f, 2.0f, step, w, 5);
                    KingLine("ABYSSAL CLAP", arenaWidthMeters * .5f, 2.2f, step * 2, w, 5);
                    break;
                }
                case 1: // VOID RAIN: shards fall where everyone stands, then scatter across the floor.
                {
                    int n = fury ? 10 : 8;
                    for (int i = 0; i < n; i++)
                    {
                        var c = KingCircle("VOID RAIN BARRAGE", RandomX(.9f), RandomZ(.85f), .85f, i * .35f, w, 4);
                        if (i % 3 == 0) c.KingHunt = true; // every third shard re-aims at a player as its warning starts
                    }
                    break;
                }
                case 2: // ROYAL EXECUTION: an X of eye beams from corner to corner, then a + through the centre.
                    KingBeam("ROYAL EXECUTION", new Vector2(0, 0), new Vector2(arenaWidthMeters, arenaDepthMeters), 1.0f, 0f, w, 3);
                    KingBeam("ROYAL EXECUTION", new Vector2(arenaWidthMeters, 0), new Vector2(0, arenaDepthMeters), 1.0f, 0f, w, 3);
                    // The + only appears after the X has landed, so the two never ask for moves at the same time.
                    KingBeam("ROYAL EXECUTION", new Vector2(arenaWidthMeters * .5f, 0), new Vector2(arenaWidthMeters * .5f, arenaDepthMeters), 1.1f, w + .6f, w, 3);
                    KingBeam("ROYAL EXECUTION", new Vector2(0, arenaDepthMeters * .5f), new Vector2(arenaWidthMeters, arenaDepthMeters * .5f), 1.0f, w + .6f, w, 3);
                    break;
                case 3: // SHADOW HANDS: the gauntlets hunt the players, slam after slam.
                {
                    // A 1.2 m slam re-aimed at you: stepping out is ~1.2 m, and the next slam waits until this one landed.
                    int n = fury ? 5 : 4;
                    float gap = Mathf.Max(1.6f, KingTravel(1.3f));
                    for (int i = 0; i < n; i++)
                    {
                        var c = KingCircle("SHADOW HANDS", RandomX(.8f), RandomZ(.7f), 1.2f, i * gap, w, 2);
                        c.KingHunt = true; c.KingHuntPlayer = i;
                    }
                    break;
                }
                case 4: // ECLIPSE BURST: centre, ring, centre again - in, out, in.
                {
                    float cx = arenaWidthMeters * .5f, cz = arenaDepthMeters * .5f;
                    float ringRx = Mathf.Min(4.3f, arenaWidthMeters * .39f), ringRz = Mathf.Min(1.85f, arenaDepthMeters * .37f);
                    KingCircle("ECLIPSE BURST", cx, cz, 2.0f, 0f, w, 4);
                    // Out of the centre (2 m) to between centre and ring, then back in: each step waits for the last hit.
                    float ring = w + .5f, back = ring + w + .5f;
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i / 10f * Mathf.PI * 2f;
                        KingCircle("ECLIPSE BURST", cx + Mathf.Cos(a) * ringRx, cz + Mathf.Sin(a) * ringRz, .9f, ring, w, 4);
                    }
                    KingCircle("ECLIPSE BURST", cx, cz, fury ? 1.8f : 1.5f, back, w, 4);
                    break;
                }
                default: // SOVEREIGN SWEEP: one half, the other half, then a blade across the middle.
                {
                    // Crossing to the other half can be ~5 m from the far edge, so the halves land a full crossing apart
                    // and each half gets a longer warning than the regular moves.
                    int side = Random.value < .5f ? -1 : 1;
                    float zoneWarn = Mathf.Max(w, KingTravel(2.5f)), cross = Mathf.Max(w, KingTravel(arenaWidthMeters * .45f));
                    KingZone("SOVEREIGN SWEEP", side, 0f, zoneWarn, 1);
                    // the second half is warned the moment the first lands, with a whole crossing's worth of warning
                    float second = zoneWarn + .2f;
                    KingZone("SOVEREIGN SWEEP", -side, second, cross, 1);
                    float blade = second + cross + .3f;
                    KingBeam("SOVEREIGN SWEEP", new Vector2(0, arenaDepthMeters * .5f), new Vector2(arenaWidthMeters, arenaDepthMeters * .5f), 1.2f, blade, w, 1);
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- the two BOSS ULTIMATE casts
        void SpawnAbyssalKingUltimate(int castIndex)
        {
            float w = KingUltWarn;
            if (castIndex == 0)
            {
                // ABYSSAL ECLIPSE: the floor burns in five lanes and only one lane is safe; the safe lane walks one step per
                // beat (2.2 m, so a beat is the hit + the walk), then a ring of void closes on everyone outside the centre.
                float lane = arenaWidthMeters / 5f, beat = w + Mathf.Max(.7f, KingTravel(lane) - w + .4f);
                int[] safe = Random.value < .5f ? new[] { 2, 1, 0, 1, 2 } : new[] { 2, 3, 4, 3, 2 };
                for (int b = 0; b < safe.Length; b++)
                    for (int l = 0; l < 5; l++)
                        if (l != safe[b]) KingLine("ULTIMATE ABYSSAL ECLIPSE", lane * (l + .5f), lane * .96f, b * beat, w, 4);
                float cx = arenaWidthMeters * .5f, cz = arenaDepthMeters * .5f, end = safe.Length * beat;
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f;
                    KingCircle("ULTIMATE ABYSSAL ECLIPSE", cx + Mathf.Cos(a) * Mathf.Min(4.4f, arenaWidthMeters * .4f), cz + Mathf.Sin(a) * Mathf.Min(1.9f, arenaDepthMeters * .38f), 1.05f, end, w, 4);
                }
            }
            else
            {
                // END OF ALL THRONES: a 5 x 2 checkerboard flips faster and faster, then every throne falls at once -
                // the X and + of ROYAL EXECUTION plus the centre - leaving only the four corners.
                // A flip is a 2.2 m step; each gap is longer than the warning, so only one set is ever warned at a time.
                float shortest = Mathf.Max(w + .3f, KingTravel(2.2f));
                float[] beats = { 0f, shortest + .9f, 0, 0, 0 };
                for (int b = 2; b < beats.Length; b++) beats[b] = beats[b - 1] + shortest + .3f * (beats.Length - 1 - b);
                for (int b = 0; b < beats.Length; b++)
                    for (int x = 0; x < 5; x++)
                        for (int z = 0; z < 2; z++)
                            if ((x + z + b) % 2 == 0)
                                KingCircle("ULTIMATE END OF ALL THRONES", (x + .5f) * arenaWidthMeters / 5f, (z + .5f) * arenaDepthMeters / 2f, 1.05f, beats[b], w, 4);
                // The finale: walking to a corner can be ~3 m, so it waits for the last flip and gets a long warning.
                float finale = beats[beats.Length - 1] + w + .4f, finaleWarn = Mathf.Max(w + .5f, KingTravel(3f));
                KingBeam("ULTIMATE END OF ALL THRONES", new Vector2(0, 0), new Vector2(arenaWidthMeters, arenaDepthMeters), 1.1f, finale, finaleWarn, 4);
                KingBeam("ULTIMATE END OF ALL THRONES", new Vector2(arenaWidthMeters, 0), new Vector2(0, arenaDepthMeters), 1.1f, finale, finaleWarn, 4);
                KingBeam("ULTIMATE END OF ALL THRONES", new Vector2(arenaWidthMeters * .5f, 0), new Vector2(arenaWidthMeters * .5f, arenaDepthMeters), 1.2f, finale, finaleWarn, 4);
                KingCircle("ULTIMATE END OF ALL THRONES", arenaWidthMeters * .5f, arenaDepthMeters * .5f, 1.8f, finale, finaleWarn, 4);
            }
        }

        // ---------------------------------------------------------------- primitives with their own start time
        // start = when the warning appears (seconds from now); warn = warning length; variant = king motion family.
        AttackState KingTimed(AttackState attack, float start, float warn, int variant)
        {
            attack.WarningDelay = start;
            attack.TelegraphTimer = start + warn;
            attack.TelegraphTotal = warn;
            attack.AnimationVariant = variant;
            if (attack.View != null) attack.View.gameObject.SetActive(start <= 0);
            return attack;
        }

        AttackState KingCircle(string title, float x, float z, float radius, float start, float warn, int variant)
        {
            float r = Mathf.Min(radius, Mathf.Min(arenaWidthMeters, arenaDepthMeters) * .5f);
            AddCircle(Title(title), Mathf.Clamp(x, r * .5f, arenaWidthMeters - r * .5f), Mathf.Clamp(z, r * .5f, arenaDepthMeters - r * .5f), r, start + warn);
            return KingTimed(attacks[attacks.Count - 1], start, warn, variant);
        }

        AttackState KingLine(string title, float x, float width, float start, float warn, int variant)
        {
            AddLine(Title(title), x, width, start + warn);
            return KingTimed(attacks[attacks.Count - 1], start, warn, variant);
        }

        AttackState KingZone(string title, int side, float start, float warn, int variant)
        {
            AddZone(Title(title), side, start + warn);
            return KingTimed(attacks[attacks.Count - 1], start, warn, variant);
        }

        // A straight beam between two floor points (metres; z = 0 is the wall edge). Drawn on the floor as a rotated
        // band and on the wall as a void ray from the king's eyes down to where the band meets the wall.
        AttackState KingBeam(string title, Vector2 from, Vector2 to, float width, float start, float warn, int variant)
        {
            if (from.y > to.y) (from, to) = (to, from); // start at the wall side, so the band's yMin end touches the seam
            var a = WorldToLocal(from); var b = WorldToLocal(to); var delta = b - a;
            var view = CreateRect("Attack == " + Title(title), (a + b) * .5f, new Vector2(width * PixelsPerMeter(), delta.magnitude), new Color(.8f, .1f, 1f, .35f));
            view.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90);
            var attack = new AttackState(Shape.Beam, view, start + warn, .6f)
            {
                BeamStart = from, BeamEnd = to, Width = width, Lingering = true, KingBeam = true,
                WarningRing = CreateSquareFrame("Void beam warning", view, new Color(.75f, .1f, 1f, 1f))
            };
            ConfigureFloorEffect(attack, title); attack.FloorForm = BossAttackVfx.AttackForm.Beam;
            attacks.Add(attack);
            return KingTimed(attack, start, warn, variant);
        }

        static string Title(string title) => title.StartsWith("ULTIMATE") ? title : KingTitle + title;

        // ---------------------------------------------------------------- per-frame: hunting aim + wall beams
        void UpdateAbyssalKingAttack(AttackState attack)
        {
            var king = ActiveAbyssalKing;
            if (king == null) return;
            if (attack.KingHunt && !attack.KingAimed && attack.WarningDelay <= 0f)
            {
                // Re-aim at a living player the moment this slam's warning appears (P1, P2... in turn).
                attack.KingAimed = true;
                var target = HuntTarget(attack.KingHuntPlayer);
                if (target.HasValue)
                {
                    float r = attack.Radius;
                    attack.X = Mathf.Clamp(target.Value.x, r * .5f, arenaWidthMeters - r * .5f);
                    attack.Z = Mathf.Clamp(target.Value.y, r * .5f, arenaDepthMeters - r * .5f);
                    if (attack.View != null) attack.View.anchoredPosition = WorldToLocal(new Vector2(attack.X, attack.Z));
                }
            }
            if (!attack.KingBeam || attack.Shape != Shape.Beam) return;
            bool warning = attack.TelegraphTimer > 0 && attack.WarningDelay <= 0;
            if (attack.ViewImage != null)
            {
                attack.ViewImage.enabled = warning;
                if (warning) { attack.ViewImage.material = null; attack.ViewImage.color = new Color(.75f, .15f, 1f, .26f + .14f * (.5f + .5f * Mathf.Sin(attack.TelegraphTimer * 14))); }
            }
            if (attack.ElementalVfx != null) attack.ElementalVfx.SetElement(warning ? new Color(.85f, .35f, 1f) : new Color(1f, .15f, .8f));
            UpdateAttackWarningRing(attack);
            var camera = bossDisplayCamera != null ? bossDisplayCamera : wallDisplayCamera;
            if (camera == null || floorDisplayCamera == null || attack.WarningDelay > 0) return;
            if (attack.BeamVfx == null)
            {
                var go = new GameObject("VFX == Void beam volume"); go.transform.SetParent(transform, false);
                attack.BeamVfx = go.AddComponent<BossCrystalBeamVfx>(); attack.BeamVfx.Initialize(bossModel.gameObject.layer);
                attack.BeamVfx.SetTint(new Color(.55f, .05f, 1f), new Color(2.4f, 1.1f, 3.2f));
                var floor = new GameObject("VFX == Floor void beam volume"); floor.transform.SetParent(transform, false);
                attack.FloorBeamVfx = floor.AddComponent<BossCrystalBeamVfx>();
                attack.FloorBeamVfx.Initialize(Visual3DFollower.FloorLayer, attack.View.GetComponentInParent<Canvas>().rootCanvas.sortingOrder + 2);
                attack.FloorBeamVfx.SetTint(new Color(.55f, .05f, 1f), new Color(2.4f, 1.1f, 3.2f));
            }
            var rect = attack.View.rect;
            float diameter = attack.View.TransformVector(Vector3.right * rect.width).magnitude;
            var lift = -floorDisplayCamera.transform.forward * diameter * .65f;
            var wallEnd = attack.View.TransformPoint(new Vector3(0, rect.yMin, 0)) + lift;
            var farEnd = attack.View.TransformPoint(new Vector3(0, rect.yMax, 0)) + lift;
            var seamViewport = floorDisplayCamera.WorldToViewportPoint(wallEnd);
            // Wall: a ray from the eyes to the shared seam above the beam's wall-side end (same mapping as the Golem).
            var origin = attack.AnimationVariant == 3 ? king.EyeWorld : king.HandWorld(attack.BeamStart.x < arenaWidthMeters * .5f ? 0 : 1);
            float wallDepth = camera.WorldToViewportPoint(origin).z;
            var seam = camera.ViewportToWorldPoint(new Vector3(Mathf.Clamp01(seamViewport.x), 0, wallDepth));
            float wallWidth = Vector3.Distance(camera.ViewportToWorldPoint(new Vector3(0, 0, wallDepth)), camera.ViewportToWorldPoint(new Vector3(.03f, 0, wallDepth)));
            attack.BeamVfx.Sample(origin, origin + (seam - origin) * 1.08f, wallWidth, attack.TelegraphTimer, attack.TelegraphTotal, attack.ActiveTimer);
            attack.FloorBeamVfx.gameObject.SetActive(!warning);
            var along = (farEnd - wallEnd) * .07f;
            attack.FloorBeamVfx.Sample(wallEnd - along, farEnd + along, diameter, attack.TelegraphTimer, attack.TelegraphTotal, attack.ActiveTimer);
        }

        Vector2? HuntTarget(int turn)
        {
            var alive = new List<Vector2>();
            foreach (var p in players)
                if (p.DownTimer <= 0f && TryGetInputLocalPoint(p.Index, out _)) alive.Add(LocalToWorld(p.Cursor.anchoredPosition));
            if (alive.Count == 0) return null;
            return alive[Mathf.Abs(turn) % alive.Count];
        }

        // Called once per attack the moment it lands: drives the strike pose and throws a burst from the right gauntlet.
        void OnAbyssalKingRelease(AttackState attack)
        {
            var king = ActiveAbyssalKing;
            if (king == null) return;
            king.Release(attack.AnimationVariant);
            if (Time.time - lastKingReleaseFx < .2f) return;
            lastKingReleaseFx = Time.time;
            int side = attack.Shape == Shape.Zone ? (attack.Side < 0 ? 0 : 1) : (attack.X < arenaWidthMeters * .5f ? 0 : 1);
            SpawnBossExplosionVfx(attack.KingBeam && attack.AnimationVariant == 3 ? king.EyeWorld : king.HandWorld(side), attack.IsBossUltimate ? 1.3f : .8f);
        }

        IEnumerator AbyssalKingDeathRoutine(BossAbyssalKing king)
        {
            king.BeginDeathPose();
            float nextBlast = 0;
            for (float age = 0; age < BossAbyssalKing.DeathPoseSeconds; age += Time.deltaTime)
            {
                king.SampleDeathPose(age);
                if (age >= nextBlast)
                {
                    nextBlast += .35f;
                    var bounds = BossWorldBounds();
                    SpawnBossExplosionVfx(bounds.center + Vector3.Scale(bounds.extents, new Vector3(Random.Range(-.8f, .8f), Random.Range(-.6f, .9f), 0)), Random.Range(.8f, 1.6f));
                }
                yield return null;
            }
            king.SampleDeathPose(BossAbyssalKing.DeathPoseSeconds);
            PlayBossDefeatBurst();
            for (float age = 0; age < BossDeathVfx.Duration; age += Time.deltaTime) { king.SampleDeathDissolve(age / BossDeathVfx.Duration); yield return null; }
            king.SampleDeathDissolve(1);
        }
    }
}
