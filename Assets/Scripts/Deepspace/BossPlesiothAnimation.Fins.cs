using UnityEngine;
namespace Deepspace
{
    public sealed partial class BossPlesiothAnimation
    {
        [Header("Flexible fins")]
        [Range(0,2)] public float finFlexAmount = 1;
        sealed class FinSurface
        {
            public MeshFilter filter;
            public Mesh source, mesh;
            public Vector3[] rest, output;
            public float extent;
            public int kind;
        }
        FinSurface[] fins;
        void SampleFinFlex(float time,float charge,float release,float stun)
        {
            if(fins==null)
            {
                fins=new FinSurface[4];
                for(int k=0;k<4;k++)
                {
                    var joint=k<2?parts[k+1]:secondaryParts[k==2?2:5];
                    var filter=joint!=null?joint.GetComponent<MeshFilter>():null;
                    if(filter==null || filter.sharedMesh==null)continue;
                    var source=filter.sharedMesh;
                    var f=new FinSurface { filter=filter,source=source,mesh=Instantiate(source),
                        rest=source.vertices,output=new Vector3[source.vertexCount],kind=k };
                    f.mesh.name=source.name+" (flexible fin)";
                    f.mesh.hideFlags=HideFlags.DontSave;
                    f.mesh.MarkDynamic();
                    for(int i=0;i<f.rest.Length;i++)
                    {
                        f.rest[i]=FromMesh*f.rest[i];
                        float distance=k<2?Mathf.Abs(f.rest[i].x):k==2?Mathf.Max(0,f.rest[i].z):Mathf.Max(0,f.rest[i].y);
                        f.extent=Mathf.Max(f.extent,distance);
                    }
                    f.extent=Mathf.Max(.001f,f.extent);
                    filter.sharedMesh=f.mesh;fins[k]=f;
                }
            }
            float strength=finFlexAmount*motionAmount*Mathf.Lerp(1,.15f,Mathf.Clamp01(stun));
            foreach(var f in fins)
            {
                if(f==null)continue;
                for(int i=0;i<f.rest.Length;i++)
                {
                    var p=f.rest[i];
                    float distance=f.kind<2?Mathf.Abs(p.x):f.kind==2?Mathf.Max(0,p.z):Mathf.Max(0,p.y);
                    float span=Mathf.Clamp01(distance/f.extent);
                    // Root remains attached; the outer fin trails the root, then ripples.
                    float soft=EaseFish(.07f,1,span);
                    float sidePhase=f.kind==1?1.05f:.35f;
                    float phase=swimPhase+sidePhase-span*3.6f;
                    float trailing=Mathf.Clamp01(p.z/f.extent);
                    float membrane=soft*(.35f+.65f*trailing);
                    float wind=EaseFish(.08f,.68f,charge);
                    float recoil=0;
                    if(release>0)
                    {
                        float age=(1-release)*RecoverySeconds;
                        wind=1-EaseFish(span*.10f,.16f+span*.10f,age);
                        float t=Mathf.Max(0,age-span*.12f);
                        recoil=Mathf.Sin(t*10)*Mathf.Exp(-t*4)*(1-EaseFish(.65f,1.05f,age));
                    }
                    float wave=Mathf.Sin(phase)*Mathf.Lerp(.16f,.11f,swimSpeed)+Mathf.Sin(phase*1.8f-span)*.028f;
                    float bend=(wave-wind*.10f+recoil*.17f)*soft*strength;
                    if(f.kind<2)
                    {
                        p.y+=f.extent*bend;
                        p.y+=f.extent*membrane*strength*Mathf.Sin(phase-trailing*2.2f)*.06f;
                        p.z+=f.extent*soft*strength*(Mathf.Sin(phase-.8f-trailing)*.12f+recoil*.08f);
                    }
                    else p.x+=f.extent*bend*(f.kind==2?.8f:.65f);
                    f.output[i]=ToMesh*p;
                }
                f.mesh.vertices=f.output;
                f.mesh.RecalculateNormals();
                f.mesh.RecalculateBounds();
            }
        }
        void ReleaseFinMeshes()
        {
            if(fins==null)return;
            foreach(var f in fins)
            {
                if(f==null)continue;
                if(f.filter!=null && f.filter.sharedMesh==f.mesh)f.filter.sharedMesh=f.source;
                if(f.mesh!=null) { if(Application.isPlaying)Destroy(f.mesh);else DestroyImmediate(f.mesh); }
            }
            fins=null;
        }
        void OnDestroy() { ReleaseDeathMeshes(); ReleaseFinMeshes(); }
    }
}
