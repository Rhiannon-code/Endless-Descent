using System.Collections;
using EndlessDescent.Data;
using EndlessDescent.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // Loads the world while the menus are up, and does not let go of the screen until the world is
    // actually ready to be stood in. The world scene holds six hundred coarse terrains, loading it
    // after BEGIN is a black screen of unknown length, and loading it behind the menu costs nothing
    // that was not already being spent choosing a class
    [DisallowMultipleComponent]
    public class BootLoader : MonoBehaviour
    {
        [SerializeField] string worldScene = "Ordovan";
        [SerializeField] MainMenuScreen menu;
        [SerializeField] CharacterCreationScreen creation;
        [SerializeField] RectTransform loading;
        [SerializeField] Text caption;
        [SerializeField] Slider bar;

        AsyncOperation load;
        bool entering;

        void Awake() => DontDestroyOnLoad(gameObject);

        void OnEnable()
        {
            if (menu != null)
            {
                menu.NewGameChosen += OnNewGame;
                menu.ContinueChosen += OnContinue;
            }

            if (creation != null)
                creation.Finished += OnCreated;
        }

        void OnDisable()
        {
            if (menu != null)
            {
                menu.NewGameChosen -= OnNewGame;
                menu.ContinueChosen -= OnContinue;
            }

            if (creation != null)
                creation.Finished -= OnCreated;
        }

        void Start()
        {
            if (loading != null)
                loading.gameObject.SetActive(false);

            creation?.Hide();

            // Held at the door rather than activated, the scene is ready long before the player is,
            // and activating it early would put the world up behind the menu for no reason
            load = SceneManager.LoadSceneAsync(worldScene);

            if (load == null)
            {
                Debug.LogError($"No scene called '{worldScene}' in the build. " +
                               "Run 'Endless Descent > Build Boot Scene', which adds both scenes to the build list.");
                return;
            }

            load.allowSceneActivation = false;
        }

        void Update()
        {
            if (menu == null || load == null || entering)
                return;

            // Unity stops a held load at 0.9 and spends the last tenth on activation
            menu.SetStatus(load.progress >= 0.9f
                ? "The world is ready when you are."
                : $"Loading the world… {Mathf.RoundToInt(load.progress / 0.9f * 100f)}%");
        }

        void OnNewGame() => creation?.Show();

        // The creator has already announced the character, this only has to open the door
        void OnCreated(CharacterBlueprint blueprint) => Enter();

        void OnContinue(string slot)
        {
            GameStart.Continue(slot);
            Enter();
        }

        void Enter()
        {
            if (entering)
                return;

            entering = true;
            creation?.Hide();
            menu?.Hide();

            if (loading != null)
                loading.gameObject.SetActive(true);

            StartCoroutine(EnterWorld());
        }

        IEnumerator EnterWorld()
        {
            Report(0f, "Loading the world");

            while (load != null && load.progress < 0.9f)
            {
                Report(load.progress / 0.9f * 0.5f, "Loading the world");
                yield return null;
            }

            Report(0.5f, "Opening the world");

            if (load != null)
            {
                load.allowSceneActivation = true;

                while (!load.isDone)
                    yield return null;
            }

            // The prewarm lives in the world scene, so it cannot be found until the scene is in
            WorldPrewarm prewarm = FindFirstObjectByType<WorldPrewarm>();

            while (prewarm != null && !prewarm.Finished)
            {
                Report(0.5f + prewarm.Progress * 0.5f, prewarm.Caption);
                yield return null;
            }

            Destroy(gameObject);
        }

        void Report(float progress, string what)
        {
            if (bar != null)
                bar.value = Mathf.Clamp01(progress);

            if (caption != null)
                caption.text = what;
        }
    }
}
