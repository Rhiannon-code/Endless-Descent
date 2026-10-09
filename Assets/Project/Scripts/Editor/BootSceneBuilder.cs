using System.Collections.Generic;
using EndlessDescent.Data;
using EndlessDescent.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace EndlessDescent.EditorTools
{
    // The scene the game opens on. It holds a menu, a creator and a loader, and nothing else, its
    // whole job is to be on screen inside a second while the world scene loads behind it
    public static class BootSceneBuilder
    {
        const string ContentRoot = "Assets/Project/Content/Greybox";
        const string SceneFolder = "Assets/Project/Scenes/Boot";
        const string ScenePath = "Assets/Project/Scenes/Boot/Boot.unity";
        const string WorldScenePath = "Assets/Project/Scenes/World/Ordovan.unity";

        [MenuItem("Endless Descent/Build Boot Scene")]
        public static void BuildBootScene()
        {
            // Rule 11: the build list is shared project settings, not this scene, and it is the one
            // thing here that changes something outside the file being written
            bool proceed = EditorUtility.DisplayDialog("Build the boot scene",
                $"Creates {ScenePath}.\n\n" +
                "It also REWRITES the scene list in Build Settings (ProjectSettings/EditorBuildSettings.asset) " +
                "so Boot is first and Ordovan second, the loader asks for the world scene by name and " +
                "cannot find it otherwise. Any other scenes in that list are kept, after those two.\n\n" +
                "The open scene is replaced, so save it first. Commit before you click if you have " +
                "unsaved work.",
                "Build", "Cancel");

            if (!proceed)
                return;

            EditorApplication.delayCall += BuildNow;
        }

        public static void BuildNow()
        {
            GameDatabase database = AssetDatabase.LoadAssetAtPath<GameDatabase>($"{ContentRoot}/GameDatabase.asset");

            if (database == null)
            {
                Debug.LogError("No GameDatabase. Run 'Endless Descent > Build Greybox Content' first.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldScenePath) == null)
            {
                Debug.LogError($"No world scene at {WorldScenePath}. " +
                               "Run 'Endless Descent > Build World Scene' first: the boot scene loads it by name.");
                return;
            }

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // NewScene unloads assets the new scene does not reference, so anything loaded before it
            // is stale and serialises as a silent null
            database = AssetDatabase.LoadAssetAtPath<GameDatabase>($"{ContentRoot}/GameDatabase.asset");

            Object.DestroyImmediate(GameObject.Find("Directional Light"));

            Camera camera = Object.FindFirstObjectByType<Camera>();
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.03f, 0.03f, 0.05f);
            }

            BuildEventSystem();

            Canvas canvas = BuildCanvas();
            RectTransform menuRoot = Full(canvas.transform, "MainMenu");
            RectTransform creationRoot = Full(canvas.transform, "CharacterCreation");
            RectTransform loadingRoot = Full(canvas.transform, "Loading");

            menuRoot.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 1f);
            creationRoot.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 0.99f);
            loadingRoot.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.03f, 1f);

            CharacterCreationScreen creation = creationRoot.gameObject.AddComponent<CharacterCreationScreen>();
            new AssetAuthoring(creation)
                .Ref("root", creationRoot)
                .Ref("database", database)
                .Save();

            MainMenuScreen menu = menuRoot.gameObject.AddComponent<MainMenuScreen>();
            new AssetAuthoring(menu)
                .Ref("root", menuRoot)
                .Ref("creation", creation)
                .Save();

            Text caption = MenuWidgets.Label(loadingRoot, new Vector2(80f, -480f), 800f, "Loading", 18);
            Slider bar = Bar(loadingRoot);

            BootLoader loader = canvas.gameObject.AddComponent<BootLoader>();
            new AssetAuthoring(loader)
                .Str("worldScene", "Ordovan")
                .Ref("menu", menu)
                .Ref("creation", creation)
                .Ref("loading", loadingRoot)
                .Ref("caption", caption)
                .Ref("bar", bar)
                .Save();

            creationRoot.gameObject.SetActive(false);
            loadingRoot.gameObject.SetActive(false);

            System.IO.Directory.CreateDirectory(SceneFolder);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            AddToBuildSettings();

            Debug.Log($"Boot scene built at {ScenePath}. Press Play from it: the menu comes up at once " +
                      "and Ordovan loads behind it. BEGIN activates the world and holds the screen " +
                      "until WorldPrewarm reports ready.");
        }

        // The loader asks for the world scene by name, and LoadSceneAsync returns null for a scene
        // that is not in the list, which looks exactly like a scene that will not load
        static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> ordered = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(WorldScenePath, true)
            };

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != ScenePath && existing.path != WorldScenePath)
                    ordered.Add(existing);
            }

            EditorBuildSettings.scenes = ordered.ToArray();
        }

        static Canvas BuildCanvas()
        {
            GameObject root = new GameObject("FrontEnd");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            root.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        // Without this nothing on the canvas is clickable, a GraphicRaycaster finds the button, but
        // there is no EventSystem to dispatch to it
        static void BuildEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            GameObject events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
        }

        static RectTransform Full(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static Slider Bar(RectTransform parent)
        {
            RectTransform root = MenuWidgets.Panel(parent, "Bar", new Vector2(80f, -520f), new Vector2(800f, 16f));

            Image track = root.gameObject.AddComponent<Image>();
            track.color = new Color(0.12f, 0.12f, 0.15f);

            RectTransform fillRect = MenuWidgets.Panel(root, "Fill", Vector2.zero, new Vector2(800f, 16f));
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fill = fillRect.gameObject.AddComponent<Image>();
            fill.color = new Color(0.72f, 0.62f, 0.34f);

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.fillRect = fillRect;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            return slider;
        }
    }
}
