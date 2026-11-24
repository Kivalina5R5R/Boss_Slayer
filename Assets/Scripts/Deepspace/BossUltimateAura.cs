using UnityEngine;
using UnityEngine.Rendering;

namespace Deepspace
{
    // A separate ultimate-only aura; follows the boss without changing its authored mesh or scale.
    public sealed class BossUltimateAura : MonoBehaviour
    {
        readonly LineRenderer[] rings = new LineRenderer[4];
        readonly LineRenderer[] streams = new LineRenderer[24];
        Material material;
        Transform boss;
        Camera view;
        Vector3 offset;
        Color accent;
        float radius, started, release = -100f, warning;
        public bool IsPlaying => gameObject.activeSelf && boss != null;

        public void Play(Transform target, Camera camera, Bounds bounds, Color color, float charge)
        {
            boss = target; view = camera; warning = charge;
            offset = bounds.center - target.position;
            radius = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.18f;
            if(camera != null && camera.orthographic) radius = Mathf.Min(radius, camera.orthographicSize * .65f);
            radius = Mathf.Max(.2f, radius);
            accent = color; accent.a = 1;
            started = Time.time; release = -100f;
            if(material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                for(int i=0;i<rings.Length;i++) rings[i]=Line("Ultimate energy ring "+i, 96, true);
                for(int i=0;i<streams.Length;i++) streams[i]=Line("Ultimate energy stream "+i, 8, false);
            }
            gameObject.SetActive(true); LateUpdate();
        }

        LineRenderer Line(string label, int count, bool loop)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false);
            go.layer = Visual3DFollower.BossLayer;
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial=material; line.useWorldSpace=true; line.loop=loop;
            line.positionCount=count; line.numCapVertices=3;
            line.shadowCastingMode=ShadowCastingMode.Off; line.receiveShadows=false;
            return line;
        }
        public void Pulse() { release = Time.time; }
        public void Stop() { gameObject.SetActive(false); }
        void LateUpdate()
        {
            if(boss==null || view==null){Stop();return;}
            float age=Time.time-started;
            float charge=Mathf.Clamp01(age/Mathf.Max(.1f,warning));
            float burst=Mathf.Clamp01((Time.time-release)/.85f);
            bool releasing=Time.time-release<.85f;
            Vector3 center=boss.position+offset-view.transform.forward*radius*.25f;
            Vector3 right=view.transform.right, up=view.transform.up;
            for(int r=0;r<rings.Length;r++)
            {
                bool shock=r==3;
                rings[r].enabled=!shock || releasing;
                float ringRadius=radius*(shock ? 1+burst*1.5f : .85f+r*.2f+.035f*Mathf.Sin(age*3+r));
                float alpha=shock?1-burst:.38f+charge*.42f;
                Color tint=Color.Lerp(accent,Color.white,r==1?.7f:.18f);tint.a=alpha;
                rings[r].startColor=rings[r].endColor=tint;
                rings[r].widthMultiplier=radius*(shock?.045f:.018f);
                for(int i=0;i<96;i++)
                {
                    float a=i*Mathf.PI*2/96+age*(r%2==0?.35f:-.5f);
                    float tooth=shock?0:(i%8<2?.055f:0);
                    rings[r].SetPosition(i,center+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*(ringRadius+radius*tooth));
                }
            }
            for(int i=0;i<streams.Length;i++)
            {
                float phase=Mathf.Repeat(age*(releasing?1.8f:.6f)+i*.618f,1);
                float a=i*Mathf.PI*2/streams.Length+age*.16f;
                float distance=releasing?Mathf.Lerp(.9f,2.2f,phase):Mathf.Lerp(1.9f,.85f,phase);
                Color tint=Color.Lerp(accent,Color.white,.45f);tint.a=Mathf.Sin(phase*Mathf.PI)*.85f;
                streams[i].startColor=tint; tint.a=0;streams[i].endColor=tint;
                streams[i].widthMultiplier=radius*.025f;
                for(int j=0;j<8;j++)
                {
                    float tail=j/7f;
                    float angle=a+tail*.1f*Mathf.Sin(age*4+i);
                    float rr=radius*(distance+(releasing?-1:1)*tail*.3f);
                    streams[i].SetPosition(j,center+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*rr);
                }
            }
        }
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}