using TMPro;
using UnityEngine;

namespace BossSlayer
{
    // บาส 2026-11-18: a countdown number that drops in like Bounty Hunter's mini game 2 count-in (its CountdownDrop):
    // each number falls from big, shrinking faster and faster as it fades in, squashes as it lands and springs back,
    // holds, then fades out near the end of its second. Show(n) starts a new drop whenever the number (or the round it
    // belongs to) changes; the same number again just keeps playing.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class BossCountdownDrop : MonoBehaviour
    {
        const float StepSeconds = 1f;
        const float FallPart = .25f, DropFromScale = 3f, LandSquash = .15f;
        const float BounceEnd = .5f, FadeFrom = .8f;

        TMP_Text text;
        int shownNumber = -1, shownKey = -1;
        float age;

        // Shows number n; key tells two counts of the same number apart (e.g. the boss picked), so a new pick re-drops.
        public void Show(int n, int key)
        {
            if (text == null) text = GetComponent<TMP_Text>();
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (n == shownNumber && key == shownKey) return;
            shownNumber = n; shownKey = key; age = 0f;
            text.text = n.ToString();
            Pose();
        }

        public void Hide()
        {
            shownNumber = shownKey = -1;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            Pose();
        }

        void Pose()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            float t = Mathf.Clamp01(age / StepSeconds);
            var scale = Vector3.one;
            float alpha = 1f;
            if (t < FallPart)
            {
                float u = t / FallPart;
                scale *= Mathf.Lerp(DropFromScale, 1f, u * u);
                alpha = Mathf.Clamp01(u * 3f);
            }
            else if (t < BounceEnd)
            {
                float u = (t - FallPart) / (BounceEnd - FallPart);
                float spring = Mathf.Cos(u * Mathf.PI * 2.5f) * (1f - u) * (1f - u) * LandSquash;
                scale = new Vector3(1f + spring, 1f - spring, 1f);
            }
            else if (t > FadeFrom)
            {
                alpha = 1f - (t - FadeFrom) / (1f - FadeFrom);
            }
            transform.localScale = scale;
            text.alpha = alpha;
        }
    }
}
