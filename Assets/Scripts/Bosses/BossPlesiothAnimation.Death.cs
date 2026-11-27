using System.Collections.Generic;
using UnityEngine;
namespace BossSlayer
{
    public sealed partial class BossPlesiothAnimation
    {
        public const float DeathPoseSeconds = 2.6f;
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

            Defeat();

            deathJoints=GetPoseTransforms();
            deathRotations=new Quaternion[deathJoints.Length];
            deathScales=new Vector3[deathJoints.Length];
            for(int i=0;i<deathJoints.Length;i++){deathRotations[i]=deathJoints[i].localRotation;deathScales[i]=deathJoints[i].localScale;}
            deathRigPosition=rigRoot.localPosition;
        }
        public void SampleDeathPose(float age)
        {
            if(deathJoints==null)return;
            float t=EaseFish(0,DeathPoseSeconds,age);
            float settle=EaseFish(.35f,DeathPoseSeconds,age);
            float flutter=Mathf.Sin(age*10-age*age*1.4f)*Mathf.Exp(-age*1.8f)*EaseFish(0,.16f,age);
            for(int i=0;i<deathJoints.Length;i++)
            {
                var joint=deathJoints[i];Vector3 target=Vector3.zero;
                if(joint==rigRoot)target=new Vector3(-12,8,82);
                if(joint==parts[1])target=new Vector3(12,8,48);
                if(joint==parts[2])target=new Vector3(-12,-8,-48);
                if(joint==parts[3])target=new Vector3(-18,0,0);
                if(joint==parts[4])target=new Vector3(-16,0,0);
                if(joint==parts[5])target=new Vector3(0,22,0);
                if(joint==secondaryParts[0])target=new Vector3(-12,0,0);
                if(joint==secondaryParts[1])target=new Vector3(0,26,0);
                if(joint==secondaryParts[2])target=new Vector3(0,18,0);
                joint.localRotation=Quaternion.Slerp(deathRotations[i],ToMesh*Quaternion.Euler(target)*FromMesh,t);
                if(joint==parts[5]||joint==secondaryParts[1]||joint==secondaryParts[2])
                    joint.localRotation*=ToMesh*Quaternion.Euler(0,flutter*12,0)*FromMesh;
            }
            float height=AnimatedMesh!=null?AnimatedMesh.bounds.size.z:1;
            rigRoot.localPosition=deathRigPosition+ToMesh*new Vector3(0,-height*.2f*settle,height*.035f*t);
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
