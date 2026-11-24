using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Deepspace
{
    public sealed partial class BossDragonAnimation
    {
        [Header("Inferno Phase Fire")]
        [Range(0f,2f)] public float phaseFireIntensity=1f;
        [Range(.002f,.04f)] public float phaseFireEdgeWidth=.012f;
        readonly List<GameObject> fireEdges=new List<GameObject>();
        Material edgeFireMaterial, flameMaterial;
        ParticleSystem phaseFlames;
        struct FireAnchor { public Transform part; public MeshFilter filter; public int vertex; public Vector3 point,normal; }
        FireAnchor[] fireAnchors;
        ParticleSystem.Particle[] fireParticles;
        bool fireSuppressed;
        const int FlameCount=144;

        void SamplePhaseFire(float time)
        {
            if(fireSuppressed || chestHeat<=0 || phaseFireIntensity<=0)
            {
                foreach(var edge in fireEdges)if(edge!=null)edge.SetActive(false);
                if(phaseFlames!=null)phaseFlames.Clear();
                return;
            }
            if(edgeFireMaterial==null)
            {
                var edgeShader=Resources.Load<Shader>("BossDragonFireEdge");
                var flameShader=Resources.Load<Shader>("BossDragonPhaseFlame");
                if(edgeShader==null || flameShader==null || rigRoot==null)return;
                edgeFireMaterial=new Material(edgeShader){name="Dragon inferno edges",hideFlags=HideFlags.DontSave};
                flameMaterial=new Material(flameShader){name="Dragon inferno flames",hideFlags=HideFlags.DontSave};
                var joints=new List<Transform>();
                joints.AddRange(parts);joints.AddRange(secondaryParts);
                var sources=new List<MeshFilter>();
                foreach(var joint in joints)
                {
                    var source=joint!=null?joint.GetComponent<MeshFilter>():null;
                    if(source==null || source.sharedMesh==null)continue;
                    sources.Add(source);
                    var edge=new GameObject("Inferno Fire Edge"){layer=joint.gameObject.layer,hideFlags=HideFlags.DontSave};
                    edge.transform.SetParent(joint,false);
                    edge.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;
                    var renderer=edge.AddComponent<MeshRenderer>();renderer.sharedMaterial=edgeFireMaterial;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    fireEdges.Add(edge);
                }
                if(sources.Count==0){ReleasePhaseFire();return;}
                fireAnchors=new FireAnchor[FlameCount];fireParticles=new ParticleSystem.Particle[FlameCount];
                for(int s=0;s<sources.Count;s++)
                {
                    var vertices=sources[s].sharedMesh.vertices;var normals=sources[s].sharedMesh.normals;
                    for(int i=s;i<FlameCount;i+=sources.Count)
                    {
                        int index=(int)(Mathf.Repeat(i*.618034f,1)*vertices.Length)%vertices.Length;
                        fireAnchors[i]=new FireAnchor{part=sources[s].transform,filter=sources[s],vertex=index,point=vertices[index],normal=normals[index]};
                    }
                }
                var go=new GameObject("Inferno Phase Flames"){layer=gameObject.layer,hideFlags=HideFlags.DontSave};
                go.transform.SetParent(transform,false);
                phaseFlames=go.AddComponent<ParticleSystem>();
                phaseFlames.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=phaseFlames.main;main.playOnAwake=false;main.maxParticles=FlameCount;
                main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=0;main.startSize3D=true;
                var emission=phaseFlames.emission;emission.enabled=false;
                var shape=phaseFlames.shape;shape.enabled=false;
                var pr=phaseFlames.GetComponent<ParticleSystemRenderer>();
                pr.renderMode=ParticleSystemRenderMode.Billboard;pr.sharedMaterial=flameMaterial;
                pr.shadowCastingMode=ShadowCastingMode.Off;pr.receiveShadows=false;
                phaseFlames.Pause();
            }
            float heat=chestHeat*phaseFireIntensity;
            float localHeight=AnimatedMesh!=null?AnimatedMesh.bounds.size.z:1;
            edgeFireMaterial.SetFloat("_Heat",heat);
            edgeFireMaterial.SetFloat("_Clock",time);
            edgeFireMaterial.SetFloat("_Width",localHeight*phaseFireEdgeWidth);
            flameMaterial.SetFloat("_Clock",time);
            foreach(var edge in fireEdges)if(edge!=null)edge.SetActive(true);
            float worldHeight=localHeight*transform.lossyScale.magnitude/Mathf.Sqrt(3);
            for(int i=0;i<FlameCount;i++)
            {
                var anchor=fireAnchors[i];if(anchor.part==null)continue;
                float seed=Mathf.Repeat(i*.618034f,1);
                float life=Mathf.Repeat(time*(.8f+seed*.5f)+seed,1);
                float fade=Mathf.Sin(life*Mathf.PI);
                Vector3 localPoint=anchor.point;
                if(wingSurfaces!=null)foreach(var wing in wingSurfaces)
                    if(wing!=null && wing.filter==anchor.filter && anchor.vertex<wing.vertices.Length)localPoint=wing.vertices[anchor.vertex];
                Vector3 position=anchor.part.TransformPoint(localPoint);
                position+=Vector3.up*(worldHeight*life*.065f);
                position+=anchor.part.TransformDirection(anchor.normal)*(worldHeight*.006f);
                var particle=new ParticleSystem.Particle();
                particle.position=position;particle.startLifetime=particle.remainingLifetime=10;
                particle.startSize3D=new Vector3(.022f,.065f,1)*worldHeight*(.65f+seed*.6f)*fade;
                particle.startColor=new Color(1,.55f,.12f,Mathf.Clamp01(heat*.65f)*fade);
                fireParticles[i]=particle;
            }
            phaseFlames.SetParticles(fireParticles,FlameCount);
        }
        void SuppressPhaseFire()
        {
            fireSuppressed=true;
            foreach(var edge in fireEdges)if(edge!=null)edge.SetActive(false);
            if(phaseFlames!=null)phaseFlames.Clear();
        }
        void ReleasePhaseFire()
        {
            foreach(var edge in fireEdges)if(edge!=null){edge.SetActive(false);DestroyHeatObject(edge);}
            fireEdges.Clear();
            if(phaseFlames!=null){phaseFlames.gameObject.SetActive(false);DestroyHeatObject(phaseFlames.gameObject);}
            if(edgeFireMaterial!=null)DestroyHeatObject(edgeFireMaterial);
            if(flameMaterial!=null)DestroyHeatObject(flameMaterial);
            edgeFireMaterial=null;flameMaterial=null;phaseFlames=null;
            fireAnchors=null;fireParticles=null;fireSuppressed=false;
        }
    }
}
