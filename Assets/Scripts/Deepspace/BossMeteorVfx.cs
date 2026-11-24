using UnityEngine;

namespace Deepspace
{
    public sealed class BossMeteorVfx : MonoBehaviour
    {
        RectTransform target, rock;
        BossMeteorGraphic graphic;
        Vector2 origin;
        float diameter;
        public bool ImpactReached { get; private set; }
        public Vector2 RockPosition => rock!=null?rock.anchoredPosition:Vector2.zero;
        public void Initialize(RectTransform footprint)
        {
            target=footprint;diameter=Mathf.Min(target.rect.width,target.rect.height);
            var go=new GameObject("Falling Meteor",typeof(RectTransform),typeof(CanvasRenderer),typeof(BossMeteorGraphic));
            go.layer=target.gameObject.layer;rock=(RectTransform)go.transform;rock.SetParent(target,false);
            rock.anchorMin=rock.anchorMax=Vector2.one*.5f;rock.sizeDelta=Vector2.one*diameter*.7f;
            graphic=go.GetComponent<BossMeteorGraphic>();graphic.raycastTarget=false;
            // Cosmetic direction does not consume gameplay random numbers.
            float side=(GetInstanceID()&1)==0?1:-1;
            origin=new Vector2(side*diameter*.9f,diameter*2.2f);
        }
        public void Tick(float warningRemaining,float warningTotal)
        {
            if(rock==null)return;
            ImpactReached=warningRemaining<=0;
            if(!ImpactReached)
            {
                float t=1-Mathf.Clamp01(warningRemaining/Mathf.Max(.01f,warningTotal));
                rock.gameObject.SetActive(true);rock.anchoredPosition=Vector2.Lerp(origin,Vector2.zero,t*t);
                rock.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(origin.y,origin.x)*Mathf.Rad2Deg-90);
                rock.localScale=Vector3.one*Mathf.Lerp(.5f,1,t);
                graphic.Impact=false;graphic.Progress=t;
            }
            else
            {
                float t=Mathf.Clamp01(-warningRemaining/.4f);
                rock.anchoredPosition=Vector2.zero;rock.localRotation=Quaternion.identity;rock.localScale=Vector3.one;
                rock.gameObject.SetActive(t<1);graphic.Impact=true;graphic.Progress=t;
            }
            graphic.SetVerticesDirty();
        }
    }
}
