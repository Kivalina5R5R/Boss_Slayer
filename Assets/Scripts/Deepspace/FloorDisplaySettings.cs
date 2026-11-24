using UnityEngine;

namespace Deepspace
{
    // Pixel layout for the LED floor on Display 3. Physical tracking calibration is independent.
    [DefaultExecutionOrder(-10000)]
    public sealed class FloorDisplaySettings : MonoBehaviour
    {
        // บาส 2026-09-30: back to the original 2816x1280 floor (was 2944x1408 from 2026-09-28).
        public const int Width = 2816;
        public const int Height = 1280;
        public static Vector2 Size => new Vector2(Width, Height);
        public static Rect LayoutRect => new Rect(-Width * .5f, -Height * .5f, Width, Height);

        void Awake() { Apply(GetComponent<Canvas>()); }

        public static void Apply(Canvas canvas)
        {
            if (canvas == null || canvas.targetDisplay != MultiDisplaySetup.FloorDisplay || canvas.renderMode != RenderMode.WorldSpace) return;
            var area = (RectTransform)canvas.transform;
            area.sizeDelta = Size;
            var camera = canvas.worldCamera;
            if (camera == null) return;
            camera.orthographic = true;
            camera.orthographicSize = area.rect.height * Mathf.Abs(area.lossyScale.y) * .5f;
            // Center in the image plane while preserving the camera's height above the floor.
            Vector3 center = area.TransformPoint(area.rect.center);
            Vector3 delta = center - camera.transform.position;
            camera.transform.position += Vector3.Project(delta, camera.transform.right) + Vector3.Project(delta, camera.transform.up);
        }
    }
}
