using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Deepspace
{
    // 2026-11-24 "ตัว UI ต้องอยู่ layout ด้านหน้าบอส": the wall HUD (boss HP / STUN / ULTIMATE bars, player HP row: the
    // "Panel == UI" panel) is drawn in front of the boss.
    // The boss renders through its own Overlay camera ("Camera == Wall Boss 3D", BossLayer only), stacked after the wall
    // camera that draws the whole wall canvas, so the boss always covered the wall's UI. Now the UI panel lives on its
    // own world-space canvas laid exactly over the wall canvas, on its own layer (HudLayer), drawn by one more Overlay
    // camera ("Camera == Wall HUD") stacked after the boss camera. The wall background stays behind the boss as before.
    // The HUD canvas shows only while the wall canvas is shown, as when the panel was part of it.
    public sealed partial class BossSlayerGameManager
    {
        // Free layer (8/9 wall/floor, 10-15 floor zones, 16 background, 17 boss: Visual3DBridge.cs).
        public const int WallHudLayer = 18;
        const string WallHudCanvasName = "Canvas == Wall HUD", WallHudCameraName = "Camera == Wall HUD";

        Canvas wallHudCanvas;
        Camera wallHudCamera;

        // From ConfigureBossDisplayCameras, after the boss camera is in the wall camera's stack.
        void EnsureWallHudInFrontOfBoss()
        {
            if (wallDisplayCamera == null || cutsceneArea == null || cutsceneUiPanel == null) return;
            var wallCanvas = cutsceneArea.GetComponentInParent<Canvas>();
            if (wallCanvas == null || wallCanvas.renderMode != RenderMode.WorldSpace) return;   // only the world-space wall is stacked

            // ---- the canvas: a twin of the wall canvas, same place, size and scale
            if (wallHudCanvas == null)
            {
                var wallRect = (RectTransform)wallCanvas.transform;
                var go = new GameObject(WallHudCanvasName, typeof(RectTransform), typeof(Canvas));
                go.layer = WallHudLayer;
                var rect = (RectTransform)go.transform;
                rect.SetParent(wallRect.parent, false);
                rect.anchorMin = wallRect.anchorMin; rect.anchorMax = wallRect.anchorMax; rect.pivot = wallRect.pivot;
                rect.sizeDelta = wallRect.sizeDelta; rect.anchoredPosition = wallRect.anchoredPosition;
                rect.localPosition = wallRect.localPosition; rect.localRotation = wallRect.localRotation; rect.localScale = wallRect.localScale;
                wallHudCanvas = go.GetComponent<Canvas>();
                wallHudCanvas.renderMode = RenderMode.WorldSpace;
                wallHudCanvas.worldCamera = wallDisplayCamera;
                wallHudCanvas.sortingOrder = wallCanvas.sortingOrder + 1;
                wallHudCanvas.additionalShaderChannels = wallCanvas.additionalShaderChannels;
                if (wallCanvas.TryGetComponent(out UnityEngine.UI.CanvasScaler wallScaler))
                {
                    var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
                    scaler.dynamicPixelsPerUnit = wallScaler.dynamicPixelsPerUnit;
                    scaler.referencePixelsPerUnit = wallScaler.referencePixelsPerUnit;
                }
                var keeper = go.AddComponent<WallHudVisibility>();
                keeper.Source = cutsceneArea.gameObject; keeper.Owner = this;
            }

            // ---- the UI panel moves onto it, keeping its layout (it stretches over its parent, which has the same size)
            if (cutsceneUiPanel.parent != wallHudCanvas.transform)
                cutsceneUiPanel.SetParent(wallHudCanvas.transform, false);
            SetLayerRecursively(wallHudCanvas.transform, WallHudLayer);

            // ---- the camera: a twin of the wall camera that sees only the HUD layer, last in the stack (after the boss)
            if (wallHudCamera == null)
            {
                var go = new GameObject(WallHudCameraName, typeof(Camera));
                go.transform.SetParent(wallDisplayCamera.transform, false);   // follows the wall camera (zoom, float)
                wallHudCamera = go.GetComponent<Camera>();
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderType = CameraRenderType.Overlay;
                data.renderPostProcessing = false;   // the UI keeps its own colours
            }
            KeepWallHudOnTop();
        }

        // Every frame (WallHudVisibility): framing follows the wall camera (its zoom / float), the wall camera never draws
        // the HUD layer itself, and the HUD camera stays LAST in the stack even if another overlay camera is added later.
        void KeepWallHudOnTop()
        {
            if (wallHudCamera == null || wallDisplayCamera == null) return;
            SyncWallHudCamera();
            wallDisplayCamera.cullingMask &= ~(1 << WallHudLayer);
            var stack = wallDisplayCamera.GetUniversalAdditionalCameraData().cameraStack;
            if (stack.Count > 0 && stack[stack.Count - 1] == wallHudCamera) return;
            stack.Remove(wallHudCamera);
            stack.Add(wallHudCamera);
        }

        // The HUD camera frames exactly what the wall camera frames.
        void SyncWallHudCamera()
        {
            if (wallHudCamera == null || wallDisplayCamera == null) return;
            var cam = wallHudCamera;
            cam.transform.localPosition = Vector3.zero; cam.transform.localRotation = Quaternion.identity; cam.transform.localScale = Vector3.one;
            cam.orthographic = wallDisplayCamera.orthographic;
            cam.orthographicSize = wallDisplayCamera.orthographicSize;
            cam.fieldOfView = wallDisplayCamera.fieldOfView;
            cam.nearClipPlane = wallDisplayCamera.nearClipPlane;
            cam.farClipPlane = wallDisplayCamera.farClipPlane;
            cam.rect = wallDisplayCamera.rect;
            cam.targetDisplay = wallDisplayCamera.targetDisplay;
            cam.clearFlags = CameraClearFlags.Depth;   // in front of everything drawn before it, the boss included
            cam.cullingMask = 1 << WallHudLayer;
            cam.backgroundColor = Color.clear;
            cam.depth = wallDisplayCamera.depth + 20f;
        }

        static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayerRecursively(root.GetChild(i), layer);
        }

        // Shows the HUD canvas only while the wall canvas is shown (the panel used to be inside it), and keeps anything
        // added to the panel later on the HUD layer, so the HUD camera draws it.
        sealed class WallHudVisibility : MonoBehaviour
        {
            public GameObject Source;
            public BossSlayerGameManager Owner;
            Canvas canvas;
            int lastChildCount = -1;

            void LateUpdate()
            {
                if (canvas == null) canvas = GetComponent<Canvas>();
                bool show = Source != null && Source.activeInHierarchy;
                if (canvas.enabled != show) canvas.enabled = show;
                if (Owner != null) Owner.KeepWallHudOnTop();
                int count = CountChildren(transform);
                if (count != lastChildCount) { lastChildCount = count; SetLayerRecursively(transform, WallHudLayer); }
            }

            static int CountChildren(Transform t)
            {
                int n = t.childCount;
                for (int i = 0; i < t.childCount; i++) n += CountChildren(t.GetChild(i));
                return n;
            }
        }
    }
}
