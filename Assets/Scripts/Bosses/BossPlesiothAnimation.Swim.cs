using UnityEngine;
namespace BossSlayer
{
    public sealed partial class BossPlesiothAnimation
    {
        float swimPhase, swimSpeed, swimHeading, swimBank, swimSampleTime=-1;
        void ResetSwim()
        { swimPhase=swimSpeed=swimHeading=swimBank=0;swimSampleTime=-1; }
        void SampleSwimClock(float time)
        {
            float target=Mathf.Clamp(Locomotion,-1,1);
            if(swimSampleTime<0 || time<swimSampleTime)
            {
                swimSpeed=Mathf.Abs(target);swimHeading=target;swimBank=0;
                swimPhase=time*Mathf.Lerp(1.65f,3.8f,swimSpeed);
            }
            else
            {
                float dt=time-swimSampleTime;
                float oldSpeed=swimSpeed,oldHeading=swimHeading;
                swimSpeed=Mathf.Lerp(swimSpeed,Mathf.Abs(target),1-Mathf.Exp(-dt*3));
                swimHeading=Mathf.Lerp(swimHeading,target,1-Mathf.Exp(-dt*2.2f));
                // Integrate frequency: accelerating never jumps the tail's phase.
                swimPhase+=dt*Mathf.Lerp(1.65f,3.8f,(oldSpeed+swimSpeed)*.5f);
                float turn=dt>0?Mathf.Clamp((swimHeading-oldHeading)/dt,-1,1):swimBank;
                swimBank=Mathf.Lerp(swimBank,turn,1-Mathf.Exp(-dt*4));
            }
            swimSampleTime=time;
        }
    }
}
