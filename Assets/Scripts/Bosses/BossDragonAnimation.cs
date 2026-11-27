using UnityEngine;
namespace BossSlayer
{
    // Long-necked, grounded dragon motion authored from the supplied Fatalis fight reference.
    public sealed partial class BossDragonAnimation : BossModelAnimation
    {
        // Lower neck, upper neck, middle tail, tail tip, rear L/R, front L/R.
        public Transform[] secondaryParts = new Transform[8];
        static readonly Quaternion ToDragonMesh = Quaternion.Euler(90,0,0);
        static readonly Quaternion FromDragonMesh = Quaternion.Euler(-90,0,0);
        protected override Transform[] GetPoseTransforms()
        {
            var list = new System.Collections.Generic.List<Transform>(base.GetPoseTransforms());
            if(secondaryParts!=null)foreach(var part in secondaryParts)
                if(part!=null&&!list.Contains(part))list.Add(part);
            return list.ToArray();
        }
        public override void ResetMotion()
        {
            SetPhaseRoar(-1);
            ResetNeckStretch();
            ReleaseDeathMeshes();
            base.ResetMotion();
            ReleasePhaseFire();
            ReleaseWingMeshes();
            ReleaseChestHeat();
            if(secondaryParts!=null)foreach(var part in secondaryParts)if(part!=null)part.localRotation=Quaternion.identity;
        }
        public override void SamplePose(float time,float charge,float release,float stun)
        {
            if(phaseRoarProgress>=0 && stun<=0){charge=Mathf.Clamp01(phaseRoarProgress/.55f);release=0;}
            base.SamplePose(time,charge,release,stun);
            SampleWingFlex(time,charge,release,stun);
            SampleChestHeat(time);
            SamplePhaseFire(time);
            if(secondaryParts==null||secondaryParts.Length!=8)return;
            float c=Mathf.SmoothStep(0,1,Mathf.Clamp01(charge/.72f));
            float r=(1-release)*1.05f;
            float strike=release>0?Mathf.SmoothStep(0,1,r/.10f)*(1-Mathf.SmoothStep(0,1,(r-.28f)/.77f)):0;
            if(release>0)c=1-Mathf.SmoothStep(0,1,r/.10f);
            float weight=motionAmount*(1-Mathf.Clamp01(stun));
            float speed=Mathf.Abs(Locomotion), gait=Mathf.Sin(time*(2.1f+speed));
            float turn=AttackVariant==1?(-24*c+32*strike):Mathf.Sin(time*.65f)*3;
            float lift=AttackVariant==4?(-18*c-8*strike):AttackVariant==3?(8*c-14*strike):6*c-8*strike;
            Set(0,new Vector3(lift,turn*.45f,Mathf.Sin(time*.8f)*2),weight);
            Set(1,new Vector3(lift*.7f,turn*.55f,Mathf.Sin(time*.8f-.45f)*2),weight);
            Set(2,new Vector3(Mathf.Sin(time*1.1f)*2,Mathf.Sin(time*1.15f-.7f)*9-turn*.3f,0),weight);
            Set(3,new Vector3(Mathf.Sin(time*1.1f-.6f)*3,Mathf.Sin(time*1.15f-1.5f)*13-turn*.2f,0),weight);
            for(int i=4;i<8;i++)
            {
                float side=i%2==0?1:-1, front=i>=6?-1:1;
                Set(i,new Vector3(gait*side*front*speed*10+strike*4,c*side*3,-side*c*3),weight);
            }
            SampleSuspendedFlight(time,charge,release,stun);
            SampleBreathNeck(time,charge,release,stun);
            if(phaseRoarProgress>=0 && stun<=0 && parts[4]!=null)
                parts[4].localRotation=ToDragonMesh*Quaternion.Euler(-42*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.4f,phaseRoarProgress)),0,0)*FromDragonMesh;
            if(stun>0)
            {
                Set(0,new Vector3(24,Mathf.Sin(time*2)*4,0),motionAmount*stun);
                Set(1,new Vector3(18,Mathf.Sin(time*2-.4f)*6,0),motionAmount*stun);
            }
        }
        void Set(int index,Vector3 angles,float weight)
        {
            if(secondaryParts[index]!=null)secondaryParts[index].localRotation=ToDragonMesh*Quaternion.Euler(angles*weight)*FromDragonMesh;
        }
    }
}
