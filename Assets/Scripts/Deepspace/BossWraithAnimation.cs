using UnityEngine;
using UnityEngine.Rendering;
namespace Deepspace
{
    public sealed class BossWraithAnimation : BossModelAnimation
    {
        ParticleSystem mist;Material mistMaterial;
        readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[160];
        Mesh tailSource,tailMesh;Vector3[] restTail,tailVertices;
        bool suppressed;float wraithHeight=1;
        public bool MistFormActive {get;private set;}
        static readonly int[] SmokeAnchors={0,1,2,5,5,5};
        public void SetMistForm(bool active){MistFormActive=active&&!suppressed;}
        static readonly Quaternion ToMeshSpace=Quaternion.Euler(90,0,0);
        public int MistParticleCount=>mist!=null?mist.particleCount:0;
        public float MistClock {get;private set;}
        public override void ResetMotion()
        {
            ReleaseMist();RestoreTail();base.ResetMotion();suppressed=false;MistFormActive=false;
            if(AnimatedMesh!=null)wraithHeight=AnimatedMesh.bounds.size.z;
        }
        public override void SamplePose(float time,float charge,float release,float stun)
        {
            base.SamplePose(time,charge,release,stun);
            if(!Initialize())return;
            if(parts[4]!=null)parts[4].localRotation=ToMeshSpace*Quaternion.Euler(-4-charge*16-release*8,0,0)*Quaternion.Inverse(ToMeshSpace);
            SampleTail(time,charge);
            SampleMist(time,AttackVariant==4?Mathf.Max(charge,release):0);
        }
        void SampleTail(float time,float charge)
        {
            if(parts[5]==null)return;
            var filter=parts[5].GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)return;
            if(tailMesh==null)
            {
                tailSource=filter.sharedMesh;restTail=tailSource.vertices;tailVertices=new Vector3[restTail.Length];
                tailMesh=Instantiate(tailSource);tailMesh.name="Wraith flowing tail";tailMesh.hideFlags=HideFlags.DontSave;tailMesh.MarkDynamic();filter.sharedMesh=tailMesh;
            }
            var down=ToMeshSpace*Vector3.down;var side=ToMeshSpace*Vector3.right;
            for(int i=0;i<restTail.Length;i++)
            {
                float t=Mathf.Clamp01(Vector3.Dot(restTail[i],down)/(wraithHeight*.52f));
                tailVertices[i]=restTail[i]+side*(Mathf.Sin(time*2.1f-t*5)*wraithHeight*.05f*t*t*(1+charge*.5f));
            }
            tailMesh.vertices=tailVertices;tailMesh.RecalculateNormals();tailMesh.RecalculateBounds();
        }
        void SampleMist(float time,float ultimate)
        {
            if(suppressed)return;
            if(mist==null)
            {
                var go=new GameObject("Void Wraith purple smoke"){layer=gameObject.layer,hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);
                mist=go.AddComponent<ParticleSystem>();mist.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                mistMaterial=new Material(Resources.Load<Shader>("BossWraithMist")){name="Wraith rolling purple smoke",hideFlags=HideFlags.DontSave};
                var main=mist.main;main.playOnAwake=false;main.maxParticles=particles.Length;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=0;
                var emission=mist.emission;emission.enabled=false;var shape=mist.shape;shape.enabled=false;
                var renderer=mist.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=mistMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;mist.Pause();
            }
            MistClock=time;mistMaterial.SetFloat("_Clock",time);
            float scale=transform.TransformVector(ToMeshSpace*Vector3.up*wraithHeight).magnitude;
            Vector3 up=transform.TransformDirection(ToMeshSpace*Vector3.up);
            int count=MistFormActive?particles.Length:96;
            float form=MistFormActive?1:0;
            for(int i=0;i<count;i++)
            {
                var joint=parts[SmokeAnchors[i%SmokeAnchors.Length]];var mesh=joint.GetComponent<MeshFilter>().sharedMesh;
                var bounds=mesh.bounds;float seed=Mathf.Repeat(i*.618034f,1),age=Mathf.Repeat(time*(.19f+seed*.13f)+seed,1),angle=i*2.39996f+time*.4f;
                var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3(Mathf.Cos(angle)*1.05f,Mathf.Sin(angle)*.8f,(seed-.5f)*1.4f));
                float fade=Mathf.Sin(age*Mathf.PI);
                Vector3 worldPoint=joint.TransformPoint(point)+up*scale*(age*.12f-.04f);
                if(i>=96)
                {
                    // Two counterwinding smoke ribbons visibly mark the intangible state.
                    float level=(i-96)/63f,turn=level*Mathf.PI*4+time*(i%2==0?1.8f:-1.8f);
                    Vector3 radial=transform.TransformDirection(ToMeshSpace*new Vector3(Mathf.Cos(turn),0,Mathf.Sin(turn)));
                    var center=transform.TransformPoint(AnimatedMesh.bounds.center);
                    worldPoint=center+up*scale*(level-.5f)*.95f+radial*scale*(.24f+.06f*Mathf.Sin(level*Mathf.PI));
                    fade=.8f;
                }
                particles[i]=new ParticleSystem.Particle{
                    position=worldPoint,
                    startLifetime=10,remainingLifetime=10,rotation=angle*Mathf.Rad2Deg+age*65,
                    startSize=scale*(.09f+seed*.09f)*(1+age*.6f)*(1+ultimate*.35f+form*.7f),
                    startColor=new Color(.9f+form*.25f,.65f+form*.2f,1,fade*(.25f+ultimate*.18f+form*.28f))
                };
            }
            mist.SetParticles(particles,count);
        }
        public void StopMist(){suppressed=true;MistFormActive=false;if(mist!=null)mist.Clear();}
        void RestoreTail()
        {
            if(tailMesh==null)return;
            if(parts!=null&&parts.Length>5&&parts[5]!=null){var f=parts[5].GetComponent<MeshFilter>();if(f!=null&&f.sharedMesh==tailMesh)f.sharedMesh=tailSource;}
            Dispose(tailMesh);tailMesh=null;tailSource=null;
        }
        void ReleaseMist(){if(mist!=null){mist.gameObject.SetActive(false);Dispose(mist.gameObject);}Dispose(mistMaterial);mist=null;mistMaterial=null;MistClock=0;}
        static void Dispose(Object o){if(o==null)return;if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
        void OnDestroy(){ReleaseMist();RestoreTail();}
    }
}
