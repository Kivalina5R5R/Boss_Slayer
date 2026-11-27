using UnityEngine;
using UnityEngine.Rendering;
namespace BossSlayer
{
    public sealed partial class BossDemonAnimation
    {
        ParticleSystem rageFlames;
        Material rageMaterial;
        readonly ParticleSystem.Particle[] rageParticles=new ParticleSystem.Particle[112];
        bool rageSuppressed;
        public int RageParticleCount=>rageFlames==null?0:rageFlames.particleCount;
        void SampleRageFire(float time)
        {
            float heat=Transformation>=0?EaseD(.25f,.6f,Transformation):Adult?1:0;
            if(rageSuppressed || heat<=0){if(rageFlames!=null)rageFlames.Clear();return;}
            if(rageFlames==null)
            {
                var shader=GameArt.Load<Shader>("Art/Shaders/BossDemonRageFlame");if(shader==null)return;
                rageMaterial=new Material(shader){name="Demon crimson rage",hideFlags=HideFlags.DontSave};
                var go=new GameObject("Demon Rage Flames"){layer=gameObject.layer,hideFlags=HideFlags.DontSave};
                go.transform.SetParent(transform,false);
                rageFlames=go.AddComponent<ParticleSystem>();rageFlames.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=rageFlames.main;main.playOnAwake=false;main.maxParticles=rageParticles.Length;
                main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=0;main.startSize3D=true;
                var emission=rageFlames.emission;emission.enabled=false;var shape=rageFlames.shape;shape.enabled=false;
                var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial=rageMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                rageFlames.Pause();
            }
            rageMaterial.SetFloat("_Clock",time);
            float height=(AnimatedMesh!=null?AnimatedMesh.bounds.size.z:1)*transform.lossyScale.magnitude/Mathf.Sqrt(3);
            float flare=Transformation>=0?1+Mathf.Sin(EaseD(.3f,1,Transformation)*Mathf.PI)*.85f:1;
            for(int i=0;i<rageParticles.Length;i++)
            {
                int index=i%10;var joint=joints[index];var mesh=joint.GetComponent<MeshFilter>().sharedMesh;
                var bounds=mesh.bounds;
                float seed=Mathf.Repeat(i*.618034f,1),phase=Mathf.Repeat(time*(.72f+seed*.45f)+seed,1);
                float angle=i*2.39996f;
                var local=bounds.center+Vector3.Scale(bounds.extents,new Vector3(Mathf.Cos(angle)*.82f,Mathf.Sin(angle)*.82f,(seed-.5f)*1.3f));
                float fade=Mathf.Sin(phase*Mathf.PI);
                rageParticles[i]=new ParticleSystem.Particle {
                    position=joint.TransformPoint(local)+Vector3.up*(height*phase*.12f*flare),
                    startLifetime=10,remainingLifetime=10,
                    startSize3D=new Vector3(.034f,.13f,1)*height*fade*flare*(.65f+seed*.5f),
                    startColor=new Color(1,.03f,.06f,heat*fade*.82f)
                };
            }
            rageFlames.SetParticles(rageParticles,rageParticles.Length);
        }
        void SuppressRageFire(){rageSuppressed=true;if(rageFlames!=null)rageFlames.Clear();}
        void ReleaseRageFire()
        {
            if(rageFlames!=null){rageFlames.gameObject.SetActive(false);if(Application.isPlaying)Destroy(rageFlames.gameObject);else DestroyImmediate(rageFlames.gameObject);}
            if(rageMaterial!=null){if(Application.isPlaying)Destroy(rageMaterial);else DestroyImmediate(rageMaterial);}
            rageFlames=null;rageMaterial=null;rageSuppressed=false;
        }
    }
}

