using UnityEngine;
namespace Deepspace
{
    public sealed partial class BossHydra
    {
        Vector3 ExtendedHeadOffset(int style,float wind,float hit,int index)
        {
            switch(style){
                case 6:return new Vector3(-.85f*wind+1.3f*hit,.12f*wind,-.3f*hit);
                case 7:return new Vector3(.9f*wind-1.35f*hit,.25f*hit,-.2f*hit);
                case 8:return new Vector3((index-3)*.08f*wind,1.15f*wind-1.5f*hit,-.7f*hit);
                case 9:return new Vector3(Mathf.Sin(wind*Mathf.PI)*.7f+hit*.7f,-.4f*wind+.55f*hit,-.55f*hit);
                case 10:return new Vector3(0,-.45f*wind+.15f*hit,1.2f*wind-1.3f*hit);
                default:return new Vector3((index-3)*(.12f*wind-.18f*hit),-.65f*wind+1.05f*hit,.5f*wind+.3f*hit);
            }
        }
        Vector3 ExtendedHeadBend(int style,float wind,float hit)
        {
            switch(style){
                case 6:return new Vector3(-.8f*wind+.6f*hit,0,0);
                case 7:return new Vector3(.8f*wind-.6f*hit,0,0);
                case 8:return new Vector3(0,.75f*wind-.4f*hit,.3f*wind);
                case 9:return new Vector3(-.7f*wind+.85f*hit,.7f*wind,.65f*wind);
                case 10:return new Vector3(0,-.5f*wind,.95f*wind);
                default:return new Vector3(.5f*wind,-.7f*wind+.3f*hit,.4f*wind);
            }
        }
        Vector3 ExtendedHeadAngles(int style,float wind,float hit)
        {
            switch(style){
                case 6:return new Vector3(-10*hit,-35*wind+45*hit,-12*wind+18*hit);
                case 7:return new Vector3(-8*hit,38*wind-48*hit,15*wind-20*hit);
                case 8:return new Vector3(25*wind-60*hit,0,0);
                case 9:return new Vector3(-15*wind+20*hit,-20*wind+30*hit,35*wind-45*hit);
                case 10:return new Vector3(-20*wind-15*hit,0,0);
                default:return new Vector3(30*wind-35*hit,0,12*wind-12*hit);
            }
        }
        void ApplyExtendedBodyMotion(float quiet)
        {
            if(defeated||SevenHeadStrikeActive)return;
            Vector3 lean=Vector3.zero;float strongest=0;
            for(int i=0;i<MaximumHeads;i++)if(alive[i]&&headAttackStyles[i]>=6)
            {
                float wind=chargeLeft[i]>0?1-chargeLeft[i]/Mathf.Max(.01f,chargeTotal[i]):0;
                float hit=strike[i]>0?Mathf.Sin(Mathf.Clamp01((1-strike[i]/StrikeSeconds)/.65f)*Mathf.PI):0;
                float strength=Mathf.Max(wind,hit);if(strength<=strongest)continue;strongest=strength;
                var offset=ExtendedHeadOffset(headAttackStyles[i],wind,hit,i);lean=new Vector3(-offset.y*4,offset.x*5,-offset.x*2);
            }
            bodyRig.localRotation*=Quaternion.Euler(lean*quiet);
        }
    }
}
