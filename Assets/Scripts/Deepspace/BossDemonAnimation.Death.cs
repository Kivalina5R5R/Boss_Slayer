using System.Collections.Generic;
using UnityEngine;
namespace Deepspace
{
    public sealed partial class BossDemonAnimation
    {
        public const float DeathPoseSeconds = 3f;
        Transform[] deathJoints;
        Quaternion[] deathRotations;
        Vector3[] deathScales;
        Vector3 deathRigPosition;
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

            Defeat();SuppressRageFire();Transformation=-1;

            deathJoints=GetPoseTransforms();
            deathRotations=new Quaternion[deathJoints.Length];
            deathScales=new Vector3[deathJoints.Length];
            for(int i=0;i<deathJoints.Length;i++){deathRotations[i]=deathJoints[i].localRotation;deathScales[i]=deathJoints[i].localScale;}
            deathRigPosition=rigRoot.localPosition;
        }
        public void SampleDeathPose(float age)
        {
            if(deathJoints==null)return;
            float kneel=EaseD(0,1.2f,age),fold=EaseD(1.05f,DeathPoseSeconds,age);
            for(int i=0;i<deathJoints.Length;i++)
            {
                var j=deathJoints[i];Vector3 target=Vector3.zero;
                if(j==rigRoot)target=new Vector3(10*fold,0,-5*fold);
                if(j==joints[0])target=new Vector3(22*kneel+28*fold,0,0);
                if(j==joints[3])target=new Vector3(32*fold,0,0);
                if(j==joints[1]||j==joints[2])target=new Vector3(-18*kneel,0,j==joints[1]?12:-12);
                if(j==joints[6]||j==joints[7])target=new Vector3(-50*kneel,0,0);
                if(j==joints[8]||j==joints[9])target=new Vector3(-45*kneel,0,j==joints[8]?-8:8);
                if(j==joints[10]||j==joints[11])target=new Vector3(110*kneel,0,0);
                if(j==joints[5])target=new Vector3(-20*fold,0,0);
                j.localRotation=Quaternion.Slerp(deathRotations[i],ToMesh*Quaternion.Euler(target)*FromMesh,EaseD(0,.7f,age));
            }
            float h=AnimatedMesh!=null?AnimatedMesh.bounds.size.z:1;
            rigRoot.localPosition=deathRigPosition+ToMesh*new Vector3(0,-h*(.16f*kneel+.035f*fold),0);
        }
        public void SampleDeathDissolve(float progress)
        {
            if(deathSurfaces.Count==0)
            {
                foreach(var filter in rigRoot.GetComponentsInChildren<MeshFilter>(true))
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
        void OnDestroy(){ReleaseRageFire();ReleaseDeathMeshes();}
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
