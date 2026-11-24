using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // 2026-11-19: "ทำ transition จากหน้าเลือก player ไปหน้าเลือกบอส เป็นดาบฟันลงมาเฉียงๆ ... ทั้งสองส่วนของจอก็ร่วงลง
    // เผยให้เห็นฉากเลือกบอสด้านหลัง". On the wall: a sword cut slashes down at a slant from the top (a little left of the
    // middle) to the bottom (a little right of it), as in the art team's sketch; the screen splits along it and both halves
    // fall away, showing the boss select behind. The screen being cut is cloned as a still into two halves, each masked by
    // a polygon on its side of the cut, so the live screen can be hidden at once. Removes itself when the halves are gone.
    public sealed class BossWallSlash : MonoBehaviour
    {
        // Where the cut enters the top and leaves the bottom, as fractions of the wall's width.
        const float TopX = .37f, BottomX = .59f;
        // Timeline (seconds): the blade crosses, the halves part a little, then they fall.
        const float SlashSeconds = .2f, PartSeconds = .3f, FallDelay = .15f, FallSeconds = 1.1f;
        // From the cut until the halves are gone (the floor's boss cards come in after it, BossSelectionPanel.Reveal.cs).
        public const float Duration = SlashSeconds + FallDelay + FallSeconds;
        // Sizes as fractions of the wall's height (width for the drift).
        const float BladeWidth = .022f, PartDistance = .015f, FallDistance = 1.5f, Drift = .05f;
        const float FallTilt = 10f, FlashSeconds = .35f;
        static readonly Color FlashColor = new(1f, .95f, .85f, .5f);

        RectTransform root;
        readonly RectTransform[] halves = new RectTransform[2];
        readonly Vector2[] homes = new Vector2[2];
        Vector2 partDirection;   // from the cut towards the left half
        BossSlashStreak streak;
        Image flash;
        float age, height, width;

        // Cuts `screen` (a full-wall panel under `wall`) and lets it fall. Does nothing when the screen is not showing.
        public static void Play(RectTransform wall, GameObject screen)
        {
            if (wall == null || screen == null || !screen.activeInHierarchy) return;
            var go = new GameObject("Wall Slash Transition", typeof(RectTransform)) { layer = wall.gameObject.layer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(wall, false);
            Stretch(rect, wall.pivot);
            go.AddComponent<BossWallSlash>().Build(screen);
        }

        void Build(GameObject screen)
        {
            root = (RectTransform)transform;
            var r = root.rect;
            width = r.width; height = r.height;
            float pad = Mathf.Max(width, height) * .1f;
            float top = r.yMax + pad, bottom = r.yMin - pad, left = r.xMin - pad, right = r.xMax + pad;
            var enter = new Vector2(Mathf.Lerp(r.xMin, r.xMax, TopX), r.yMax);
            var leave = new Vector2(Mathf.Lerp(r.xMin, r.xMax, BottomX), r.yMin);
            float XAt(float y) => leave.x + (y - leave.y) * (enter.x - leave.x) / (enter.y - leave.y);
            var cutTop = new Vector2(XAt(top), top);
            var cutBottom = new Vector2(XAt(bottom), bottom);

            halves[0] = Half(screen, "Left Half", new[] { new Vector2(left, bottom), new Vector2(left, top), cutTop, cutBottom }, 0);
            halves[1] = Half(screen, "Right Half", new[] { cutBottom, cutTop, new Vector2(right, top), new Vector2(right, bottom) }, 1);
            var down = (leave - enter).normalized;
            partDirection = new Vector2(down.y, -down.x);   // the cut's normal on the left side
            if (partDirection.x > 0f) partDirection = -partDirection;

            flash = NewChild<Image>("Flash");
            flash.color = Color.clear; flash.raycastTarget = false;
            streak = NewChild<BossSlashStreak>("Sword Cut");
            streak.raycastTarget = false;
            streak.Configure(Vector2.Lerp(enter, leave, -.08f), Vector2.Lerp(enter, leave, 1.08f), height * BladeWidth);
            streak.Show(0f, 1f);
            transform.SetAsLastSibling();
        }

        // One half: a still clone of the screen inside a mask shaped like its side of the cut, pivoting on its own centre.
        RectTransform Half(GameObject screen, string name, Vector2[] corners, int index)
        {
            var r = root.rect;
            Vector2 centre = Vector2.zero;
            foreach (var p in corners) centre += p;
            centre /= corners.Length;

            var go = new GameObject(name, typeof(RectTransform)) { layer = gameObject.layer };
            var half = (RectTransform)go.transform;
            half.SetParent(root, false);
            half.anchorMin = half.anchorMax = new Vector2(.5f, .5f);
            half.sizeDelta = r.size;
            half.pivot = new Vector2((centre.x - r.xMin) / r.width, (centre.y - r.yMin) / r.height);
            half.anchoredPosition = homes[index] = centre - r.center;

            var shape = go.AddComponent<UiPolygonGraphic>();
            shape.raycastTarget = false;
            var local = new Vector2[corners.Length];
            for (int i = 0; i < corners.Length; i++) local[i] = corners[i] - centre;
            shape.SetPoints(local);
            go.AddComponent<Mask>().showMaskGraphic = false;

            var still = Instantiate(screen, half, false);
            // A still: the screen's own animation needs its Build, which a clone does not get.
            if (still.TryGetComponent(out BossStartWall live)) Destroy(live);
            Stretch((RectTransform)still.transform, new Vector2(.5f, .5f));
            still.transform.localRotation = Quaternion.identity; still.transform.localScale = Vector3.one;
            still.SetActive(true);
            return half;
        }

        T NewChild<T>(string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = gameObject.layer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            Stretch(rect, root.pivot);   // same pivot as the root, so local points match the root's
            return go.AddComponent<T>();
        }

        static void Stretch(RectTransform rect, Vector2 pivot)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.pivot = pivot;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float t = age;
            float slash = Mathf.Clamp01(t / SlashSeconds);
            streak.Show(1f - Mathf.Pow(1f - slash, 3f), 1f - Mathf.Clamp01((t - SlashSeconds) / PartSeconds));
            float flashT = (t - SlashSeconds) / FlashSeconds;
            flash.color = flashT < 0f ? Color.clear : new Color(FlashColor.r, FlashColor.g, FlashColor.b, FlashColor.a * (1f - Mathf.Clamp01(flashT)));

            float part = Mathf.Clamp01((t - SlashSeconds) / PartSeconds);
            Vector2 apart = partDirection * height * PartDistance * (1f - (1f - part) * (1f - part));
            float fall = Mathf.Max(0f, t - SlashSeconds - FallDelay);
            float f = fall / FallSeconds;
            float drop = height * FallDistance * f * f;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;   // left half: away to the left and turning anticlockwise
                halves[i].anchoredPosition = homes[i] + apart * side + new Vector2(-side * width * Drift * f, -drop);
                halves[i].localEulerAngles = new Vector3(0f, 0f, side * FallTilt * f);
            }
            if (f >= 1f) Destroy(gameObject);
        }

        // The boss select puts itself on top when it is shown or refreshed: the falling halves stay over it.
        void LateUpdate()
        {
            if (transform.GetSiblingIndex() != transform.parent.childCount - 1) transform.SetAsLastSibling();
        }
    }
}
