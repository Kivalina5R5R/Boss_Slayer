using UnityEngine;
using UnityEngine.Rendering;
namespace BossSlayer
{
    // Mesh volumes and mesh particles; no LineRenderer or canvas beam graphic.
    public sealed class BossCrystalBeamVfx : MonoBehaviour
    {
        readonly Transform[] tubes=new Transform[3],rings=new Transform[4];
        readonly Material[] materials=new Material[4];
        Mesh tubeMesh,ringMesh,shardMesh;
        ParticleSystem shards;
        readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[64];
        Transform muzzle,impact;
        bool initialized;
        public float Diameter {get;private set;}
        public float SampleClock {get;private set;}
        public int ParticleCount=>shards!=null?shards.particleCount:0;
        public Vector3 End {get;private set;}
        public void Initialize(int layer,int sortingOrder=0)
        {
            if(initialized)return;initialized=true;gameObject.layer=layer;
            var shader=GameArt.Load<Shader>("Art/Shaders/BossCrystalBeamVolume");
            for(int i=0;i<4;i++){materials[i]=new Material(shader){name="Crimson beam volume "+i,hideFlags=HideFlags.DontSave};materials[i].SetFloat("_Layer",i);}
            tubeMesh=Tube();ringMesh=Torus();shardMesh=Crystal();
            for(int i=0;i<3;i++)tubes[i]=Piece("Beam volume "+i,tubeMesh,materials[i],layer,sortingOrder);
            for(int i=0;i<4;i++)rings[i]=Piece("Traveling refraction ring "+i,ringMesh,materials[3],layer,sortingOrder);
            muzzle=Piece("Crystal discharge",shardMesh,materials[3],layer,sortingOrder);
            impact=Piece("Beam impact",shardMesh,materials[3],layer,sortingOrder);
            var go=new GameObject("Crystalline beam spray"){layer=layer,hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);
            shards=go.AddComponent<ParticleSystem>();shards.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=shards.main;main.playOnAwake=false;main.loop=false;main.startSpeed=0;main.maxParticles=64;
            main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Shape;
            var emission=shards.emission;emission.enabled=false;var shape=shards.shape;shape.enabled=false;
            var renderer=shards.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;
            renderer.mesh=shardMesh;renderer.sharedMaterial=materials[3];renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.sortingOrder=sortingOrder;
        }
        Transform Piece(string label,Mesh mesh,Material material,int layer,int order)
        {
            var go=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer)){layer=layer,hideFlags=HideFlags.DontSave};
            go.transform.SetParent(transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.sortingOrder=order;
            return go.transform;
        }
        // บาส 2026-10-20: Abyssal King's void beams reuse the crystal volume with a violet tint.
        public void SetTint(Color tint,Color core){foreach(var m in materials){if(m==null)continue;m.SetColor("_Tint",tint);m.SetColor("_CoreTint",core);}}
        public void SetGolden(){foreach(var m in materials){if(m==null)continue;m.SetColor("_Tint",new Color(1,.65f,.025f));m.SetColor("_CoreTint",new Color(2.2f,2f,1.2f));}}
        public void Sample(Vector3 start,Vector3 end,float diameter,float warningRemaining,float warningTotal,float remaining)
        {
            if(!initialized)Initialize(gameObject.layer);
            var direction=end-start;float length=direction.magnitude;
            if(length<.00001f)return;
            transform.SetPositionAndRotation(start,Quaternion.LookRotation(direction));
            Diameter=diameter;End=end;
            float clock=warningTotal-warningRemaining;SampleClock=clock;
            bool fired=warningRemaining<=0;
            float fade=Mathf.Clamp01((remaining+.45f)/.45f);
            float power=fired?fade:Mathf.Lerp(.18f,.65f,Mathf.Clamp01(clock/warningTotal));
            float pulse=1+Mathf.Sin(clock*29)*.025f;
            for(int i=0;i<materials.Length;i++){materials[i].SetFloat("_Clock",clock);materials[i].SetFloat("_Power",power);}
            for(int i=0;i<3;i++)
            {
                float factor=i==0?.48f:i==1?.92f:1.4f;
                float thickness=diameter*factor*(fired?pulse:.09f);
                tubes[i].localScale=new Vector3(thickness,thickness,length);
                tubes[i].gameObject.SetActive(remaining>-.4f);
            }
            for(int i=0;i<4;i++)
            {
                float progress=Mathf.Repeat(clock*(fired?.65f:.22f)+i*.25f,1);
                rings[i].localPosition=Vector3.forward*length*progress;
                rings[i].localRotation=Quaternion.Euler(0,0,clock*80+i*47);
                rings[i].localScale=Vector3.one*diameter*(fired?1.15f:.45f);
                rings[i].gameObject.SetActive(remaining>0);
            }
            muzzle.localScale=Vector3.one*diameter*(fired?1.25f:.5f+Mathf.Clamp01(clock/warningTotal)*.65f);
            muzzle.localRotation=Quaternion.Euler(clock*90,clock*130,0);
            impact.localPosition=Vector3.forward*length;impact.localScale=Vector3.one*diameter*(fired?1.3f:.15f);
            impact.localRotation=Quaternion.Euler(clock*120,0,clock*80);
            muzzle.gameObject.SetActive(remaining>0);impact.gameObject.SetActive(remaining>0);
            int count=remaining>0?(fired?64:20):0;
            for(int i=0;i<count;i++)
            {
                float phase=Mathf.Repeat(clock*(.8f+i%4*.08f)+i*.618f,1),angle=i*2.39996f+clock*.8f;
                bool atEnd=i>=40;
                float radius=diameter*(atEnd?.3f+phase*.95f:.3f+Mathf.Sin(phase*Mathf.PI)*.4f);
                float z=atEnd?length+(phase-.5f)*diameter:phase*length;
                particles[i]=new ParticleSystem.Particle{
                    position=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,z),
                    startColor=new Color(1,.16f+i%3*.12f,.13f,(1-phase)*fade),
                    startSize=diameter*(.055f+i%4*.025f),rotation3D=new Vector3(i*37+clock*70,i*29,clock*110),
                    startLifetime=2,remainingLifetime=2
                };
            }
            shards.SetParticles(particles,count);
        }
        static Mesh Tube()
        {
            const int sides=20,segments=24;var vertices=new Vector3[(sides+1)*(segments+1)];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];var triangles=new int[sides*segments*6];
            for(int j=0;j<=segments;j++)for(int i=0;i<=sides;i++)
            {
                int k=j*(sides+1)+i;float a=i*Mathf.PI*2/sides;
                normals[k]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);vertices[k]=normals[k]*.5f+Vector3.forward*j/segments;uv[k]=new Vector2(i/(float)sides,j/(float)segments);
            }
            int t=0;for(int j=0;j<segments;j++)for(int i=0;i<sides;i++)
            {int k=j*(sides+1)+i;triangles[t++]=k;triangles[t++]=k+1;triangles[t++]=k+sides+1;triangles[t++]=k+1;triangles[t++]=k+sides+2;triangles[t++]=k+sides+1;}
            var mesh=new Mesh{name="Crystal beam cylindrical volume",hideFlags=HideFlags.DontSave};mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
        static Mesh Torus()
        {
            const int sides=32,minor=6;var v=new Vector3[(sides+1)*(minor+1)];var n=new Vector3[v.Length];var uv=new Vector2[v.Length];var c=new Color[v.Length];var tri=new int[sides*minor*6];
            for(int i=0;i<=sides;i++)for(int j=0;j<=minor;j++)
            {int k=i*(minor+1)+j;float a=i*Mathf.PI*2/sides,b=j*Mathf.PI*2/minor;var radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);n[k]=radial*Mathf.Cos(b)+Vector3.forward*Mathf.Sin(b);v[k]=radial*.5f+n[k]*.035f;uv[k]=new Vector2(i/(float)sides,j/(float)minor);c[k]=Color.white;}
            int t=0;for(int i=0;i<sides;i++)for(int j=0;j<minor;j++){int k=i*(minor+1)+j;tri[t++]=k;tri[t++]=k+minor+1;tri[t++]=k+1;tri[t++]=k+1;tri[t++]=k+minor+1;tri[t++]=k+minor+2;}
            var mesh=new Mesh{name="Crystal beam energy ring",hideFlags=HideFlags.DontSave};mesh.vertices=v;mesh.normals=n;mesh.uv=uv;mesh.colors=c;mesh.triangles=tri;mesh.RecalculateBounds();return mesh;
        }
        static Mesh Crystal()
        {
            var mesh=new Mesh{name="Beam crystal particle",hideFlags=HideFlags.DontSave};
            mesh.vertices=new[]{Vector3.up*.65f,Vector3.down*.65f,Vector3.left*.3f,Vector3.forward*.3f,Vector3.right*.3f,Vector3.back*.3f};
            mesh.triangles=new[]{0,3,2,0,4,3,0,5,4,0,2,5,1,2,3,1,3,4,1,4,5,1,5,2};
            mesh.colors=new[]{Color.white,Color.white,Color.white,Color.white,Color.white,Color.white};
            mesh.uv=new[]{Vector2.one*.5f,Vector2.one*.5f,Vector2.one*.5f,Vector2.one*.5f,Vector2.one*.5f,Vector2.one*.5f};
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void OnDestroy()
        {
            foreach(var m in materials)if(m!=null)Destroy(m);
            if(tubeMesh!=null)Destroy(tubeMesh);if(ringMesh!=null)Destroy(ringMesh);if(shardMesh!=null)Destroy(shardMesh);
        }
    }
}
