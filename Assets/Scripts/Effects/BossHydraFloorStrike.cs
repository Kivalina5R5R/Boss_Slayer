using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace BossSlayer
{
    // Render the same skull/jaw meshes in floor UI coordinates, keeping the two display cameras independent.
    public sealed partial class BossHydraFloorStrike : MaskableGraphic
    {
        BossHydra hydra;
        Vector2[] targets;
        readonly List<HeadPart> parts=new List<HeadPart>();
        struct Face { public int a,b,c; public float depth; }
        readonly List<Face> sortedFaces=new List<Face>();
        
        // Neck colours: the old model's emerald, or the new model's own neck colour (บาส 2026-11-12: "คอยังเป็นสีเขียวอยู่เลยอัลติไฮดร้า").
        Color neckDark=new Color(.08f,.22f,.13f),neckLight=new Color(.3f,.53f,.28f);
        sealed class HeadPart{public int head;public Transform root,part;public Vector3[] vertices,normals;public Color[] colors;public int[] triangles;public bool recentre;}
        // The most common colour of the new model's neck voxels becomes the drawn neck (lit side), a third of it the shadow.
        void UseNeckColour(BossVoxelSkin skin)
        {
            var bone=skin.FirstBone("neck0");if(bone==null)return;
            skin.VoxelPart(bone.name,2,out _,out _,out var colors,out _);
            var count=new Dictionary<Color32,int>();Color32 best=default;int bestCount=0;
            foreach(var c in colors)
            {
                Color32 key=c;count.TryGetValue(key,out int n);count[key]=++n;
                if(n>bestCount){bestCount=n;best=key;}
            }
            if(bestCount==0)return;
            neckLight=((Color)best).gamma;neckLight.a=1;
            neckDark=neckLight*.35f;neckDark.a=1;
        }
        public void Initialize(BossHydra owner,Vector2[] points)
        {
            hydra=owner;targets=points;raycastTarget=false;
            var skin=BossVoxelSkin.ActiveOn(owner.transform);
            if(skin!=null)
            {
                // New model: each strike head is that head's voxels, seen from the old head's angle so the dive still reads.
                for(int i=0;i<7;i++)
                {
                    var bone=skin.FirstBone("head"+BossVoxelSkin.HydraOldToNew[i]);if(bone==null)continue;
                    skin.VoxelPart(bone.name,2,out var v,out var n,out var c,out var t);
                    parts.Add(new HeadPart{head=i,root=owner.Skull(i),part=bone,vertices=v,normals=n,colors=c,triangles=t,recentre=true});
                }
                UseNeckColour(skin);
                SetVerticesDirty();return;
            }
            for(int i=0;i<7;i++)
            {
                var head=owner.AttackEffectAnchor(i);
                foreach(var f in head.GetComponentsInChildren<MeshFilter>())
                {
                    var m=f.sharedMesh;parts.Add(new HeadPart{head=i,root=head,part=f.transform,vertices=m.vertices,normals=m.normals,colors=m.colors,triangles=m.triangles});
                }
            }
            SetVerticesDirty();
        }
        void LateUpdate(){if(hydra==null||!hydra.SevenHeadStrikeActive){Destroy(gameObject);return;}SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(hydra==null||targets==null||!hydra.SevenHeadStrikeActive||hydra.SevenHeadAge<BossHydra.SevenHeadChargeSeconds)return;
            float extension=hydra.SevenHeadExtension;
            var viewer=new Vector3(0,Mathf.Lerp(.55f,.95f,extension),-Mathf.Lerp(.7f,.22f,extension)).normalized;
            float hitAge=hydra.SevenHeadAge-BossHydra.SevenHeadImpactTime;
            for(int h=0;h<7;h++)DrawStrikeWater(vh,targets[h],h,hitAge,false);
            for(int head=0;head<7;head++)
            {
                Vector2 start=new Vector2(Mathf.Lerp(rectTransform.rect.xMin,rectTransform.rect.xMax,(head+.5f)/7),rectTransform.rect.yMax+110);
                Vector2 end=Vector2.Lerp(start,targets[head]+Vector2.up*72,extension);
                Vector2 direction=(targets[head]-start).normalized;
                Vector2 sideDirection=new Vector2(-direction.y,direction.x);
                if(hitAge<0)for(int trail=0;trail<5;trail++)
                {
                    float offset=(trail-2)*38;
                    var tip=end+sideDirection*offset;
                    Stroke(vh,tip-direction*(100+trail*32)*extension,tip,3+extension*4,new Color(.55f,1,1,extension*.65f));
                }
                // Compress into the floor, rebound once, then withdraw.
                float recoil=hitAge<0?0:Mathf.Sin(Mathf.Clamp01(hitAge/.28f)*Mathf.PI)*22;
                end+=Vector2.up*recoil;
                Vector2 previous=start;
                for(int ring=1;ring<=32;ring++)
                {
                    float t=ring/32f;
                    var next=Vector2.Lerp(start,end,t)+Vector2.right*Mathf.Sin(t*Mathf.PI)*Mathf.Sin(t*4-hydra.SevenHeadAge*3+head*.7f)*30;
                    var side=new Vector2(-(next-previous).y,(next-previous).x).normalized;
                    float width=Mathf.Lerp(28,46,t);
                    Color shade=Color.Lerp(neckDark,neckLight,.65f+.12f*Mathf.Sin(ring*1.5f));
                    for(int strip=0;strip<4;strip++)
                    {
                        float left=(strip/4f*2-1)*width,right=((strip+1)/4f*2-1)*width;
                        var tint=shade*(strip==0?.45f:strip==3?.6f:strip==1?1.25f:1f);tint.a=1;
                        int v=vh.currentVertCount;
                        vh.AddVert(previous+side*left,tint,Vector2.zero);vh.AddVert(previous+side*right,tint,Vector2.zero);
                        vh.AddVert(next+side*right,tint,Vector2.zero);vh.AddVert(next+side*left,tint,Vector2.zero);
                        vh.AddTriangle(v,v+1,v+2);vh.AddTriangle(v,v+2,v+3);
                    }
                    previous=next;
                }
                sortedFaces.Clear();
                foreach(var part in parts)
                {
                    if(part.head!=head)continue;
                    int first=vh.currentVertCount;var matrix=part.root.worldToLocalMatrix*part.part.localToWorldMatrix;
                    if(part.recentre&&part.vertices.Length>0)
                    {
                        // The new head sits elsewhere than the hidden one: centre it on the old head, at the old head's size.
                        var centre=Vector3.zero;foreach(var pv in part.vertices)centre+=matrix.MultiplyPoint3x4(pv);centre/=part.vertices.Length;
                        matrix=Matrix4x4.Translate(-centre)*matrix;
                    }
                    for(int i=0;i<part.vertices.Length;i++)
                    {
                        var p=matrix.MultiplyPoint3x4(part.vertices[i]);var n=matrix.MultiplyVector(part.normals[i]);
                        float light=.65f+.35f*Mathf.Max(0,Vector3.Dot(n,new Vector3(-.3f,.8f,-.5f).normalized));
                        Color tint=part.colors[i].gamma;tint.r*=light;tint.g*=light;tint.b*=light;tint.a=1;
                        float dive=Mathf.Clamp01(extension);
                        float headScale=Mathf.Lerp(.65f,1.18f,dive);
                        float compression=hitAge>=0?1-.20f*Mathf.Exp(-hitAge*16):1;
                        vh.AddVert(end+new Vector2(p.x,(p.z*Mathf.Lerp(.55f,.95f,dive)+p.y*Mathf.Lerp(.7f,.22f,dive))*compression)*195*headScale,tint,Vector2.zero);
                    }
                    for(int i=0;i<part.triangles.Length;i+=3)
                    {
                        int a=part.triangles[i],b=part.triangles[i+1],c=part.triangles[i+2];
                        var normal=matrix.MultiplyVector(part.normals[a]+part.normals[b]+part.normals[c]);
                        if(Vector3.Dot(normal,viewer)<=0)continue;
                        var center=matrix.MultiplyPoint3x4((part.vertices[a]+part.vertices[b]+part.vertices[c])/3);
                        sortedFaces.Add(new Face{a=first+a,b=first+b,c=first+c,depth=Vector3.Dot(center,viewer)});
                    }
                }
                sortedFaces.Sort((a,b)=>a.depth.CompareTo(b.depth));
                foreach(var face in sortedFaces)vh.AddTriangle(face.a,face.b,face.c);
                DrawStrikeWater(vh,targets[head],head,hitAge,true);
            }
        }
    }
}
