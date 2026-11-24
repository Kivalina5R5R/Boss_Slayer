using UnityEngine;

namespace Deepspace
{
    public sealed partial class BossSlayerGameManager
    {
        void SpawnDragonMeteorShower(string title)
        {
            const int count = 12;
            const float interval = .6f;
            float radius = Mathf.Min(.9f, Mathf.Min(arenaWidthMeters, arenaDepthMeters) * .115f);
            Vector2 previous = new Vector2(-100,-100);
            for (int i=0;i<count;i++)
            {
                Vector2 point=Vector2.zero;
                for(int attempt=0;attempt<24;attempt++)
                {
                    point=new Vector2(Random.Range(radius,arenaWidthMeters-radius),Random.Range(radius,arenaDepthMeters-radius));
                    if(Vector2.Distance(point,previous)>radius*2.2f)break;
                }
                previous=point;
                AddCircle("ULTIMATE "+title+" / METEOR "+(i+1),point.x,point.y,radius,SignatureWarning+i*interval);
                var attack=attacks[attacks.Count-1];
                SignatureTiming(attack,i*interval);
                attack.IsMeteor=true;
                attack.AnimationVariant=4;
                attack.FloorForm=BossAttackVfx.AttackForm.Barrage;
                // The rock hits the full marked circle, then leaves its existing short burning patch.
                attack.FireSpreadSeconds=0;
            }
        }

        int AttackMotionFor(AttackState attack)
        {
            string title=attack.View!=null?attack.View.name.ToUpperInvariant():"";
            if(attack.AnimationVariant>=DemonJudgementMotion)return attack.AnimationVariant; // Demon ultimate's own motions
            if(title.Contains("ULTIMATE"))return 4;
            if(attack.AnimationVariant>=6)return attack.AnimationVariant;
            if(title.Contains("BITE")||title.Contains("FANG")||title.Contains("WING CRASH")||title.Contains("HELL CLAW / CRUSH")||
                title.Contains("PULSE ECHO")||title.Contains("CRYSTAL SPIKE")||title.Contains("SHADOW SWAP")||title.Contains("PINPOINT"))return 5;
            if(attack.AnimationVariant>0)return attack.AnimationVariant;
            if(title.Contains("BREATH"))return 3;
            if(attack.Shape==Shape.Zone)return 1;
            if(attack.Shape==Shape.Line||attack.FloorForm==BossAttackVfx.AttackForm.Barrage||attack.FloorForm==BossAttackVfx.AttackForm.Pool)return 3;
            return 2;
        }
    }
}
