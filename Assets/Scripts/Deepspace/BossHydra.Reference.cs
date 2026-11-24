using System.Collections.Generic;
using UnityEngine;

namespace Deepspace
{
    public sealed partial class BossHydra
    {
        const float StrikeSeconds = 1.15f;
        const int NeckRings = 26, NeckSides = 12;
        readonly Transform[] jaws = new Transform[MaximumHeads];
        readonly Mesh[] neckMeshes = new Mesh[MaximumHeads];
        readonly Vector3[][] neckVertices = new Vector3[MaximumHeads][];
        readonly Vector3[][] neckCoordinates = new Vector3[MaximumHeads][];
        readonly float[] displayedCharge = new float[MaximumHeads];
        float deathTime;
        readonly Vector3[][] neckWorldPose = new Vector3[MaximumHeads][];
        readonly Vector3[][] neckPoseVelocity = new Vector3[MaximumHeads][];

        // Original sculpt inspired by the silhouette of the supplied aquatic hydra.
        // All meshes are generated locally; no source model or textures are imported.
        void BuildReferenceModel()
        {
            voxelMaterial = new Material(Resources.Load<Shader>("BossThemeSurface")) { name = "Hydra venom emerald" };
            materials.Add(voxelMaterial);
            Color skin = BossThemePalette.Hex(0x20563A), armor = BossThemePalette.Hex(0x3B8050);
            Color belly = BossThemePalette.Hex(0x8AAB79), fin = BossThemePalette.Hex(0x12382F);
            Color mouth = new Color(.27f,.095f,.075f), tooth = new Color(.78f,.75f,.57f);
            bodyRig = new GameObject("Hydra body motion").transform; bodyRig.SetParent(transform,false);
            Part("Body",bodyRig,new Vector3(0,-1.13f,.48f),new Vector3(3.1f,1.30f,2.1f),skin);
            Part("Haunch",bodyRig,new Vector3(0,-1.05f,1.1f),new Vector3(2.5f,.95f,1.9f),armor);
            for(int s=-1;s<=1;s+=2)
            {
                for(int f=0;f<4;f++)
                    Blade("Swimming fin",bodyRig,new Vector3(s*(.95f+f*.08f),-1.15f,.1f+f*.4f),
                        new Vector3(s*(2.2f-f*.12f),-1.5f,1.4f+f*.46f),.42f,fin);
            }
            for(int p=0;p<8;p++)
                Part("Breast scale",bodyRig,new Vector3((p-3.5f)*.31f,-1.18f,-.48f),new Vector3(.29f,.61f,.16f),belly);
            BakeParts(bodyRig);
            BuildSerpentineTail(skin,belly,fin);
            for(int i=0;i<MaximumHeads;i++)
            {
                var neck=new GameObject("Neck "+(i+1)).transform;neck.SetParent(transform,false);
                neck.localPosition=FrontNeckSocket(i);necks[i]=neck;
                BuildFlexibleNeck(i,skin,belly,fin);
                var head=new GameObject("Head "+(i+1)).transform;head.SetParent(neck,false);skulls[i]=head;
                Part("Skull",head,new Vector3(0,.03f,-.06f),new Vector3(.69f,.57f,.83f),skin);
                Part("Snout",head,new Vector3(0,-.06f,-.58f),new Vector3(.55f,.27f,.76f),armor);
                Part("Palate",head,new Vector3(0,-.205f,-.49f),new Vector3(.48f,.07f,.83f),mouth);
                Blade("Cranial sail",head,new Vector3(0,.16f,.22f),new Vector3(0,.78f,.56f),.35f,fin);
                var jaw=new GameObject("Articulated jaw").transform;jaw.SetParent(head,false);
                jaw.localPosition=new Vector3(0,-.19f,.06f);jaws[i]=jaw;
                Part("Jaw",jaw,new Vector3(0,-.055f,-.49f),new Vector3(.50f,.15f,.93f),belly);
                Part("Mouth lining",jaw,new Vector3(0,.028f,-.47f),new Vector3(.42f,.055f,.81f),mouth);
                for(int s=-1;s<=1;s+=2)
                {
                    Part("Eye",head,new Vector3(s*.29f,.13f,-.35f),new Vector3(.12f,.095f,.18f),BossThemePalette.Hex(0xC7F05B));
                    Part("Pupil",head,new Vector3(s*.32f,.135f,-.405f),new Vector3(.035f,.07f,.07f),Color.black);
                    var brow=Part("Heavy brow",head,new Vector3(s*.24f,.22f,-.33f),new Vector3(.22f,.105f,.37f),armor);
                    brow.localRotation=Quaternion.Euler(0,s*9,-s*12);
                    Blade("Cheek fin",head,new Vector3(s*.24f,0,.08f),new Vector3(s*.53f,.29f,.58f),.19f,fin);
                    for(int t=0;t<5;t++)
                    {
                        float z=-.25f-t*.135f;
                        Part("Upper fang",head,new Vector3(s*.225f,-.27f,z),new Vector3(.045f,.15f+(t%2)*.045f,.06f),tooth);
                        Part("Lower fang",jaw,new Vector3(s*.205f,.095f,z-.04f),new Vector3(.04f,.12f,.055f),tooth);
                    }
                }
                BakeParts(jaw);BakeParts(head,jaw);
            }
            built=true;
        }

        // บาส 2026-10-02: fins were flat triangles (read as low poly next to the voxel bosses). Now a staircase of voxel
        // blocks from the root to the tip, as wide (front-back) as the old fin and tapering the same way; the parent's
        // BakeParts merges them like every other part.
        void Blade(string name,Transform parent,Vector3 start,Vector3 tip,float width,Color color)
        {
            const float cell=.15f;
            int steps=Mathf.Max(2,Mathf.CeilToInt(Vector3.Distance(start,tip)/cell));
            for(int k=0;k<steps;k++)
            {
                float t=(k+.5f)/steps;
                float depth=Mathf.Max(cell,Mathf.Round(width*2*(1-t)/cell)*cell);
                Part(name,parent,Vector3.Lerp(start,tip,t),new Vector3(cell,cell,depth),color*(k%2==0?1f:.82f));
            }
        }

        // One block of a bendable ring mesh (neck / tail) in its (along, side, up) coordinates. Faces get their own
        // vertices (flat voxel shading) and both windings, so the block reads solid whichever way a ring frame turns.
        static void RingBlock(List<Vector3> coordinates,List<Color> colors,List<int> triangles,float u0,float u1,float y0,float y1,float z0,float z1,Color side,Color top,Color bottom)
        {
            void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
            {
                for(int pass=0;pass<2;pass++)
                {
                    int n=coordinates.Count;
                    coordinates.Add(a);coordinates.Add(b);coordinates.Add(c);coordinates.Add(d);
                    for(int v=0;v<4;v++)colors.Add(color.linear);
                    if(pass==0)triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});else triangles.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
                }
            }
            Face(new Vector3(u0,y0,z1),new Vector3(u1,y0,z1),new Vector3(u1,y1,z1),new Vector3(u0,y1,z1),top);
            Face(new Vector3(u0,y0,z0),new Vector3(u1,y0,z0),new Vector3(u1,y1,z0),new Vector3(u0,y1,z0),bottom);
            Face(new Vector3(u0,y0,z0),new Vector3(u1,y0,z0),new Vector3(u1,y0,z1),new Vector3(u0,y0,z1),side);
            Face(new Vector3(u0,y1,z0),new Vector3(u1,y1,z0),new Vector3(u1,y1,z1),new Vector3(u0,y1,z1),side);
            Face(new Vector3(u0,y0,z0),new Vector3(u0,y1,z0),new Vector3(u0,y1,z1),new Vector3(u0,y0,z1),side*.9f);
            Face(new Vector3(u1,y0,z0),new Vector3(u1,y1,z0),new Vector3(u1,y1,z1),new Vector3(u1,y0,z1),side*.9f);
        }

        void BuildFlexibleNeck(int index,Color skin,Color belly,Color fin)
        {
            var coordinates=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
            // บาส 2026-10-02: square voxel blocks, one per ring, alternating size and shade (was a smooth 12-sided tube
            // with a flat membrane - read as low poly). The dorsal crest is a row of block spikes.
            for(int ring=0;ring<NeckRings;ring++)
            {
                float u0=ring/(float)NeckRings,u1=(ring+1f)/NeckRings;
                float s=ring%2==0?.84f:.74f; float shade=ring%2==0?1f:.84f;
                RingBlock(coordinates,colors,triangles,u0,u1,-s,s,-s,s,skin*shade,skin*shade*1.06f,belly*shade);
                if(ring%2==0) RingBlock(coordinates,colors,triangles,u0,u1,-.26f,.26f,s-.05f,ring%4==0?1.75f:1.4f,fin,fin*1.15f,fin);
            }
            var mesh=new Mesh {name="Hydra continuous neck "+index};mesh.MarkDynamic();
            neckWorldPose[index]=new Vector3[NeckRings+1]; neckPoseVelocity[index]=new Vector3[NeckRings+1];
            neckCoordinates[index]=coordinates.ToArray();neckVertices[index]=new Vector3[coordinates.Count];
            mesh.vertices=neckVertices[index];mesh.SetColors(colors);mesh.SetTriangles(triangles,0);
            neckMeshes[index]=mesh;meshes.Add(mesh);
            necks[index].gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            necks[index].gameObject.AddComponent<MeshRenderer>().sharedMaterial=voxelMaterial;
        }

        Vector3 NeckPoint(int index,float t,float charge,float lunge,float quiet,float death)
        {
            float spread=index-3,phase=elapsed*.85f+index*1.14f;
            var end=new Vector3(spread*.79f,4.42f-Mathf.Abs(spread)*.38f,-1.04f);
            end+=new Vector3(Mathf.Sin(phase)*.28f+Mathf.Sin(phase*.57f)*.09f-swimSpeed*.28f,Mathf.Sin(phase*.8f)*.17f,Mathf.Cos(phase*.73f)*.24f)*quiet;
            float style=headAttackStyles[index];
            end+=new Vector3(spread*.055f*charge,charge*.32f,charge*.52f);
            end+=new Vector3((style==1||style==4?Mathf.Sign(spread)*.28f:0)*lunge,-(style==2||style==5?1.65f:.72f)*lunge,-1.75f*lunge);
            if(style>=6 && !SevenHeadStrikeActive)end+=ExtendedHeadOffset((int)style,charge,lunge,index)*quiet;
            if(SevenHeadStrikeActive)
            {
                float windup=Mathf.SmoothStep(0,1,Mathf.Clamp01(sevenHeadAge/SevenHeadChargeSeconds))*(1-SevenHeadExtension);
                end+=new Vector3(0,.42f*windup,.7f*windup);
                end=Vector3.Lerp(end,new Vector3(spread*.98f,-2.1f,-5.5f),SevenHeadExtension);
            }
            end=Vector3.Lerp(end,new Vector3(spread*.86f,.10f,-1.9f-Mathf.Abs(spread)*.12f),death);
            // The neck exits the front breast horizontally before curling upwards.
            var a=new Vector3(spread*.12f,.20f,-1.35f);
            var b=end+new Vector3(-spread*.35f,-1.35f,1.05f+charge*.25f);
            if(style>=6 && !SevenHeadStrikeActive)b+=ExtendedHeadBend((int)style,charge,lunge)*quiet;
            a=Vector3.Lerp(a,new Vector3(spread*.16f,.04f,-.8f),death);
            b=Vector3.Lerp(b,end+new Vector3(-spread*.15f,.12f,.55f),death);
            float u=1-t;var p=3*u*u*t*a+3*u*t*t*b+t*t*t*end;
            p.x+=Mathf.Sin(t*Mathf.PI)*Mathf.Sin(phase-t*4.2f)*.27f*quiet;
            p.x+=Mathf.Sin(t*Mathf.PI)*Mathf.Sin(swimPhase-t*2.2f+index*.24f)*Mathf.Abs(swimSpeed)*.3f*quiet;
            return p;
        }

        void PoseReferenceHead(int index,float charge,float quiet,float dt,float death=0)
        {
            displayedCharge[index]=Mathf.Lerp(displayedCharge[index],charge,1-Mathf.Exp(-12*dt));
            if(dt==0)displayedCharge[index]=charge;
            float c=displayedCharge[index],progress=1-strike[index]/StrikeSeconds;
            float lunge=strike[index]>0?(progress<.17f?Mathf.SmoothStep(0,1,progress/.17f):progress<.35f?1:1-Mathf.SmoothStep(0,1,(progress-.35f)/.65f)):0;
            lunge*=quiet;
            float stun=BossModelAnimation.StunWeight(stunMotion,stunMotionDuration);
            float stunAge=stunMotionDuration-stunMotion;
            float headDelay=Mathf.Abs(index-3)*.07f;
            float headStun=stun*Mathf.SmoothStep(0,1,Mathf.Max(0,stunAge-headDelay)/.24f);
            float droop=Mathf.Max(death,headStun*(.62f+.07f*Mathf.Sin(stunAge*2.4f-index*.6f)));
            var coordinates=neckCoordinates[index];var vertices=neckVertices[index];
            // Sample each ring once; reuse its moving frame for all surface vertices.
            for(int ring=0;ring<=NeckRings;ring++)
            {
                float t=ring/(float)NeckRings;
                var localTarget=NeckPoint(index,t,c,lunge,quiet,droop);
                if(!defeated) localTarget+=new Vector3(Mathf.Sin(stunAge*11-index*.9f-t*5)*.16f*Mathf.Exp(-stunAge*1.2f),0,Mathf.Sin(stunAge*2-index)*.08f)*headStun*t*t;
                var target=necks[index].TransformPoint(localTarget);
                bool resetPose=dt==0 || ring==0 || growth[index]<1 || necks[index].lossyScale.sqrMagnitude<.000001f;
                if(resetPose) { neckWorldPose[index][ring]=target; neckPoseVelocity[index][ring]=Vector3.zero; }
                else
                {
                    // Increasing world-space follow-through: the socket follows the chest,
                    // while the upper neck and head retain momentum when the body turns.
                    float lag=Mathf.Lerp(.055f,.26f,t)*(1+index*.025f);
                    lag=Mathf.Lerp(lag,.045f,Mathf.Max(c,Mathf.Max(lunge,SevenHeadExtension)));
                    neckWorldPose[index][ring]=Vector3.SmoothDamp(neckWorldPose[index][ring],target,ref neckPoseVelocity[index][ring],lag,Mathf.Infinity,dt);
                }
                centers[ring]=resetPose?localTarget:necks[index].InverseTransformPoint(neckWorldPose[index][ring]);
            }
            for(int ring=0;ring<=NeckRings;ring++)
            {
                var tangent=(centers[Mathf.Min(NeckRings,ring+1)]-centers[Mathf.Max(0,ring-1)]).normalized;
                sides[ring]=Vector3.ProjectOnPlane(Vector3.right,tangent).normalized;
                backs[ring]=Vector3.Cross(sides[ring],tangent).normalized;
            }
            for(int v=0;v<vertices.Length;v++)
            {
                var q=coordinates[v];int ring=Mathf.RoundToInt(q.x*NeckRings);
                float radius=Mathf.Lerp(.37f,.20f,Mathf.SmoothStep(0,1,q.x))+Mathf.Sin(q.x*Mathf.PI)*.022f;
                vertices[v]=centers[ring]+(sides[ring]*q.y+backs[ring]*q.z)*radius;
            }
            var mesh=neckMeshes[index];mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
            skulls[index].localPosition=centers[NeckRings];
            var rotation=Quaternion.Euler(-c*16+lunge*22+droop*48+Mathf.Sin(elapsed*.81f+index*1.1f)*4*quiet,Mathf.Sin(elapsed*.7f+index)*12*quiet+(index-3)*3,-(index-3)*2*quiet);
            if(headAttackStyles[index]>=6 && !SevenHeadStrikeActive)rotation*=Quaternion.Euler(ExtendedHeadAngles(headAttackStyles[index],c,lunge));
            if(SevenHeadStrikeActive)rotation=Quaternion.Slerp(rotation,Quaternion.Euler(-65,(index-3)*4,0),SevenHeadExtension);
            skulls[index].localRotation=dt==0?rotation:Quaternion.Slerp(skulls[index].localRotation,rotation,1-Mathf.Exp(-16*dt));
            float jaw=defeated?Mathf.Lerp(32,5,death):Mathf.Lerp(9,42,Mathf.Max(c,lunge));
            if(!defeated && headStun>0)jaw=Mathf.Lerp(jaw,18+Mathf.Sin(stunAge*4-index)*5,headStun);
            if(SevenHeadStrikeActive)
            {
                jaw=Mathf.Lerp(18,52,Mathf.Sin(Mathf.Clamp01(sevenHeadAge/SevenHeadImpactTime)*Mathf.PI*.5f));
                if(sevenHeadAge>=SevenHeadImpactTime)jaw=Mathf.Lerp(52,6,Mathf.Clamp01((sevenHeadAge-SevenHeadImpactTime)/.075f));
                if(sevenHeadAge>SevenHeadImpactTime+SevenHeadHoldSeconds)jaw=Mathf.Lerp(6,18,1-SevenHeadExtension);
            }
            jaws[index].localRotation=Quaternion.Euler(-jaw,0,0);
        }
        readonly Vector3[] centers=new Vector3[NeckRings+1],sides=new Vector3[NeckRings+1],backs=new Vector3[NeckRings+1];
    }
}
