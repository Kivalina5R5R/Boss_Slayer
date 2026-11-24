using UnityEngine;
namespace Deepspace
{
    public partial class BossSlayerGameManager
    {
        float demonTransformationTime=-1;
        int demonTransformationCast=-1;
        const float DemonTransformationSeconds=3f;
        void ResetDemonPhase()
        {
            demonTransformationTime=-1;
            if(bossModel!=null)bossModel.GetComponent<BossDemonAnimation>()?.SetAdult(false);
        }
        bool AdvanceDemonTransformation(float dt)
        {
            if(passives.Active!=BossPassiveState.Kind.Demon || !passives.DemonPhasePending || bossHp<=0)return false;
            if(dt<=0 || Time.timeScale<=0)return demonTransformationTime>=0;
            if(stunTimer>0 || ultimateActive)return false;
            if(attacks.Count>0)return false;
            var demon=bossModel!=null?bossModel.GetComponent<BossDemonAnimation>():null;
            if(demonTransformationTime<0)
            {
                demonTransformationTime=0;
                if(demon!=null){demon.SetLocomotion(0);demon.SetAttackVariant(4);demonTransformationCast=demon.BeginCast(DemonTransformationSeconds);}
                SkinTransform(DemonTransformationSeconds,.9f,"ember");sfx?.Score?.Roar();passiveFeedback?.Notify("RED BOY AWAKENS");ReplayBossTransform();
            }
            demonTransformationTime=Mathf.Min(DemonTransformationSeconds,demonTransformationTime+dt);
            demon?.SetTransformation(demonTransformationTime/DemonTransformationSeconds);
            if(demonTransformationTime>=DemonTransformationSeconds)
            {
                SkinDemonAdult();passives.EnterDemonAdult();demon?.SetAdult(true);demon?.ReleaseAttack(demonTransformationCast);
                demonTransformationTime=-1;attackTimer=Mathf.Max(attackTimer,1.2f);
                passiveFeedback?.Notify("PHASE 2: YAKSHA KING");
            }
            return true;
        }
    }
}

