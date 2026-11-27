using UnityEngine;
using UnityEngine.Rendering;
namespace BossSlayer
{
    public sealed partial class BossDragonAnimation
    {
        public bool HeatedChest { get; private set; }
        float chestHeat, chestSampleTime = -1;
        Mesh chestMesh;
        Material chestMaterial;
        GameObject chestObject;
        public void SetHeatedChest(bool heated) { HeatedChest = heated; }

        void SampleChestHeat(float time)
        {
            float dt = chestSampleTime < 0 ? 0 : Mathf.Max(0, time - chestSampleTime);
            chestSampleTime = time;
            chestHeat = Mathf.MoveTowards(chestHeat, HeatedChest ? 1 : 0, dt * 1.2f);
            if (!HeatedChest && chestHeat <= 0) return;
            if (chestObject == null)
            {
                var body = parts != null && parts.Length > 0 ? parts[0] : null;
                var filter = body != null ? body.GetComponent<MeshFilter>() : null;
                var shader = GameArt.Load<Shader>("Art/Shaders/BossDragonChestHeat");
                if (filter == null || filter.sharedMesh == null || shader == null) return;
                chestMesh = Instantiate(filter.sharedMesh);
                chestMesh.name = "Dragon heated chest (runtime)";
                chestMesh.hideFlags = HideFlags.DontSave;
                var vertices = chestMesh.vertices;
                var normals = chestMesh.normals;
                var colors = new Color[vertices.Length];
                Vector3 min = new Vector3(float.MaxValue,float.MaxValue,float.MaxValue), max = -min;
                foreach(var vertex in vertices)
                {
                    var p = FromDragonMesh * vertex;
                    min = Vector3.Min(min,p); max = Vector3.Max(max,p);
                }
                for(int i=0;i<vertices.Length;i++)
                {
                    var p = FromDragonMesh * vertices[i];
                    float x = Mathf.Abs((p.x-(min.x+max.x)*.5f)/(max.x-min.x));
                    float y = Mathf.InverseLerp(min.y,max.y,p.y);
                    float z = Mathf.InverseLerp(min.z,max.z,p.z);
                    float mask = (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,.40f,x)))
                        * Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.33f,y)) * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1f,y)))
                        * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.30f,z)));
                    colors[i] = new Color(1,.035f,.002f,mask);
                    vertices[i] += normals[i] * (max.x-min.x) * .001f;
                }
                chestMesh.vertices=vertices; chestMesh.colors=colors; chestMesh.RecalculateBounds();
                chestMaterial = new Material(shader) { name="Dragon chest embers",hideFlags=HideFlags.DontSave };
                chestObject = new GameObject("Heated Chest") { layer=body.gameObject.layer, hideFlags=HideFlags.DontSave };
                chestObject.transform.SetParent(body,false);
                chestObject.AddComponent<MeshFilter>().sharedMesh=chestMesh;
                var renderer=chestObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=chestMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            chestMaterial.SetFloat("_Heat",chestHeat*(1.4f + Mathf.Sin(time*3.3f)*.18f));
        }
        void ReleaseChestHeat()
        {
            HeatedChest=false; chestHeat=0; chestSampleTime=-1;
            if(chestObject!=null) { chestObject.SetActive(false); DestroyHeatObject(chestObject); }
            if(chestMesh!=null)DestroyHeatObject(chestMesh);
            if(chestMaterial!=null)DestroyHeatObject(chestMaterial);
            chestObject=null;chestMesh=null;chestMaterial=null;
        }
        static void DestroyHeatObject(Object item)
        {
            if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);
        }
    }
}
