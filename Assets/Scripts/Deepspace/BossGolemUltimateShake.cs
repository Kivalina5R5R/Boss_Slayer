using UnityEngine;
namespace Deepspace
{
    // Shake presentation layers only: beam seams, tracked input and hitboxes stay fixed.
    public sealed class BossGolemUltimateShake : MonoBehaviour
    {
        RectTransform wall,floor;
        Vector3 wallPosition,floorPosition,wallScale,floorScale;
        Quaternion wallRotation,floorRotation;
        bool saved;
        public bool Playing {get;private set;}
        public float Strength {get;private set;}
        public float Clock {get;private set;}
        public Vector2 FloorOffset {get;private set;}
        public Vector2 WallOffset {get;private set;}
        public void Initialize(RectTransform wallBackground,RectTransform floorBackground){Clear();wall=wallBackground;floor=floorBackground;}
        public void Sample(float warning,float total,float remaining)
        {
            if(!saved)
            {
                if(wall!=null){wallPosition=wall.localPosition;wallScale=wall.localScale;wallRotation=wall.localRotation;}
                if(floor!=null){floorPosition=floor.localPosition;floorScale=floor.localScale;floorRotation=floor.localRotation;}
                saved=true;
            }
            Playing=true;Clock=total-warning;
            float age=Mathf.Max(0,-warning);
            float charge=Mathf.Clamp01(Clock/Mathf.Max(.01f,total));
            float fade=Mathf.Clamp01((remaining+.4f)/.4f);
            Strength=(warning>0?.08f+.3f*charge*charge:
                1.4f*Mathf.Exp(-age*5)+.5f+.18f*Mathf.Pow(.5f+.5f*Mathf.Sin(age*17),4))*fade;
            Vector2 vibration=new Vector2(Mathf.Sin(Clock*91)+Mathf.Sin(Clock*43)*.35f,Mathf.Sin(Clock*113+.9f)*.7f+Mathf.Cos(Clock*37)*.25f);
            WallOffset=Apply(wall,wallPosition,wallScale,wallRotation,vibration,.019f,.28f);
            FloorOffset=Apply(floor,floorPosition,floorScale,floorRotation,new Vector2(vibration.x*.85f,vibration.y),.027f,.42f);
        }
        Vector2 Apply(RectTransform rect,Vector3 rest,Vector3 scale,Quaternion rotation,Vector2 vibration,float amount,float roll)
        {
            if(rect==null)return Vector2.zero;
            var offset=vibration*Mathf.Min(rect.rect.width,rect.rect.height)*amount*Strength;
            rect.localPosition=rest+(Vector3)offset;
            rect.localScale=Vector3.Scale(scale,new Vector3(1+.10f*Strength,1+.10f*Strength,1));
            rect.localRotation=rotation*Quaternion.Euler(0,0,Mathf.Sin(Clock*67)*roll*Strength);
            return offset;
        }
        public void Clear()
        {
            if(saved)
            {
                if(wall!=null){wall.localPosition=wallPosition;wall.localScale=wallScale;wall.localRotation=wallRotation;}
                if(floor!=null){floor.localPosition=floorPosition;floor.localScale=floorScale;floor.localRotation=floorRotation;}
            }
            saved=false;Playing=false;Strength=Clock=0;FloorOffset=WallOffset=Vector2.zero;
        }
        void OnDisable(){Clear();}
        void OnDestroy(){Clear();}
    }
    public sealed partial class BossSlayerGameManager
    {
        BossGolemUltimateShake golemUltimateShake;
        void PrepareGolemUltimateShake()
        {
            if(bossModel==null||bossModel.GetComponent<BossGolemAnimation>()==null)return;
            bool ultimate=false;foreach(var a in attacks)ultimate|=a.IsBossUltimate;
            if(!ultimate)return;
            if(ultimateImpactFrame==null)
            {
                ultimateImpactFrame=gameObject.AddComponent<BossUltimateImpactFrame>();
                ultimateImpactFrame.Initialize(cutsceneArea,playArea,bossFloorBackgroundImage!=null?bossFloorBackgroundImage.rectTransform:null);
            }
            ultimateImpactFrame.SetExternalShake(true);
            if(golemUltimateShake==null)
            {
                golemUltimateShake=gameObject.AddComponent<BossGolemUltimateShake>();
                golemUltimateShake.Initialize(bossWallBackgroundImage!=null?bossWallBackgroundImage.rectTransform:null,
                    bossFloorBackgroundImage!=null?bossFloorBackgroundImage.rectTransform:null);
            }
        }
        void UpdateGolemUltimateShake()
        {
            if(golemUltimateShake==null)return;
            AttackState current=null;
            foreach(var a in attacks)if(a.IsBossUltimate&&a.WarningDelay<=0&&a.ActiveTimer>-.4f&&(current==null||a.TelegraphTimer<current.TelegraphTimer))current=a;
            var golem=bossModel!=null?bossModel.GetComponent<BossGolemAnimation>():null;
            if(current==null||golem==null){ClearGolemUltimateShake();return;}
            golemUltimateShake.Sample(current.TelegraphTimer,current.TelegraphTotal,current.ActiveTimer);
            golem.SetUltimateRecoil(golemUltimateShake.Clock,golemUltimateShake.Strength);
        }
        void ClearGolemUltimateShake()
        {
            if(golemUltimateShake!=null)golemUltimateShake.Clear();
            if(ultimateImpactFrame!=null)ultimateImpactFrame.SetExternalShake(false);
            if(bossModel!=null)bossModel.GetComponent<BossGolemAnimation>()?.SetUltimateRecoil(0,0);
        }
    }
}
