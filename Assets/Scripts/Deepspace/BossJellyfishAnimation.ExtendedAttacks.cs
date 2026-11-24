using UnityEngine;
namespace Deepspace
{
    public sealed partial class BossJellyfishAnimation
    {
        public void SampleExtendedTentacles(int move,float wind,float hit,float follow,float time)
        {
            var up=bodyUp.normalized;var right=Vector3.ProjectOnPlane(Vector3.right,up).normalized;
            if(right.sqrMagnitude<.01f)right=Vector3.ProjectOnPlane(Vector3.forward,up).normalized;
            float twist=move==0?-40*wind+70*hit:move==3?-25*wind+40*hit:move==5?20*wind-35*hit:0;
            float tilt=move==2?-25*wind+35*hit:move==4?30*wind-45*hit:move==1?-18*wind+22*hit:0;
            float rise=move==2?.1f*wind-.07f*hit:move==5?-.045f*wind+.06f*hit:.025f*wind-.015f*hit;
            var rotation=Quaternion.AngleAxis(twist*motionAmount,up)*Quaternion.AngleAxis(tilt*motionAmount,right);
            foreach(var root in visualRoots)
            {
                root.localPosition=bodyCenter+rotation*(root.localPosition-bodyCenter)+up*(rise*bodyHeight*motionAmount);
                root.localRotation=rotation*root.localRotation;
            }
            for(int i=0;i<tentacles.Length;i++)
            {
                float side=i%2==0?1:-1,angle=0,swirl=0;
                switch(move){
                    case 0:angle=-25*wind+45*hit+follow*Mathf.Sin(i*1.2f)*18;swirl=-45*wind+75*hit;break;
                    case 1:angle=side*(-35*wind+65*hit);swirl=side*(30*wind-35*hit);break;
                    case 2:angle=55*wind-35*hit;swirl=follow*20;break;
                    case 3:angle=-48*wind+80*hit;swirl=Mathf.Sin(i*.8f)*follow*35;break;
                    case 4:angle=i%3==0?30*wind-55*hit:-15*wind+35*hit;swirl=side*15*hit;break;
                    default:angle=65*wind-45*hit;swirl=side*(25*wind+25*hit);break;
                }
                if(tentacles[i].parent==transform)tentacles[i].localRotation=rotation*tentacles[i].localRotation;
                tentacles[i].localRotation*=Quaternion.AngleAxis(angle*motionAmount,tangent[i])*Quaternion.AngleAxis(swirl*motionAmount,tentacleDown[i]);
            }
            // Deform the bell as well as moving pivots, retaining the existing soft surface continuity.
            SampleSoftBody(time,wind,hit);
        }
    }
}
