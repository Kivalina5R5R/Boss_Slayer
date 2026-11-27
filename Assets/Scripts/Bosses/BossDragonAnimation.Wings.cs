using UnityEngine;
namespace BossSlayer
{
    public sealed partial class BossDragonAnimation
    {
        [Header("Wing Flex")]
        [Range(0f, 2f)] public float wingFlexAmount = 1f;
        [Range(0f, 1f)] public float wingTipLag = .55f;
        [Range(0f,2f)] public float wingMembraneFlow = 1f;
        WingSurface[] wingSurfaces;
        sealed class WingSurface
        {
            public MeshFilter filter;
            public Mesh source, mesh;
            public Vector3[] rest, normals, vertices, outputNormals;
            public float[] span, membrane;
            public float width, side;
            public int tip;
        }
        public bool TryGetWingTip(int side, out Vector3 point)
        {
            point=transform.position;
            if(wingSurfaces==null || side<0 || side>=wingSurfaces.Length)return false;
            var wing=wingSurfaces[side];
            if(wing==null || wing.filter==null || wing.vertices.Length==0)return false;
            point=wing.filter.transform.TransformPoint(wing.vertices[wing.tip]);
            return true;
        }
        void SampleWingFlex(float time, float charge, float release, float stun)
        {
            if (parts == null || parts.Length < 3) return;
            if (wingSurfaces == null)
            {
                wingSurfaces = new WingSurface[2];
                for (int k = 0; k < 2; k++)
                {
                    var filter = parts[k + 1] != null ? parts[k + 1].GetComponent<MeshFilter>() : null;
                    if (filter == null || filter.sharedMesh == null) continue;
                    var source = filter.sharedMesh;
                    var v = source.vertices;
                    var n = source.normals;
                    var surface = new WingSurface {
                        filter = filter, source = source, mesh = Instantiate(source),
                        rest = v, normals = n, vertices = new Vector3[v.Length],
                        outputNormals = new Vector3[v.Length], span = new float[v.Length],
                        membrane = new float[v.Length], side = k == 0 ? -1 : 1
                    };
                    surface.mesh.name = source.name + " (runtime flex)";
                    surface.mesh.hideFlags = HideFlags.DontSave;
                    surface.mesh.MarkDynamic();
                    float depth = .001f;
                    for (int i = 0; i < v.Length; i++)
                    {
                        v[i] = FromDragonMesh * v[i];
                        n[i] = FromDragonMesh * n[i];
                        surface.width = Mathf.Max(surface.width, Mathf.Abs(v[i].x));
                        depth = Mathf.Max(depth, v[i].z);
                    }
                    surface.width = Mathf.Max(.001f, surface.width);
                    for (int i = 0; i < v.Length; i++)
                    {
                        surface.span[i] = Mathf.Clamp01(Mathf.Abs(v[i].x) / surface.width);
                        if(surface.span[i]>surface.span[surface.tip])surface.tip=i;
                        // Distance behind the leading edge, measured in the wing plane.
                        float x=surface.span[i];
                        float leading=x<.316f?Mathf.Lerp(0,1.6f,x/.316f):Mathf.Lerp(1.6f,2.35f,(x-.316f)/.684f);
                        leading*=surface.width/4.75f;
                        surface.membrane[i]=Mathf.Clamp01((leading-v[i].y)/(surface.width*.58f));
                    }
                    filter.sharedMesh = surface.mesh;
                    wingSurfaces[k] = surface;
                }
            }
            float strength = wingFlexAmount * motionAmount * Mathf.Lerp(1f, .15f, Mathf.Clamp01(stun));
            foreach (var wing in wingSurfaces)
            {
                if (wing == null || wing.mesh == null) continue;
                for (int i = 0; i < wing.rest.Length; i++)
                {
                    float span = wing.span[i];
                    // Anchor the shoulder; curve progressively toward the tip with phase lag.
                    float bendWeight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.08f, 1f, span));
                    float phase = time * hoverWingSpeed - span * (1.1f+wingTipLag*2.2f) + wing.side*.08f;
                    float wind = Mathf.SmoothStep(0, 1, Mathf.Clamp01((charge - span * .12f) / .72f));
                    float recoil = 0;
                    if (release > 0)
                    {
                        float t = (1 - release) * 1.05f;
                        float delay = span * wingTipLag * .28f;
                        wind = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - delay) / .22f));
                        float age = Mathf.Max(0, t - delay);
                        recoil = Mathf.Sin(age * 10f) * Mathf.Exp(-age * 4f)
                            * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.7f, 1.05f, t)));
                    }
                    float angle = (Mathf.Sin(phase) * (17f + Mathf.Abs(Locomotion) * 5f)
                        - wind * 13f + recoil * 24f) * bendWeight * strength;
                    var bend = Quaternion.AngleAxis(wing.side * angle, Vector3.forward);
                    // Wrist and trailing edge lag behind the stronger upper spar.
                    float wrist=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,.95f,span));
                    float wristAngle=Mathf.Sin(phase-.8f)*18*wrist*strength;
                    var wristBend=Quaternion.AngleAxis(wing.side*wristAngle,Vector3.forward);
                    var point=wristBend*(bend*wing.rest[i]);
                    float membrane=wing.membrane[i];
                    float soft=membrane*membrane*bendWeight*strength*wingMembraneFlow;
                    float billow=Mathf.Sin(time*hoverWingSpeed*1.25f-span*3.4f-membrane*2.1f);
                    float ripple=Mathf.Sin(time*hoverWingSpeed*2.6f-span*5.2f-membrane*3.5f);
                    point.y+=wing.width*soft*(billow*.085f+ripple*.018f);
                    point.z+=wing.width*soft*(billow*.15f+ripple*.028f);
                    point.x-=wing.side*wing.width*soft*.045f*(.5f+.5f*billow);
                    bend=wristBend*bend;
                    wing.vertices[i] = ToDragonMesh * point;
                    wing.outputNormals[i] = ToDragonMesh * (bend * wing.normals[i]);
                }
                wing.mesh.vertices = wing.vertices;
                // Rebuild normals from the deformed surface so the billowing membrane shades correctly.
                wing.mesh.RecalculateNormals();
                wing.mesh.RecalculateBounds();
            }
        }
        void ReleaseWingMeshes()
        {
            if (wingSurfaces == null) return;
            foreach (var wing in wingSurfaces)
            {
                if (wing == null) continue;
                if (wing.filter != null && wing.filter.sharedMesh == wing.mesh)
                    wing.filter.sharedMesh = wing.source;
                if (wing.mesh != null)
                {
                    if (Application.isPlaying) Destroy(wing.mesh);
                    else DestroyImmediate(wing.mesh);
                }
            }
            wingSurfaces = null;
        }
        void OnDestroy() { ReleasePhaseFire(); ReleaseDeathMeshes(); ReleaseWingMeshes(); ReleaseChestHeat(); }
    }
}
