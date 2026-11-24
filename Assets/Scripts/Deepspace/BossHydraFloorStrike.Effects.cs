using UnityEngine;
using UnityEngine.UI;
namespace Deepspace
{
    public sealed partial class BossHydraFloorStrike
    {
        static void Stroke(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
        {
            var side=new Vector2(-(b-a).y,(b-a).x).normalized*width;
            int n=vh.currentVertCount;
            vh.AddVert(a-side,color,Vector2.zero);vh.AddVert(a+side,color,Vector2.zero);
            vh.AddVert(b+side,color,Vector2.zero);vh.AddVert(b-side,color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        static void DrawStrikeWater(VertexHelper vh,Vector2 center,int head,float age,bool front)
        {
            if(age<0||age>.85f)return;
            float t=age/.85f,fade=(1-t)*(1-t);
            if(!front)
            {
                for(int wave=0;wave<2;wave++)for(int i=0;i<48;i++)
                {
                    float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;
                    float radius=125+Mathf.Sqrt(t)*(170+wave*70);
                    Stroke(vh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a)*.65f)*radius,
                        center+new Vector2(Mathf.Cos(b),Mathf.Sin(b)*.65f)*radius,4+8*fade,new Color(.55f,.95f,1,fade*.9f));
                }
                return;
            }
            for(int i=0;i<13;i++)
            {
                float angle=i*2.39996f+head*.47f;
                var dir=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)*.65f);
                float speed=170+Mathf.Repeat(i*.618f,1)*170;
                var tip=center+dir*(85+Mathf.Repeat(i*.37f,1)*50+speed*t)+Vector2.up*(Mathf.Sin(t*Mathf.PI)*95);
                Stroke(vh,tip-dir*(12+Mathf.Repeat(i*.71f,1)*45*fade),tip,2+3*fade,new Color(.8f,1,1,fade));
            }
            if(age<.13f)for(int side=-1;side<=1;side+=2)
            {
                var tip=center+Vector2.right*side*38;
                Stroke(vh,tip+new Vector2(-side*16,100*(1-age/.13f)),tip,5,new Color(1,1,.85f,1-age/.13f));
            }
        }
    }
}
