using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BossMeteorGraphic : MaskableGraphic
    {
        public bool Impact;
        public float Progress;
        void Tri(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color ca,Color cb,Color cc)
        {
            float scale=rectTransform.rect.width;var center=rectTransform.rect.center;int start=vh.currentVertCount;
            vh.AddVert(center+a*scale,ca,Vector2.zero);vh.AddVert(center+b*scale,cb,Vector2.zero);vh.AddVert(center+c*scale,cc,Vector2.zero);vh.AddTriangle(start,start+1,start+2);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Color hot=new Color(1,.7f,.12f),orange=new Color(1,.18f,.015f),stone=new Color(.16f,.055f,.025f);
            if(!Impact)
            {
                Tri(vh,new Vector2(-.23f,.02f),new Vector2(.23f,.02f),new Vector2(.05f,1.8f),orange,orange,new Color(1,.08f,0,0));
                Tri(vh,new Vector2(-.14f,.02f),new Vector2(.14f,.02f),new Vector2(-.06f,1.3f),hot,hot,new Color(1,.35f,.01f,0));
                for(int i=0;i<9;i++)
                {
                    float a=i*Mathf.PI*2/9,b=(i+1)*Mathf.PI*2/9;
                    float ra=.27f+(i%3)*.015f,rb=.27f+((i+1)%3)*.015f;
                    var p=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*ra;var q=new Vector2(Mathf.Cos(b),Mathf.Sin(b))*rb;
                    Tri(vh,Vector2.zero,p,q,stone,i%2==0?orange:stone,orange);
                }
                Tri(vh,new Vector2(-.16f,.1f),new Vector2(.02f,-.17f),new Vector2(.045f,.12f),orange,hot,stone);
            }
            else
            {
                for(int i=0;i<10;i++)
                {
                    float a=i*Mathf.PI*2/10;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var side=new Vector2(-dir.y,dir.x);
                    float distance=.15f+Progress*.75f,size=(1-Progress)*.11f;
                    var tint=i%2==0?orange:stone;tint.a=1-Progress;
                    Tri(vh,dir*distance+side*size,dir*(distance+size*2),dir*distance-side*size,tint,tint,tint);
                }
            }
        }
    }
}
