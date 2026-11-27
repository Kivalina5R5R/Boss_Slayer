using UnityEngine;
namespace BossSlayer
{
 public sealed partial class BossSlayerGameManager
 {
  void SpawnSentinelArsenal(string title)
  {
   for(int volley=0;volley<3;volley++)for(int shot=0;shot<4;shot++)
   {
    // บาส 2026-10-06 (B2): warning 1.25 -> 1.5; volley gap keeps its 0.4 s of air (1.65 -> 1.9) so volleys don't overlap.
    float warning=SignatureWarning,delay=volley*(warning+.4f)+shot*.10f;
    float x=(shot+.5f)/4f,z=volley%2==0?(shot%2==0?.3f:.7f):(shot%2==0?.7f:.3f);
    AddCircle("ULTIMATE "+title,x*arenaWidthMeters,z*arenaDepthMeters,Mathf.Min(arenaWidthMeters,arenaDepthMeters)*.115f,delay+warning);
    var attack=attacks[attacks.Count-1];attack.SentinelMissile=true;attack.BeamIndex=shot%2==0?-1:1;
    attack.WarningDelay=delay;attack.TelegraphTotal=warning;attack.ActiveTimer=delay+warning+.35f;attack.AnimationVariant=4;
    attack.View.gameObject.SetActive(delay<=0);
   }
   // Laser still starts 0.35 s after the last volley lands (5.2 s with the old 1.25/1.65 timing).
   float wait=5.2f+2*(SignatureWarning+.4f-1.65f)+(SignatureWarning-1.25f),charge=1.6f,width=arenaWidthMeters*.19f;
   var start=new Vector2(arenaWidthMeters*.5f,0);var end=new Vector2(start.x,arenaDepthMeters);
   var a=WorldToLocal(start);var b=WorldToLocal(end);
   var view=CreateRect("ULTIMATE GOLDEN CORE LASER",(a+b)*.5f,new Vector2(width*PixelsPerMeter(),(b-a).magnitude),new Color(1,.62f,.06f,.4f));
   view.localRotation=Quaternion.Euler(0,0,Mathf.Atan2((b-a).y,(b-a).x)*Mathf.Rad2Deg-90);
   var beam=new AttackState(Shape.Beam,view,wait+charge,2.4f){BeamStart=start,BeamEnd=end,Width=width,WarningDelay=wait,TelegraphTotal=charge,Lingering=true,AnimationVariant=4,SentinelLaser=true,WarningRing=CreateSquareFrame("Golden laser warning",view,new Color(1,.8f,.1f))};
   ConfigureFloorEffect(beam,title);beam.FloorForm=BossAttackVfx.AttackForm.Beam;view.gameObject.SetActive(false);attacks.Add(beam);
  }
  void SampleSentinelLaser(AttackState attack,BossSentinelAnimation sentinel,Camera camera,bool warning)
  {
            if(attack.BeamVfx==null)
            {
                var go=new GameObject("VFX == Golden core beam volume");go.transform.SetParent(transform,false);
                attack.BeamVfx=go.AddComponent<BossCrystalBeamVfx>();attack.BeamVfx.Initialize(bossModel.gameObject.layer);
                var floor=new GameObject("VFX == Floor crystal beam volume");floor.transform.SetParent(transform,false);
                attack.FloorBeamVfx=floor.AddComponent<BossCrystalBeamVfx>();
                attack.FloorBeamVfx.Initialize(Visual3DFollower.FloorLayer,attack.View.GetComponentInParent<Canvas>().rootCanvas.sortingOrder+2);
            }
            Vector3 origin=sentinel.WeaponOrigin(0);
            bool firing=attack.TelegraphTimer<=0&&attack.ActiveTimer>0;
            attack.BeamVfx.SetGolden();attack.FloorBeamVfx.SetGolden();
            var beamRect=attack.View.rect;
            float diameter=attack.View.TransformVector(Vector3.right*beamRect.width).magnitude;
            Vector3 offset=-floorDisplayCamera.transform.forward*diameter*.65f;
            var floorAnchor=attack.View.TransformPoint(new Vector3(0,beamRect.yMin,0))+offset;
            var floorViewport=floorDisplayCamera.WorldToViewportPoint(floorAnchor);
            float seamX=floorViewport.x;
            // Normalized X is the shared physical seam across displays of different resolutions.
            float wallDepth=camera.WorldToViewportPoint(origin).z;
            var seam=camera.ViewportToWorldPoint(new Vector3(seamX,0,wallDepth));
            var wallDirection=seam-origin;
            float floorWidth=Mathf.Abs(floorDisplayCamera.WorldToViewportPoint(floorAnchor+floorDisplayCamera.transform.right*diameter).x-seamX);
            float wallWidth=Vector3.Distance(camera.ViewportToWorldPoint(new Vector3(0,0,wallDepth)),camera.ViewportToWorldPoint(new Vector3(floorWidth,0,wallDepth)));
            // Match the horizontal footprint where the diagonal wall beam crosses the seam.
            wallWidth*=Mathf.Max(.05f,Mathf.Abs(Vector3.Dot(wallDirection.normalized,camera.transform.up)));
            attack.BeamVfx.Sample(origin,origin+wallDirection*1.08f,wallWidth,
                attack.TelegraphTimer,attack.TelegraphTotal,attack.ActiveTimer);
            // End caps and shader taper sit outside the display so no gap appears at either edge.
            var start=floorDisplayCamera.ViewportToWorldPoint(new Vector3(seamX,1.07f,floorViewport.z));
            var end=floorDisplayCamera.ViewportToWorldPoint(new Vector3(seamX,-.07f,floorViewport.z));
            attack.FloorBeamVfx.gameObject.SetActive(!warning);
            attack.FloorBeamVfx.Sample(start,end,diameter,attack.TelegraphTimer,attack.TelegraphTotal,attack.ActiveTimer);
  }
  void UpdateSentinelArsenal()
  {
   if(bossModel==null||!bossModel.TryGetComponent<BossSentinelAnimation>(out var sentinel))return;
   bool active=false,firing=false;
   foreach(var attack in attacks){active|=attack.SentinelMissile||attack.SentinelLaser;firing|=attack.SentinelLaser&&attack.TelegraphTimer<=0&&attack.ActiveTimer>0;}
   sentinel.SetArsenal(active,firing);
  }
  void UpdateSentinelAttack(AttackState attack)
  {
   if(bossModel==null||!bossModel.TryGetComponent<BossSentinelAnimation>(out var sentinel))return;
   if(!attack.SentinelMissile&&!attack.SentinelLaser)return;
   var camera=bossDisplayCamera!=null?bossDisplayCamera:wallDisplayCamera;if(camera==null||floorDisplayCamera==null)return;
   if(attack.ElementalVfx!=null)attack.ElementalVfx.SetElement(new Color(1,.65f,.06f));
   if(attack.SentinelMissile)
   {
    if(attack.MissileVfx==null){
     var go=new GameObject("VFX == Shoulder missile");go.transform.SetParent(transform,false);
     attack.MissileVfx=go.AddComponent<BossSentinelMissileVfx>();attack.MissileVfx.Initialize(bossModel.gameObject.layer);
     var floor=new GameObject("VFX == Descending missile");floor.transform.SetParent(transform,false);
     attack.FloorMissileVfx=floor.AddComponent<BossSentinelMissileVfx>();attack.FloorMissileVfx.Initialize(Visual3DFollower.FloorLayer);
    }
    var origin=sentinel.WeaponOrigin(attack.BeamIndex);
    var target=attack.View.position;var fp=floorDisplayCamera.WorldToViewportPoint(target);
    var seam=camera.ViewportToWorldPoint(new Vector3(fp.x,0,camera.WorldToViewportPoint(origin).z));
    float progress=(.85f-attack.TelegraphTimer)/.85f;
    float diameter=Vector3.Distance(origin,seam)*.035f;
    attack.MissileVfx.Sample(origin,seam,camera.transform.up,diameter,progress/.55f);
    var floorStart=floorDisplayCamera.ViewportToWorldPoint(new Vector3(fp.x,1.04f,fp.z));
    float floorSize=attack.View.TransformVector(Vector3.right*attack.View.rect.width).magnitude*.075f;
    var offset=-floorDisplayCamera.transform.forward*floorSize*2;
    attack.FloorMissileVfx.Sample(floorStart+offset,target+offset,floorDisplayCamera.transform.up,floorSize,(progress-.55f)/.45f);
    return;
   }
   bool warning=attack.TelegraphTimer>0;attack.ViewImage.enabled=warning;
   if(warning){attack.ViewImage.material=null;attack.ViewImage.color=new Color(1,.65f,.02f,.35f);}
   UpdateAttackWarningRing(attack);
   // Shared mesh beam implementation retains matching wall/floor seam and full floor length.
   SampleSentinelLaser(attack,sentinel,camera,warning);
  }
 }
}