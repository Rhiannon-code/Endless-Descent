using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Player;
using EndlessDescent.World;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // One menu with tabs, rather than five screens on five hotkeys. Attributes, skills, spells, maps
    // and the journal all live behind it, the HUD keeps only what you need while walking
    [DisallowMultipleComponent]
    public class GameMenu : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] RectTransform root;
        [SerializeField] RectTransform tabBar;
        [SerializeField] Behaviour[] suspendWhileOpen;
        [SerializeField] MonoBehaviour[] pages;
        [SerializeField] bool captureCursorOnStart = true;

        readonly List<IMenuPage> tabs = new List<IMenuPage>();
        readonly List<Text> tabLabels = new List<Text>();

        int current;

        public bool IsOpen => root != null && root.gameObject.activeSelf;

        void Awake()
        {
            foreach (MonoBehaviour page in pages)
                if (page is IMenuPage menu) tabs.Add(menu);

            BuildTabs();

            if (root != null)
                root.gameObject.SetActive(false);
        }

        // Nothing else claims the mouse when the character creator is skipped, so the menu does it,
        // it is already the thing that hands the cursor back and forth
        void Start()
        {
            if (captureCursorOnStart)
                Suspend(false);
        }

        void BuildTabs()
        {
            if (tabBar == null)
                return;

            float x = 0f;

            for (int i = 0; i < tabs.Count; i++)
            {
                int captured = i;
                float width = 118f;

                Text label = MenuWidgets.Button(tabBar, new Vector2(x, 0f), new Vector2(width, 26f),
                    () => Show(captured), new Color(0.14f, 0.14f, 0.18f, 0.95f));

                label.text = tabs[i].Title;
                label.alignment = TextAnchor.MiddleCenter;
                tabLabels.Add(label);
                x += width + 4f;
            }
        }

        void Update()
        {
            if (input == null)
                return;

            if (input.InventoryPressed)
                Toggle(TabWithTitle("Inventory"));
            else if (input.MapPressed)
                Toggle(TabWithTitle("Local Map"));
            else if (input.CharacterPressed)
                Toggle(TabWithTitle("Character"));
            else if (input.JournalPressed)
                Toggle(TabWithTitle("Journal"));
        }

        int TabWithTitle(string title)
        {
            for (int i = 0; i < tabs.Count; i++)
                if (tabs[i].Title == title) return i;

            return 0;
        }

        // Pressing the key for the tab you are already on closes the menu, pressing another key
        // while it is open switches to that tab instead of shutting it in your face
        public void Toggle(int tab)
        {
            if (IsOpen && tab == current)
            {
                Close();
                return;
            }

            Open();
            Show(tab);
        }

        public void Open()
        {
            if (root == null)
                return;

            root.gameObject.SetActive(true);
            Suspend(true);
            Show(current);
        }

        public void Close()
        {
            if (root == null)
                return;

            root.gameObject.SetActive(false);
            Suspend(screens > 0);
        }

        int screens;

        // Daggerfall stops the world while a menu is open, the clock and everything in it. An active
        // journey is the one thing that keeps going, its map stays open to show the trail
        Journey journey;
        bool searchedForJourney;
        bool paused;

        bool Travelling
        {
            get
            {
                if (journey == null && !searchedForJourney)
                {
                    journey = FindFirstObjectByType<Journey>();
                    searchedForJourney = true;
                }

                return journey != null && journey.Travelling;
            }
        }

        void LateUpdate()
        {
            bool wanted = (IsOpen || screens > 0) && !Travelling;

            if (wanted != paused)
                Pause(wanted);
        }

        void Pause(bool value)
        {
            paused = value;
            Time.timeScale = value ? 0f : 1f;

            if (WorldClock.Instance != null)
                WorldClock.Instance.Paused = value;
        }

        // Leaving for the main menu destroys this with the world, and a time scale of zero would
        // follow the player into the next scene
        void OnDestroy()
        {
            if (paused)
                Pause(false);
        }

        // A screen opened from the world holds the same suspension the menu does, and counts, so
        // closing one of two does not hand the player back their sword while the other is up
        public void HoldForScreen(bool held)
        {
            screens = Mathf.Max(0, screens + (held ? 1 : -1));
            Suspend(IsOpen || screens > 0);
        }

        void Show(int tab)
        {
            current = Mathf.Clamp(tab, 0, Mathf.Max(0, tabs.Count - 1));

            for (int i = 0; i < tabs.Count; i++)
            {
                bool active = i == current;

                if (tabs[i].Root != null)
                    tabs[i].Root.gameObject.SetActive(active);

                if (i < tabLabels.Count)
                    tabLabels[i].color = active ? new Color(0.95f, 0.85f, 0.5f) : new Color(0.62f, 0.62f, 0.62f);
            }

            if (tabs.Count > 0)
                tabs[current].Refresh();
        }

        void Suspend(bool suspended)
        {
            if (suspendWhileOpen != null)
            {
                foreach (Behaviour behaviour in suspendWhileOpen)
                    if (behaviour != null) behaviour.enabled = !suspended;
            }

            Cursor.lockState = suspended ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = suspended;
        }
    }
}
