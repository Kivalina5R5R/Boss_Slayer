using System.Collections.Generic;
using UnityEngine;
namespace BossSlayer
{
    public sealed partial class BossJellyfishAnimation
    {
        public const float DeathPoseSeconds = 2.8f;
        Transform[] deathJoints;
        Quaternion[] deathRotations;
        Vector3[] deathScales;
        Vector3[] deathPositions;float deathStartTime;
        readonly List<DeathSurface> deathSurfaces=new List<DeathSurface>();
        sealed class DeathSurface
        {
            public MeshFilter filter;
            public Mesh source, mesh;
            public int[] triangles;
            public float[] order;
            public readonly List<int> remaining=new List<int>();
        }
        public void BeginDeathPose()
        {
            Defeat();electricitySuppressed=true;
            DisableElectricity();
            deathJoints=GetPoseTransforms();deathRotations=new Quaternion[deathJoints.Length];
            deathPositions=new Vector3[deathJoints.Length];
            for(int i=0;i<deathJoints.Length;i++){deathRotations[i]=deathJoints[i].localRotation;deathPositions[i]=deathJoints[i].localPosition;}
            deathStartTime=fluidTime;
        }
        public void SampleDeathPose(float age)
        {
            if(deathJoints==null)return;
            float t=Ease(0,DeathPoseSeconds,age);
            SampleSoftBody(deathStartTime+age*.3f,0,0,t);
            var right=Vector3.Cross(bodyUp,Vector3.forward).normalized;
            if(right.sqrMagnitude<.01f)right=Vector3.right;
            for(int i=0;i<visualRoots.Length;i++)
            {
                int n=System.Array.IndexOf(deathJoints,visualRoots[i]);
                visualRoots[i].localPosition=deathPositions[n]-bodyUp.normalized*bodyHeight*.16f*t;
                visualRoots[i].localRotation=Quaternion.AngleAxis(t*22,right)*deathRotations[n];
            }
            for(int i=0;i<9;i++)
            {
                int n=System.Array.IndexOf(deathJoints,tentacles[i]);
                tentacles[i].localRotation=Quaternion.Slerp(deathRotations[n],tentacleRotations[i],t);
            }
        }
        public void SampleDeathDissolve(float progress)
        {
            if(deathSurfaces.Count==0)
            {
                foreach(var filter in GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter.sharedMesh==null || filter.name=="Inferno Fire Edge")continue;
                    var surface=new DeathSurface{filter=filter,source=filter.sharedMesh,mesh=Instantiate(filter.sharedMesh)};
                    surface.mesh.name=surface.source.name+" (death dissolve)";
                    surface.mesh.hideFlags=HideFlags.DontSave;
                    surface.triangles=surface.mesh.triangles;
                    var vertices=surface.mesh.vertices;
                    surface.order=new float[surface.triangles.Length/6];
                    for(int q=0;q<surface.order.Length;q++)
                    {
                        var p=vertices[surface.triangles[q*6]];
                        surface.order[q]=Mathf.Repeat(Mathf.Sin(Vector3.Dot(p,new Vector3(12.9898f,78.233f,37.719f)))*43758.5453f,1);
                    }
                    surface.remaining.Capacity=surface.triangles.Length;
                    filter.sharedMesh=surface.mesh;deathSurfaces.Add(surface);
                }
            }
            progress=Mathf.Clamp01(progress);
            foreach(var surface in deathSurfaces)
            {
                surface.remaining.Clear();
                for(int q=0;q<surface.order.Length;q++)
                    if(progress<1 && surface.order[q]>=progress)
                        for(int j=0;j<6;j++)surface.remaining.Add(surface.triangles[q*6+j]);
                surface.mesh.SetTriangles(surface.remaining,0,false);
            }
        }

        void ReleaseDeathMeshes()
        {
            foreach(var surface in deathSurfaces)
            {
                if(surface.filter!=null && surface.filter.sharedMesh==surface.mesh)surface.filter.sharedMesh=surface.source;
                if(surface.mesh!=null){ if(Application.isPlaying)Destroy(surface.mesh);else DestroyImmediate(surface.mesh); }
            }
            deathSurfaces.Clear();deathJoints=null;deathRotations=null;
        }
    }
}
