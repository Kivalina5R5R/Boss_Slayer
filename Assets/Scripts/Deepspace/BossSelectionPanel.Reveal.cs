using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Deepspace
{
    // 2026-11-19: "ตอนฉากบนจอกำแพงร่วงเสร็จค่อย animation เปิดบอสทีละตัว ละเปิด select อันสุดท้าย". On the floor's card
    // look the boss cards pop in one at a time (row by row, left to right), then SELECT. Nothing can be picked until the
    // last one is in (IsRevealing). Scale and fade are on each slot itself (a CanvasGroup); the card inside keeps its
    // own scale for the picked card's grow.
    public sealed partial class BossSelectionPanel
    {
        const float RevealStep = .12f, RevealPop = .35f, SelectGap = .2f;

        Sequence reveal;

        public bool IsRevealing => reveal != null && reveal.IsActive();

        // Hides the cards and SELECT now and brings them in after `delay` seconds (unscaled).
        public void PlayReveal(float delay)
        {
            reveal?.Kill(true);
            if (!cardLayout) return;
            var order = new List<Transform>();
            foreach (var slot in slots) if (slot != null) order.Add(slot.transform);
            bool select = confirm != null && confirm.gameObject.activeSelf;
            if (select) order.Add(confirm.transform);

            reveal = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            for (int i = 0; i < order.Count; i++)
            {
                var item = order[i];
                var group = item.TryGetComponent(out CanvasGroup existing) ? existing : item.gameObject.AddComponent<CanvasGroup>();
                item.localScale = Vector3.zero;
                group.alpha = 0f;
                float start = delay + i * RevealStep + (select && i == order.Count - 1 ? SelectGap : 0f);
                reveal.Insert(start, item.DOScale(1f, RevealPop).SetEase(Ease.OutBack));
                reveal.Insert(start, group.DOFade(1f, RevealPop * .5f));
            }
            reveal.OnComplete(() => reveal = null);
        }

        // Hidden half-way (a kiosk CONFIRM): end the reveal so the cards are whole next time.
        void OnDisable()
        {
            if (reveal != null) reveal.Complete(true);
        }
    }
}
