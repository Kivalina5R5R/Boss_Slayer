using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Deepspace
{
    public sealed partial class BossJellyfishAnimation
    {
        sealed class SoftSurface
        {
            public MeshFilter filter;public Mesh source,mesh;
            public Vector3[] rest,output;
            public Matrix4x4 toPivot,fromPivot;
            public int tentacle=-1;public float length;
            public Vector3 center,up;
        }
        readonly List<SoftSurface> softSurfaces=new List<SoftSurface>();
        readonly float[] electricLengths=new float[9],electricRadii=new float[9];
        Vector3 bellRight,bellForward,bellOrigin;float bellWidth,bellDepth,bellRise;
        SoftSurface bellSurface;
        LineRenderer[] bodyElectricLines;
        public int BodyElectricArcs=>bodyElectricLines==null?0:bodyElectricLines.Length;
        LineRenderer[] electricLines;
        Material electricMaterial;
        float fluidTime,fluidDeath;
        bool electricitySuppressed;
        public int ElectricStrands=>electricLines==null?0:electricLines.Length;
        Vector3 TentacleFlow(int index,Vector3 point,float length,float time,float death)
        {
            var down=tentacleDown[index].normalized;
            float t=Mathf.Clamp01(Vector3.Dot(point,down)/Mathf.Max(length,.000001f));
            float soft=t*t, phase=time*1.75f-t*5.2f-index*.63f;
            return point+length*soft*(1-death*.85f)*
                (radial[index]*(Mathf.Sin(phase)*.095f)+tangent[index]*(Mathf.Sin(phase*.83f-1)*.065f));
        }
        // Portal entrance starts with a zero-scale boss. World matrices are
        // singular then; compose only the local chain below the reference pivot.
        static Matrix4x4 MeshToAncestor(Transform child,Transform ancestor)
        {
            var matrix=Matrix4x4.identity;
            for(var current=child;current!=ancestor;current=current.parent)
            {
                if(current==null)throw new System.InvalidOperationException("Jelly mesh is outside its pose hierarchy.");
                matrix=Matrix4x4.TRS(current.localPosition,current.localRotation,current.localScale)*matrix;
            }
            return matrix;
        }
        void EnsureSoftSurfaces()
        {
            if(softSurfaces.Count>0)return;
            var up=bodyUp.normalized;bellRight=Vector3.Cross(up,Vector3.forward).normalized;
            if(bellRight.sqrMagnitude<.01f)bellRight=Vector3.right;bellForward=Vector3.Cross(up,bellRight).normalized;
            float minX=float.MaxValue,maxX=float.MinValue,minZ=float.MaxValue,maxZ=float.MinValue,minY=float.MaxValue,maxY=float.MinValue;
            foreach(var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null)continue;
                var s=new SoftSurface{filter=filter,source=filter.sharedMesh,mesh=Instantiate(filter.sharedMesh)};
                for(int k=0;k<9;k++)if(filter.transform.IsChildOf(tentacles[k])){s.tentacle=k;break;}
                var basis=s.tentacle>=0?tentacles[s.tentacle]:transform;
                s.toPivot=MeshToAncestor(filter.transform,basis);s.fromPivot=s.toPivot.inverse;
                s.rest=s.source.vertices;s.output=new Vector3[s.rest.Length];
                for(int i=0;i<s.rest.Length;i++)
                {
                    s.rest[i]=s.toPivot.MultiplyPoint3x4(s.rest[i]);
                    if(s.tentacle>=0)
                    {
                        var down=tentacleDown[s.tentacle].normalized;s.length=Mathf.Max(s.length,Vector3.Dot(s.rest[i],down));
                        electricRadii[s.tentacle]=Mathf.Max(electricRadii[s.tentacle],Vector3.ProjectOnPlane(s.rest[i],down).magnitude);
                    }
                    else
                    {
                        bellSurface=s;
                        maxY=Mathf.Max(maxY,Vector3.Dot(s.rest[i],up));
                        float x=Vector3.Dot(s.rest[i],bellRight),z=Vector3.Dot(s.rest[i],bellForward);
                        minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minZ=Mathf.Min(minZ,z);maxZ=Mathf.Max(maxZ,z);minY=Mathf.Min(minY,Vector3.Dot(s.rest[i],up));
                    }
                }
                if(s.tentacle>=0)electricLengths[s.tentacle]=Mathf.Max(electricLengths[s.tentacle],s.length);
                s.center=bodyCenter;s.up=bodyUp.normalized;
                s.mesh.name=s.source.name+" (jelly soft motion)";s.mesh.hideFlags=HideFlags.DontSave;s.mesh.MarkDynamic();
                filter.sharedMesh=s.mesh;softSurfaces.Add(s);
            }
            bellWidth=Mathf.Max(.000001f,(maxX-minX)*.5f);bellDepth=Mathf.Max(.000001f,(maxZ-minZ)*.5f);
            bellRise=Mathf.Max(.000001f,maxY-minY);
            bellOrigin=bellRight*((minX+maxX)*.5f)+bellForward*((minZ+maxZ)*.5f)+up*minY;
        }
        void SampleSoftBody(float time,float charge,float release,float death=0)
        {
            EnsureSoftSurfaces();fluidTime=time;fluidDeath=death;
            float cycle=Mathf.Repeat(time/2.7f,1);
            float pulse=Ease(0,.22f,cycle)*(1-Ease(.22f,1,cycle));
            float gather=release>0?0:Ease(0,.8f,charge);
            foreach(var s in softSurfaces)
            {
                for(int i=0;i<s.rest.Length;i++)
                {
                    var p=s.rest[i];
                    if(s.tentacle>=0)p=TentacleFlow(s.tentacle,p,s.length,time,death);
                    else
                    {
                        var delta=p-bellOrigin;float height=Vector3.Dot(delta,s.up);
                        var planar=delta-s.up*height;
                        // Bell contraction is quick; its relaxation takes most of the cycle.
                        float squeeze=(pulse*.15f+gather*.08f)*(1-death);
                        p=bellOrigin+planar*(1-squeeze-death*.16f)+s.up*height*(1+pulse*.1f-death*.62f);
                    }
                    s.output[i]=s.fromPivot.MultiplyPoint3x4(p);
                }
                s.mesh.vertices=s.output;s.mesh.RecalculateNormals();s.mesh.RecalculateBounds();
            }
            SampleElectricity(time,charge,release);
        }
        void SampleElectricity(float time,float charge,float release)
        {
            if(electricitySuppressed){DisableElectricity();return;}
            if(electricLines==null)
            {
                var shader=Resources.Load<Shader>("BossJellyElectric");if(shader==null)return;
                electricMaterial=new Material(shader){name="Jelly cyan current",hideFlags=HideFlags.DontSave};
                electricLines=new LineRenderer[9];
                for(int k=0;k<9;k++)
                {
                    var go=new GameObject("Tentacle electric flow"){layer=gameObject.layer,hideFlags=HideFlags.DontSave};
                    go.transform.SetParent(tentacles[k],false);
                    var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=40;
                    line.sharedMaterial=electricMaterial;line.shadowCastingMode=ShadowCastingMode.Off;
                    line.receiveShadows=false;line.numCornerVertices=2;line.numCapVertices=2;
                    electricLines[k]=line;
                }
            }
            float ultimate=AttackVariant==4?Mathf.Max(charge,release):0;
            SampleBodyElectricity(time,charge,release,ultimate);
            for(int k=0;k<9;k++)
            {
                var line=electricLines[k];float length=electricLengths[k];
                line.enabled=true;
                line.widthMultiplier=tentacles[k].TransformVector(tentacleDown[k].normalized*length).magnitude*(.013f+ultimate*.022f);
                float head=Mathf.Repeat(time*(.65f+ultimate*.8f)+k*.11f,1);
                for(int i=0;i<40;i++)
                {
                    float t=i/39f;
                    var point=tentacleDown[k].normalized*(length*t)+radial[k]*electricRadii[k]*1.04f;
                    point=TentacleFlow(k,point,length,time,fluidDeath);
                    float jag=Mathf.Sin(i*2.7f+time*18+k)*Mathf.Sin(t*Mathf.PI)*length*(.003f+ultimate*.009f);
                    point+=tangent[k]*jag;line.SetPosition(i,point);
                }
                var gradient=new Gradient();
                float tail=Mathf.Max(0,head-.16f),tip=Mathf.Min(1,head+.1f);
                gradient.SetKeys(new[]{new GradientColorKey(new Color(.02f,.25f,1),0),new GradientColorKey(new Color(.6f,1,1),head),new GradientColorKey(new Color(.02f,.25f,1),1)},
                    new[]{new GradientAlphaKey(.15f+ultimate*.3f,0),new GradientAlphaKey(.2f,tail),new GradientAlphaKey(1,head),new GradientAlphaKey(.2f,tip),new GradientAlphaKey(.15f+ultimate*.3f,1)});
                line.colorGradient=gradient;
            }
        }

        void DisableElectricity()
        {
            if(electricLines!=null)foreach(var line in electricLines)if(line!=null)line.enabled=false;
            if(bodyElectricLines!=null)foreach(var line in bodyElectricLines)if(line!=null)line.enabled=false;
        }
        void SampleBodyElectricity(float time,float charge,float release,float ultimate)
        {
            if(bellSurface==null)return;
            if(bodyElectricLines==null)
            {
                bodyElectricLines=new LineRenderer[6];
                for(int k=0;k<bodyElectricLines.Length;k++)
                {
                    var go=new GameObject("Bell electric arc "+k){layer=gameObject.layer,hideFlags=HideFlags.DontSave};
                    go.transform.SetParent(bellSurface.filter.transform,false);
                    var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=28;
                    line.sharedMaterial=electricMaterial;line.shadowCastingMode=ShadowCastingMode.Off;
                    line.receiveShadows=false;line.numCornerVertices=1;line.numCapVertices=2;
                    bodyElectricLines[k]=line;
                }
            }
            float cycle=Mathf.Repeat(time/2.7f,1);
            float pulse=Ease(0,.22f,cycle)*(1-Ease(.22f,1,cycle));
            float squeeze=pulse*.15f+(release>0?0:Ease(0,.8f,charge))*.08f;
            for(int k=0;k<bodyElectricLines.Length;k++)
            {
                var line=bodyElectricLines[k];line.enabled=true;
                float flicker=.35f+.65f*Mathf.Pow(.5f+.5f*Mathf.Sin(time*19+k*4.7f),3);
                line.widthMultiplier=Mathf.Min(transform.TransformVector(bellRight*bellWidth).magnitude,transform.TransformVector(bellForward*bellDepth).magnitude)*(.014f+ultimate*.022f);
                line.startColor=new Color(.12f,.55f,1,(.3f+ultimate*.5f)*flicker);
                line.endColor=new Color(.65f,1,1,(.6f+ultimate*.4f)*flicker);
                for(int i=0;i<28;i++)
                {
                    float t=i/27f;
                    float azimuth=k*Mathf.PI/3+time*.38f+t*(k%2==0?1.35f:-1.35f);
                    float elevation=.08f+Mathf.Sin(t*Mathf.PI)*(.6f+(k%3)*.23f);
                    float jitter=Mathf.Sin(i*2.31f+Mathf.Floor(time*14)*1.73f+k)*.028f*Mathf.Sin(t*Mathf.PI);
                    azimuth+=jitter;
                    float radialScale=Mathf.Cos(elevation)*(1.06f+jitter);
                    var point=bellOrigin+
                        (bellRight*Mathf.Cos(azimuth)*bellWidth+bellForward*Mathf.Sin(azimuth)*bellDepth)*radialScale*(1-squeeze)+
                        bodyUp.normalized*(Mathf.Sin(elevation)*bellRise*(1+pulse*.1f)+bellRise*.035f);
                    line.SetPosition(i,bellSurface.fromPivot.MultiplyPoint3x4(point));
                }
            }
        }

        void ReleaseSoftBody()
        {
            if(electricLines!=null)foreach(var l in electricLines)if(l!=null){l.gameObject.SetActive(false);DisposeSoft(l.gameObject);}
            if(bodyElectricLines!=null)foreach(var l in bodyElectricLines)if(l!=null){l.gameObject.SetActive(false);DisposeSoft(l.gameObject);}
            bodyElectricLines=null;bellSurface=null;
            electricLines=null;if(electricMaterial!=null)DisposeSoft(electricMaterial);electricMaterial=null;
            foreach(var s in softSurfaces){if(s.filter!=null && s.filter.sharedMesh==s.mesh)s.filter.sharedMesh=s.source;if(s.mesh!=null)DisposeSoft(s.mesh);}
            softSurfaces.Clear();electricitySuppressed=false;fluidDeath=fluidTime=0;
            for(int i=0;i<9;i++)electricLengths[i]=electricRadii[i]=0;
        }
        static void DisposeSoft(Object o){if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
        void OnDestroy(){ReleaseDeathMeshes();ReleaseSoftBody();}
    }
}

