using UnityEngine;
using UnityEngine.Rendering;
namespace Deepspace
{
 public sealed class BossSerpentWaveVfx:MonoBehaviour
 {
  RectTransform area;Camera camera;bool horizontal;Mesh crest,cube;Material water,foam;MeshRenderer crestRenderer;ParticleSystem spray;
  Vector3[] vertices=new Vector3[49*13];Color[] colors=new Color[49*13];readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[64];
  public float SampleClock{get;private set;}public int SprayCount=>spray!=null?spray.particleCount:0;
  public void Initialize(RectTransform rect,Camera cam,bool across)
  {
   area=rect;camera=cam;horizontal=across;gameObject.layer=Visual3DFollower.FloorLayer;
   water=new Material(Resources.Load<Shader>("BossSerpentWater")){hideFlags=HideFlags.DontSave};
   foam=new Material(Resources.Load<Shader>("BossVoxelEnergy")){hideFlags=HideFlags.DontSave,renderQueue=3032};foam.SetFloat("_ZWrite",0);
   crest=new Mesh{name="Curling translucent water crest",hideFlags=HideFlags.DontSave};crest.MarkDynamic();
   var indices=new int[48*12*6];int k=0;var uv=new Vector2[vertices.Length];
   for(int x=0;x<=48;x++)for(int y=0;y<=12;y++)uv[x*13+y]=new Vector2(x/48f,y/12f);
   for(int x=0;x<48;x++)for(int y=0;y<12;y++){int q=x*13+y,n=q+13;indices[k++]=q;indices[k++]=n;indices[k++]=q+1;indices[k++]=q+1;indices[k++]=n;indices[k++]=n+1;}
   crest.vertices=vertices;crest.uv=uv;crest.triangles=indices;
   gameObject.AddComponent<MeshFilter>().sharedMesh=crest;crestRenderer=gameObject.AddComponent<MeshRenderer>();crestRenderer.sharedMaterial=water;
   crestRenderer.shadowCastingMode=ShadowCastingMode.Off;crestRenderer.receiveShadows=false;
   crestRenderer.sortingOrder=area.GetComponentInParent<Canvas>().rootCanvas.sortingOrder+3;
   var primitive=GameObject.CreatePrimitive(PrimitiveType.Sphere);primitive.SetActive(false);cube=Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);Destroy(primitive);
   var cc=new Color[cube.vertexCount];for(int i=0;i<cc.Length;i++)cc[i]=new Color(1.5f,2,2);cube.colors=cc;
   var go=new GameObject("Breaking wave spray"){layer=gameObject.layer};go.transform.SetParent(transform,false);
   spray=go.AddComponent<ParticleSystem>();spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var main=spray.main;main.playOnAwake=false;main.maxParticles=64;main.startSpeed=0;main.simulationSpace=ParticleSystemSimulationSpace.Local;
   var emission=spray.emission;emission.enabled=false;var shape=spray.shape;shape.enabled=false;
   var r=spray.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Mesh;r.mesh=cube;r.sharedMaterial=foam;r.sortingOrder=crestRenderer.sortingOrder+1;r.shadowCastingMode=ShadowCastingMode.Off;spray.Pause();
  }
  public void Sample(float warning,float remaining)
  {
   if(area==null||camera==null)return;
   bool active=warning<=0&&remaining>0;crestRenderer.enabled=active;
   if(!active){spray.Clear();return;}
   float time=-warning;SampleClock=time;water.SetFloat("_Clock",time);water.SetFloat("_Power",Mathf.Clamp01(remaining/.25f));
   var rect=area.rect;float span=horizontal?rect.width:rect.height,width=horizontal?rect.height:rect.width;
   float worldWidth=area.TransformVector((horizontal?Vector3.up:Vector3.right)*width).magnitude;
   Vector3 depth=area.InverseTransformVector(-camera.transform.forward*worldWidth);
   for(int x=0;x<=48;x++)for(int y=0;y<=12;y++){
    float u=x/48f,v=y/12f,angle=v*Mathf.PI*1.45f;
    float curl=(1-Mathf.Cos(angle))*.5f-.5f;
    float height=Mathf.Sin(v*Mathf.PI)*(.52f+.07f*Mathf.Sin(u*18-time*5));
    float ripple=Mathf.Sin(u*35-time*8)*.025f*Mathf.Sin(v*Mathf.PI);
    var p=horizontal?new Vector3((u-.5f)*span,curl*width,0):new Vector3(curl*width,(u-.5f)*span,0);
    vertices[x*13+y]=p+depth*(.06f+height+ripple);colors[x*13+y]=Color.white;
   }
   crest.vertices=vertices;crest.colors=colors;crest.RecalculateNormals();crest.RecalculateBounds();
   for(int i=0;i<particles.Length;i++){
    float u=Mathf.Repeat(i*.618034f,1),life=Mathf.Repeat(time*.8f+i*.37f,1),v=(life-.5f)*.7f;
    var p=horizontal?new Vector3((u-.5f)*span,v*width,0):new Vector3(v*width,(u-.5f)*span,0);
    particles[i]=new ParticleSystem.Particle{position=p+depth*(.18f+Mathf.Sin(life*Mathf.PI)*.75f),startSize=width*(.025f+i%3*.01f)*(1-life*.6f),startColor=new Color(.8f,1,1),startLifetime=5,remainingLifetime=5,rotation3D=new Vector3(i*17,time*90,i*53)};
   }
   spray.SetParticles(particles,particles.Length);
  }
  void OnDestroy(){if(crest!=null)Destroy(crest);if(cube!=null)Destroy(cube);if(water!=null)Destroy(water);if(foam!=null)Destroy(foam);}
 }
}