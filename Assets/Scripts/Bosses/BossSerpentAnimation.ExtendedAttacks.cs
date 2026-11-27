using UnityEngine;
namespace BossSlayer
{
    public sealed partial class BossSerpentAnimation
    {
        public void SampleExtendedSpine(int move,float wind,float hit,float follow)
        {
            if(flowingPath==null||dying)return;
            for(int i=0;i<flowingPath.Length;i++)
            {
                float u=i/(float)(flowingPath.Length-1),head=1-u,tail=Mathf.Sin(u*Mathf.PI*.5f);
                Vector3 offset;
                switch(move){
                    case 0:offset=new Vector3((-.14f*wind+.22f*hit)*head*head,hit*.035f*head,-hit*.12f*head);break;
                    case 1:offset=new Vector3((.16f*wind-.25f*hit)*tail,Mathf.Sin(u*Mathf.PI)*hit*.06f,Mathf.Sin(u*5)*hit*.1f);break;
                    case 2:offset=new Vector3(0,(.16f*wind-.18f*hit)*head,-hit*.18f*head);break;
                    case 3:offset=new Vector3(Mathf.Sin(u*6.28f)*(-.12f*wind+.2f*hit),Mathf.Cos(u*6.28f)*(.04f*wind-.07f*hit),Mathf.Cos(u*6.28f)*(-.1f*wind+.15f*hit));break;
                    case 4:offset=new Vector3(Mathf.Sin(u*9)*.04f*wind*head,wind*.05f*head,(.12f*wind-.28f*hit)*head*head);break;
                    default:offset=new Vector3(Mathf.Sin(u*6)*(.16f*wind-.18f*hit),(-.07f*wind+.12f*hit)*head,Mathf.Cos(u*6)*(.12f*wind-.16f*hit));break;
                }
                offset.x+=Mathf.Sin(u*9)*follow*.03f;
                flowingPath[i]+=Space*(offset*serpentHeight*motionAmount);
            }
            ApplyPath();parts[3].localPosition=headRest+HeadPathOffset;
            parts[3].localRotation=frames[0]*Space*Quaternion.Euler((move==2?-35:move==5?25:-12)*hit,(move==0?35:move==1?-25:0)*hit,0)*FromSpace;
        }
    }
}
