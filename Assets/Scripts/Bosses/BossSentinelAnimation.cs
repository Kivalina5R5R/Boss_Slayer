using UnityEngine;
using System.Collections.Generic;
namespace BossSlayer
{
 public sealed class BossSentinelAnimation:BossModelAnimation
 {
  public Transform[] mechanisms=new Transform[6];
  public Transform[] weaponSockets=new Transform[3];

  [Header("Chest reactor blink")]
  [Min(.1f)] public float reactorBlinkSpeed=3.5f;
  [Min(0f)] public float reactorPeak=4f;
  MeshFilter reactorFilter;Mesh reactorSource,reactorMesh;Color[] reactorOriginal,reactorColors;bool[] reactorMask;
  public float ReactorBrightness {get;private set;}
  void SampleReactor(float time,bool shutdown=false)
  {
   if(reactorMesh==null)
   {
    if(parts==null||parts.Length==0||parts[0]==null)return;
    reactorFilter=parts[0].GetComponent<MeshFilter>();if(reactorFilter==null||reactorFilter.sharedMesh==null)return;
    reactorSource=reactorFilter.sharedMesh;reactorOriginal=reactorSource.colors;
    reactorColors=(Color[])reactorOriginal.Clone();reactorMask=new bool[reactorColors.Length];
    for(int i=0;i<reactorMask.Length;i++){var c=reactorOriginal[i];reactorMask[i]=c.r>.65f&&c.g>.15f&&c.b<c.r*.55f;}
    reactorMesh=Instantiate(reactorSource);reactorMesh.name="Sentinel chest reactor (runtime)";reactorMesh.hideFlags=HideFlags.DontSave;
    reactorMesh.MarkDynamic();reactorFilter.sharedMesh=reactorMesh;
   }
   ReactorBrightness=shutdown?.08f:Mathf.PingPong(time*reactorBlinkSpeed,1)>.5f?reactorPeak:.25f;
   for(int i=0;i<reactorColors.Length;i++)if(reactorMask[i]){reactorColors[i]=reactorOriginal[i]*ReactorBrightness;reactorColors[i].a=reactorOriginal[i].a;}
   reactorMesh.colors=reactorColors;
  }
  void RestoreReactor()
  {
   if(reactorMesh!=null){
    if(reactorFilter!=null&&reactorFilter.sharedMesh==reactorMesh)reactorFilter.sharedMesh=reactorSource;
    if(Application.isPlaying)Destroy(reactorMesh);else DestroyImmediate(reactorMesh);
   }
   reactorMesh=null;reactorSource=null;ReactorBrightness=0;
  }
  void OnDestroy(){RestoreReactor();}

  public const float DeathPoseSeconds=3.6f;
  static readonly Quaternion Space=Quaternion.Euler(90,0,0);
  Vector3 rest,restScale;float sentinelHeight=1;bool prepared,ultimate,beam;
  public bool ShoulderOpen=>ultimate;
  Transform[] deathParts;Quaternion[] deathRotations;Vector3 deathPosition;
  public override bool Initialize(){if(!base.Initialize())return false;if(!prepared){prepared=true;rest=rigRoot.localPosition;restScale=rigRoot.localScale;sentinelHeight=AnimatedMesh.bounds.size.z;}return true;}
  protected override Transform[] GetPoseTransforms(){var list=new List<Transform>(base.GetPoseTransforms());foreach(var t in mechanisms)if(t!=null)list.Add(t);return list.ToArray();}
  public override void ResetMotion(){RestoreReactor();ultimate=beam=false;base.ResetMotion();if(prepared)rigRoot.localScale=restScale;foreach(var t in mechanisms)if(t!=null)t.localRotation=Quaternion.identity;}
  static Quaternion Rot(float x,float y=0,float z=0)=>Space*Quaternion.Euler(x,y,z)*Quaternion.Inverse(Space);
  public void SetArsenal(bool active,bool firing){ultimate=active;beam=firing;}
  public Vector3 WeaponOrigin(int side)
  {
   var skin=BossVoxelSkin.ActiveOn(transform);
   if(skin!=null)return skin.Tip(side==0?"chest":side<0?"pod_L":"pod_R",side==0?skin.transform.forward:Vector3.up); // core laser from the chest, missiles from the shoulder pods
   if(!Initialize())return transform.position;
   int socket=side==0?0:side<0?1:2;if(weaponSockets!=null&&weaponSockets.Length>socket&&weaponSockets[socket]!=null)return weaponSockets[socket].position;
   // Coordinates relative to the complete sculpt bounds, safe during zero-scale portals.
   var b=AnimatedMesh.bounds;
   var offset=side==0?new Vector3(0,.735f,-.115f):new Vector3(side*.17f,.84f,0);
   return transform.TransformPoint(new Vector3(b.center.x,b.center.y,b.min.z)+Space*(offset*sentinelHeight));
  }
  public override void SamplePose(float time,float charge,float release,float stun)
  {
   base.SamplePose(time,charge,release,stun);if(!Initialize())return;SampleReactor(time);
   float move=Mathf.Abs(Locomotion)*(1-Mathf.Max(charge,release))*(ultimate?0:1)*(1-stun);
   float step=Mathf.Sin(time*4.6f),stride=step*move;
   parts[4].localRotation=Rot(stride*25);parts[5].localRotation=Rot(-stride*25);
   mechanisms[2].localRotation=Rot(-Mathf.Max(0,-step)*move*36);
   mechanisms[3].localRotation=Rot(-Mathf.Max(0,step)*move*36);
   parts[1].localRotation*=Rot(-stride*17);parts[2].localRotation*=Rot(stride*17);
   mechanisms[0].localRotation=Rot(10+charge*38-release*10);
   mechanisms[1].localRotation=Rot(10+charge*38-release*10);
   float open=ultimate&&stun<=0?1:0;
   mechanisms[4].localRotation=Rot(0,0,open*108);
   mechanisms[5].localRotation=Rot(0,0,-open*108);
   if(ultimate&&stun<=0){
    rigRoot.localPosition=rest+Space*new Vector3(0,-.025f,0)*sentinelHeight;
    rigRoot.localRotation=Rot(beam?-7:3);
    parts[1].localRotation=Rot(15,0,-18);parts[2].localRotation=Rot(15,0,18);
    parts[4].localRotation=Rot(12,0,-5);parts[5].localRotation=Rot(-8,0,5);
    mechanisms[2].localRotation=Rot(-15);mechanisms[3].localRotation=Rot(-12);
    mechanisms[0].localRotation=Rot(28);mechanisms[1].localRotation=Rot(28);
   }
  }
  public void BeginDeathPose(){Initialize();SampleReactor(0,true);Defeat();ultimate=beam=false;deathParts=GetPoseTransforms();deathRotations=new Quaternion[deathParts.Length];for(int i=0;i<deathParts.Length;i++)deathRotations[i]=deathParts[i].localRotation;deathPosition=rigRoot.localPosition;}
  public void SampleDeathPose(float age)
  {
   if(deathParts==null)return;float kneel=Mathf.SmoothStep(0,1,age/1.8f),fall=Mathf.SmoothStep(0,1,(age-1.7f)/1.9f);
   for(int i=0;i<deathParts.Length;i++)deathParts[i].localRotation=Quaternion.Slerp(deathRotations[i],Quaternion.identity,kneel);
   rigRoot.localPosition=Vector3.Lerp(deathPosition,rest+Space*new Vector3(0,-.24f,-.12f)*sentinelHeight,kneel);
   rigRoot.localRotation=Rot(18*kneel+52*fall,0,8*fall);
   parts[4].localRotation=Rot(66*kneel);parts[5].localRotation=Rot(35*kneel);
   mechanisms[2].localRotation=Rot(-100*kneel);mechanisms[3].localRotation=Rot(-82*kneel);
   parts[1].localRotation=Rot(36*kneel);parts[2].localRotation=Rot(28*kneel);
   parts[3].localRotation=Rot(27*kneel);
  }
  public void SampleDeathDissolve(float progress){if(prepared)rigRoot.localScale=restScale*(1-Mathf.SmoothStep(0,1,progress));}
 }
}