using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // Saving, loading, options and going back to the front end. A tab of its own rather than a
    // seventh job on GameMenu, which already has six pages to keep straight
    [DisallowMultipleComponent]
    public class SystemPage : MonoBehaviour, IMenuPage
    {
        [SerializeField] SaveCoordinator saves;
        [SerializeField] RectTransform root;
        [SerializeField] string bootScene = "Boot";

        readonly List<GameObject> rows = new List<GameObject>();

        RectTransform slots;
        RectTransform options;
        OptionsScreen optionsScreen;
        Text notice;

        public string Title => "System";
        public RectTransform Root => root;

        void Awake()
        {
            if (root == null)
                return;

            MenuWidgets.Label(root, new Vector2(20f, -14f), 400f, "SYSTEM", 18)
                .color = new Color(0.95f, 0.85f, 0.5f);

            notice = MenuWidgets.Label(root, new Vector2(20f, -44f), 600f, string.Empty, 12);
            notice.color = new Color(0.74f, 0.72f, 0.66f);

            float y = -76f;
            y = Item(y, "Save", Save);
            y = Item(y, "Load", ShowSlots);
            y = Item(y, "Options", ShowOptions);
            Item(y, "Leave to the main menu", LeaveToMenu);

            slots = MenuWidgets.ScrollPanel(root, "Slots", new Vector2(320f, -76f), new Vector2(480f, 380f));
            options = MenuWidgets.Panel(root, "Options", new Vector2(320f, -76f), new Vector2(480f, 380f));

            optionsScreen = gameObject.AddComponent<OptionsScreen>();
            optionsScreen.Build(options);
            optionsScreen.Closed += () => options.gameObject.SetActive(false);

            slots.gameObject.SetActive(false);
            options.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (notice != null)
                notice.text = string.Empty;
        }

        float Item(float y, string caption, System.Action onClick)
        {
            MenuWidgets.Button(root, new Vector2(20f, y), new Vector2(280f, 34f), () => onClick()).text = caption;
            return y - 42f;
        }

        void Save()
        {
            if (saves == null)
                return;

            // To the second, two saves inside the same minute wrote over each other
            string slot = $"slot{System.DateTime.Now:yyyyMMdd-HHmmss}";
            notice.text = saves.Save(slot) ? $"Saved as {slot}." : "Could not save.";
        }

        void ShowOptions()
        {
            slots.gameObject.SetActive(false);
            options.gameObject.SetActive(true);
            optionsScreen.Refresh();
        }

        void ShowSlots()
        {
            options.gameObject.SetActive(false);
            slots.gameObject.SetActive(true);

            foreach (GameObject row in rows)
                Destroy(row);

            rows.Clear();

            List<string> found = SaveSystem.ListSlots();
            float y = -12f;

            if (found.Count == 0)
            {
                rows.Add(MenuWidgets.Label(slots, new Vector2(12f, y), 440f, "Nothing saved yet.").gameObject);
                return;
            }

            foreach (string slot in found)
            {
                string captured = slot;

                Text row = MenuWidgets.Button(slots, new Vector2(12f, y), new Vector2(440f, 30f),
                    () => Load(captured));

                row.text = slot;
                rows.Add(row.transform.parent.gameObject);
                y -= 36f;
            }

            MenuWidgets.Fit(slots, -y);
        }

        void Load(string slot)
        {
            if (saves == null)
                return;

            notice.text = saves.Load(slot) ? $"Loaded {slot}." : $"Could not load {slot}.";
        }

        // Back through the front door rather than straight into a fresh world, the boot scene is
        // where a save is chosen, and its loader is what holds the screen until the world is ready
        void LeaveToMenu()
        {
            GameStart.ClearForMenu();
            SceneManager.LoadScene(bootScene);
        }
    }
}
