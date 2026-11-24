using UnityEngine;

namespace Deepspace
{
    // An empty, centred 1x1 RectTransform child on the parent's layer. Was DeepspaceWeaponPresentation.MakeRect in
    // deepspace (the space shooter's presentation); Boss Slayer only ever needed this helper (2026-11-20).
    public static class BossUiRect
    {
        public static RectTransform Make(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = Vector2.one;
            return rect;
        }
    }
}
