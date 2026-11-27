using UnityEngine;

namespace BossSlayer
{
    public sealed partial class BossSlayerGameManager
    {
        float bossMotionClock, bossTravelSpeed;
        int previousBossMove = -1;
        readonly System.Collections.Generic.List<int> bossMoveBag = new();

        void ResetBossVariety()
        {
            bossMotionClock = bossTravelSpeed = 0;
            previousBossMove = -1; bossMoveBag.Clear();
        }

        void AdvanceBossLocomotion(float dt)
        {
            bossMotionClock += dt;
            var kind = BossPassiveState.Identify(bossModel != null ? bossModel.name : "");
            float cycle = kind == BossPassiveState.Kind.Golem ? 6.8f : kind == BossPassiveState.Kind.Sentinel ? 5f : 5.8f;
            float stride = Mathf.Repeat(bossMotionClock, cycle);
            float moving = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.1f, 1.8f, stride))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(cycle - .8f, cycle, stride)));
            float speed = kind == BossPassiveState.Kind.Golem ? .65f : kind == BossPassiveState.Kind.Fish ? 1.35f
                : kind == BossPassiveState.Kind.Serpent ? 1.2f : kind == BossPassiveState.Kind.Wraith ? .75f : .95f;
            if(kind==BossPassiveState.Kind.Serpent) moving=.78f+.22f*Mathf.Sin(bossMotionClock*1.8f);
            var animation = bossModel != null ? bossModel.GetComponent<BossModelAnimation>() : null;
            if (animation != null && animation.IsAttackMotionActive) moving *= .15f;
            if(bossModel!=null && bossModel.TryGetComponent<BossHydra>(out var hydra) && hydra.SevenHeadStrikeActive)moving=0;
            bossTravelSpeed = Mathf.Lerp(bossTravelSpeed, moving * speed, 1 - Mathf.Exp(-dt * 5));
            bossX += bossDir * bossTravelSpeed * dt;
            animation?.SetLocomotion(bossDir * bossTravelSpeed / speed);
            if (bossModel != null) bossModel.GetComponent<BossHydra>()?.SetLocomotion(bossDir * bossTravelSpeed / speed);
        }

        int NextBossMove()
        {
            if (bossMoveBag.Count == 0)
            {
                for (int i = 0; i < 10; i++) bossMoveBag.Add(i);
                for (int i = bossMoveBag.Count - 1; i > 0; i--)
                { int j = Random.Range(0, i + 1); int value = bossMoveBag[i]; bossMoveBag[i] = bossMoveBag[j]; bossMoveBag[j] = value; }
                if (bossMoveBag[bossMoveBag.Count - 1] == previousBossMove)
                { int value = bossMoveBag[0]; bossMoveBag[0] = bossMoveBag[bossMoveBag.Count - 1]; bossMoveBag[bossMoveBag.Count - 1] = value; }
            }
            int last = bossMoveBag.Count - 1; previousBossMove = bossMoveBag[last]; bossMoveBag.RemoveAt(last);
            return previousBossMove;
        }

        void SpawnVariedBossAttack(int ordinaryPattern)
        {
            int chosenPattern = passives.ChoosePattern(ordinaryPattern);
            // Preserve the serpent's fourth-cast passive independently of its move bag.
            if (passives.Active == BossPassiveState.Kind.Serpent && passives.AttacksLaunched % 4 == 0)
            { SpawnAttackPattern(chosenPattern); return; }
            int move = NextBossMove();
            if (move < 9 && SpawnBossTechnique(move)) return;
            var hydra = bossModel != null ? bossModel.GetComponent<BossHydra>() : null;
            if (hydra != null) SpawnHydraVolley(hydra);
            else SpawnAttackPattern(chosenPattern);
        }

        // Each stage has its own warning and recovery. Later stages never appear early.
        // The interval includes Dragon's lingering fire, so changing shape cannot trap a player.
        float TechniqueInterval => Mathf.Max(.8f, telegraphSeconds) + .45f + passives.ScorchSeconds + .85f;
        void TimeTechnique(AttackState attack, int beat, int variant)
        {
            attack.WarningDelay = beat * TechniqueInterval;
            attack.TelegraphTotal = Mathf.Max(.8f, telegraphSeconds);
            attack.AnimationVariant = variant;
            attack.View.gameObject.SetActive(beat == 0);
        }
        void TechniqueCircle(string title, float x, float z, float radius, int beat, int variant)
        {
            float span = Mathf.Min(arenaWidthMeters, arenaDepthMeters);
            float r = Mathf.Min(radius * span, span * .28f);
            AddCircle(title, Mathf.Clamp(x * arenaWidthMeters, r, arenaWidthMeters - r),
                Mathf.Clamp(z * arenaDepthMeters, r, arenaDepthMeters - r), r,
                Mathf.Max(.8f, telegraphSeconds) + beat * TechniqueInterval);
            TimeTechnique(attacks[attacks.Count - 1], beat, variant);
        }
        void TechniqueLine(string title, float x, int beat, int variant)
        {
            AddLine(title, x * arenaWidthMeters, Mathf.Min(1.45f, arenaWidthMeters * .16f),
                Mathf.Max(.8f, telegraphSeconds) + beat * TechniqueInterval);
            TimeTechnique(attacks[attacks.Count - 1], beat, variant);
        }
        void TechniqueZone(string title, int side, int beat, int variant)
        {
            AddZone(title, side, Mathf.Max(.8f, telegraphSeconds) + beat * TechniqueInterval);
            TimeTechnique(attacks[attacks.Count - 1], beat, variant);
        }
        void TechniqueWave(string title, bool horizontal, int direction)
        {
            float width = Mathf.Min(1.1f, arenaDepthMeters * .2f);
            var size = horizontal ? new Vector2(FloorRect.width, width * FloorRect.height / Mathf.Max(.01f,arenaDepthMeters)) : new Vector2(width * PixelsPerMeter(), FloorRect.height);
            var view = CreateRect("Attack == " + title, Vector2.zero, size, new Color(.15f,.7f,1f,.5f));
            float warning = Mathf.Max(.8f, telegraphSeconds);
            var attack = new AttackState(Shape.Wave, view, warning, 3.2f)
            { WaveHorizontal = horizontal, WaveDir = direction, Width = width, MoveSeconds = 3.2f, TelegraphTotal = warning, AnimationVariant = 2 };
            float span = horizontal ? arenaWidthMeters : arenaDepthMeters;
            float gapWidth = Mathf.Min(span * .45f, Mathf.Max(1.4f, span * .2f));
            float center = span * (Random.value < .5f ? .32f : .68f);
            attack.WaveGaps.Add(new Vector2(center - gapWidth * .5f, center + gapWidth * .5f));
            attack.ViewImage.enabled = false;
            CreateWaveSection(attack, 0, center - gapWidth * .5f, false);
            CreateWaveSection(attack, center - gapWidth * .5f, center + gapWidth * .5f, true);
            CreateWaveSection(attack, center + gapWidth * .5f, span, false);
            PlaceHazardBelowPlayers(view); UpdateWaveView(attack); attacks.Add(attack);
        }

        bool SpawnBossTechnique(int move)
        {
            var kind = BossPassiveState.Identify(bossModel != null ? bossModel.name : "");
            if(move>=3 && move<9)return SpawnExtendedBossTechnique(kind,move-3);
            int shots = BossPhase() >= 3 ? 4 : 3;
            int side = Random.value < .5f ? -1 : 1;
            switch (kind)
            {
                case BossPassiveState.Kind.Demon:
                    if(!passives.DemonAdult)
                    {
                        if(move==0){TechniqueLine("RED BOY / SPEAR THRUST",.35f,0,3);TechniqueLine("RED BOY / RETURN THRUST",.65f,1,3);}
                        else if(move==1){TechniqueZone("RED BOY / SPEAR SWEEP",side,0,1);TechniqueZone("RED BOY / RETURN SWEEP",-side,1,1);}
                        else TechniqueCircle("RED BOY / AERIAL SPEAR",.5f,.5f,.25f,0,2);
                    }
                    else
                    {
                        if(move==0){TechniqueLine("YAKSHA / LEFT BLADE",.25f,0,1);TechniqueLine("YAKSHA / RIGHT BLADE",.75f,1,1);TechniqueCircle("YAKSHA / CRUSH",.5f,.5f,.25f,2,2);}
                        else if(move==1){TechniqueZone("YAKSHA / CRESCENT",side,0,1);TechniqueZone("YAKSHA / RETURN CRESCENT",-side,1,1);}
                        else {TechniqueLine("YAKSHA / BLADE WAVE",.5f,0,3);TechniqueCircle("YAKSHA / DESCENT",.5f,.5f,.25f,1,2);}
                    }
                    break;
                case BossPassiveState.Kind.Fish:
                    if (move == 0) TechniqueWave("TIDAL SURGE",true,side);
                    else if (move == 1) { TechniqueZone("TAIL SWEEP LEFT",-1,0,1); TechniqueZone("TAIL SWEEP RIGHT",1,1,1); }
                    else TechniqueLine("WATER JET",.5f,0,3);
                    break;
                case BossPassiveState.Kind.Dragon:
                    if(move==0) for(int i=0;i<shots;i++) TechniqueLine("FLAME STRAFE",side<0?.2f+i*.2f:.8f-i*.2f,i,1);
                    else if(move==1) for(int i=0;i<shots;i++) TechniqueCircle("EMBER TRAIL",.2f+i*.2f,.25f+(i%2)*.5f,.18f,i,3);
                    else { TechniqueCircle("WING CRASH",.5f,.35f,.26f,0,2); TechniqueZone("FIRE BREATH",side,1,3); }
                    break;
                case BossPassiveState.Kind.Jellyfish:
                    if(move==0) for(int i=0;i<shots;i++) TechniqueCircle("LIGHTNING HOP",i%2==0?.25f:.75f,.25f+i*.16f,.19f,i,3);
                    else if(move==1) { TechniqueLine("TENTACLE LASH",.25f,0,1); TechniqueLine("TENTACLE LASH",.75f,1,1); TechniqueLine("TENTACLE LASH",.5f,2,1); }
                    else { TechniqueCircle("PULSE CORE",.5f,.5f,.27f,0,2); TechniqueCircle("PULSE ECHO",.25f,.5f,.2f,1,2); TechniqueCircle("PULSE ECHO",.75f,.5f,.2f,2,2); }
                    break;
                case BossPassiveState.Kind.Golem:
                    if(move==0) { TechniqueCircle("LEFT HAMMER",.28f,.5f,.22f,0,1); TechniqueCircle("RIGHT HAMMER",.72f,.5f,.22f,1,1); TechniqueCircle("DOUBLE HAMMER",.5f,.5f,.28f,2,2); }
                    else if(move==1) for(int i=0;i<shots;i++) TechniqueLine("CRYSTAL FAULT",.2f+i*.2f,i,3);
                    else { TechniqueZone("SEISMIC STOMP",side,0,2); TechniqueLine("CRYSTAL SPIKE",side<0?.75f:.25f,1,3); }
                    break;
                case BossPassiveState.Kind.Wraith:
                    if(move==0) { TechniqueCircle("PHANTOM MARK",.25f,.25f,.2f,0,3); TechniqueCircle("PHANTOM MARK",.75f,.75f,.2f,1,3); TechniqueCircle("PHANTOM MARK",.5f,.5f,.23f,2,3); }
                    else if(move==1) { TechniqueLine("RIFT SCISSORS",.2f,0,1); TechniqueLine("RIFT SCISSORS",.8f,1,1); TechniqueLine("RIFT SCISSORS",.5f,2,1); }
                    else { TechniqueZone("SHADOW SWAP",side,0,1); TechniqueZone("SHADOW SWAP",-side,1,1); }
                    break;
                case BossPassiveState.Kind.Sentinel:
                    if(move==0) for(int i=0;i<shots;i++) TechniqueLine("RAIL SWEEP",side<0?.2f+i*.2f:.8f-i*.2f,i,1);
                    else if(move==1) for(int i=0;i<shots;i++) TechniqueCircle("MORTAR GRID",.25f+(i%2)*.5f,.25f+(i/2)*.5f,.18f,i,3);
                    else { TechniqueLine("PINPOINT LASER",.5f,0,3); TechniqueZone("SUPPRESSION",side,1,2); }
                    break;
                case BossPassiveState.Kind.Serpent:
                    if(move==0) TechniqueWave("CROSSCURRENT",false,side);
                    else if(move==1) for(int i=0;i<shots;i++) TechniqueCircle("SERPENT COIL",.25f+(i%2)*.5f,.2f+i*.2f,.19f,i,1);
                    else { TechniqueZone("TAIL SWEEP",side,0,1); TechniqueLine("FANG STRIKE",side<0?.75f:.25f,1,3); }
                    break;
                case BossPassiveState.Kind.Hydra:
                    var hydra = bossModel.GetComponent<BossHydra>(); if(hydra==null) return false;
                    int shot=0;
                    for(int head=0;head<BossHydra.MaximumHeads;head++)
                    {
                        if(!hydra.IsHeadReady(head)) continue;
                        if(move==0) TechniqueLine("VENOM FAN / " +(head+1),.15f+head/6f*.7f,shot,3);
                        else TechniqueCircle(move==1?"VENOM ZIGZAG":"FANG BARRAGE",move==1?(shot%2==0?.25f:.75f):.2f+(shot%4)*.2f,
                            move==1?.25f+(shot%3)*.25f:.5f,.18f,shot,move==1?1:2);
                        var breath=attacks[attacks.Count-1]; breath.HydraOwner=hydra; breath.HydraHeadIndex=head; shot++;
                    }
                    return shot>0;
                default: return false;
            }
            return true;
        }

        void StartAttackStageMotion(AttackState attack)
        {
            if(attack.MotionStarted) return;
            attack.MotionStarted=true;
            attack.AnimationVariant=AttackMotionFor(attack);
            if(!attack.IsBossUltimate && attack.AnimationVariant==3 && bossModel!=null && bossModel.GetComponent<BossPlesiothAnimation>()!=null)
            {
                attack.ActiveTimer=attack.TelegraphTimer+BossPlesiothAnimation.WaterJetSeconds;
                attack.Lingering=true;
            }
            if(attack.HydraOwner!=null) { attack.HydraOwner.SetHeadAttackStyle(attack.HydraHeadIndex,attack.AnimationVariant); attack.HydraOwner.BeginHeadCharge(attack.HydraHeadIndex,Mathf.Max(.01f,attack.TelegraphTimer)); }
            SkinOnAttackStart(attack);
            var king=bossModel!=null?bossModel.GetComponent<BossAbyssalKing>():null;
            if(king!=null){ if(!king.IsCasting) king.BeginCast(attack.AnimationVariant,Mathf.Max(.05f,attack.TelegraphTimer)); return; }
            var motion=bossModel!=null?bossModel.GetComponent<BossModelAnimation>():null;
            if(motion==null) return;
            motion.SetAttackVariant(attack.AnimationVariant);
            float firstRelease=attack.TelegraphTimer; foreach(var pending in attacks) if(!pending.MotionStarted && pending.WarningDelay<=0) firstRelease=Mathf.Min(firstRelease,pending.TelegraphTimer); int id=motion.BeginCast(Mathf.Max(.01f,firstRelease));
            foreach(var sibling in attacks)
            {
                if(sibling.MotionStarted && sibling!=attack || sibling.WarningDelay>0) continue;
                sibling.MotionStarted=true; sibling.AnimationCastId=id;
            }
        }
    }
}
