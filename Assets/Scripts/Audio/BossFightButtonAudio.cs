using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BossSlayer
{
    public sealed class BossFightButtonAudio : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        BossFightAudio audio;
        Button button;
        public void Bind(BossFightAudio owner, Button target) { audio = owner; button = target; }
        public void OnPointerEnter(PointerEventData data) { Preview(); }
        public void OnSelect(BaseEventData data) { Preview(); }
        void Preview()
        {
            if (audio != null && button != null && button.IsInteractable()) audio.Play("ui_hover", 0.18f, 0, 0.15f, 0, true);
        }
    }
}
