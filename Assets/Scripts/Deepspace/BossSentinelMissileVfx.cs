using UnityEngine;
namespace Deepspace
{
 public sealed class BossSentinelMissileVfx:MonoBehaviour
 {
  Transform rocket;readonly Transform[] smoke=new Transform[9];Material shell,fire;
  public void Initialize(int layer)
  {
   gameObject.layer=layer;var shader=Shader.Find("Universal Render Pipeline/Unlit");
   shell=new Material(shader){hideFlags=HideFlags.DontSave};shell.SetColor("_BaseColor",new Color(.65f,.73f,.83f));
   fire=new Material(shader){hideFlags=HideFlags.DontSave};fire.SetColor("_BaseColor",new Color(3,1.1f,.08f));
   rocket=Make("Shoulder missile",PrimitiveType.Capsule,shell,layer);
   for(int i=0;i<smoke.Length;i++)smoke[i]=Make("Rocket exhaust",PrimitiveType.Sphere,fire,layer);
  }
  Transform Make(string name,PrimitiveType type,Material mat,int layer){var go=GameObject.CreatePrimitive(type);go.name=name;go.layer=layer;Destroy(go.GetComponent<Collider>());go.transform.SetParent(transform,false);go.GetComponent<Renderer>().sharedMaterial=mat;return go.transform;}
  static Vector3 Point(Vector3 start,Vector3 end,Vector3 up,float t,float lift)=>Vector3.Lerp(start,end,t)+up*Mathf.Sin(t*Mathf.PI)*lift;
  public void Sample(Vector3 start,Vector3 end,Vector3 up,float diameter,float progress)
  {
   float t=Mathf.Clamp01(progress);bool visible=progress>=0&&progress<1;
   rocket.gameObject.SetActive(visible);
   float lift=Vector3.Distance(start,end)*.35f;
   Vector3 point=Point(start,end,up,t,lift),next=Point(start,end,up,Mathf.Min(1,t+.015f),lift);
   rocket.position=point;if((next-point).sqrMagnitude>.000001f)rocket.rotation=Quaternion.FromToRotation(Vector3.up,next-point);
   rocket.localScale=new Vector3(diameter,diameter*2.7f,diameter);
   for(int i=0;i<smoke.Length;i++){smoke[i].gameObject.SetActive(visible&&t>i*.017f);smoke[i].position=Point(start,end,up,Mathf.Max(0,t-i*.017f),lift);smoke[i].localScale=Vector3.one*diameter*(1-i*.07f);}
  }
  void OnDestroy(){if(shell!=null)Destroy(shell);if(fire!=null)Destroy(fire);}
 }
}