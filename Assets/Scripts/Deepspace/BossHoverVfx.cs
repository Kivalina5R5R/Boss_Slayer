using UnityEngine;
using UnityEngine.Rendering;
namespace Deepspace
{
    // Theme-specific airborne support. The emitter is outside the model so framing bounds never include its plume.
    public sealed class BossHoverVfx : MonoBehaviour
    {
        const int Capacity=96;
        readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[Capacity];
        ParticleSystem system; Material material; Mesh voxel; Camera view;
        BossModelAnimation motion; MeshRenderer[] hull; Transform[] feet=new Transform[2];
        MeshRenderer[][] footSurfaces=new MeshRenderer[2][];
        float clock;
        public int ParticleCount=>system!=null?system.particleCount:0;
        public static void Install(Transform model,Camera camera)
        {
            if(BossVoxelSkin.ActiveOn(model)!=null)
            {
                // The new models make their own feet effects (BossVoxelSkin.Fx); this one measured the hidden old body.
                var existing=model.GetComponent<BossHoverVfx>();
                if(existing!=null){existing.enabled=false;if(existing.system!=null)existing.system.gameObject.SetActive(false);}
                return;
            }
            var pose=model.GetComponent<BossModelAnimation>();if(pose==null)return;
            if(pose.character!=BossModelAnimation.Kind.Demon && pose.character!=BossModelAnimation.Kind.Sentinel && pose.character!=BossModelAnimation.Kind.Golem && pose.character!=BossModelAnimation.Kind.Dragon)return;
            var effect=model.GetComponent<BossHoverVfx>()??model.gameObject.AddComponent<BossHoverVfx>();
            effect.view=camera;effect.motion=pose;
        }
        bool Build()
        {
            var shader=Resources.Load<Shader>("BossVoxelEnergy");if(shader==null||view==null)return false;
            hull=GetComponentsInChildren<MeshRenderer>(true);
            if(motion is BossDemonAnimation demon){feet[0]=demon.joints[12];feet[1]=demon.joints[13];}
            else if(motion is BossSentinelAnimation sentinel){feet[0]=sentinel.parts[4];feet[1]=sentinel.parts[5];}
            else if(motion is BossDragonAnimation dragon){feet[0]=dragon.parts[1];feet[1]=dragon.parts[2];}
            for(int i=0;i<2;i++)if(feet[i]!=null)footSurfaces[i]=feet[i].GetComponentsInChildren<MeshRenderer>(true);
            var go=new GameObject("VFX == "+motion.character+" airborne support"){layer=gameObject.layer,hideFlags=HideFlags.DontSave};
            go.transform.SetParent(transform.parent,false);
            system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.playOnAwake=false;main.maxParticles=Capacity;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSize3D=true;main.startRotation3D=true;
            var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
            material=new Material(shader){name="Boss hover energy",hideFlags=HideFlags.DontSave};voxel=BossModelElementVfx.CreateVoxel();
            if(motion.character!=BossModelAnimation.Kind.Golem){material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",10);material.SetFloat("_ZWrite",0);material.SetFloat("_Alpha",motion.character==BossModelAnimation.Kind.Dragon?.42f:.8f);material.renderQueue=3000;}
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=voxel;renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            system.Pause();return true;
        }
        void LateUpdate(){Advance(Time.deltaTime);}
        public void Advance(float dt)
        {
            if(motion==null||view==null)return;
            if(motion.IsDefeated){Clear();return;}
            if(dt<=0)return;
            if(system==null&&!Build())return;
            clock+=dt;
            bool found=false;var bounds=new Bounds(transform.position,Vector3.zero);
            foreach(var r in hull)if(r!=null&&r.enabled&&r.gameObject.activeInHierarchy){if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);}
            if(!found){Clear();return;}
            float size=Mathf.Max(.001f,bounds.size.magnitude);
            Vector3 up=transform.TransformDirection(Quaternion.Euler(90,0,0)*Vector3.up).normalized;
            Vector3 right=Vector3.ProjectOnPlane(view.transform.right,up).normalized;
            if(right.sqrMagnitude<.01f)right=transform.right;
            Vector3 forward=Vector3.Cross(right,up).normalized;
            float extent=Vector3.Dot(new Vector3(Mathf.Abs(up.x),Mathf.Abs(up.y),Mathf.Abs(up.z)),bounds.extents);
            var kind=motion.character;bool jet=kind==BossModelAnimation.Kind.Sentinel, stone=kind==BossModelAnimation.Kind.Golem, wind=kind==BossModelAnimation.Kind.Dragon;
            float power=motion.IsStunned?.65f:1f;
            for(int i=0;i<Capacity;i++)
            {
                int side=i%2;float seed=Mathf.Repeat(i*.618034f,1),life=Mathf.Repeat(clock*(jet?1.9f:wind?1.25f:.8f)+seed,1),angle=i*2.39996f+clock*(stone?3:1.7f);
                Vector3 origin=bounds.center-up*extent+right*(side==0?-1:1)*size*(wind?.21f:.12f);
                if(feet[side]!=null){origin=feet[side].position;var surfaces=footSurfaces[side];bool footFound=false;var b=new Bounds(origin,Vector3.zero);
                    foreach(var foot in surfaces)if(foot!=null&&foot.enabled&&foot.gameObject.activeInHierarchy){if(!footFound){b=foot.bounds;footFound=true;}else b.Encapsulate(foot.bounds);}
                    if(footFound){float e=Vector3.Dot(new Vector3(Mathf.Abs(up.x),Mathf.Abs(up.y),Mathf.Abs(up.z)),b.extents);origin=b.center-up*e;}}
                if(wind && motion is BossDragonAnimation dragon && dragon.TryGetWingTip(side,out var tip))origin=tip;
                var p=particles[i]; Color color; float particleSize;
                if(stone)
                {
                    // A single mantle of heavy rubble close to the lower body, never paired exhausts.
                    bool crystal=i%8==0;
                    bool red=motion is BossGolemAnimation golem && golem.RedPhase;
                    float orbit=size*(.105f+seed*.055f);
                    p.position=bounds.center-up*extent*.72f+(right*Mathf.Cos(angle)+forward*Mathf.Sin(angle))*orbit
                        +up*Mathf.Sin(clock*1.6f+i*.7f)*size*.025f;
                    color=crystal?(red?new Color(1f,.25f,.06f):new Color(.16f,.75f,.82f)):Color.Lerp(new Color(.12f,.15f,.18f),new Color(.36f,.39f,.41f),seed);
                    particleSize=size*(crystal?.012f:.018f+seed*.012f);
                    p.startSize3D=new Vector3(particleSize,particleSize*.7f,particleSize*1.15f);
                }
                else if(jet)
                {
                    float radius=size*(.008f+life*.012f);
                    p.position=origin-up*(.005f+life*.085f)*size+(right*Mathf.Cos(angle)+forward*Mathf.Sin(angle))*radius;
                    color=Color.Lerp(new Color(1f,.96f,.72f),new Color(.28f,.7f,1f),life);
                    particleSize=size*.013f*(1-life*.8f);p.startSize3D=new Vector3(particleSize,particleSize*3,particleSize);
                }
                else if(wind)
                {
                    // Short wing-tip eddies, rather than a column below the feet.
                    p.position=origin-up*life*size*.025f+right*Mathf.Sin(angle)*size*.035f+forward*life*size*.055f;
                    color=new Color(.64f,.82f,.9f);particleSize=size*.008f*Mathf.Sin(life*Mathf.PI);
                    p.startSize3D=new Vector3(particleSize*3,particleSize*.5f,particleSize);
                }
                else
                {
                    // Embers lick upward around the ankles and stay anchored to each foot.
                    float radius=size*(.012f+life*.018f);
                    p.position=origin+up*(life*.055f-.012f)*size+(right*Mathf.Cos(angle)+forward*Mathf.Sin(angle))*radius;
                    color=Color.Lerp(new Color(1f,.6f,.08f),new Color(.65f,.035f,.015f),life);
                    particleSize=size*.021f*(1-life*.85f);p.startSize3D=new Vector3(particleSize,particleSize*1.6f,particleSize);
                }
                color.a=power;p.startColor=color;
                p.rotation3D=jet?Quaternion.LookRotation(forward,up).eulerAngles:new Vector3(i*17+clock*(stone?12:35),i*29,clock*13);
                p.velocity=Vector3.zero;p.startLifetime=2;p.remainingLifetime=1;
                particles[i]=p;
            }
            system.SetParticles(particles,Capacity);
        }
        public void Clear(){if(system!=null)system.Clear();}
        void OnDisable(){Clear();clock=0;}
        void OnDestroy(){if(system!=null)Destroy(system.gameObject);if(material!=null)Destroy(material);if(voxel!=null)Destroy(voxel);}
    }
}
