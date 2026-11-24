using UnityEngine;
using UnityEngine.Rendering;
namespace Deepspace
{
 public sealed class BossSerpentWaterVfx:MonoBehaviour
 {
  Mesh mesh,droplet;Material water,foam;ParticleSystem spray;Vector3[] vertices;Vector2[] uv;readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[96];
  public int ParticleCount=>spray!=null?spray.particleCount:0;
  public float SampleClock{get;private set;}
  public void Initialize(int count,int layer)
  {
   gameObject.layer=layer;mesh=new Mesh{name="Serpent flowing water ribbons",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();
   vertices=new Vector3[count*4];uv=new Vector2[vertices.Length];var triangles=new int[(count-1)*12];int k=0;
   for(int ribbon=0;ribbon<2;ribbon++)for(int i=0;i<count-1;i++){int a=ribbon*count*2+i*2;triangles[k++]=a;triangles[k++]=a+2;triangles[k++]=a+1;triangles[k++]=a+1;triangles[k++]=a+2;triangles[k++]=a+3;}
   mesh.vertices=vertices;mesh.triangles=triangles;
   water=new Material(Resources.Load<Shader>("BossSerpentWater")){hideFlags=HideFlags.DontSave};
   gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=water;renderer.shadowCastingMode=ShadowCastingMode.Off;
   var p=GameObject.CreatePrimitive(PrimitiveType.Sphere);p.SetActive(false);droplet=Instantiate(p.GetComponent<MeshFilter>().sharedMesh);Destroy(p);
   var colors=new Color[droplet.vertexCount];for(int i=0;i<colors.Length;i++)colors[i]=new Color(.35f,1.2f,1.5f);droplet.colors=colors;
   foam=new Material(Resources.Load<Shader>("BossVoxelEnergy")){hideFlags=HideFlags.DontSave,renderQueue=3032};foam.SetFloat("_ZWrite",0);
   var go=new GameObject("Sea serpent water droplets"){layer=layer};go.transform.SetParent(transform,false);spray=go.AddComponent<ParticleSystem>();
   spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=spray.main;main.playOnAwake=false;main.startSpeed=0;main.maxParticles=96;main.simulationSpace=ParticleSystemSimulationSpace.Local;
   var emission=spray.emission;emission.enabled=false;var shape=spray.shape;shape.enabled=false;
   var r=spray.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Mesh;r.mesh=droplet;r.sharedMaterial=foam;r.shadowCastingMode=ShadowCastingMode.Off;spray.Pause();
  }
  public void Sample(Vector3[] path,float size,float time,float power)
  {
   SampleClock=time;water.SetFloat("_Clock",time);water.SetFloat("_Power",.95f+power*.65f);int count=path.Length;
   for(int ribbon=0;ribbon<2;ribbon++)for(int i=0;i<count;i++){
    float t=i/(float)(count-1),angle=time*1.7f-t*15+ribbon*Mathf.PI;
    var tangent=(path[Mathf.Min(count-1,i+1)]-path[Mathf.Max(0,i-1)]).normalized;
    var side=Vector3.Cross(tangent,Vector3.up).normalized;var normal=Vector3.Cross(tangent,side).normalized;
    float radius=size*(.11f*(1-t*t*t)+.02f),width=size*(.014f+power*.018f);
    var radial=side*Mathf.Cos(angle)+normal*Mathf.Sin(angle);var center=path[i]+radial*radius;
    int k=ribbon*count*2+i*2;vertices[k]=center-radial*width;vertices[k+1]=center+radial*width;uv[k]=new Vector2(t,0);uv[k+1]=new Vector2(t,1);
   }
   mesh.vertices=vertices;mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();
   for(int i=0;i<particles.Length;i++){
    float life=Mathf.Repeat(time*(.13f+power*.08f)+i*.618034f,1);int p=Mathf.Clamp(Mathf.RoundToInt(life*(count-1)),0,count-1);float angle=i*2.39996f+time*2;
    var radial=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),Mathf.Cos(angle*.7f));
    particles[i]=new ParticleSystem.Particle{position=path[p]+radial.normalized*size*(.10f+.045f*(1-life)),startSize=size*(.007f+i%3*.003f)*(1+power),startColor=Color.white,startLifetime=5,remainingLifetime=5};
   }
   spray.SetParticles(particles,particles.Length);
  }
  void OnDestroy(){if(mesh!=null)Destroy(mesh);if(droplet!=null)Destroy(droplet);if(water!=null)Destroy(water);if(foam!=null)Destroy(foam);}
 }
}