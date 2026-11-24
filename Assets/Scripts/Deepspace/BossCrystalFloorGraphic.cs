using UnityEngine;
using UnityEngine.UI;
namespace Deepspace
{
    public sealed class BossCrystalFloorGraphic : MaskableGraphic
    {
        float age,remaining;bool fired,red,beam;
        public void Sample(float time,bool active,float life,bool crimson,bool isBeam=false){age=time;fired=active;remaining=life;red=crimson;beam=isBeam;SetVerticesDirty();}
        void Quad(VertexHelper vh,float left,float bottom,float right,float top,Color tint)
        {
            int i=vh.currentVertCount;vh.AddVert(new Vector3(left,bottom),tint,Vector2.zero);vh.AddVert(new Vector3(right,bottom),tint,Vector2.zero);
            vh.AddVert(new Vector3(right,top),tint,Vector2.zero);vh.AddVert(new Vector3(left,top),tint,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
        void DrawBeam(VertexHelper vh)
        {
            var r=rectTransform.rect;float fade=Mathf.Clamp01((remaining+.45f)/.45f);
            var redColor=new Color(1,.025f,.065f,(fired?.8f:.2f)*fade);
            Quad(vh,r.xMin,r.yMin,r.xMax,r.yMax,redColor);
            float edge=r.width*.055f;
            Quad(vh,r.xMin,r.yMin,r.xMin+edge,r.yMax,new Color(1,.32f,.4f,fade));
            Quad(vh,r.xMax-edge,r.yMin,r.xMax,r.yMax,new Color(1,.32f,.4f,fade));
            if(fired&&remaining>0)
            {
                float core=r.width*(.1f+Mathf.Sin(age*35)*.018f);
                Quad(vh,-core,r.yMin,core,r.yMax,new Color(1,.88f,.88f,fade));
            }
            else if(!fired)for(int i=0;i<12;i++)
            {
                float y=Mathf.Lerp(r.yMin,r.yMax,i/12f);
                Quad(vh,-edge,y,edge,y+r.height*.035f,new Color(1,.5f,.55f,.8f));
            }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if(beam)return;
            if(!fired)return;
            var r=rectTransform.rect;float unit=Mathf.Min(r.width,r.height)*(r.height>r.width*4?2.5f:1),fade=Mathf.Clamp01((remaining+.45f)/.45f);
            Color tint=red?new Color(1,.06f,.12f,fade):new Color(.13f,.84f,1,fade);
            for(int i=0;i<32;i++)
            {
                float a=i*2.39996f,life=Mathf.Clamp01(age/1.1f);
                var origin=new Vector2(Mathf.Sin(i*13.3f)*r.width*.36f,Mathf.Cos(i*7.1f)*r.height*.36f);
                var travel=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*unit*.16f*life;
                var p=origin+travel+Vector2.up*(Mathf.Sin(life*Mathf.PI)*unit*.23f);
                float size=unit*(.025f+i%3*.012f)*(1-Mathf.Clamp01((age-1.5f)/1.3f));
                if(size<=0)continue;
                Vector2 axis=new Vector2(Mathf.Cos(a+age*2),Mathf.Sin(a+age*2)),side=new Vector2(-axis.y,axis.x);
                int start=vh.currentVertCount;
                vh.AddVert(p+axis*size,tint,Vector2.zero);
                vh.AddVert(p+side*size*.43f,Color.Lerp(tint,Color.white,.55f),Vector2.zero);
                vh.AddVert(p-axis*size,tint*.65f,Vector2.zero);
                vh.AddVert(p-side*size*.43f,tint,Vector2.zero);
                vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
            }
            // Persistent short fracture branches on the impact footprint.
            for(int i=0;i<9;i++)
            {
                float a=i*Mathf.PI*2/9;Vector2 p=Vector2.zero;
                for(int j=0;j<3;j++)
                {
                    Vector2 q=p+new Vector2(Mathf.Cos(a+j*.24f),Mathf.Sin(a+j*.24f))*unit*.115f;
                    Vector2 n=new Vector2(-(q-p).y,(q-p).x).normalized*unit*.004f;
                    int k=vh.currentVertCount;var c=tint;c.a*=Mathf.Clamp01(1-age/3);
                    vh.AddVert(p+n,c,Vector2.zero);vh.AddVert(q+n,c,Vector2.zero);vh.AddVert(q-n,c,Vector2.zero);vh.AddVert(p-n,c,Vector2.zero);
                    vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);p=q;
                }
            }
        }
    }
}
