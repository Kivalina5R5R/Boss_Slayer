using UnityEngine;
namespace Deepspace
{
    public sealed partial class BossDragonAnimation
    {
        [Header("Breath Neck Motion")]
        [Range(0f, .4f)] public float neckExtension = .22f;
        [Range(.2f, .7f)] public float headLiftDelay = .48f;
        float phaseRoarProgress=-1;
        public void SetPhaseRoar(float progress) { phaseRoarProgress=progress; }
        Transform neckRestRig;
        Vector3 lowerNeckScale, upperNeckScale, headScale;
        bool CacheNeckRest()
        {
            if(rigRoot==null || secondaryParts==null || secondaryParts.Length<2 ||
                secondaryParts[0]==null || secondaryParts[1]==null || parts==null || parts.Length<4 || parts[3]==null)return false;
            if(neckRestRig==rigRoot)return true;
            neckRestRig=rigRoot;
            lowerNeckScale=secondaryParts[0].localScale;
            upperNeckScale=secondaryParts[1].localScale;
            headScale=parts[3].localScale;
            return true;
        }
        void ResetNeckStretch()
        {
            if(!CacheNeckRest())return;
            secondaryParts[0].localScale=lowerNeckScale;
            secondaryParts[1].localScale=upperNeckScale;
            parts[3].localScale=headScale;
        }
        static float NeckEase(float from,float to,float value)
            => Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,value));
        void SampleBreathNeck(float time,float charge,float release,float stun)
        {
            if(!CacheNeckRest())return;
            ResetNeckStretch();
            // Ground-impact attacks retain their existing silhouette.
            if(AttackVariant==2 || stun>0 || (charge<=0 && release<=0))return;
            float q=release>0?1:Mathf.Clamp01(charge);
            float age=(1-Mathf.Clamp01(release))*1.05f;
            float lowerReturn=release>0?1-NeckEase(.15f,.82f,age):1;
            float upperReturn=release>0?1-NeckEase(.22f,.94f,age):1;
            float headReturn=release>0?1-NeckEase(.30f,1.05f,age):1;
            float lower=NeckEase(.02f,.34f,q)*lowerReturn;
            float upper=NeckEase(.22f,.62f,q)*upperReturn;
            float lift=NeckEase(headLiftDelay,.94f,q)*headReturn;
            float motion=motionAmount;
            float lowerPitch=(-22*lower+36*lift)*motion;
            float upperPitch=(-16*upper+24*lift)*motion;
            float sweep=AttackVariant==1?Mathf.Sin(time*.8f)*12*Mathf.Max(lower,lift):0;
            // First uncoil from the chest. The head counter-rotates to stay low until the wave arrives.
            Set(0,new Vector3(lowerPitch,sweep*.4f,Mathf.Sin(time*.8f)*1.2f),1);
            Set(1,new Vector3(upperPitch,sweep*.6f,Mathf.Sin(time*.8f-.5f)*1.2f),1);
            float headPitch=-(lowerPitch+upperPitch)*(1-lift)+30*lift*motion;
            parts[3].localRotation=ToDragonMesh*Quaternion.Euler(headPitch,sweep*.25f,0)*FromDragonMesh;
            float lowerStretch=1+neckExtension*lower*motion;
            float upperStretch=1+neckExtension*.6f*upper*motion;
            // Mesh Z is sculpt Y. Compensate inherited stretch so the skull keeps its size.
            secondaryParts[0].localScale=Vector3.Scale(lowerNeckScale,new Vector3(1,1,lowerStretch));
            secondaryParts[1].localScale=Vector3.Scale(upperNeckScale,new Vector3(1,1,upperStretch/lowerStretch));
            parts[3].localScale=Vector3.Scale(headScale,new Vector3(1,1,1/upperStretch));
        }
    }
}
