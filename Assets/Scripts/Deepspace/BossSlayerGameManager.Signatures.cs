using UnityEngine;

namespace Deepspace
{
    public sealed partial class BossSlayerGameManager
    {
        int bossSignatureCastCount;
        float lowestBossSignatureHp = 1f;
        string bossSignatureTitle;
        BossUltimateAura bossUltimateAura;
        TMPro.TextMeshProUGUI floorSignatureBanner, wallSignatureBanner;
        // บาส 2026-10-06 (Balance Note B2): 0.7 s let a real player walk < 0.6 m, but ultimate circles are 1.4-1.65 m
        // radius, so standing in the middle was undodgeable. 1.5 s per beat; beats 3 s apart so longer warnings don't overlap.
        // Every ultimate with its own warning (Demon cuts/spear, Golem beams, Sentinel volleys, Serpent waves) uses >= this.
        const float SignatureWarning = 1.5f;
        const float SignatureBeat = 3f;
        const float SignatureWaveSeconds = 2.1f;
        const float SignatureWaveBeat = 4.2f;

        void ObserveBossSignatureHealth()
        {
            lowestBossSignatureHp = Mathf.Min(lowestBossSignatureHp, bossHp / (float)Mathf.Max(1, bossMaxHp));
        }

        // บาส 2026-11-12 "ทุกตัวที่มีร่างสองให้แปลงร่างก่อนแล้วค่อยทำท่าดาเมจ ... hitbox จริงที่โดนผู้เล่นจริงๆก็ต้องขึ้นมาหลังเปลี่ยนร่าง":
        // at half HP a boss with a second form (Dragon, Demon, Golem, Abyssal King) transforms first; its ultimate (hazards and
        // clip) waits until the new form is in. Read from HP, not only the pending flags, so the frame that crosses 50% is covered.
        bool AwaitingPhaseTransform()
        {
            if (bossHp <= 0) return false;
            bool half = bossHp <= bossMaxHp * .5f;
            switch (passives.Active)
            {
                case BossPassiveState.Kind.Dragon: return passives.DragonPhasePending || (half && !passives.DragonHeated);
                case BossPassiveState.Kind.Demon: return passives.DemonPhasePending || (half && !passives.DemonAdult);
                case BossPassiveState.Kind.Golem: return golemPhasePending || (half && !golemRedPhase);
                case BossPassiveState.Kind.AbyssalKing: return kingPhasePending || (half && !passives.KingAwakened);
                default: return false;
            }
        }

        bool TrySpawnBossSignature()
        {
            ObserveBossSignatureHealth();
            if (AwaitingPhaseTransform()) return false; // the threshold is remembered: the ultimate comes right after the change
            // An interrupted opening ultimate must finish before the transformation.
            if(bossHp>0 && passives.DragonPhasePending && bossSignatureCastCount==1 && !dragonFirstUltimateCompleted) return SpawnBossSignature();
            // Remember crossed thresholds even if Fish heals or the boss is stunned.
            // A large hit crossing both thresholds queues two separate casts, never stacked.
            if (bossHp <= 0 || bossSignatureCastCount >= 2) return false;
            float threshold = bossSignatureCastCount == 0 ? .5f : .15f;
            if (lowestBossSignatureHp > threshold || !SpawnBossSignature()) return false;
            bossSignatureCastCount++;
            return true;
        }
        // All stages belong to the normal attack queue: stun, death and menu cancel them together.
        bool SpawnBossSignature()
        {
            var kind = BossPassiveState.Identify(bossModel != null ? bossModel.name : string.Empty);
            if (kind == BossPassiveState.Kind.None) return false;
            int first = attacks.Count;
            string title;
            switch (kind)
            {
                case BossPassiveState.Kind.Demon:
                    // Blades and spear now (was INFERNAL CROWN fire circles): see BossSlayerGameManager.DemonUltimate.cs.
                    title = "SEVERING JUDGEMENT";
                    SpawnDemonJudgement(title);
                    break;
                case BossPassiveState.Kind.Fish:
                    title = "TIDAL JAWS";
                    for (int i = 0; i < 3; i++)
                    {
                        AddZone("ULTIMATE " + title, i % 2 == 0 ? -1 : 1, SignatureWarning + i * SignatureBeat);
                        SignatureTiming(attacks[attacks.Count - 1], i * SignatureBeat);
                    }
                    SignatureCircle(title, .5f, .5f, .28f, 3);
                    break;
                case BossPassiveState.Kind.Dragon:
                    title = "METEOR APOCALYPSE";
                    SpawnDragonMeteorShower(title);
                    break;
                case BossPassiveState.Kind.Jellyfish:
                    title = "THUNDER BLOOM";
                    for (int beat = 0; beat < 3; beat++)
                        for (int x = 0; x < 4; x++)
                            for (int z = 0; z < 2; z++)
                                if ((x + z) % 2 == beat % 2)
                                    SignatureCircle(title, (x + .5f) / 4, (z + .5f) / 2, .19f, beat);
                    break;
                case BossPassiveState.Kind.Golem:
                    if(golemRedPhase){title="CRIMSON REFRACTION";SpawnGolemBeams(title);break;}
                    title = "CRYSTAL PRISON";
                    SignatureLine(title, .22f, .075f, 0);
                    SignatureLine(title, .78f, .075f, 0);
                    SignatureCircle(title, .5f, .5f, .3f, 1);
                    SignatureLine(title, .4f, .075f, 2);
                    SignatureLine(title, .6f, .075f, 2);
                    break;
                case BossPassiveState.Kind.Wraith:
                    title = "VOID COLLAPSE";
                    // Portals close inward; retreat toward either end of the arena.
                    for (int beat = 0; beat < 3; beat++)
                    {
                        SignatureCircle(title, .18f + beat * .12f, .3f, .22f, beat);
                        SignatureCircle(title, .82f - beat * .12f, .7f, .22f, beat);
                    }
                    SignatureCircle(title, .5f, .5f, .33f, 3);
                    break;
                case BossPassiveState.Kind.Sentinel:
                    title = "ARSENAL OVERDRIVE";
                    SpawnSentinelArsenal(title);
                    break;
                case BossPassiveState.Kind.Serpent:
                    title = "LEVIATHAN SURGE";
                    SpawnSerpentSurge(title);
                    break;
                case BossPassiveState.Kind.AbyssalKing:
                    title = bossSignatureCastCount == 0 ? "ABYSSAL ECLIPSE" : "END OF ALL THRONES";
                    SpawnAbyssalKingUltimate(bossSignatureCastCount);
                    break;
                case BossPassiveState.Kind.Hydra:
                    title = "SEVENFOLD FANGS";
                    var hydra = bossModel.GetComponent<BossHydra>();
                    if (hydra == null) return false;
                    SpawnHydraSevenHeadStrike(title,hydra);
                    break;
                default: return false;
            }
            if (attacks.Count == first) return false;
            for (int i = first; i < attacks.Count; i++) attacks[i].IsBossUltimate = true;
            float chargeSeconds=kind==BossPassiveState.Kind.Hydra?BossHydra.SevenHeadImpactTime:SignatureWarning;
            bossSignatureTitle = title;
            if (bossUltimateAura == null)
            {
                var aura = new GameObject("VFX == Boss Ultimate Aura"); aura.transform.SetParent(transform, false);
                bossUltimateAura = aura.AddComponent<BossUltimateAura>();
            }
            var ultimateColor = bossGlowColorByModel.TryGetValue(bossModel, out var glow) ? glow : new Color(1, .25f, .1f);
            bossUltimateAura.Play(bossModel, bossDisplayCamera != null ? bossDisplayCamera : wallDisplayCamera, BossWorldBounds(), ultimateColor, chargeSeconds);
            UpdateBossSignatureBanner();
            passiveFeedback?.Notify("ULTIMATE: " + title);
            // Larger elemental charge distinguishes the ultimate from regular casts.
            if (elementalChargeVfx != null)
            {
                var accent = bossGlowColorByModel.TryGetValue(bossModel, out var color) ? color : Color.white;
                elementalChargeVfx.Play(bossModel, bossDisplayCamera != null ? bossDisplayCamera : wallDisplayCamera,
                    activeBossRenderers, accent, chargeSeconds, attackChargeVfxScale * 1.6f);
            }
            sfx?.StartCharge(SoundTheme, chargeSeconds);
            // บาส 2026-10-06: with 1.5 s warnings the ultimate is dodgeable, so the team escaping it is a clip (Replay Spec §6.2).
            ReplayLog.Event("boss_ultimate", 1, 6, null, ReplayLog.Data().Add("boss", ReplayBoss()).Add("title", title)
                .Add("bossHpPct", Mathf.RoundToInt(100f * bossHp / Mathf.Max(1, bossMaxHp))));
            return true;
        }

        void UpdateBossSignatureBanner()
        {
            bool visible = !string.IsNullOrEmpty(bossSignatureTitle) && (attacks.Count > 0 || HydraComboPending) && gameStarted && !resultShown;
            if (visible)
            {
                // 2026-11-24 "ลบ UI Skill จากจอพื้นออก": the ultimate's name is shown on the wall only; the floor keeps
                // its view clear (floorSignatureBanner stays null, every user of it checks for that).
                if (wallSignatureBanner == null) wallSignatureBanner = CreateSignatureBanner(cutsceneArea, true);
            }
            foreach (var label in new[] { floorSignatureBanner, wallSignatureBanner })
            {
                if (label == null) continue;
                label.transform.parent.gameObject.SetActive(visible);
                if (!visible) continue;
                // Wall: two lines, small dimmed kicker over a big ultimate name (see CreateSignatureBanner).
                string text = label == wallSignatureBanner ? "<size=50%><alpha=#B8>BOSS ULTIMATE</size><alpha=#FF>\n" + bossSignatureTitle
                    : "BOSS ULTIMATE  /  " + bossSignatureTitle;
                if (label.text != text) label.text = text;
            }
            if (!visible && bossUltimateAura != null) bossUltimateAura.Stop();
            if (attacks.Count == 0 && !HydraComboPending) bossSignatureTitle = null;
        }

        TMPro.TextMeshProUGUI CreateSignatureBanner(RectTransform area, bool wall = false)
        {
            if (area == null) return null;
            var source = area.GetComponentInParent<Canvas>().rootCanvas;
            var root = new GameObject("Boss Ultimate Announcement", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = source.targetDisplay;
            canvas.sortingOrder = 210;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var textObject = new GameObject("Ultimate Name", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));

            textObject.transform.SetParent(root.transform, false);
            var label = textObject.GetComponent<TMPro.TextMeshProUGUI>();
            var rect = label.rectTransform;
            // บาส 2026-09-29 playtest fix: on the wall the old top strip (.12-.88 x, .89-.99 y) sat right on the P1-P3
            // cards, the boss name plate and the HP bar. The wall banner now mirrors the passive panel in the free
            // bottom-left corner (passive = bottom-right, .975/.04, 29%x24%): nothing lives there for any 1-6 player
            // count (P1-P3 top-left, P4-P6 top-right, ultimate gauge starts at x=.385), so HP, name and banner all stay
            // readable, and it is ~2.7x taller than the old strip (two lines, name up to 84 units vs 54).
            // The floor strip stays at the top: nothing collides there.
            // บาส 2026-10-22: the player HP cards took the bottom-left corner (up to y≈.40), so the banner sits above them.
            if (wall) { rect.anchorMin = new Vector2(.015f, .43f); rect.anchorMax = new Vector2(.32f, .70f); }
            else { rect.anchorMin = new Vector2(.12f, .89f); rect.anchorMax = new Vector2(.88f, .99f); }
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            BossFantasyTheme.UseFont(label, BossUiFont);
            label.fontStyle = TMPro.FontStyles.Bold;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = wall ? 84 : 54;
            if (wall) { label.lineSpacing = -8; label.textWrappingMode = TMPro.TextWrappingModes.NoWrap; label.margin = new Vector4(24, 12, 24, 12); }
            label.color = new Color(1, .88f, .25f); label.outlineWidth = .22f;
            label.raycastTarget = false;
            var background = new GameObject("Ultimate Banner Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            background.transform.SetParent(textObject.transform, false);
            var bg = background.GetComponent<RectTransform>(); bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
            background.GetComponent<UnityEngine.UI.Image>().color = new Color(.08f, .015f, .04f, .88f);
            background.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            // Background must render before the label, so place it as a sibling.
            bg.SetParent(root.transform, false); bg.anchorMin = rect.anchorMin; bg.anchorMax = rect.anchorMax; bg.offsetMin = bg.offsetMax = Vector2.zero; bg.SetAsFirstSibling();
            Visual3DFollower.SetLayerRecursive(root, Visual3DFollower.UILayer);
            return label;
        }
        void SignatureCircle(string title, float x, float z, float radius, int beat)
        {
            AddCircle("ULTIMATE " + title, x * arenaWidthMeters, z * arenaDepthMeters,
                radius * Mathf.Min(arenaWidthMeters, arenaDepthMeters), SignatureWarning + beat * SignatureBeat);
            SignatureTiming(attacks[attacks.Count - 1], beat * SignatureBeat);
        }

        void SignatureLine(string title, float x, float width, int beat)
        {
            AddLine("ULTIMATE " + title, x * arenaWidthMeters, width * arenaWidthMeters,
                SignatureWarning + beat * SignatureBeat);
            SignatureTiming(attacks[attacks.Count - 1], beat * SignatureBeat);
        }

        static void SignatureTiming(AttackState attack, float delay)
        {
            attack.WarningDelay = delay;
            attack.TelegraphTotal = SignatureWarning;
            attack.View.gameObject.SetActive(delay <= 0);
        }

        void SignatureWave(string title, bool horizontal, int direction, float delay)
        {
            float width = Mathf.Min(1.1f, arenaDepthMeters * .22f);
            var size = horizontal ? new Vector2(FloorRect.width, width * FloorRect.height / Mathf.Max(.01f, arenaDepthMeters))
                : new Vector2(width * PixelsPerMeter(), FloorRect.height);
            var view = CreateRect("ULTIMATE " + title, Vector2.zero, size, new Color(.1f, .7f, 1f, .5f));
            var attack = new AttackState(Shape.Wave, view, SignatureWarning + delay, SignatureWaveSeconds)
            { WaveHorizontal = horizontal, WaveDir = direction, Width = width, MoveSeconds = SignatureWaveSeconds };
            SignatureTiming(attack, delay);
            float span = horizontal ? arenaWidthMeters : arenaDepthMeters;
            float center = span * .5f;
            float halfGap = Mathf.Min(1f, span * .22f);
            attack.WaveGaps.Add(new Vector2(center - halfGap, center + halfGap));
            attack.ViewImage.enabled = false;
            CreateWaveSection(attack, 0, center - halfGap, false);
            CreateWaveSection(attack, center - halfGap, center + halfGap, true);
            CreateWaveSection(attack, center + halfGap, span, false);
            PlaceHazardBelowPlayers(view);
            UpdateWaveView(attack);
            attacks.Add(attack);
        }
    }
}