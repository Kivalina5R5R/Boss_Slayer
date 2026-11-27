using UnityEngine;
using UnityEngine.UI;
namespace BossSlayer
{
    public sealed partial class BossSlayerGameManager
    {
        bool golemRedPhase,golemPhasePending;
        float golemTransformation=-1;
        int golemTransformCast;
        const float GolemTransformSeconds=2.6f;
        void ResetGolemPhase(){golemRedPhase=golemPhasePending=false;golemTransformation=-1;}
        void ObserveGolemHealth()
        {
            if(bossModel==null||!bossModel.TryGetComponent<BossGolemAnimation>(out var golem))return;
            float hp=bossHp/(float)Mathf.Max(1,bossMaxHp);
            golem.ObserveHealth(hp);
            if(hp<=.5f&&bossHp>0&&!golemRedPhase)golemPhasePending=true;
        }
        bool AdvanceGolemTransformation(float dt)
        {
            if(!golemPhasePending||bossHp<=0||bossModel==null||!bossModel.TryGetComponent<BossGolemAnimation>(out var golem))return false;
            if(dt<=0||Time.timeScale<=0)return golemTransformation>=0;
            if(stunTimer>0||ultimateActive||attacks.Count>0)return false;
            if(golemTransformation<0)
            {
                golemTransformation=0;golem.SetLocomotion(0);golem.SetAttackVariant(4);
                golemTransformCast=golem.BeginCast(GolemTransformSeconds);sfx?.Score?.Roar();
                SkinTransform(GolemTransformSeconds,1.75f,"crimson");passiveFeedback?.Notify("CRIMSON CRYSTAL AWAKENING");
                ReplayBossTransform();
            }
            golemTransformation=Mathf.Min(GolemTransformSeconds,golemTransformation+dt);
            golem.SetTransformation(golemTransformation/GolemTransformSeconds);
            if(golemTransformation>=GolemTransformSeconds)
            {
                golemRedPhase=true;golemPhasePending=false;golemTransformation=-1;
                golem.ReleaseAttack(golemTransformCast);attackTimer=.35f;
                passiveFeedback?.Notify("PHASE 2: CRIMSON REFRACTION");
            }
            return true;
        }
        void SpawnGolemBeams(string title)
        {
            // Two interleaved five-lane volleys: the second lands halfway between the first.
            for(int wave=0;wave<2;wave++)for(int i=0;i<5;i++)
            {
                float lane=(i*2+wave+.5f)/10f;
                Vector2 start=new Vector2(arenaWidthMeters*lane,0);
                Vector2 end=new Vector2(arenaWidthMeters*lane,arenaDepthMeters);
                float delay=wave*4.4f,warning=SignatureWarning,/* บาส 2026-10-06: was 1.4, B2 floor 1.5 */width=Mathf.Min(arenaWidthMeters,arenaDepthMeters)*.085f;
                var a=WorldToLocal(start);var b=WorldToLocal(end);var delta=b-a;
                var view=CreateRect("ULTIMATE "+title,(a+b)*.5f,new Vector2(width*PixelsPerMeter(),delta.magnitude),new Color(1,.05f,.12f,.35f));
                view.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg-90);
                var attack=new AttackState(Shape.Beam,view,warning+delay,2.4f)
                {
                    BeamStart=start,BeamEnd=end,BeamIndex=i,Width=width,WarningDelay=delay,TelegraphTotal=warning,
                    Lingering=true,AnimationVariant=4,IsBossUltimate=true,
                    WarningRing=CreateSquareFrame("Crystal beam warning",view,new Color(1,.1f,.22f,1))
                };
                ConfigureFloorEffect(attack,title);attack.FloorForm=BossAttackVfx.AttackForm.Beam;
                view.gameObject.SetActive(delay<=0);attacks.Add(attack);
            }
        }
        static bool InGolemBeam(Vector2 point,Vector2 start,Vector2 end,float width)
        {
            var delta=end-start;float t=Vector2.Dot(point-start,delta)/Mathf.Max(.0001f,delta.sqrMagnitude);
            return t>=0&&t<=1&&Vector2.Distance(point,start+delta*t)<width*.5f;
        }
        void UpdateGolemAttack(AttackState attack)
        {
            if(bossModel==null||!bossModel.TryGetComponent<BossGolemAnimation>(out var golem))return;
            if(attack.FloorCrystals==null)
            {
                var go=new GameObject("Crystal ground fractures",typeof(RectTransform),typeof(CanvasRenderer),typeof(BossCrystalFloorGraphic));
                go.transform.SetParent(attack.View,false);
                var rect=go.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                attack.FloorCrystals=go.GetComponent<BossCrystalFloorGraphic>();attack.FloorCrystals.raycastTarget=false;
            }
            attack.FloorCrystals.Sample(Mathf.Max(0,-attack.TelegraphTimer),attack.TelegraphTimer<=0,attack.ActiveTimer,golemRedPhase,attack.Shape==Shape.Beam);
            if(golemRedPhase&&attack.ElementalVfx!=null)attack.ElementalVfx.SetElement(new Color(1,.035f,.09f));
            if(attack.Shape!=Shape.Beam)return;
            bool warning=attack.TelegraphTimer>0 && attack.WarningDelay<=0;
            attack.ViewImage.enabled=warning;
            if(warning)
            {
                attack.ViewImage.material=null;
                attack.ViewImage.color=new Color(1,.38f,.025f,.28f+.12f*(.5f+.5f*Mathf.Sin(attack.TelegraphTimer*14)));
            }
            if(attack.ElementalVfx!=null)attack.ElementalVfx.SetElement(warning?new Color(1,.48f,.025f):new Color(1,.035f,.09f));
            UpdateAttackWarningRing(attack);
            var camera=bossDisplayCamera!=null?bossDisplayCamera:wallDisplayCamera;if(camera==null||floorDisplayCamera==null)return;
            if(attack.BeamVfx==null)
            {
                var go=new GameObject("VFX == Crystal beam volume");go.transform.SetParent(transform,false);
                attack.BeamVfx=go.AddComponent<BossCrystalBeamVfx>();attack.BeamVfx.Initialize(bossModel.gameObject.layer);
                var floor=new GameObject("VFX == Floor crystal beam volume");floor.transform.SetParent(transform,false);
                attack.FloorBeamVfx=floor.AddComponent<BossCrystalBeamVfx>();
                attack.FloorBeamVfx.Initialize(Visual3DFollower.FloorLayer,attack.View.GetComponentInParent<Canvas>().rootCanvas.sortingOrder+2);
            }
            Vector3 origin=golem.CrystalOrigin(attack.BeamIndex);
            bool firing=attack.TelegraphTimer<=0&&attack.ActiveTimer>0;
            if(firing)golem.SetBeamBraced(true);
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
    }
}
