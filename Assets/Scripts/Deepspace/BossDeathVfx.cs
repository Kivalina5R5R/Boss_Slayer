using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deepspace
{
    public sealed class BossDeathVfx : MonoBehaviour
    {
        public const float Duration=3.1f;
        public const int VoxelCount=2560;
        const int Count=VoxelCount,BodyCount=1600,RingCount=576;
        readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[Count];
        readonly Vector3[] origins=new Vector3[Count];
        readonly Color[] colors=new Color[Count];
        readonly List<Renderer> body=new List<Renderer>();
        readonly List<bool> visibility=new List<bool>();
        ParticleSystem system;Mesh mesh;Material material;
        Vector3 center;float radius;bool hidden;
        public float Age {get;private set;}
        public bool ManualPlayback {get;set;}
        public bool IsPlaying {get;private set;}
        public int Theme {get;private set;}
        public int ParticleCount=>system!=null?system.particleCount:0;
        public bool Play(Transform boss,Renderer[] renderers,bool driftOnly=false)
        {
            Stop();if(boss==null||renderers==null)return false;
            var filters=new List<MeshFilter>();
            Bounds bounds=default;bool first=true;
            foreach(var r in renderers)
            {
                if(r==null||!r.enabled||!r.gameObject.activeInHierarchy||r is ParticleSystemRenderer)continue;
                var filter=r.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                filters.Add(filter);body.Add(r);visibility.Add(r.enabled);
                if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);
            }
            if(filters.Count==0)return false;
            if(system==null)
            {
                var shader=Resources.Load<Shader>("BossVoxelEnergy");if(shader==null||!shader.isSupported)return false;
                material=new Material(shader){name="Boss death elemental voxels"};mesh=BossModelElementVfx.CreateVoxel();
                var go=new GameObject("VFX == Boss elemental disintegration",typeof(ParticleSystem));go.layer=Visual3DFollower.BossLayer;go.transform.SetParent(transform,false);
                system=go.GetComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                system.useAutoRandomSeed=false;system.randomSeed=31;
                var main=system.main;main.playOnAwake=false;main.maxParticles=Count;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=0;main.startSize3D=true;main.startRotation3D=true;
                var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
                var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=mesh;renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,ParticleSystemVertexStream.Color});
            }
            Theme=BossAttackVfx.ThemeFor(boss.name);center=bounds.center;radius=Mathf.Max(.01f,bounds.extents.magnitude);
            // Sample the actual animated meshes in world space, so the burst starts on the body.
            for(int f=0;f<filters.Count;f++)
            {
                var filter=filters[f];var vertices=filter.sharedMesh.vertices;var tint=filter.sharedMesh.colors;
                for(int i=f;i<Count;i+=filters.Count)
                {
                    int index=(int)(Mathf.Repeat(i*.618034f,1)*vertices.Length)%vertices.Length;
                    origins[i]=filter.transform.TransformPoint(vertices[index]);
                    colors[i]=tint.Length==vertices.Length?tint[index]:BossAttackVfx.ElementColor(Theme);
                }
            }
            Age=0;hidden=false;IsPlaying=true;system.Play();Tick(0);return true;
        }
        void Update(){if(IsPlaying&&!ManualPlayback)Tick(Age+Time.deltaTime);}
        public void Tick(float age)
        {
            if(!IsPlaying)return;Age=Mathf.Max(0,age);
            const float detonate=.45f;
            if(age>=detonate&&!hidden){foreach(var renderer in body)if(renderer!=null)renderer.enabled=false;hidden=true;}
            float burst=Mathf.Max(0,age-detonate),progress=Mathf.Clamp01(burst/(Duration-detonate));
            float gather=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/detonate));
            Color element=BossAttackVfx.ElementColor(Theme);
            for(int i=0;i<Count;i++)
            {
                float seed=Mathf.Repeat(i*.618034f,1),second=Mathf.Repeat(i*.414214f+.1f,1);
                Vector3 position,stretch=Vector3.one;float size,fade=1-Mathf.SmoothStep(.45f,1,progress);
                Color tint=Color.Lerp(colors[i],element,Mathf.Clamp01(age*.8f));
                if(i<BodyCount)
                {
                    var radial=(origins[i]-center).normalized;if(radial.sqrMagnitude<.01f)radial=Vector3.up;
                    float launch=1-Mathf.Exp(-burst*4);
                    var offset=radial*radius*(launch*(.32f+seed*.9f)+burst*.09f);
                    if(Theme==7)offset=Quaternion.AngleAxis(burst*100,Vector3.up)*offset+Vector3.up*(radius*burst*.13f);
                    else if(Theme==2)offset+=Vector3.up*Mathf.Sin(burst*12+i)*radius*.07f*launch;
                    else offset.y+=radius*(burst*(Theme==6?.48f:.32f)-burst*burst*(Theme==4?.26f:.14f));
                    position=Vector3.Lerp(origins[i],center,.14f*gather*(1-launch))+offset;
                    size=radius*(.025f+second*.035f)*fade;
                    if(i%11==0)size*=1.65f;
                    tint=Color.Lerp(tint,Color.white,Mathf.Exp(-burst*6)*gather*.7f);
                }
                else if(i<BodyCount+RingCount)
                {
                    int ring=(i-BodyCount)/192,index=(i-BodyCount)%192;
                    float angle=index*Mathf.PI*2/192+ring*.18f;
                    float expand=radius*(.18f+Mathf.Pow(Mathf.Clamp01(burst/1.2f),.7f)*(1.45f+ring*.23f));
                    var circle=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                    circle=Quaternion.Euler(ring==1?65:0,ring==2?55:0,ring*18)*circle;
                    position=center+circle*expand;
                    fade=burst<=0?0:Mathf.Clamp01(1-burst/1.5f);
                    size=radius*(.022f+seed*.012f)*fade;stretch=new Vector3(1,1,1);
                    tint=Color.Lerp(element,Color.white,.6f);
                }
                else
                {
                    float angle=seed*Mathf.PI*2,up=.2f+second*.8f;
                    var direction=new Vector3(Mathf.Cos(angle),up,Mathf.Sin(angle)*.6f).normalized;
                    float trail=1-Mathf.Exp(-burst*2.3f);
                    position=center+direction*radius*(trail*(.7f+second*.8f)+burst*.12f)+Vector3.up*radius*(burst*.22f-burst*burst*.1f);
                    fade=burst<=0?0:Mathf.Pow(1-progress,1.5f);
                    size=radius*(.013f+second*.018f)*fade;stretch=new Vector3(1,2+seed*3,1);
                    tint=Color.Lerp(element,new Color(1,.9f,.5f),.4f+seed*.3f);
                }
                var p=new ParticleSystem.Particle();p.position=position;p.startLifetime=p.remainingLifetime=10;
                p.startSize3D=stretch*size;p.rotation3D=new Vector3(i*31+burst*(80+seed*100),i*17+burst*120,i*11+burst*70);
                tint.a=fade;p.startColor=tint;particles[i]=p;
            }
            system.SetParticles(particles,Count);
            if(age>=Duration){system.Clear();IsPlaying=false;}
        }
        public void Stop()
        {
            IsPlaying=false;if(system!=null)system.Clear();
            for(int i=0;i<body.Count;i++)if(body[i]!=null)body[i].enabled=visibility[i];
            body.Clear();visibility.Clear();hidden=false;
        }
        void OnDisable(){Stop();}
        void OnDestroy(){Stop();if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
