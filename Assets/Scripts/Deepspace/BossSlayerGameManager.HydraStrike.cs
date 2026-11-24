using UnityEngine;
namespace Deepspace
{
    public sealed partial class BossSlayerGameManager
    {
        BossHydraFloorStrike hydraFloorStrike;
        BossHydra hydraComboOwner;
        int hydraRoundsRemaining;
        float hydraRoundTimer;
        string hydraComboTitle;
        const float HydraRoundGap = .25f;
        bool HydraComboPending => hydraComboOwner != null && hydraRoundsRemaining > 0;

        void SpawnHydraSevenHeadStrike(string title,BossHydra hydra)
        {
            ClearHydraSevenHeadStrike();
            hydraComboOwner=hydra;hydraComboTitle=title;hydraRoundsRemaining=2;
            hydraRoundTimer=BossHydra.SevenHeadDuration+HydraRoundGap;
            SpawnHydraStrikeRound(title,hydra);
        }
        void AdvanceHydraStrikeRounds(float dt)
        {
            if(!HydraComboPending || dt<=0)return;
            if(bossHp<=0 || stunTimer>0 || bossModel!=hydraComboOwner.transform){ClearHydraSevenHeadStrike();return;}
            hydraRoundTimer-=dt;
            if(hydraRoundTimer>0)return;
            hydraRoundsRemaining--;
            SpawnHydraStrikeRound(hydraComboTitle,hydraComboOwner);
            hydraRoundTimer=BossHydra.SevenHeadDuration+HydraRoundGap;
            sfx?.StartCharge(SoundTheme,BossHydra.SevenHeadImpactTime);
            if(bossUltimateAura!=null)bossUltimateAura.Play(bossModel,bossDisplayCamera!=null?bossDisplayCamera:wallDisplayCamera,BossWorldBounds(),BossThemePalette.Accent(bossModel.name),BossHydra.SevenHeadImpactTime);
        }
        void SpawnHydraStrikeRound(string title,BossHydra hydra)
        {
            hydra.BeginSevenHeadStrike();var points=new Vector2[7];
            var targets=RandomHydraStrikeTargets();
            for(int i=0;i<7;i++)
            {
                float x=targets[i].x,z=targets[i].y;
                points[i]=WorldToLocal(new Vector2(x,z));
                AddCircle("ULTIMATE "+title,x,z,.65f,BossHydra.SevenHeadImpactTime);
                var attack=attacks[attacks.Count-1];attack.TelegraphTotal=BossHydra.SevenHeadImpactTime;
                attack.ActiveTimer=BossHydra.SevenHeadDuration-.45f;attack.Lingering=false;
                attack.HydraOwner=hydra;attack.HydraHeadIndex=i;attack.IsHydraSevenHeadStrike=true;attack.MotionStarted=true;attack.IsBossUltimate=true;
            }
            // บาส 2026-11-12: MotionStarted=true above skips StartAttackStageMotion, so the voxel skin never got its "ultimate"
            // clip and stood idle through the whole strike. Start it here, once per round, like every other boss's ultimate.
            SkinOnAttackStart(attacks[attacks.Count-1]);
            if(hydraFloorStrike!=null)Destroy(hydraFloorStrike.gameObject);
            var go=new GameObject("Hydra seven-head floor strike",typeof(RectTransform),typeof(CanvasRenderer),typeof(BossHydraFloorStrike));
            go.layer=InputArea.gameObject.layer;var rect=(RectTransform)go.transform;rect.SetParent(InputArea,false);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            hydraFloorStrike=go.GetComponent<BossHydraFloorStrike>();hydraFloorStrike.Initialize(hydra,points);
        }
        Vector2[] RandomHydraStrikeTargets()
        {
            var targets=new Vector2[7];
            float marginX=Mathf.Min(.8f,arenaWidthMeters*.2f),marginZ=Mathf.Min(.8f,arenaDepthMeters*.2f);
            for(int i=0;i<targets.Length;i++)
            {
                Vector2 best=Vector2.zero;float bestDistance=-1;
                // Keep warning circles apart where space permits; bounded work on smaller arenas.
                for(int attempt=0;attempt<64;attempt++)
                {
                    var candidate=new Vector2(Random.Range(marginX,arenaWidthMeters-marginX),Random.Range(marginZ,arenaDepthMeters-marginZ));
                    float distance=float.PositiveInfinity;
                    for(int j=0;j<i;j++)distance=Mathf.Min(distance,(candidate-targets[j]).sqrMagnitude);
                    if(distance>bestDistance){best=candidate;bestDistance=distance;}
                    if(distance>=1.4f*1.4f)break;
                }
                targets[i]=best;
            }
            // Assign heads left to right so their necks do not weave across the entire floor.
            System.Array.Sort(targets,(a,b)=>a.x.CompareTo(b.x));
            return targets;
        }
        void ClearHydraSevenHeadStrike()
        {
            if(hydraComboOwner!=null)hydraComboOwner.CancelSevenHeadStrike();
            hydraComboOwner=null;hydraRoundsRemaining=0;hydraRoundTimer=0;hydraComboTitle=null;
            if(bossModel!=null)bossModel.GetComponent<BossHydra>()?.CancelSevenHeadStrike();
            if(hydraFloorStrike!=null)Destroy(hydraFloorStrike.gameObject);hydraFloorStrike=null;
        }
    }
}
