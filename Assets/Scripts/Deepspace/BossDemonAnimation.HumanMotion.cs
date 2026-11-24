using UnityEngine;
namespace Deepspace
{
    public sealed partial class BossDemonAnimation
    {
        float humanClock=-1, gaitPhase, humanSpeed, humanLean;
        void ResetHumanMotion(){humanClock=-1;gaitPhase=humanSpeed=humanLean=0;}
        void HumanOffset(int index,Vector3 degrees)
        {
            if(joints[index]!=null)joints[index].localRotation*=ToMesh*Quaternion.Euler(degrees*motionAmount)*FromMesh;
        }
        void SampleHumanMotion(float time,float charge,float release,float wind,float hit)
        {
            if(Transformation>=0)return;
            float dt=humanClock<0?0:Mathf.Clamp(time-humanClock,0,.1f);humanClock=time;
            // A continuous gait phase prevents a foot jump when speed or direction changes.
            float response=1-Mathf.Exp(-dt*7);
            humanSpeed=Mathf.Lerp(humanSpeed,Mathf.Abs(Locomotion),response);
            humanLean=Mathf.Lerp(humanLean,Locomotion,response*.65f);
            gaitPhase+=dt*Mathf.Lerp(Adult?3.3f:4.1f,Adult?5.3f:6.2f,humanSpeed);
            float quiet=1-Mathf.Clamp01(Mathf.Max(charge,release))* .9f;
            float walk=humanSpeed*quiet, sway=Mathf.Sin(gaitPhase), bounce=Mathf.Cos(gaitPhase*2);
            float breath=Mathf.Sin(time*1.65f), shift=Mathf.Sin(time*.72f);
            float height=AnimatedMesh!=null?AnimatedMesh.bounds.size.z:1;
            float idleWeight=(1-humanSpeed)*quiet;
            // Weight crosses the supporting leg; the chest counters the pelvis and the head stays steadier.
            rigRoot.localPosition+=ToMesh*new Vector3(sway*.014f*walk+shift*.006f*idleWeight,
                (-.012f+(1-bounce)*.004f)*walk+breath*.0025f*idleWeight,0)*height*motionAmount;
            rigRoot.localRotation*=ToMesh*Quaternion.Euler(new Vector3(3*walk,sway*5*walk, -sway*3*walk+shift*1.6f*idleWeight+humanLean*1.5f)*motionAmount)*FromMesh;
            HumanOffset(0,new Vector3(breath*1.4f,-sway*8*walk+shift*2*idleWeight,sway*2*walk));
            HumanOffset(3,new Vector3(-breath,-sway*2*walk-shift*2*idleWeight,-sway*2*walk));
            for(int side=0;side<2;side++)
            {
                float phase=gaitPhase+side*Mathf.PI;
                float step=Mathf.Sin(phase), swing=Mathf.Max(0,step), plant=Mathf.Max(0,-step);
                // Flex the recovering knee and roll the ankle while the opposite leg bears weight.
                Pose(8+side,step*27*walk+6*quiet+wind*12,0,(side==0?-1:1)*(4+shift*1.5f*idleWeight));
                float knee=(20+Mathf.Pow(swing,.75f)*36+plant*5)*walk+18*idleWeight+wind*16;
                // Sculpt faces -Z: thigh lifts forward, shin flexes backward behind the knee.
                Pose(10+side,-knee,0,0);
                Pose(12+side,-step*14*walk+knee*.42f-plant*9*walk,0,0);
                float armPhase=Mathf.Sin(phase-.3f);
                HumanOffset(1+side,new Vector3(-armPhase*(Adult?13:19)*walk+breath*(side==0?1.7f:2.3f)*quiet,0,shift*(side==0?1:-1)*2*idleWeight));
                HumanOffset(6+side,new Vector3(-Mathf.Max(0,-armPhase)*13*walk+Mathf.Sin(time*1.65f-.45f)*2*quiet,0,0));
            }
            // Torso leads the strike; elbows and the weapon lag, then settle instead of stopping together.
            float attackAge=(1-release)*1.05f;
            float follow=release>0?Mathf.Sin(Mathf.Clamp01((attackAge-.07f)/.65f)*Mathf.PI)*Mathf.Exp(-attackAge*1.4f):0;
            HumanOffset(0,new Vector3(-wind*4+follow*6,-wind*7+follow*12,wind*2));
            HumanOffset(3,new Vector3(wind*3-follow*4,wind*4-follow*7,0));
            HumanOffset(6,new Vector3(-wind*8+follow*12,0,0));
            HumanOffset(7,new Vector3(-wind*14+follow*20,0,0));
            HumanOffset(14,new Vector3(Adult?0:follow*10,0,Adult?0:-follow*7));
            HumanOffset(15,new Vector3(Mathf.Sin(gaitPhase-.65f)*walk*5,Mathf.Sin(time*1.1f-.8f)*3*quiet,0));
        }
    }
}
