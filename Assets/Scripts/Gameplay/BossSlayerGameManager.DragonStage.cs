using UnityEngine;
namespace BossSlayer
{
    public partial class BossSlayerGameManager
    {
        const float DragonTransformationSeconds=2.8f;
        float dragonTransformationTime=-1;
        int dragonRoarCast=-1;
        bool dragonFirstUltimateCompleted;

        void UpdateDragonStage()
        {
            passives.ObserveHealth(bossHp/(float)Mathf.Max(1,bossMaxHp));
        }
        void ResetDragonPhase()
        {
            dragonTransformationTime=-1;dragonFirstUltimateCompleted=false;dragonRoarCast=-1;
            if(bossModel!=null)
            {
                var dragon=bossModel.GetComponent<BossDragonAnimation>();
                if(dragon!=null){dragon.SetPhaseRoar(-1);dragon.SetHeatedChest(false);}
            }
        }
        bool AdvanceDragonTransformation(float dt)
        {
            if(passives.Active!=BossPassiveState.Kind.Dragon || !passives.DragonPhasePending || bossHp<=0)return false;
            if(dt<=0 || Time.timeScale<=0)return dragonTransformationTime>=0;
            if(stunTimer>0 || ultimateActive)return false;
            if(attacks.Count>0)return false; // 2026-11-12: transforms first now; the ultimate waits for it (AwaitingPhaseTransform)
            var dragon=bossModel!=null?bossModel.GetComponent<BossDragonAnimation>():null;
            if(dragonTransformationTime<0)
            {
                dragonTransformationTime=0;
                if(dragon!=null)
                {
                    dragon.SetAttackVariant(4);dragon.SetLocomotion(0);
                    dragonRoarCast=dragon.BeginCast(DragonTransformationSeconds);
                }
                sfx?.Score?.Roar();
                SkinTransform(DragonTransformationSeconds,1.7f,"inferno");passiveFeedback?.Notify("INFERNO AWAKENING");
                ReplayBossTransform();
            }
            dragonTransformationTime=Mathf.Min(DragonTransformationSeconds,dragonTransformationTime+dt);
            float progress=dragonTransformationTime/DragonTransformationSeconds;
            if(dragon!=null)
            {
                dragon.SetPhaseRoar(progress);
                dragon.SetHeatedChest(progress>=.42f);
            }
            if(progress>=1)
            {
                passives.EnterDragonInferno();
                if(dragon!=null){dragon.SetPhaseRoar(-1);dragon.ReleaseAttack(dragonRoarCast);}
                dragonTransformationTime=-1;
                attackTimer=Mathf.Max(attackTimer,1.2f);
                passiveFeedback?.Notify("PHASE 2: INFERNO");
            }
            return true;
        }
    }
}
