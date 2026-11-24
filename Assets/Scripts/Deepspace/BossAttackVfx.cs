using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Deepspace
{
    public sealed class BossAttackVfx : MonoBehaviour
    {
        public enum AttackForm { Impact, Beam, Sweep, Wave, Barrage, Pool }
        RectTransform view;
        Material surface, sparkMaterial;
        ParticleSystem sparks;
        Camera floorCamera;
        Color element;
        bool circle, wave, fired, burning;
        int theme, propagation;
        float spread=1, lastBurst=-1;
        uint randomState;
        public bool IsReady => surface != null;
        public int Theme => theme;
        public AttackForm Form { get; private set; }
        public float SpreadProgress => spread;
        public Material Surface => surface;

        public static int ThemeFor(string boss)
        {
            if(string.IsNullOrEmpty(boss))return 0;
            if(boss.Contains("AbyssalKing"))return 7; // void floor shader / charge+release sounds, same family as the Void Wraith
            if(boss.Contains("Hydra"))return 8;
            if(boss.Contains("Giant Fish"))return 1;
            if(boss.Contains("JeelyFish")||boss.Contains("Jellyfish"))return 2;
            if(boss.Contains("Dragon"))return 3;
            if(boss.Contains("Crystal"))return 4;
            if(boss.Contains("SeaSerpent"))return 5;
            if(boss.Contains("Sentinel"))return 6;
            if(boss.Contains("VoidWraith"))return 7;
            return 0;
        }
        public static AttackForm FormFor(string title,bool circular,bool zone,bool movingWave)
        {
            if(movingWave)return AttackForm.Wave;
            if(zone)return AttackForm.Sweep;
            if(!circular)return AttackForm.Beam;
            string upper=(title??"").ToUpperInvariant();
            if(upper.Contains("VENOM"))return AttackForm.Pool;
            if(upper.Contains("BUBBLE")||upper.Contains("MORTAR")||upper.Contains("EMBER")||upper.Contains("BARRAGE")||upper.Contains("LIGHTNING")||upper.Contains("PHANTOM"))return AttackForm.Barrage;
            return AttackForm.Impact;
        }
        public static Color ElementColor(int value)
        {
            switch(value)
            {
                case 1:return new Color(.025f,.55f,1);
                case 2:return new Color(.3f,.65f,1);
                case 3:return new Color(1,.22f,.015f);
                case 4:return new Color(.27f,.84f,1);
                case 5:return new Color(.02f,.85f,.65f);
                case 6:return new Color(1,.65f,.04f);
                case 7:return new Color(.48f,.09f,.85f);
                case 8:return new Color(.4f,1,.025f);
                default:return new Color(1,.065f,.012f);
            }
        }
        public void Initialize(RectTransform rect,Camera camera,int elementTheme,Color color,bool circular,bool movingWave)
        {
            view=rect;floorCamera=camera;circle=circular;wave=movingWave;theme=elementTheme;
            element=ElementColor(theme);randomState=unchecked((uint)GetInstanceID())|1u;
            var shader=Resources.Load<Shader>("BossAttackSurface");if(shader==null)return;
            surface=new Material(shader){name="Boss floor element "+theme};
            surface.SetFloat("_Theme",theme);surface.SetFloat("_Circle",circle?1:0);
            surface.SetColor("_ColorA",element);surface.SetColor("_ColorB",Color.Lerp(element,Color.white,.7f));
            var size=view.rect.size;float unit=Mathf.Max(1,Mathf.Min(size.x,size.y));
            surface.SetVector("_Size",new Vector4(size.x/unit,size.y/unit,0,0));
            var image=view.GetComponent<Image>();image.material=surface;image.color=Color.white;image.raycastTarget=false;
            Configure(movingWave?AttackForm.Wave:circular?AttackForm.Impact:AttackForm.Beam,0,1,true,1);
            var particleShader=Resources.Load<Shader>("BossAttackSpark");if(particleShader==null||camera==null)return;
            sparkMaterial=new Material(particleShader){name="Boss floor particles "+theme};sparkMaterial.SetFloat("_Theme",theme);
            var go=new GameObject("VFX == Floor element particles",typeof(ParticleSystem));go.layer=Visual3DFollower.FloorLayer;go.transform.SetParent(view,false);
            sparks=go.GetComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=sparks.main;main.playOnAwake=false;main.loop=false;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            main.scalingMode=ParticleSystemScalingMode.Shape;main.startSpeed=0;main.maxParticles=96;
            var emission=sparks.emission;emission.enabled=false;var shape=sparks.shape;shape.enabled=false;
            var sizeLife=sparks.sizeOverLifetime;sizeLife.enabled=true;sizeLife.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
            var colorLife=sparks.colorOverLifetime;colorLife.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.8f,.5f),new GradientAlphaKey(0,1)});colorLife.color=gradient;
            var renderer=sparks.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=sparkMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        public void SetElement(Color value)
        {
            element=value;
            if(surface!=null){surface.SetColor("_ColorA",value);surface.SetColor("_ColorB",Color.Lerp(value,Color.white,.7f));}
        }
        public void Configure(AttackForm form,int spreadMode,float progress,bool horizontal=true,int direction=1)
        {
            Form=form;propagation=spreadMode;spread=Mathf.Clamp01(progress);
            if(surface==null)return;
            surface.SetFloat("_Form",(int)form);surface.SetFloat("_Propagation",propagation);surface.SetFloat("_Spread",spread);
            surface.SetFloat("_WaveAxis",horizontal?0:1);surface.SetFloat("_WaveDirection",direction);
        }
        public void SetScorch(bool afterImpact,float remaining)
        {
            burning=afterImpact&&remaining>0;
            if(surface!=null)surface.SetFloat("_Scorch",!afterImpact?0:burning?1:2);
        }
        public void Tick(float telegraphRemaining,float telegraphTotal,float activeRemaining)
        {
            if(surface==null)return;
            bool active=telegraphRemaining<=0;
            float age=Mathf.Max(0,-telegraphRemaining);
            surface.SetFloat("_Progress",1-Mathf.Clamp01(telegraphRemaining/Mathf.Max(.01f,telegraphTotal)));
            surface.SetFloat("_Phase",active?1+age:0);surface.SetFloat("_Age",age);
            surface.SetFloat("_Fade",activeRemaining<0?Mathf.Clamp01(1+activeRemaining/.45f):1);
            if(activeRemaining<=0){if(sparks!=null)sparks.Stop(true,ParticleSystemStopBehavior.StopEmitting);return;}
            if(active&&!fired){fired=true;Burst(propagation>0?10:wave?16:30);lastBurst=age;}
            if(active&&age-lastBurst>=.09f){Burst(burning||propagation>0?10:wave?6:4);lastBurst=age;}
        }
        float Random01(){randomState^=randomState<<13;randomState^=randomState>>17;randomState^=randomState<<5;return (randomState&0xffffff)/16777216f;}
        void Burst(int count)
        {
            if(sparks==null||floorCamera==null)return;sparks.Play();
            var rect=view.rect;
            float unit=Vector3.Distance(view.TransformPoint(Vector3.zero),view.TransformPoint(Vector3.right*Mathf.Min(rect.width,rect.height)));
            for(int i=0;i<count;i++)
            {
                var uv=new Vector2(.1f+Random01()*.8f,.1f+Random01()*.8f);
                if(propagation==1)uv=Vector2.one*.5f+(uv-Vector2.one*.5f)*spread;
                if(propagation==2)uv.y=1-(1-uv.y)*spread;
                if(circle&&(uv-Vector2.one*.5f).sqrMagnitude>.18f)continue;
                if(propagation>0&&spread<.06f)continue;
                Vector3 world=view.TransformPoint(new Vector3(Mathf.Lerp(rect.xMin,rect.xMax,uv.x),Mathf.Lerp(rect.yMin,rect.yMax,uv.y),0));
                float size=unit*(.035f+Random01()*.045f)* (propagation>0?Mathf.Min(1,spread*2):1);
                var emit=new ParticleSystem.EmitParams{
                    position=sparks.transform.InverseTransformPoint(world-floorCamera.transform.forward*unit*.012f),
                    velocity=Vector3.zero,startSize=size,startLifetime=.18f+Random01()*.28f,
                    startColor=theme==4&&i%3==0?new Color(.25f,.27f,.32f):Color.Lerp(element,Color.white,Random01()*(theme==0?.1f:.45f)),rotation=theme==3?0:Random01()*80-40};
                sparks.Emit(emit,1);
            }
        }
        void OnDestroy(){if(surface!=null)Destroy(surface);if(sparkMaterial!=null)Destroy(sparkMaterial);}
    }
}
