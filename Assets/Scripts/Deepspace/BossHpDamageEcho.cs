using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // A trailing lost-health layer. The authoritative fill and its layout are never animated here.
    [RequireComponent(typeof(Image))]
    public sealed class BossHpDamageEcho : MonoBehaviour
    {
        Image source, echo;
        float delayedScale, delayedFill, hold;
        public float DisplayedScale => delayedScale;
        void Awake()
        {
            source = GetComponent<Image>();
            var rect = BossUiRect.Make(transform.parent, "Boss HP recent damage");
            rect.SetSiblingIndex(transform.GetSiblingIndex());
            echo = rect.gameObject.AddComponent<Image>(); echo.raycastTarget = false;
            echo.color = new Color(1, .65f, .16f, .9f); ResetEcho();
        }
        void ResetEcho()
        { if (source == null) return; delayedScale = source.transform.localScale.x; delayedFill = source.fillAmount; hold = 0; }
        void LateUpdate() { Advance(Time.deltaTime); }
        public void Advance(float dt)
        {
            if (source == null || echo == null) return;
            echo.gameObject.SetActive(source.enabled && source.gameObject.activeInHierarchy);
            float scale = source.transform.localScale.x, fill = source.fillAmount;
            if (scale > delayedScale || fill > delayedFill) ResetEcho();
            else if (dt > 0)
            {
                if (scale < delayedScale - .001f || fill < delayedFill - .001f) hold += dt;
                else hold = 0;
                if (hold > .18f) { delayedScale = Mathf.MoveTowards(delayedScale, scale, dt * .9f); delayedFill = Mathf.MoveTowards(delayedFill, fill, dt * .9f); }
            }
            var a = source.rectTransform; var b = echo.rectTransform;
            b.anchorMin = a.anchorMin; b.anchorMax = a.anchorMax; b.pivot = a.pivot;
            b.sizeDelta = a.sizeDelta; b.anchoredPosition3D = a.anchoredPosition3D; b.localRotation = a.localRotation;
            b.localScale = new Vector3(delayedScale, a.localScale.y, a.localScale.z);
            echo.sprite = source.sprite; echo.type = source.type; echo.fillMethod = source.fillMethod;
            echo.fillOrigin = source.fillOrigin; echo.fillClockwise = source.fillClockwise; echo.fillAmount = delayedFill;
        }
        protected void OnEnable() { ResetEcho(); }
        void OnDisable() { if (echo != null) echo.gameObject.SetActive(false); ResetEcho(); }
        void OnDestroy() { if (echo != null) Destroy(echo.gameObject); }
    }
}
