using UnityEngine;
namespace Deepspace
{
    public partial class BossModelAnimation
    {
        protected float stunDuration, stunElapsed;
        readonly Vector3[] stunPose = new Vector3[6];
        public static float StunWeight(float remaining,float duration)
        {
            if(remaining<=0||duration<=0)return 0;
            float age=duration-remaining;
            return Mathf.SmoothStep(0,1,age/Mathf.Min(.12f,duration*.2f))
                *Mathf.SmoothStep(0,1,remaining/Mathf.Min(.4f,duration*.3f));
        }
        static void StunJoint(Transform[] joints,int index,Vector3 angles,float weight)
        {
            if(joints==null||index>=joints.Length||joints[index]==null)return;
            joints[index].localRotation=Quaternion.Slerp(joints[index].localRotation,ToMesh*Quaternion.Euler(angles)*ToSculpt,weight);
        }
        void PoseStunned(float time,float weight)
        {
            if(weight<=0 || character==Kind.Jellyfish || rigRoot==null)return;
            float sway=Mathf.Sin(stunElapsed*2.1f),shake=Mathf.Sin(stunElapsed*27)*Mathf.Exp(-stunElapsed*2.5f)*5;
            float breath=Mathf.Sin(stunElapsed*3.4f), wobble=Mathf.Sin(stunElapsed*5.2f)*Mathf.Exp(-stunElapsed*.7f);
            float impact=Mathf.Sin(Mathf.Clamp01(stunElapsed/.22f)*Mathf.PI)*12;
            Vector3 body=Vector3.zero,head=Vector3.zero,left=Vector3.zero,right=Vector3.zero,tail=Vector3.zero;
            float drop=.025f;
            switch(character)
            {
                case Kind.Dragon:
                    body=new Vector3(18+impact*.4f,0,6*sway);head=new Vector3(24+breath*5,7*sway,shake*.5f);
                    left=new Vector3(-22,0,38+shake);right=new Vector3(-22,0,-38-shake);tail=new Vector3(8,12*sway,0);drop=.075f;break;
                case Kind.Demon:
                    body=new Vector3(-32-impact,8*sway,-9+3*breath);head=new Vector3(-48,-12*sway,shake);
                    left=new Vector3(-52,18,-38);right=new Vector3(38,-10,20+shake);drop=.10f;break;
                case Kind.Fish:
                    body=new Vector3(-8,9*sway,62+8*wobble);head=new Vector3(-28,shake*2,0);
                    left=new Vector3(0,0,-25+shake*3);right=new Vector3(0,0,12-shake*3);tail=new Vector3(0,shake*4,0);break;
                case Kind.Golem:
                    body=new Vector3(-31-impact*.6f,0,2*breath);head=new Vector3(-46+breath*2,0,shake*.4f);
                    left=new Vector3(48+breath*2,0,-14);right=new Vector3(39-breath*2,0,17);drop=.12f;break;
                case Kind.Wraith:
                    body=new Vector3(-30,14*sway,18*wobble);head=new Vector3(-65,14*sway,0);
                    left=new Vector3(35,0,-28);right=new Vector3(35,0,28);tail=new Vector3(12,12*sway,0);drop=.085f;break;
                case Kind.Sentinel:
                    body=new Vector3(-18-impact*.4f,0,-13+shake);head=new Vector3(-54,shake*4,0);
                    left=new Vector3(52+shake*2,0,12);right=new Vector3(28-shake*2,0,-22);drop=.14f;break;
                case Kind.Serpent:
                    body=new Vector3(-30,8*sway,12*wobble);head=new Vector3(-72+breath*4,-12*sway,0);
                    left=new Vector3(0,0,-20);right=new Vector3(0,0,20);tail=new Vector3(12,30*sway,10);drop=.075f;break;
            }
            float w=Mathf.Clamp01(weight)*motionAmount;
            var pose=stunPose;pose[0]=Vector3.zero;pose[1]=left;pose[2]=right;pose[3]=head;pose[4]=new Vector3(-12,0,0);pose[5]=tail;
            for(int i=0;i<parts.Length;i++)if(parts[i]!=null && (character!=Kind.Serpent || i==4))
                parts[i].localRotation=Quaternion.Slerp(parts[i].localRotation,ToMesh*Quaternion.Euler(pose[i])*ToSculpt,w);
            if (this is BossDragonAnimation dragon)
            {
                StunJoint(dragon.secondaryParts,0,new Vector3(32+breath*3,5*sway,0),w);
                StunJoint(dragon.secondaryParts,1,new Vector3(26+Mathf.Sin(stunElapsed*3.4f-.6f)*4,7*sway,0),w);
                for(int i=4;i<8;i++) StunJoint(dragon.secondaryParts,i,new Vector3(i<6?20:38,0,(i%2==0?-1:1)*8),w);
            }
            if (this is BossDemonAnimation demon)
            {
                StunJoint(demon.joints,6,new Vector3(-105,0,-18),w);
                StunJoint(demon.joints,7,new Vector3(-22,0,16),w);
                StunJoint(demon.joints,8,new Vector3(42,0,-10),w); StunJoint(demon.joints,9,new Vector3(18,0,12),w);
                StunJoint(demon.joints,10,new Vector3(62,0,0),w); StunJoint(demon.joints,11,new Vector3(28,0,0),w);
            }
            if (this is BossSentinelAnimation sentinel)
            {
                StunJoint(parts,4,new Vector3(58,0,-7),w);StunJoint(parts,5,new Vector3(22,0,10),w);
                StunJoint(sentinel.mechanisms,2,new Vector3(-95,0,0),w);StunJoint(sentinel.mechanisms,3,new Vector3(-35,0,0),w);
                StunJoint(sentinel.mechanisms,0,new Vector3(12+shake*4,0,0),w);StunJoint(sentinel.mechanisms,1,new Vector3(35-shake*3,0,0),w);
            }
            if (this is BossPlesiothAnimation fish)
                for(int i=0;i<fish.secondaryParts.Length;i++) StunJoint(fish.secondaryParts,i,new Vector3(i==0?-28:0,Mathf.Sin(stunElapsed*11-i*.8f)*8/(1+stunElapsed),0),w);
            rigRoot.localRotation=Quaternion.Slerp(rigRoot.localRotation,ToMesh*Quaternion.Euler(body)*ToSculpt,w);
            rigRoot.localPosition=Vector3.Lerp(rigRoot.localPosition,restPosition+ToMesh*new Vector3(sway*.006f,-drop,0)*height,w);
        }
    }
}
