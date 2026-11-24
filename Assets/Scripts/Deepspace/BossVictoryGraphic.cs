using UnityEngine;
using UnityEngine.UI;
namespace Deepspace
{
    // Pooled UI geometry complements world-space cubes on the independently framed LED floor.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BossVictoryGraphic:MaskableGraphic
    {
        public Color Accent=new Color(.2f,.8f,1);
        public bool BurstOnly,Floor;
        public float Age{get;private set;}
        public bool ManualPlayback;
        public void Sample(float age){Age=Mathf.Max(0,age);SetVerticesDirty();}
        void Update(){if(!ManualPlayback)Sample(Age+Time.deltaTime);}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float unit=Mathf.Min(r.width,r.height);var center=r.center;
            if(BurstOnly)
            {
                float age=Mathf.Max(0,Age-.45f),fade=Mathf.Clamp01(1-age/2.65f);
                if(Age<.45f||fade<=0)return;
                for(int i=0;i<240;i++)
                {
                    float seed=Mathf.Repeat(i*.754877666f,1),a=i*2.39996f;
                    var direction=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                    float travel=(1-Mathf.Exp(-age*3))*(.1f+seed*.62f);
                    var p=center+direction*unit*travel;
                    Square(vh,p,unit*(.003f+seed*.008f)*fade,a+age*(1+seed*3),Tint(i%4==0?Color.white:Accent,fade));
                }
                for(int ring=0;ring<2;ring++)for(int i=0;i<96;i++)
                {
                    float a=i*Mathf.PI*2/96;var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                    Square(vh,center+d*unit*(.08f+age*(.65f+ring*.2f)),unit*.004f*fade,a,Tint(Accent,fade*.7f));
                }
                return;
            }
            for(int i=0;i<130;i++)
            {
                float seed=Mathf.Repeat(i*.618034f,1),speed=.025f+Mathf.Repeat(i*.317f,1)*.045f;
                float x=Mathf.Repeat(seed+Mathf.Sin(Age*.6f+i)*.018f,1),y=Mathf.Repeat(i*.414214f-Age*speed,1);
                Square(vh,new Vector2(Mathf.Lerp(r.xMin,r.xMax,x),Mathf.Lerp(r.yMin,r.yMax,y)),unit*(.002f+seed*.006f),i+Age*(seed-.5f),Tint(i%4==0?new Color(1,.8f,.25f):Accent,.15f+seed*.35f));
            }
            float pulse=.8f+Mathf.Sin(Age*1.8f)*.2f;
            for(int i=0;i<56;i++)
            {
                float a=i*Mathf.PI*2/56;
                Vector2 p=Floor?new Vector2(r.center.x,Mathf.Lerp(r.yMin,r.yMax,.515f)):new Vector2(Mathf.Lerp(r.xMin,r.xMax,.32f),r.center.y);
                Square(vh,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*unit*.255f,unit*.0035f,a,Tint(Accent,.5f*pulse));
            }
        }
        static Color Tint(Color c,float alpha){c.a=alpha;return c;}
        static void Square(VertexHelper vh,Vector2 center,float radius,float angle,Color color)
        {
            var x=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;var y=new Vector2(-x.y,x.x);int n=vh.currentVertCount;
            vh.AddVert(center-x-y,color,Vector2.zero);vh.AddVert(center+x-y,color,Vector2.zero);vh.AddVert(center+x+y,color,Vector2.zero);vh.AddVert(center-x+y,color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
