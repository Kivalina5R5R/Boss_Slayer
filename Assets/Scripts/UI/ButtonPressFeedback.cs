using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // Attach to any Button (mouse click, LED floor via LedButtonPress, or anything else that
    // routes through Button.onClick — all of them fire the same event, so this one hook covers
    // every input path at once) — บาส 2026-08-25 wanted the same reaction ปุ่ม Ready gets
    // (scale punch) on every button in every game mode, but NOT the permanent scale+fade+hide,
    // since these buttons (Start/Restart/Exit, and any future one) need to stay pressable —
    // there's no more player-count select/toggle button that would need to persist after one
    // press either way. DOPunchScale returns to the original scale on its own; nothing to
    // reset/restore afterward the way the Ready Button's permanent pop needed.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ButtonPressFeedback : MonoBehaviour
    {
        [SerializeField] float punchScale = 0.15f;
        [SerializeField] float punchSeconds = 0.25f;
        [SerializeField] int vibrato = 6;
        [SerializeField, Range(0f, 1f)] float elasticity = 0.8f;

        Button button;
        Vector3 baseScale;
        Tween punchTween;

        // บาส 2026-08-26: lets an external idle scale-pulse (e.g. Restart/Exit's breathing
        // effect, DeepspaceGameManager) coexist with this punch instead of fighting it for
        // localScale — the pulse owner kills its own tween on OnPunchStart and starts a fresh
        // one on OnPunchComplete, so the two are never actually running at the same time on
        // the same property. Null for every button that doesn't wire an idle pulse — zero
        // behavior change for those (Start, Ready, etc.).
        public System.Action OnPunchStart;
        public System.Action OnPunchComplete;

        void Awake()
        {
            button = GetComponent<Button>();
            baseScale = transform.localScale;
            button.onClick.AddListener(PlayPunch);
        }

        // บาส 2026-10-02: for code that replaces button.onClick wholesale (new ButtonClickedEvent()) after
        // Awake already hooked the punch - call this afterwards to put it back. No-op before Awake.
        public void Rehook()
        {
            if (button == null) return;
            button.onClick.RemoveListener(PlayPunch);
            button.onClick.AddListener(PlayPunch);
        }

        void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(PlayPunch);
            }
            punchTween?.Kill();
        }

        // 2026-11-19: the same punch for input that does not click the button (Boss Slayer's walking players step onto
        // a card / SELECT, BossSlayerGameManager.PickBossUnderPlayers).
        public void Punch() => PlayPunch();

        void PlayPunch()
        {
            punchTween?.Kill();
            OnPunchStart?.Invoke();
            transform.localScale = baseScale;
            punchTween = transform
                .DOPunchScale(baseScale * punchScale, Mathf.Max(0.01f, punchSeconds), vibrato, elasticity)
                .SetTarget(transform)
                .OnComplete(() => OnPunchComplete?.Invoke());
        }
    }
}
