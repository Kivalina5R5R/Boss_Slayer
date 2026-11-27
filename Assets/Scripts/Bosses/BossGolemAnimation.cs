using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace BossSlayer
{
    public sealed class BossGolemAnimation : BossModelAnimation
    {
        public const float DeathPoseSeconds=3.4f;
        public bool RedPhase {get;private set;}
        public int FractureLevel {get;private set;}
        public float Transformation {get;private set;}
        public int CrystalFaceCount {get;private set;}
        public int VisibleFaceCount {get;private set;}
        static readonly Quaternion MeshSpace=Quaternion.Euler(90,0,0);
        sealed class Surface
        {
            public MeshFilter filter; public Mesh source,mesh;
            public Vector3[] vertices; public Color[] colors;public int[] triangles;
            public bool[] crystal;public float[] order;
            public readonly List<int> visible=new List<int>();
            public Color[] work; // บาส 2026-10-06: reused colour buffer (was a fresh Clone() per surface per frame)
        }
        readonly List<Surface> surfaces=new List<Surface>();
        float refreshedTransformation=-1,refreshedDissolve=-1;int refreshedFracture=-1;
        Vector3 restRig;float size=1,lowestHp=1,damageKick,clock;
        bool prepared,dying,beamBraced;
        float seismicClock,seismicStrength;
        public void SetUltimateRecoil(float time,float strength){seismicClock=time;seismicStrength=strength;}
        public void SetBeamBraced(bool active){beamBraced=active;}
        Transform[] deathParts;Vector3[] deathPositions;Quaternion[] deathRotations;
        readonly List<Shard> shards=new List<Shard>();
        sealed class Shard {public Transform transform;public Vector3 start,velocity;public float born;}
        Mesh shardMesh;Material shardMaterial;
        public override bool Initialize()
        {
            if(!base.Initialize())return false;
            if(!prepared){prepared=true;restRig=rigRoot.localPosition;var mesh=GetComponent<MeshFilter>()?.sharedMesh;size=mesh!=null?mesh.bounds.size.z:1;}
            return true;
        }
        public override void ResetMotion()
        {
            RestoreSurfaces();ClearShards();RedPhase=false;beamBraced=false;Transformation=0;FractureLevel=0;lowestHp=1;damageKick=clock=seismicClock=seismicStrength=0;dying=false;
            base.ResetMotion();
        }
        public void SetTransformation(float progress)
        {
            Transformation=Mathf.Clamp01(progress);RedPhase=Transformation>=1;RefreshSurface(0);
        }
        public void ObserveHealth(float fraction)
        {
            lowestHp=Mathf.Min(lowestHp,Mathf.Clamp01(fraction));
            int level=lowestHp<=.2f?3:lowestHp<=.45f?2:lowestHp<=.75f?1:0;
            if(level<=FractureLevel)return;
            FractureLevel=level;damageKick=1;EnsureSurfaces();EmitShards(18);RefreshSurface(0);
        }
        static Matrix4x4 Relative(Transform child,Transform ancestor)
        {
            var matrix=Matrix4x4.identity;
            for(var t=child;t!=ancestor;t=t.parent)
            {
                if(t==null)throw new System.InvalidOperationException("Golem mesh outside rig");
                matrix=Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale)*matrix;
            }
            return matrix;
        }
        void EnsureSurfaces()
        {
            if(surfaces.Count>0)return;
            CrystalFaceCount=0;
            foreach(var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();
                if(filter.transform==transform||renderer==null||!renderer.enabled||filter.sharedMesh==null||filter.transform.name=="Crystal fragment")continue;
                var s=new Surface{filter=filter,source=filter.sharedMesh};
                s.vertices=s.source.vertices;s.colors=s.source.colors;s.triangles=s.source.triangles;
                if(s.colors.Length!=s.vertices.Length)continue;
                int count=s.triangles.Length/6;s.crystal=new bool[count];s.order=new float[count];
                for(int f=0;f<count;f++)
                {
                    var c=s.colors[s.triangles[f*6]].gamma;
                    s.crystal[f]=c.g>.65f&&c.b>.65f;
                    if(s.crystal[f])CrystalFaceCount++;
                    s.order[f]=Mathf.Repeat(Mathf.Sin(f*12.9898f+surfaces.Count*78.233f)*43758.5453f,1);
                }
                s.mesh=Instantiate(s.source);s.mesh.name=s.source.name+" (golem fracture)";s.mesh.hideFlags=HideFlags.DontSave;s.mesh.MarkDynamic();
                filter.sharedMesh=s.mesh;surfaces.Add(s);
            }
        }
        // บาส 2026-10-06 (Balance Note A5): transform + death called this every frame, cloning every vertex colour array
        // and re-uploading it - a frame-hitch risk on the LED PCs. Now it skips frames where nothing changed and
        // rewrites one cached buffer per surface (SetColors(array) does not allocate).
        void RefreshSurface(float dissolve)
        {
            EnsureSurfaces();
            if(surfaces.Count>0&&Mathf.Approximately(refreshedTransformation,Transformation)&&Mathf.Approximately(refreshedDissolve,dissolve)&&refreshedFracture==FractureLevel)return;
            refreshedTransformation=Transformation;refreshedDissolve=dissolve;refreshedFracture=FractureLevel;
            VisibleFaceCount=0;
            foreach(var s in surfaces)
            {
                if(s.work==null||s.work.Length!=s.colors.Length)s.work=new Color[s.colors.Length];
                var colors=s.work;System.Array.Copy(s.colors,colors,colors.Length);s.visible.Clear();
                for(int f=0;f<s.crystal.Length;f++)
                {
                    bool crystal=s.crystal[f];
                    if((crystal&&s.order[f]<FractureLevel*.075f)||s.order[f]<dissolve)continue;
                    VisibleFaceCount++;
                    for(int j=0;j<6;j++)
                    {
                        int v=s.triangles[f*6+j];s.visible.Add(v);
                        if(crystal)
                        {
                            Color red=new Color(1,.035f,.065f).linear*Mathf.Lerp(.7f,1.55f,s.colors[v].maxColorComponent);
                            Color tint=Color.Lerp(s.colors[v],red,Transformation);
                            if(FractureLevel>0 && s.order[f]<FractureLevel*.16f)tint*=.22f;
                            colors[v]=tint;
                        }
                    }
                }
                s.mesh.SetColors(colors);s.mesh.SetTriangles(s.visible,0,false);
            }
        }
        public override void SamplePose(float time,float charge,float release,float stun)
        {
            if(!Initialize()||dying)return;
            float dt=Mathf.Max(0,time-clock);clock=time;damageKick=Mathf.Max(0,damageKick-dt*2.5f);
            // Keep the base attack/framing state, then replace the light idle with planted weight shifts.
            base.SamplePose(time*.58f,charge,release,stun);
            float walk=Mathf.Abs(Locomotion),step=Mathf.Sin(time*2.15f),weight=Mathf.Sign(step)*Mathf.Pow(Mathf.Abs(step),.45f);
            float wind=release>0?1-Mathf.SmoothStep(0,1,Mathf.Clamp01((1-release)/.15f)):Mathf.SmoothStep(0,1,charge);
            float hit=release>0?Mathf.Sin(Mathf.Clamp01((1-release)/.35f)*Mathf.PI):0;
            float quiet=1-Mathf.Max(wind,hit);
            rigRoot.localRotation=MeshSpace*Quaternion.Euler(4+wind*10-hit*23,weight*walk*2,weight*walk*5-damageKick*5)*Quaternion.Inverse(MeshSpace);
            rigRoot.localPosition=restRig+MeshSpace*new Vector3(weight*walk*.006f,-(Mathf.Pow(Mathf.Abs(step),8)*walk*.018f+hit*.065f),0)*size;
            for(int k=1;k<=2;k++)if(parts[k]!=null)
            {
                float side=k==1?-1:1;
                float hammer=AttackVariant==1?(k==1?1:.28f):1;
                var angles=new Vector3(-10+side*step*walk*14+wind*128*hammer-hit*94*hammer,side*wind*8,side*(5+wind*12));
                if(AttackVariant==4 && RedPhase){float brace=Mathf.Max(wind,beamBraced?1:hit);angles=Vector3.Lerp(angles,new Vector3(38,side*24,side*68),brace);}
                parts[k].localRotation=MeshSpace*Quaternion.Euler(angles*motionAmount*Mathf.Lerp(1,.15f,stun))*Quaternion.Inverse(MeshSpace);
            }
            if(parts[3]!=null)parts[3].localRotation=MeshSpace*Quaternion.Euler(5+wind*9-hit*14+damageKick*12,Mathf.Sin(time*.45f)*2,0)*Quaternion.Inverse(MeshSpace);
            if(Transformation>0&&Transformation<1)
            {
                rigRoot.localPosition-=MeshSpace*Vector3.up*(Mathf.Sin(Transformation*Mathf.PI)*size*.045f);
                rigRoot.localRotation*=Quaternion.AngleAxis(Mathf.Sin(time*24)*Transformation*1.5f,MeshSpace*Vector3.forward);
            }
            rigRoot.localPosition+=MeshSpace*new Vector3(Mathf.Sin(seismicClock*91)*.005f,Mathf.Sin(seismicClock*113)*.003f,0)*size*seismicStrength;
            SampleShards(time);
        }
        readonly Transform[] crystalEmitters=new Transform[5];
        readonly Vector3[] crystalPoints=new Vector3[5];
        public Vector3 CrystalOrigin(int index)
        {
            var skin = BossVoxelSkin.ActiveOn(transform);
            if (skin != null) { var part = new[] { "arm_L", "chest", "head", "chest", "arm_R" }[Mathf.Abs(index) % 5]; return skin.Tip(part, skin.transform.forward); }
            EnsureSurfaces();index=Mathf.Abs(index)%5;
            if(crystalEmitters[index]==null)
            {
                var sculpt=new[]{new Vector3(-8,27,0),new Vector3(-2,29,0),new Vector3(0,20,-4),new Vector3(2,29,0),new Vector3(8,27,0)};
                var source=GetComponent<MeshFilter>().sharedMesh;
                var target=source.bounds.center+MeshSpace*(sculpt[index]-sculptCenter)*(size/Mathf.Max(1,sculptHeight));
                float nearest=float.MaxValue;
                foreach(var s in surfaces)
                {
                    var matrix=Relative(s.filter.transform,transform);
                    for(int f=0;f<s.crystal.Length;f++)if(s.crystal[f]&&s.order[f]>.45f)
                    {
                        var point=s.vertices[s.triangles[f*6]];float distance=(matrix.MultiplyPoint3x4(point)-target).sqrMagnitude;
                        if(distance>=nearest)continue;nearest=distance;crystalEmitters[index]=s.filter.transform;crystalPoints[index]=point;
                    }
                }
            }
            return crystalEmitters[index]!=null?crystalEmitters[index].TransformPoint(crystalPoints[index]):transform.position;
        }
        void EmitShards(int count)
        {
            if(!Application.isPlaying)return;
            if(shardMesh==null)
            {
                shardMesh=new Mesh{name="Crystal fracture shard",hideFlags=HideFlags.DontSave};
                shardMesh.vertices=new[]{Vector3.up,Vector3.down,Vector3.left*.45f,Vector3.forward*.45f,Vector3.right*.45f,Vector3.back*.45f};
                shardMesh.triangles=new[]{0,3,2,0,4,3,0,5,4,0,2,5,1,2,3,1,3,4,1,4,5,1,5,2};
                var tint=RedPhase?new Color(1,.05f,.1f):new Color(.18f,.9f,1);
                var colors=new Color[6];for(int i=0;i<6;i++)colors[i]=tint.linear;shardMesh.colors=colors;shardMesh.RecalculateNormals();
                shardMaterial=new Material(GameArt.Load<Shader>("Art/Shaders/BossVoxelEnergy")){hideFlags=HideFlags.DontSave};
            }
            for(int i=0;i<count&&shards.Count<72;i++)
            {
                var go=new GameObject("Crystal fragment",typeof(MeshFilter),typeof(MeshRenderer)){layer=gameObject.layer,hideFlags=HideFlags.DontSave};
                go.transform.SetParent(transform,false);go.GetComponent<MeshFilter>().sharedMesh=shardMesh;go.GetComponent<MeshRenderer>().sharedMaterial=shardMaterial;
                var world=CrystalOrigin(i);
                // Emission happens in combat, after the portal has restored the root scale.
                var start=transform.InverseTransformPoint(world);
                go.transform.localScale=Vector3.one*size*(.008f+(i%4)*.003f);
                shards.Add(new Shard{transform=go.transform,start=start,born=clock,velocity=MeshSpace*new Vector3(Mathf.Sin(i*2.4f)*.24f,.15f+i%3*.04f,Mathf.Cos(i*2.4f)*.14f)*size});
            }
            SampleShards(clock);
        }
        void SampleShards(float time)
        {
            for(int i=shards.Count-1;i>=0;i--)
            {
                var s=shards[i];float age=time-s.born;
                if(age>1.5f){Dispose(s.transform.gameObject);shards.RemoveAt(i);continue;}
                s.transform.localPosition=s.start+s.velocity*age-MeshSpace*Vector3.up*(size*.38f*age*age);
                s.transform.localRotation=Quaternion.Euler(age*130,age*90,age*170);
                s.transform.localScale=Vector3.one*size*.014f*(1-Mathf.Clamp01((age-1)/.5f));
            }
        }
        public void BeginDeathPose()
        {
            Defeat();dying=true;EnsureSurfaces();deathParts=GetPoseTransforms();deathPositions=new Vector3[deathParts.Length];deathRotations=new Quaternion[deathParts.Length];
            for(int i=0;i<deathParts.Length;i++){deathPositions[i]=deathParts[i].localPosition;deathRotations[i]=deathParts[i].localRotation;}
            EmitShards(30);
        }
        public void SampleDeathPose(float age)
        {
            if(deathParts==null)return;
            float sink=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/2.2f)),fall=Mathf.SmoothStep(0,1,Mathf.Clamp01((age-1.2f)/1.6f));
            rigRoot.localPosition=deathPositions[0]-MeshSpace*Vector3.up*(size*.23f*sink);
            rigRoot.localRotation=deathRotations[0]*MeshSpace*Quaternion.Euler(68*fall,0,7*sink)*Quaternion.Inverse(MeshSpace);
            for(int i=1;i<deathParts.Length;i++)deathParts[i].localRotation=Quaternion.Slerp(deathRotations[i],MeshSpace*Quaternion.Euler(-30,0,i%2==0?10:-10)*Quaternion.Inverse(MeshSpace),sink);
            RefreshSurface(Mathf.Clamp01((age-2.5f)/.9f)*.18f);SampleShards(clock+age);
        }
        public void SampleDeathDissolve(float progress){RefreshSurface(.18f+.82f*Mathf.Clamp01(progress));}
        void RestoreSurfaces()
        {
            foreach(var s in surfaces){if(s.filter!=null&&s.filter.sharedMesh==s.mesh)s.filter.sharedMesh=s.source;Dispose(s.mesh);}
            surfaces.Clear();System.Array.Clear(crystalEmitters,0,crystalEmitters.Length);CrystalFaceCount=VisibleFaceCount=0;
            refreshedTransformation=refreshedDissolve=-1;refreshedFracture=-1; // fresh meshes: next refresh must write
        }
        void ClearShards(){foreach(var s in shards)if(s.transform!=null)Dispose(s.transform.gameObject);shards.Clear();Dispose(shardMesh);Dispose(shardMaterial);shardMesh=null;shardMaterial=null;}
        static void Dispose(Object obj){if(obj==null)return;if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
        void OnDestroy(){RestoreSurfaces();ClearShards();}
    }
}
