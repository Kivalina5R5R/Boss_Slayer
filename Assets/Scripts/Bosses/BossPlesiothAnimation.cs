using UnityEngine;
namespace BossSlayer
{
    public sealed partial class BossPlesiothAnimation : BossModelAnimation
    {
        // Neck, middle tail, tail fin, two unused slots, dorsal sail.
        public const float WaterJetSeconds=3.2f;
        protected override float RecoverySeconds => AttackVariant==3 ? WaterJetSeconds : 1.05f;
        public Transform[] secondaryParts=new Transform[6];
        static readonly Quaternion ToMesh=Quaternion.Euler(90,0,0),FromMesh=Quaternion.Euler(-90,0,0);
        Transform cachedFishRig;
        Vector3 fishRest;
        protected override Transform[] GetPoseTransforms()
        {
            var list=new System.Collections.Generic.List<Transform>(base.GetPoseTransforms());
            if(secondaryParts!=null)foreach(var part in secondaryParts)if(part!=null&&!list.Contains(part))list.Add(part);
            return list.ToArray();
        }
        public override bool Initialize()
        {
            if(!base.Initialize())return false;
            if(cachedFishRig!=rigRoot){cachedFishRig=rigRoot;fishRest=rigRoot.localPosition;}
            return true;
        }
        public override void ResetMotion()
        {
            ResetSwim();
            ReleaseDeathMeshes();
            ReleaseFinMeshes();
            base.ResetMotion();
            if(secondaryParts!=null)foreach(var part in secondaryParts)if(part!=null)part.localRotation=Quaternion.identity;
        }
        static float EaseFish(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
        static void Pose(Transform part,Vector3 angles,float weight)
        { if(part!=null)part.localRotation=ToMesh*Quaternion.Euler(angles*weight)*FromMesh; }
        public override void SamplePose(float time,float charge,float release,float stun)
        {
            base.SamplePose(time,charge,release,stun);
            if(!Initialize() || secondaryParts==null || secondaryParts.Length!=6)return;
            SampleSwimClock(time);
            SampleFinFlex(time,charge,release,stun);
            if(stun>0)
            {
                Pose(secondaryParts[0],new Vector3(-20,Mathf.Sin(time*3)*8,0),stun);
                for(int i=1;i<6;i++)Pose(secondaryParts[i],new Vector3(0,Mathf.Sin(time*3-i)*4,0),stun);
                return;
            }
            float age=(1-release)*RecoverySeconds;
            float wind=release>0?1-EaseFish(0,.16f,age):EaseFish(.08f,.68f,charge);
            float strike=release>0?EaseFish(0,.16f,age)*(1-EaseFish(AttackVariant==3?2.75f:.48f,RecoverySeconds,age)):0;
            float speed=swimSpeed,idle=Mathf.Sin(time*1.15f);
            float sweep=AttackVariant==1?-48*wind+68*strike:0;
            float hip=AttackVariant==2?-20*wind+34*strike:0;
            float jet=AttackVariant==3?1:0,roar=AttackVariant==4?1:0;
            float weight=motionAmount;
            float swim=swimPhase, propulsion=Mathf.Lerp(.32f,1.1f,speed)*(1-strike*jet*.65f);
            float calm=1-Mathf.Max(wind,strike)*.8f;
            float heading=swimHeading*12*calm,bank=swimBank*9*calm;
            float finLeft=Mathf.Sin(swim+.35f),finRight=Mathf.Sin(swim+1.05f);
            float finStroke=Mathf.Lerp(11,5,speed);
            float bodyWave=Mathf.Sin(swim)*2.5f*propulsion;
            Pose(rigRoot,new Vector3(idle+jet*(9*wind-13*strike)-roar*(14*wind+7*strike),heading+bodyWave+sweep*.5f+hip,Mathf.Sin(swim-.5f)*1.8f*propulsion+bank+hip*.45f),weight);
            float height=AnimatedMesh!=null?AnimatedMesh.bounds.size.z:1;
            rigRoot.localPosition=fishRest+ToMesh*new Vector3(Mathf.Sin(time*.7f)*.006f*calm+hip*.0035f,(idle*.009f)-strike*.012f,jet*(wind*.05f-strike*.075f))*height*weight;
            Pose(parts[0],new Vector3(0,Mathf.Sin(swim-.4f)*2.5f*propulsion,0),weight);
            Pose(parts[1],new Vector3(finLeft*4,finLeft*7-wind*12,12+finLeft*finStroke+bank*.5f-wind*(roar>0?52:25)+strike*18),weight);
            Pose(parts[2],new Vector3(-finRight*4,-finRight*7+wind*12,-12-finRight*finStroke+bank*.5f+wind*(roar>0?52:25)-strike*18),weight);
            Pose(secondaryParts[0],new Vector3(idle*1.5f+jet*(25*wind-20*strike)+roar*(30*wind+12*strike),-bodyWave*.55f-sweep*.15f,0),weight);
            Pose(parts[3],new Vector3(-idle+jet*(18*wind-20*strike)+roar*(18*wind+6*strike),-bodyWave*.35f-sweep*.12f,0),weight);
            Pose(parts[4],new Vector3(-3-(.5f+.5f*Mathf.Sin(time*1.9f))*1.5f-wind*(jet>0?26:roar>0?38:12)-strike*(jet>0?42:roar>0?44:20),0,0),weight);
            Pose(parts[5],new Vector3(0,Mathf.Sin(swim-.85f)*12*propulsion-sweep,0),weight);
            Pose(secondaryParts[1],new Vector3(Mathf.Sin(time*1.2f)*2,Mathf.Sin(swim-1.6f)*19*propulsion-sweep*.55f,0),weight);
            Pose(secondaryParts[2],new Vector3(0,Mathf.Sin(swim-2.4f)*25*propulsion-sweep*.3f,0),weight);
            Pose(secondaryParts[5],new Vector3(0,0,Mathf.Sin(swim-1.1f)*3+bank*.2f+hip*.2f),weight);
        }
    }
}
