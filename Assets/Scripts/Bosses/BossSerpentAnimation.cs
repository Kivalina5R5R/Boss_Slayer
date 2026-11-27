using UnityEngine;
using System.Collections.Generic;
namespace BossSlayer
{
 public sealed partial class BossSerpentAnimation:BossModelAnimation
 {
  public const float DeathPoseSeconds=4.2f;
  static readonly Quaternion Space=Quaternion.Euler(90,0,0),FromSpace=Quaternion.Inverse(Space);
  sealed class Surface{public MeshFilter filter;public Mesh source,mesh;public Vector3[] rest,vertices;public Vector2[] uv;}
  readonly List<Surface> surfaces=new List<Surface>();
  public Vector3[] waterPath;
  Vector3[] flowingPath,deathPath;Quaternion[] frames;
  BossSerpentWaterVfx waterVfx;bool waterStopped,dying,prepared;
  Vector3 headRest,rigScale;Quaternion deathHead,deathJaw,deathRig;
  float serpentHeight=1,deathFloor,travelDirection;
  public float TravelDirection=>travelDirection;
  public int WaterParticleCount=>waterVfx!=null?waterVfx.ParticleCount:0;
  public float SwimClock{get;private set;}
  public Vector3 HeadPathOffset=>flowingPath!=null?flowingPath[0]-waterPath[0]:Vector3.zero;
  public override bool Initialize(){
   if(!base.Initialize())return false;
   if(!prepared){prepared=true;headRest=parts[3].localPosition;rigScale=rigRoot.localScale;serpentHeight=AnimatedMesh.bounds.size.z;}
   return waterPath!=null&&waterPath.Length>2;
  }
  // Head follows the same analytic path as the neck mesh, avoiding interpolation gaps.
  protected override Transform[] GetPoseTransforms(){var list=new List<Transform>(base.GetPoseTransforms());list.Remove(parts[3]);return list.ToArray();}
  public override void ResetMotion(){
   StopWater();waterStopped=false;dying=false;Restore();base.ResetMotion();
   if(prepared){parts[3].localPosition=headRest;rigRoot.localScale=rigScale;}
   SwimClock=0;travelDirection=0;flowingPath=null;deathPath=null;frames=null;
  }
  void Prepare(){
   if(flowingPath==null){flowingPath=(Vector3[])waterPath.Clone();frames=new Quaternion[waterPath.Length];}
   if(surfaces.Count>0)return;
   foreach(int part in new[]{0,5}){
    var f=parts[part].GetComponent<MeshFilter>();if(f==null||f.sharedMesh==null)continue;
    var source=f.sharedMesh;var s=new Surface{filter=f,source=source,mesh=Instantiate(source),rest=source.vertices,uv=source.uv2};
    s.vertices=new Vector3[s.rest.Length];s.mesh.name="Serpent flowing skin";s.mesh.hideFlags=HideFlags.DontSave;s.mesh.MarkDynamic();f.sharedMesh=s.mesh;surfaces.Add(s);
   }
  }
  public override void SamplePose(float time,float charge,float release,float stun){
   if(dying)return;
   base.SamplePose(time,charge,release,stun);if(!Initialize())return;Prepare();travelDirection=Mathf.Lerp(travelDirection,Locomotion,1-Mathf.Exp(-Mathf.Clamp(time-SwimClock,0,.1f)*6));SwimClock=time;
   parts[0].localRotation=parts[1].localRotation=parts[2].localRotation=parts[5].localRotation=Quaternion.identity;
   float hit=release>0?Mathf.Sin(Mathf.Clamp01((1-release)*3)*Mathf.PI):0;
   float coil=AttackVariant==4?charge:charge*.5f;
   var neck=FromSpace*waterPath[0];
   float move=Mathf.Abs(travelDirection);
   // Continuously open and close the coil; movement is delayed down the spine.
   float open=(.48f+.20f*Mathf.Sin(time*.65f)+move*.24f)*(1-coil*.6f)*(1-stun*.85f);
   for(int i=0;i<waterPath.Length;i++){
    float u=i/(float)(waterPath.Length-1),follow=time-u*2.4f;
    var rest=FromSpace*waterPath[i];
    float envelope=Mathf.Sin(u*Mathf.PI);
    var lead=new Vector3(Mathf.Sin(follow*1.05f)*.115f+travelDirection*(.13f-u*.22f),Mathf.Sin(follow*.72f)*.045f,Mathf.Cos(follow*.85f)*.065f)*serpentHeight;
    var extended=neck+new Vector3(Mathf.Sin(u*Mathf.PI*2.3f-time*.95f)*(.24f+move*.065f)*envelope,
       -u*.72f+Mathf.Sin(u*7-time*1.3f)*.07f*envelope,Mathf.Sin(u*5+time*.8f)*.17f*envelope+u*.12f)*serpentHeight;
    var p=Vector3.Lerp(rest,extended,Mathf.Clamp01(open))+lead*(1-stun*.85f);
    float pulse=hit*Mathf.Exp(-Mathf.Pow((u-(1-release)*1.5f)*5,2));
    p+=new Vector3(Mathf.Sin(time*1.8f-u*9)*.025f*u,pulse*.075f,Mathf.Sin(time*1.6f-u*8)*.022f*u)*serpentHeight*(1-stun*.85f);
    if(stun>0){float jolt=Mathf.Sin(stunElapsed*12-u*13)*Mathf.Exp(-stunElapsed*1.3f);
     p+=new Vector3(jolt*.045f,-.08f*Mathf.Sin(u*Mathf.PI),Mathf.Sin(stunElapsed*2-u*5)*.025f)*serpentHeight*stun;}
    flowingPath[i]=Space*p;
   }
   ApplyPath();
   parts[3].localPosition=headRest+HeadPathOffset;
   float headCoil=release>0?release:coil;
   parts[3].localRotation=frames[0]*Space*Quaternion.Euler(-headCoil*12+hit*10,Mathf.Sin(time*1.05f)*12-travelDirection*32,0)*FromSpace;
   if(stun>0) parts[3].localRotation=Quaternion.Slerp(parts[3].localRotation,frames[0]*Space*Quaternion.Euler(22+Mathf.Sin(stunElapsed*3)*5,Mathf.Sin(stunElapsed*2)*8,0)*FromSpace,stun);
   parts[4].localRotation=Space*Quaternion.Euler(-5-coil*22-hit*15,0,0)*FromSpace;
   SampleWater(time,AttackVariant==4?Mathf.Max(coil,release):Mathf.Max(coil,hit)*.35f);
  }
  void ApplyPath(){
   int count=waterPath.Length;
   for(int i=0;i<count;i++){
    int a=Mathf.Max(0,i-1),b=Mathf.Min(count-1,i+1);
    frames[i]=Quaternion.FromToRotation(waterPath[b]-waterPath[a],flowingPath[b]-flowingPath[a]);
   }
   foreach(var s in surfaces){
    for(int i=0;i<s.rest.Length;i++){
     float along=Mathf.Clamp01(s.uv[i].x)*(count-1);int a=Mathf.Min(count-2,(int)along);float t=along-a;
     var origin=Vector3.Lerp(waterPath[a],waterPath[a+1],t);
     var target=Vector3.Lerp(flowingPath[a],flowingPath[a+1],t);
     var turn=Quaternion.Slerp(frames[a],frames[a+1],t);
     s.vertices[i]=target+turn*(s.rest[i]-origin);
    }
    s.mesh.vertices=s.vertices;s.mesh.RecalculateNormals();s.mesh.RecalculateBounds();
   }
  }
  void SampleWater(float time,float power){
   if(waterStopped)return;
   if(waterVfx==null){
    var go=new GameObject("VFX == Serpent water mantle"){hideFlags=HideFlags.DontSave};go.transform.SetParent(parts[0],false);
    waterVfx=go.AddComponent<BossSerpentWaterVfx>();waterVfx.Initialize(waterPath.Length,gameObject.layer);
   }
   waterVfx.Sample(flowingPath,serpentHeight,time,power);
  }
  public void BeginDeathPose(){
   if(!Initialize())return;Prepare();Defeat();StopWater();dying=true;
   deathPath=(Vector3[])flowingPath.Clone();deathHead=parts[3].localRotation;deathJaw=parts[4].localRotation;deathRig=rigRoot.localRotation;
   deathFloor=float.MaxValue;foreach(var p in deathPath)deathFloor=Mathf.Min(deathFloor,(FromSpace*p).y);
  }
  public void SampleDeathPose(float age){
   if(deathPath==null)return;float settle=Mathf.SmoothStep(0,1,age/DeathPoseSeconds);
   for(int i=0;i<deathPath.Length;i++){
    float u=i/(float)(deathPath.Length-1),collapse=Mathf.SmoothStep(0,1,Mathf.Clamp01((age-u*.8f)/3.3f));
    var start=FromSpace*deathPath[i];
    var rest=new Vector3((u-.5f)*serpentHeight*.90f,deathFloor+serpentHeight*(.025f+.025f*Mathf.Sin(u*6)),Mathf.Sin(u*5)*serpentHeight*.12f);
    float twitch=Mathf.Sin(age*8-u*11)*Mathf.Sin(Mathf.Min(1,age/.3f)*Mathf.PI*.5f)*Mathf.Exp(-age*1.4f);
    flowingPath[i]=Space*(Vector3.Lerp(start,rest,collapse)+Vector3.right*twitch*serpentHeight*.045f*u);
   }
   ApplyPath();parts[3].localPosition=headRest+HeadPathOffset;
   parts[3].localRotation=Quaternion.Slerp(deathHead,Space*Quaternion.Euler(32,0,20)*FromSpace,settle);
   parts[4].localRotation=Quaternion.Slerp(deathJaw,Space*Quaternion.Euler(-24,0,0)*FromSpace,settle);
   rigRoot.localRotation=Quaternion.Slerp(deathRig,Quaternion.identity,settle);
  }
  public void SampleDeathDissolve(float progress){if(prepared)rigRoot.localScale=rigScale*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01(progress)));}
  public void StopWater(){waterStopped=true;if(waterVfx!=null){waterVfx.gameObject.SetActive(false);if(Application.isPlaying)Destroy(waterVfx.gameObject);else DestroyImmediate(waterVfx.gameObject);}waterVfx=null;}
  void Restore(){foreach(var s in surfaces){if(s.filter!=null&&s.filter.sharedMesh==s.mesh)s.filter.sharedMesh=s.source;if(s.mesh!=null){if(Application.isPlaying)Destroy(s.mesh);else DestroyImmediate(s.mesh);}}surfaces.Clear();}
  void OnDestroy(){StopWater();Restore();}
 }
}