using Fortal.Configuration;
using Fortal.Displays;
using Fortal.Players;
using Fortal.UWB;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fortal.Editor
{
    // Builds the UWB arena layout from Bounty Hunter into the open scene, without any artwork:
    // UWB systems, the floor (Display 1) and wall (Display 2) cameras, a plain black floor and wall,
    // and the player spawner. Sizes match the installation: an 11 x 5.26 floor seen from above and a
    // 1536 x 435 wall canvas standing along the back edge of the floor.
    // It only adds what is missing, so it is safe to run again.
    [InitializeOnLoad]
    public static class UwbArenaSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/UWBPlayer.prefab";
        private const string AutoBuildKey = "Fortal.BossSlayer.UwbArenaSceneBuilt";

        // The floor is 11 world units wide; its depth follows the 2944 x 1408 LED floor output.
        private static readonly Vector2 ArenaSize = new Vector2(11f, 5.2608695f);
        private const float FloorCameraSize = 2.6304348f;
        private const float WallCameraSize = 1.5576172f;
        private static readonly Vector2 WallCanvasSize = new Vector2(1536f, 435f);
        private const float WallCanvasScale = 0.0071614585f;
        private const float WallCanvasDepth = 2.52f;
        private static readonly Vector2 FloorCanvasSize = new Vector2(2944f, 1408f);
        private static readonly Color MapColor = Color.black;

        static UwbArenaSceneBuilder()
        {
            // Builds SampleScene once after this script is first imported.
            EditorApplication.delayCall += () =>
            {
                Scene scene = SceneManager.GetActiveScene();
                if (!scene.isLoaded || scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (EditorPrefs.GetBool(AutoBuildKey, false)) return;
                EditorPrefs.SetBool(AutoBuildKey, true);
                bool wasDirty = scene.isDirty;
                Build();
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
            };
        }

        [MenuItem("Tools/UWB Arena/Build Scene Layout")]
        public static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            var playerPrefab = AssetDatabase.LoadAssetAtPath<UWBPlayerController>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogError($"[UwbArenaSceneBuilder] Player prefab missing at {PlayerPrefabPath}");
                return;
            }

            RemoveDefaultMainCamera();
            UWBManager uwbManager = BuildSystems();
            BuildDisplays(out Camera wallCamera, out Camera floorCamera);
            Canvas wallCanvas = BuildMap(wallCamera, floorCamera, out Canvas floorCanvas);
            BuildPlayers(playerPrefab);
            BuildEventSystem();

            DisplayManager displayManager = Object.FindFirstObjectByType<DisplayManager>(FindObjectsInactive.Include);
            var displayData = new SerializedObject(displayManager);
            displayData.FindProperty("sideCamera").objectReferenceValue = wallCamera;
            displayData.FindProperty("floorCamera").objectReferenceValue = floorCamera;
            SetSingle(displayData.FindProperty("sideCanvases"), wallCanvas);
            SetSingle(displayData.FindProperty("floorCanvases"), floorCanvas);
            displayData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[UwbArenaSceneBuilder] Scene layout ready (UWB {uwbManager.name}). Game view: Display 1 floor, Display 2 wall.");
        }

        private static UWBManager BuildSystems()
        {
            GameObject systems = FindOrCreateRoot("Systems");
            GetOrAdd<UWBConfigManager>(systems);
            UWBManager manager = systems.GetComponent<UWBManager>();
            if (manager != null) return manager;

            manager = systems.AddComponent<UWBManager>();
            // Start in Simulation so the scene runs without hardware; switch to Serial on site.
            var data = new SerializedObject(manager);
            data.FindProperty("transportMode").enumValueIndex = (int)UWBTransportMode.Simulation;
            data.ApplyModifiedPropertiesWithoutUndo();
            return manager;
        }

        private static void BuildDisplays(out Camera wallCamera, out Camera floorCamera)
        {
            GameObject display = FindOrCreateRoot("Display");
            GetOrAdd<DisplayManager>(display);

            Transform wallRoot = FindOrCreateChild(display.transform, "Display2_Wall");
            wallCamera = FindOrCreateCamera(wallRoot, "SideCamera", out bool wallCreated);
            if (wallCreated)
            {
                wallCamera.transform.localPosition = new Vector3(0f, WallCameraSize, WallCanvasDepth - 10f);
                wallCamera.orthographic = true;
                wallCamera.orthographicSize = WallCameraSize;
                wallCamera.clearFlags = CameraClearFlags.SolidColor;
                wallCamera.backgroundColor = MapColor;
                wallCamera.targetDisplay = DisplayOutput.WallDisplayIndex;
            }

            Transform floorRoot = FindOrCreateChild(display.transform, "Display1_Floor");
            floorCamera = FindOrCreateCamera(floorRoot, "FloorCamera", out bool floorCreated);
            if (floorCreated)
            {
                floorCamera.transform.localPosition = new Vector3(0f, 10f, 0f);
                floorCamera.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                floorCamera.orthographic = true;
                floorCamera.orthographicSize = FloorCameraSize;
                floorCamera.clearFlags = CameraClearFlags.SolidColor;
                floorCamera.backgroundColor = MapColor;
                floorCamera.targetDisplay = DisplayOutput.FloorDisplayIndex;
                floorCamera.gameObject.AddComponent<AudioListener>();
            }
        }

        private static Canvas BuildMap(Camera wallCamera, Camera floorCamera, out Canvas floorCanvas)
        {
            GameObject map = FindOrCreateRoot("Map");
            Transform arena = FindOrCreateChild(map.transform, "Arena");
            floorCanvas = BuildFloorDisplay(arena, floorCamera);

            Transform existing = map.transform.Find("SideDisplay");
            Canvas wallCanvas = existing != null ? existing.GetComponent<Canvas>() : CreateWallCanvas(map.transform, wallCamera);
            BuildBlackBackground(wallCanvas.transform);
            return wallCanvas;
        }

        // World-space wall canvas standing upright along the back edge of the floor (L-shaped preview).
        private static Canvas CreateWallCanvas(Transform map, Camera wallCamera)
        {
            var canvasObject = new GameObject("SideDisplay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(map, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = wallCamera;
            canvas.targetDisplay = DisplayOutput.WallDisplayIndex;
            var rect = (RectTransform)canvasObject.transform;
            rect.sizeDelta = WallCanvasSize;
            rect.localScale = Vector3.one * WallCanvasScale;
            rect.localPosition = new Vector3(0f, WallCameraSize, WallCanvasDepth);
            canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 16f;
            return canvas;
        }

        // World-space floor canvas lying flat over the 11 x 5.26 arena, seen by the floor camera.
        private static Canvas BuildFloorDisplay(Transform arena, Camera floorCamera)
        {
            Transform floor = arena.Find("FloorDisplay");
            if (floor == null)
            {
                var canvasObject = new GameObject("FloorDisplay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(arena, false);
                canvasObject.transform.SetAsFirstSibling();
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = floorCamera;
                canvas.targetDisplay = DisplayOutput.FloorDisplayIndex;
                var rect = (RectTransform)canvasObject.transform;
                rect.sizeDelta = FloorCanvasSize;
                rect.localScale = Vector3.one * (ArenaSize.x / FloorCanvasSize.x);
                rect.localRotation = Quaternion.Euler(90f, 0f, 0f);
                rect.localPosition = Vector3.zero;
                floor = canvasObject.transform;
            }

            BuildBlackBackground(floor);
            return floor.GetComponent<Canvas>();
        }

        // A plain black Image filling the canvas, in place of Bounty Hunter's painted artwork.
        private static void BuildBlackBackground(Transform canvas)
        {
            if (canvas.Find("Background") != null) return;

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.layer = canvas.gameObject.layer;
            background.transform.SetParent(canvas, false);
            background.transform.SetAsFirstSibling();
            var rect = (RectTransform)background.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            var image = background.GetComponent<Image>();
            image.color = MapColor;
            image.raycastTarget = false;
        }

        private static void BuildPlayers(UWBPlayerController playerPrefab)
        {
            GameObject players = FindOrCreateRoot("Players");
            if (players.GetComponent<UWBPlayerSpawner>() != null) return;

            UWBPlayerSpawner spawner = players.AddComponent<UWBPlayerSpawner>();
            var data = new SerializedObject(spawner);
            data.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // The template's Main Camera would render onto Display 1 on top of the floor.
        private static void RemoveDefaultMainCamera()
        {
            GameObject mainCamera = GameObject.Find("Main Camera");
            if (mainCamera != null && mainCamera.transform.parent == null)
            {
                Undo.DestroyObjectImmediate(mainCamera);
            }
        }

        private static Camera FindOrCreateCamera(Transform parent, string name, out bool created)
        {
            Transform existing = parent.Find(name);
            created = existing == null;
            if (!created) return existing.GetComponent<Camera>();

            var cameraObject = new GameObject(name, typeof(Camera));
            cameraObject.transform.SetParent(parent, false);
            return cameraObject.GetComponent<Camera>();
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name) return root;
            }

            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Build UWB Arena Scene");
            return created;
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;

            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void SetSingle(SerializedProperty array, Object value)
        {
            array.arraySize = 1;
            array.GetArrayElementAtIndex(0).objectReferenceValue = value;
        }
    }
}
