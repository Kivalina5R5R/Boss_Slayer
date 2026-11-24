using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    [DisallowMultipleComponent]
    public sealed class BossSafeZoneVfx : MonoBehaviour
    {
        Material material;
        static readonly int FadeId=Shader.PropertyToID("_Fade");
        static readonly int ActiveId=Shader.PropertyToID("_Active");

        public void Initialize(RectTransform view, int index)
        {
            var shader=Resources.Load<Shader>("BossSafeZone");
            if (shader==null) return;
            material=new Material(shader) { name="Safe zone protective ward" };
            float unit=Mathf.Max(1,Mathf.Min(view.rect.width,view.rect.height));
            material.SetVector("_Aspect",new Vector4(view.rect.width/unit,view.rect.height/unit,0,0));
            material.SetFloat("_Offset",index*1.7f);
            var image=view.GetComponent<Image>();
            image.material=material;
            image.color=Color.white;
            image.raycastTarget=false;
        }

        public void SetState(float activeRemaining, bool active)
        {
            if (material==null) return;
            material.SetFloat(FadeId,activeRemaining<0 ? Mathf.Clamp01(1+activeRemaining/0.45f) : 1);
            material.SetFloat(ActiveId,active ? 1 : 0);
        }

        void OnDestroy()
        {
            if (material==null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
    }
}
