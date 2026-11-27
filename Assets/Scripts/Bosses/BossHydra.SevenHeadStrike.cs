using UnityEngine;
namespace BossSlayer
{
    public sealed partial class BossHydra
    {
        public const float SevenHeadChargeSeconds=1.65f, SevenHeadTravelSeconds=.42f, SevenHeadHoldSeconds=.35f, SevenHeadReturnSeconds=1.1f;
        public const float SevenHeadImpactTime=SevenHeadChargeSeconds+SevenHeadTravelSeconds;
        public const float SevenHeadDuration=SevenHeadImpactTime+SevenHeadHoldSeconds+SevenHeadReturnSeconds;
        float sevenHeadAge=-1;
        public float SevenHeadAge=>sevenHeadAge;
        public bool SevenHeadStrikeActive=>sevenHeadAge>=0 && sevenHeadAge<SevenHeadDuration && !defeated;
        public float SevenHeadExtension
        {
            get
            {
                if(!SevenHeadStrikeActive)return 0;
                if(sevenHeadAge<SevenHeadImpactTime)return Mathf.Pow(Mathf.Clamp01((sevenHeadAge-SevenHeadChargeSeconds)/SevenHeadTravelSeconds),2.6f);
                return 1-Mathf.SmoothStep(0,1,Mathf.Clamp01((sevenHeadAge-SevenHeadImpactTime-SevenHeadHoldSeconds)/SevenHeadReturnSeconds));
            }
        }
        public void BeginSevenHeadStrike()
        {
            if(defeated)return;
            // The signature completes regeneration before its seven-head wind-up.
            pending.Clear();ActiveHeads=MaximumHeads;sevenHeadAge=0;
            for(int i=0;i<MaximumHeads;i++)
            {
                alive[i]=true;growth[i]=1;strike[i]=0;chargeLeft[i]=0;
                necks[i].gameObject.SetActive(true);necks[i].localScale=Vector3.one;
                PoseReferenceHead(i,0,1,0);
            }
        }
        public void CancelSevenHeadStrike(){sevenHeadAge=-1;}
    }
}
