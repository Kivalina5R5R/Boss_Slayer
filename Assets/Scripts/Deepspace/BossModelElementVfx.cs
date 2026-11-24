using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deepspace
{
    // Runs after the rigid-joint animation. No changes to boss materials or gameplay RNG.
    [DefaultExecutionOrder(500)]
    public sealed partial class BossModelElementVfx : MonoBehaviour
    {
        struct Cue { public Transform anchor; public int variant; public float charge, release; }
        readonly List<Cue> cues = new List<Cue>(24);
        readonly Dictionary<Transform, Vector3> sockets = new Dictionary<Transform, Vector3>();
        readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[1536];
        ParticleSystem system;
        Material material;
        Mesh voxelMesh;
        Transform boss;
        Camera viewCamera;
        BossModelAnimation motion;
        BossJellyfishAnimation jelly;
        BossHydra hydra;
        float size;
        Renderer[] bodyRenderers;
        public int Theme { get; private set; }
        public int ActiveSockets => cues.Count;
        public int ParticleCount => system == null ? 0 : system.particleCount;

        public void BeginFrame(Transform target, Camera view, Renderer[] renderers)
        {
            cues.Clear();
            viewCamera = view;
            bodyRenderers = renderers;
            if (boss == target) return;
            Clear(); boss = target; sockets.Clear(); socketDirections.Clear(); socketWidths.Clear();
            if (boss == null) return;
            Theme = BossAttackVfx.ThemeFor(boss.name);
            motion = boss.GetComponent<BossModelAnimation>();
            jelly = boss.GetComponent<BossJellyfishAnimation>();
            hydra = boss.GetComponent<BossHydra>();
            Bounds bounds = new Bounds(boss.position, Vector3.zero);
            bool first = true;
            foreach (var r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
            }
            size = Mathf.Max(.01f, bounds.size.magnitude * .06f);


        }

        public void AddAttack(int variant, float remaining, float total, int head, float releaseSeconds=.55f)
        {
            if(variant>=6)variant=variant==8?2:variant==10?((Theme==1||Theme==3)?3:5):variant==11||variant==13?3:1;
            if (boss == null || remaining < -releaseSeconds) return;
            float charge = Mathf.Clamp01(1 - remaining / Mathf.Max(.01f, total));
            float release = remaining <= 0 ? Mathf.Clamp01(-remaining / releaseSeconds) : -1;
            if (hydra != null)
            {
                if (head >= 0) Add(hydra.AttackEffectAnchor(head), variant, charge, release);
                else for (int i = 0; i < BossHydra.MaximumHeads; i++) Add(hydra.AttackEffectAnchor(i), variant, charge, release);
                return;
            }
            if (jelly != null)
            {
                for (int i = 0; i < jelly.tentacles.Length; i++)
                    if (variant == 4 || variant == 2 || i % 3 == variant % 3) Add(jelly.tentacles[i], variant, charge, release);
                return;
            }
            if (Theme == 3)
            {
                if (variant == 4 || variant == 5) { Add(Part(1),variant,charge,release); Add(Part(2),variant,charge,release); }
                else Add(Part(4) != null ? Part(4) : Part(3),variant,charge,release);
                return;
            }
            if(Theme==0 && motion is BossDemonAnimation demon)
            {
                if(!demon.Adult)Add(demon.joints[14],variant,charge,release);
                else {Add(demon.joints[6],variant,charge,release);Add(demon.joints[7],variant,charge,release);}
                return;
            }
            // Sweeps and impacts gather in limbs; breath/thrust gathers at the mouth.
            if (Theme == 0 || Theme == 4 || Theme == 6 || Theme == 7 || variant == 1 || variant == 4)
            { Add(Part(1), variant, charge, release); Add(Part(2), variant, charge, release); }
            if (Theme == 1 || Theme == 3 || Theme == 5 || variant == 4)
                Add(Part(4) != null ? Part(4) : Part(3), variant, charge, release);
            if (Theme == 5 && variant == 1) Add(Part(5), variant, charge, release);
            if (cues.Count == 0) Add(boss, variant, charge, release);
        }

        Transform Part(int index) => motion != null && motion.parts != null && index < motion.parts.Length ? motion.parts[index] : null;
        void Add(Transform anchor, int variant, float charge, float release)
        {
            anchor = BossVoxelSkin.Redirect(boss, anchor); // the new model's bone in place of the hidden old socket
            if (anchor == null || !anchor.gameObject.activeInHierarchy) return;
            for (int i = 0; i < cues.Count; i++)
                if (cues[i].anchor == anchor)
                {
                    // A fresh windup takes priority over an older release on the same socket.
                    if (cues[i].release < 0 && (release >= 0 || cues[i].charge >= charge)) return;
                    cues[i] = new Cue { anchor = anchor, variant = variant, charge = charge, release = release }; return;
                }
            if (cues.Count < 24) cues.Add(new Cue { anchor = anchor, variant = variant, charge = charge, release = release });
            if (!sockets.ContainsKey(anchor)) BindSocket(anchor);
        }

        bool Initialize()
        {
            if (system != null) return true;
            var shader = Resources.Load<Shader>("BossVoxelEnergy");
            if (shader == null || !shader.isSupported) return false;
            material = new Material(shader) { name = "Boss volumetric voxel energy" };
            voxelMesh = CreateVoxel();
            var go = new GameObject("VFX == Voxel attack energy", typeof(ParticleSystem));
            go.layer = Visual3DFollower.BossLayer; go.transform.SetParent(transform, false);
            system = go.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed = false; system.randomSeed = 17;
            var main = system.main; main.playOnAwake = false; main.maxParticles = particles.Length;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.startSpeed = 0;
            main.startSize3D = true; main.startRotation3D = true;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = voxelMesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color });
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false; renderer.lightProbeUsage = LightProbeUsage.Off;
            return true;
        }

        internal static Mesh CreateVoxel()
        {
            var vertices = new List<Vector3>(24); var normals = new List<Vector3>(24); var triangles = new List<int>(36);
            var axes = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach(var normal in axes)
            {
                var u = Vector3.Cross(normal, Mathf.Abs(normal.y)>.5f ? Vector3.forward : Vector3.up);
                var v = Vector3.Cross(normal,u); int start=vertices.Count;
                vertices.Add((normal-u-v)*.5f);vertices.Add((normal+u-v)*.5f);
                vertices.Add((normal+u+v)*.5f);vertices.Add((normal-u+v)*.5f);
                for(int i=0;i<4;i++)normals.Add(normal);
                triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            var mesh=new Mesh { name="Boss solid voxel cube" };
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }

        void LateUpdate()
        {
            if (boss == null || !boss.gameObject.activeInHierarchy || viewCamera == null || cues.Count == 0) { Clear(); return; }
            if (!Initialize()) return;

            int count=0, perSocket=Mathf.Min(144,particles.Length/Mathf.Max(1,cues.Count));
            float clock=Time.time;
            foreach(var cue in cues)
            {
                if(cue.anchor==null||!cue.anchor.gameObject.activeInHierarchy)continue;
                // Real socket space, with a small muzzle offset; no camera-facing plane or depth flattening.
                EmissionFrame(cue.anchor,out var center,out var frame);
                var right=frame*Vector3.right;var up=frame*Vector3.up;var forward=frame*Vector3.forward;
                bool mouth=IsMouth(cue.anchor);
                float emitterSize=mouth?Mathf.Clamp(socketWidths[cue.anchor]*Mathf.Abs(cue.anchor.lossyScale.x)*.30f,size*.3f,size*.75f):size*.9f;
                float strength=.65f+cue.charge*.75f;
                float ultimate=cue.variant==4?1.4f:1;
                int socketParticles=Theme==3 && mouth ? Mathf.Min(576,particles.Length-count) : perSocket;
                if(Theme==2 && cue.variant==4)socketParticles=Mathf.Min(socketParticles,72);
                // บาส 2026-11-10: the Wraith's gathering cloud hid its new model - fewer, smaller blocks (the King keeps its own).
                if(boss.name.Contains("VoidWraith")){socketParticles=Mathf.Max(8,socketParticles*2/5);emitterSize*=.6f;}
                for(int i=0;i<socketParticles;i++)
                {
                    float seed=Mathf.Repeat(i*.618034f,1), seed2=Mathf.Repeat(i*.414214f+.21f,1);
                    float travel=Mathf.Repeat(seed+clock*(Theme==7?.35f:.85f),1);
                    float angle=i*2.39996f+clock*(Theme==7?-.8f:1.7f);
                    float orbit=Mathf.Lerp(2.5f,.45f,travel)*ultimate;
                    float latitude=seed2*2-1;
                    var direction=new Vector3(Mathf.Cos(angle)*Mathf.Sqrt(1-latitude*latitude),latitude,Mathf.Sin(angle)*Mathf.Sqrt(1-latitude*latitude));
                    Vector3 offset=direction*orbit;
                    Vector3 stretch=Vector3.one;
                    float block=.25f+seed2*.34f;
                    Color tint=BossAttackVfx.ElementColor(Theme);
                    switch(Theme)
                    {
                        case 0: case 3:
                            offset=new Vector3(Mathf.Cos(angle)*(1-travel)*1.2f,travel*3.7f-.6f,Mathf.Sin(angle)*(1-travel)*1.2f);
                            block*=1.35f-travel*.9f;stretch.y=1.15f;
                            tint=Theme==0?Color.Lerp(new Color(.28f,.003f,.006f),new Color(1,.055f,.008f),1-travel)
                                :Color.Lerp(new Color(1,.12f,.006f),new Color(1,.85f,.18f),1-travel);
                            break;
                        case 2:
                            // Three jagged, rotating chains of solid electric segments.
                            float along=(i%24)/23f;float branch=(i/24)*Mathf.PI*2/3+clock;
                            offset=new Vector3(Mathf.Cos(branch)*along*3,Mathf.Sin(branch)*along*3,Mathf.Sin(along*15+branch)*.75f);
                            offset+=new Vector3(Mathf.Sin(i*7+Mathf.Floor(clock*14))*.2f,Mathf.Cos(i*5)*.18f,0);
                            block=.21f;stretch=new Vector3(1,1.65f,1);
                            tint=i%4==0?new Color(.75f,.94f,1):new Color(.12f,.4f,1);
                            break;
                        case 4:
                            offset=direction*(1.1f+seed*1.1f);stretch=new Vector3(.7f,1.9f,.7f);
                            tint=i%3==0?new Color(.15f,.24f,.3f):Color.Lerp(new Color(.05f,.45f,.7f),new Color(.6f,1,1),seed);
                            break;
                        case 7:
                            offset=new Vector3(Mathf.Cos(angle)*orbit,Mathf.Sin(angle*.6f)*1.6f,Mathf.Sin(angle)*orbit);
                            block*=1.4f;tint=Color.Lerp(new Color(.09f,.012f,.17f),new Color(.62f,.12f,.95f),seed*seed);
                            break;
                        case 6:
                            float ring=i%24/24f*Mathf.PI*2;
                            offset=new Vector3(Mathf.Cos(ring)*(1.1f+.2f*(i/24)),Mathf.Sin(ring)*(1.1f+.2f*(i/24)),(i/24-1)*.75f);
                            stretch=new Vector3(.5f,.5f,2.6f);block=.28f;
                            tint=Color.Lerp(new Color(.7f,.23f,.01f),new Color(1,.86f,.2f),seed);
                            break;
                        case 8:
                            offset=new Vector3(Mathf.Cos(angle)*orbit*.7f,Mathf.Sin(angle*2)*.8f-travel,Mathf.Sin(angle)*orbit*.7f);
                            tint=Color.Lerp(new Color(.07f,.3f,.008f),new Color(.55f,1,.045f),seed);break;
                        default:
                            offset=new Vector3(Mathf.Cos(angle)*orbit,Mathf.Sin(angle*2)*.8f,Mathf.Sin(angle)*orbit);
                            stretch=new Vector3(1.35f,.6f,1);tint=Color.Lerp(tint, new Color(.65f,1,1),seed*.65f);break;
                    }
                    if(cue.variant==1)offset.x*=1.3f;
                    if(cue.variant==2)offset*=.8f;
                    if(cue.variant==3)offset.z-=travel*1.5f;
                    if(cue.variant==5){offset.x*=.65f;offset.z*=1.3f;}
                    if(cue.variant==4)offset*=1.3f;
                    float fade=1;
                    if(!mouth && Theme!=6){offset*=1.45f;block*=1.25f;}
                    if(cue.release>=0)
                    {
                        offset+=direction*cue.release*(cue.variant==4?7:4);
                        if(cue.variant==3||cue.variant==5)offset.z-=cue.release*5;
                        fade=Mathf.Sqrt(1-cue.release);
                    }
                    if(mouth || Theme==6)
                    {
                        // Narrow muzzle, widening forward plume; the larger volume stays outside the anatomy.
                                                float flow=cue.release<0?1-travel:travel;
                        float spread=.12f+flow*(cue.release<0?.45f+cue.charge*.65f:2.4f);
                        float distance=cue.release<0?.08f+flow*(1.4f+cue.charge*3):.15f+flow*(8+cue.release*16);
                        offset=new Vector3(Mathf.Cos(angle)*spread,Mathf.Sin(angle)*spread,distance);
                        block=cue.release<0?.25f+seed2*.23f:.38f+seed2*.38f;
                        if(i%4==0){offset.x*=1.45f;offset.y*=1.45f;block*=.55f;}
                        stretch=cue.release<0?Vector3.one:new Vector3(1,1,2.6f);
                    }
                    else if(Theme==3)
                    {
                        // Wing attacks and meteor summons gather along the wing tips, not a bonfire.
                        offset=direction*(cue.release<0?.6f+(1-travel)*2.1f:.7f+cue.release*7);
                        block*=.65f;
                    }
                    if(Theme==1 && mouth && cue.variant==3 && cue.release>=0)
                    {
                        fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.86f,1,cue.release));
                        float radius=.22f+travel*.48f;
                        offset=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,.15f+travel*20);
                        stretch=new Vector3(.8f,.8f,4);block=.45f+seed2*.18f;
                        tint=Color.Lerp(new Color(.12f,.65f,1),new Color(.72f,1,1),seed2);
                    }
                    if(Theme==3 && mouth) DragonFireCone(i,clock,cue.charge,cue.release,out offset,out stretch,out block,out tint);
                    if(Theme==0 && cue.release>=0)
                    {
                        float along=(i%48)/47f;
                        float arc=Mathf.Lerp(-1.15f,1.15f,along);
                        float radius=3+cue.release*7;
                        offset=new Vector3(Mathf.Sin(arc)*radius,Mathf.Cos(arc)*radius-radius*.65f,(i/48-1)*.18f+cue.release*9);
                        if(cue.anchor==Part(2))offset.y=-offset.y;
                        stretch=new Vector3(2.6f,.45f,.65f);block=.3f+Mathf.Sin(along*Mathf.PI)*.22f;
                        tint=i%3==0?new Color(1,.85f,.78f):new Color(1,.045f,.08f);
                    }
                    if(Theme==2 && cue.variant==4)
                    {
                        float theta=(i%48)/48f*Mathf.PI*2;
                        float radius=(cue.release<0?1.5f+cue.charge*2:3+cue.release*9);
                        offset=new Vector3(Mathf.Cos(theta)*radius,Mathf.Sin(theta)*radius,(i/48-1)*.75f);
                        offset.z+=Mathf.Sin(i*7+clock*18)*.35f;
                        stretch=new Vector3(.55f,2.4f,.55f);block=.13f;fade*=.75f;
                        tint=i%3==0?new Color(.75f,1,1):new Color(.05f,.3f,1);
                    }
                    // Quantized shapes and independently shaded faces give a voxel silhouette.
                    float grid=.13f;
                    offset=new Vector3(Mathf.Round(offset.x/grid),Mathf.Round(offset.y/grid),Mathf.Round(offset.z/grid))*grid;
                    var particle=new ParticleSystem.Particle();
                    particle.position=center+(right*offset.x+up*offset.y+forward*offset.z)*emitterSize*strength;
                    particle.startLifetime=particle.remainingLifetime=10;
                    particle.startSize3D=stretch*emitterSize*block*strength*fade;
                    particle.rotation3D=(frame*Quaternion.Euler(15+seed*35,clock*35+i*23,seed2*40)).eulerAngles;
                    if(Theme==2||Theme==6)particle.rotation3D=frame.eulerAngles+new Vector3(0,0,Theme==2?angle*Mathf.Rad2Deg:0);
                    if(Theme==0 && cue.release>=0)particle.rotation3D=(frame*Quaternion.Euler(0,0,-Mathf.Lerp(-1.15f,1.15f,(i%48)/47f)*Mathf.Rad2Deg)).eulerAngles;
                    tint.a=fade;particle.startColor=tint;particles[count++]=particle;
                }
            }
            if(!system.isPlaying)system.Play();
            system.SetParticles(particles,count);
        }

        public void Clear(){cues.Clear();if(system!=null)system.Clear();}
        void OnDisable(){Clear();}
        void OnDestroy(){if(material!=null)Destroy(material);if(voxelMesh!=null)Destroy(voxelMesh);}
    }
}
