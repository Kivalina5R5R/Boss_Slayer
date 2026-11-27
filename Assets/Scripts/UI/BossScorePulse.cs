using TMPro;
using UnityEngine;
namespace BossSlayer
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class BossScorePulse : MonoBehaviour
    {
        [Range(0,.15f)]public float amount=.045f;
        TMP_Text label;string previous;float elapsed=1, kick;Vector3 baseline;
        public void Kick(float strength=.12f){kick=strength;elapsed=0;}
        void Awake(){label=GetComponent<TMP_Text>();baseline=transform.localScale;}
        void OnEnable(){if(label!=null)previous=label.text;elapsed=1;}
        void LateUpdate(){if(label.text!=previous){previous=label.text;elapsed=0;}elapsed+=Time.deltaTime;
            float pulse=elapsed<.3f?Mathf.Sin(elapsed/.3f*Mathf.PI)*Mathf.Max(amount,kick):0;if(elapsed>=.3f)kick=0;transform.localScale=baseline*(1+pulse);}
        void OnDisable(){if(label!=null)transform.localScale=baseline;}
    }
}
