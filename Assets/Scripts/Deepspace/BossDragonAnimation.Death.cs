using System.Collections.Generic;
using UnityEngine;
namespace Deepspace
{
    public sealed partial class BossDragonAnimation
    {
        [HideInInspector] public float modelSizeMultiplier = 1f;
        public const float DeathPoseSeconds = 2f;
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
            SetPhaseRoar(-1);
            Defeat();
            SuppressPhaseFire();
            deathJoints=GetPoseTransforms();
            deathRotations=new Quaternion[deathJoints.Length];
            deathScales=new Vector3[deathJoints.Length];
            for(int i=0;i<deathJoints.Length;i++){deathRotations[i]=deathJoints[i].localRotation;deathScales[i]=deathJoints[i].localScale;}
            deathRigPosition=rigRoot.localPosition;
        }
        public void SampleDeathPose(float age)
        {
            if(deathJoints==null)return;
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/DeathPoseSeconds));
            for(int i=0;i<deathJoints.Length;i++)
            {
                var joint=deathJoints[i];Vector3 target=Vector3.zero;
                joint.localScale=Vector3.Lerp(deathScales[i],Vector3.one,t);
                if(joint==rigRoot)target=new Vector3(-12,0,-9);
                if(joint==parts[1])target=new Vector3(14,-12,60);
                if(joint==parts[2])target=new Vector3(14,12,-60);
                if(joint==parts[3])target=new Vector3(-28,0,0);
                if(joint==parts[4])target=new Vector3(-12,0,0);
                for(int k=0;k<secondaryParts.Length;k++)
                    if(joint==secondaryParts[k])
                        target=k<2?new Vector3(-42+k*8,0,0):k>=4?new Vector3(38,0,(k%2==0?-1:1)*10):new Vector3(6,0,0);
                joint.localRotation=Quaternion.Slerp(deathRotations[i],ToDragonMesh*Quaternion.Euler(target)*FromDragonMesh,t);
            }
            float height=AnimatedMesh!=null?AnimatedMesh.bounds.size.z:1;
            rigRoot.localPosition=deathRigPosition+ToDragonMesh*new Vector3(0,-height*.13f*t,0);
            if(chestMaterial!=null)chestMaterial.SetFloat("_Heat",chestHeat*(1-t*.8f));
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
                if(surface.mesh!=null)DestroyHeatObject(surface.mesh);
            }
            deathSurfaces.Clear();deathJoints=null;deathRotations=null;
        }
    }
}
