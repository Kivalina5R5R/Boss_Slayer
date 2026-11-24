using System.Collections.Generic;
using UnityEngine;
namespace Deepspace
{
    public sealed partial class BossDemonAnimation : BossModelAnimation
    {
        public Transform[] joints=new Transform[16];
        public Mesh[] childMeshes=new Mesh[16],adultMeshes=new Mesh[16];
        public Vector3[] childPositions=new Vector3[16],adultPositions=new Vector3[16];
        public bool Adult {get;private set;}
        public float Transformation {get;private set;}=-1;
        static readonly Quaternion ToMesh=Quaternion.Euler(90,0,0),FromMesh=Quaternion.Euler(-90,0,0);
        Vector3 demonRest;Transform cachedDemon;
        public override bool Initialize()
        {
            if(!base.Initialize())return false;
            if(cachedDemon!=rigRoot){cachedDemon=rigRoot;demonRest=rigRoot.localPosition;}
            return true;
        }
        protected override Transform[] GetPoseTransforms()
        {
            var list=new List<Transform>(base.GetPoseTransforms());
            foreach(var j in joints)if(j!=null&&!list.Contains(j))list.Add(j);
            return list.ToArray();
        }
        public void SetTransformation(float progress)
        {
            Transformation=progress;
            ApplyForm(progress<0?(Adult?1:0):Mathf.SmoothStep(0,1,Mathf.InverseLerp(.23f,.8f,progress)));
        }
        public void SetAdult(bool adult) { Adult=adult;Transformation=-1;ApplyForm(adult?1:0); }
        void ApplyForm(float blend)
        {
            if(joints==null||joints.Length!=16)return;
            for(int i=0;i<16;i++)if(joints[i]!=null)
            {
                joints[i].localPosition=Vector3.Lerp(childPositions[i],adultPositions[i],blend);
                var filter=joints[i].GetComponent<MeshFilter>();
                var mesh=blend>=.5f?adultMeshes[i]:childMeshes[i];
                if(filter!=null && mesh!=null && filter.sharedMesh!=mesh)filter.sharedMesh=mesh;
            }
        }
        public override void ResetMotion()
        {
            ResetHumanMotion();ReleaseRageFire();ReleaseDeathMeshes();base.ResetMotion();Adult=false;Transformation=-1;ApplyForm(0);
            foreach(var j in joints)if(j!=null){j.localRotation=Quaternion.identity;j.localScale=Vector3.one;}
        }
        static float EaseD(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        void Pose(int index,float x,float y,float z)
        { if(joints[index]!=null)joints[index].localRotation=ToMesh*Quaternion.Euler(new Vector3(x,y,z)*motionAmount)*FromMesh; }
        // BOSS ULTIMATE "SEVERING JUDGEMENT" motions (variants 12 / 13, BossSlayerGameManager.DemonUltimate.cs).
        // 12, judgement cut: charge = low iaido crouch, hand on the hilt at the left hip; release = a lunge into a flurry of
        // whirling cuts, then the slow re-sheathe (the "click") before recovery.
        // 13, spear throw: charge = lean back with the spear cocked over the shoulder and the free arm aiming; release = the
        // whole body whips forward, the spear leaves the hand (hidden) and the arm follows through low.
        void SampleUltimateMotion(float time,float release,float age,float height)
        {
            bool cut=AttackVariant==12,spear=AttackVariant==13;
            if(joints[14]!=null&&!(spear&&release>0)&&joints[14].localScale!=Vector3.one)joints[14].localScale=Vector3.one;
            if(!cut&&!spear)return;
            float charge=Mathf.Clamp01(chargeForUltimate);
            float ready=release>0?0:EaseD(.05f,.8f,charge);
            if(cut)
            {
                float flurry=release>0?EaseD(0,.05f,age)*(1-EaseD(.55f,.8f,age)):0;
                float sheath=release>0?EaseD(.55f,.8f,age)*(1-EaseD(.92f,1.05f,age)):0;
                float crouch=Mathf.Max(ready,sheath),phase=age*26,sw=Mathf.Sin(phase),cw=Mathf.Cos(phase);
                float tremble=Mathf.Sin(time*40)*1.5f*ready;
                rigRoot.localRotation=ToMesh*Quaternion.Euler(10*crouch-6*flurry,-35*crouch+sw*40*flurry,tremble)*FromMesh;
                rigRoot.localPosition=demonRest+ToMesh*new Vector3(sw*.02f*flurry,(-.05f*crouch+.04f*flurry)*height,-.06f*flurry*height);
                Pose(0,14*crouch-8*flurry,-20*crouch+sw*25*flurry,0);
                Pose(3,10*crouch,sw*-12*flurry,0);
                Pose(1,-45*crouch-30*flurry,-25*crouch-sw*30*flurry,-35*crouch);
                Pose(2,-35*crouch-95*flurry+sw*50*flurry,55*crouch+sw*85*flurry,25*crouch-30*cw*flurry);
                Pose(6,-40*crouch,0,0);Pose(7,-60*crouch-20*flurry,0,0);
                Pose(8,-25*crouch-15*flurry,0,-16*crouch);Pose(9,25*crouch+10*flurry,0,16*crouch);
                Pose(10,-40*crouch-20*flurry,0,0);Pose(11,-30*crouch,0,0);
                return;
            }
            float cock=ready,throwing=release>0?EaseD(0,.1f,age)*(1-EaseD(.5f,1.05f,age)):0;
            rigRoot.localRotation=ToMesh*Quaternion.Euler(-14*cock+18*throwing,25*cock-30*throwing,0)*FromMesh;
            rigRoot.localPosition=demonRest+ToMesh*new Vector3(0,-.02f*throwing*height,(.04f*cock-.07f*throwing)*height);
            Pose(0,-12*cock+22*throwing,25*cock-30*throwing,0);
            Pose(3,-8*cock+6*throwing,-10*cock,0);
            Pose(2,-165*cock+40*throwing,-15*cock+20*throwing,-35*cock+10*throwing);
            Pose(7,-45*cock-5*throwing,0,0);
            Pose(1,-75*cock+10*throwing,-30*cock,25*cock-20*throwing);
            Pose(6,-10*cock,0,0);
            Pose(8,-30*cock+20*throwing,0,-10*cock);Pose(9,20*cock-15*throwing,0,12*cock);
            Pose(10,-20*cock-15*throwing,0,0);Pose(11,-40*cock,0,0);
            if(!Adult&&joints[14]!=null)
            {
                Pose(14,160*cock,0,0);
                // The spear has left the hand once the throw lands.
                joints[14].localScale=Vector3.one*(release>0?1-EaseD(.05f,.12f,age):1);
            }
        }
        float chargeForUltimate;

        public override void SamplePose(float time,float charge,float release,float stun)
        {
            base.SamplePose(time,charge,release,stun);
            chargeForUltimate=charge;
            if(!Initialize()||joints.Length!=16)return;
            SampleRageFire(time);
            if(stun>0){for(int i=6;i<16;i++)Pose(i,Mathf.Sin(time*4-i)*6*stun,0,0);return;}
            float age=(1-release)*1.05f;
            float wind=release>0?1-EaseD(0,.12f,age):EaseD(.05f,.76f,charge);
            float hit=release>0?EaseD(0,.12f,age)*(1-EaseD(.38f,1.05f,age)):0;
            float stride=Mathf.Sin(time*(Adult?3.8f:5.2f))*Mathf.Abs(Locomotion);
            float breath=Mathf.Sin(time*1.8f),yaw=0;
            float slash=AttackVariant==1?1:0,slam=AttackVariant==2?1:0,thrust=AttackVariant==3||AttackVariant==5?1:0,ult=AttackVariant==4?1:0;
            yaw=slash*(-42*wind+62*hit)+thrust*(-22*wind+14*hit);
            rigRoot.localRotation=ToMesh*Quaternion.Euler(-4*wind+8*hit,yaw,Mathf.Sin(time*2)*1.2f)*FromMesh;
            float height=AnimatedMesh!=null?AnimatedMesh.bounds.size.z:1;
            rigRoot.localPosition=demonRest+ToMesh*new Vector3(0,(Mathf.Abs(stride)*.009f-slam*wind*.022f+ult*wind*.055f),-thrust*hit*.08f)*height;
            Pose(0,breath*.7f+wind*5-hit*7,0,0);
            Pose(1,Adult?-12-105*wind+60*hit:-12-65*wind+30*hit,-yaw*.25f,Adult?12+ult*wind*55:8);
            Pose(2,-8-(slam+ult)*wind*125-slash*wind*115-thrust*wind*145+hit*(slam*45-thrust*65+slash*40),-slash*wind*50+slash*hit*65,-8-ult*wind*55-slash*wind*25-thrust*wind*16);
            Pose(3,-breath*.7f+wind*8,-yaw*.35f,0);
            Pose(4,0,0,0);Pose(5,breath*3-hit*12,Mathf.Sin(time*2-.7f)*3,0);
            Pose(6,-25-thrust*wind*35+thrust*hit*30,slash*hit*25,Adult?6:-18);
            Pose(7,-28-thrust*wind*55+thrust*hit*28-slash*wind*35+slash*hit*22,0,Adult?-6:8);
            Pose(8,stride*24+wind*12,0,-4);Pose(9,-stride*24+wind*12,0,4);
            Pose(10,Mathf.Max(0,-stride)*28+wind*16,0,0);Pose(11,Mathf.Max(0,stride)*28+wind*16,0,0);
            Pose(12,-Mathf.Max(0,-stride)*14,0,0);Pose(13,-Mathf.Max(0,stride)*14,0,0);
            Pose(14,Adult?0:thrust*(140*wind-12*hit),0,Adult?0:slash*(-20*wind+45*hit));
            Pose(15,Mathf.Sin(time*2.5f-.6f)*4+Mathf.Abs(stride)*5,0,Mathf.Sin(time*1.7f)*3);
            SampleHumanMotion(time,charge,release,wind,hit);
            SampleUltimateMotion(time,release,age,height);
            if(Transformation>=0)
            {
                float p=Transformation,crouch=EaseD(0,.25f,p)*(1-EaseD(.35f,.85f,p)),rise=EaseD(.35f,.9f,p);
                Pose(0,28*crouch-8*rise,0,0);Pose(3,20*crouch-22*rise,0,0);
                Pose(1,-50*crouch,-20*rise,20+50*rise);Pose(2,-50*crouch,20*rise,-20-50*rise);
                Pose(6,-70*crouch,0,0);Pose(7,-70*crouch,0,0);
                Pose(8,35*crouch,0,-8);Pose(9,35*crouch,0,8);
                Pose(10,55*crouch,0,0);Pose(11,55*crouch,0,0);
                rigRoot.localPosition=demonRest+ToMesh*new Vector3(0,-height*.10f*crouch,0);
            }
        }
    }
}

