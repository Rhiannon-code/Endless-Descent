using System;
using System.Collections.Generic;
using EndlessDescent.Core;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // The front of the game. It exists so that the world can be loading behind it (BootLoader): the
    // menu is up in the first second, and every second spent choosing a class is a second the ground
    // is being built in
    [DisallowMultipleComponent]
    public class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] RectTransform root;
        [SerializeField] CharacterCreationScreen creation;

        RectTransform menu;
        RectTransform slots;
        RectTransform options;
        Text status;
        OptionsScreen optionsScreen;

        readonly List<GameObject> slotRows = new List<GameObject>();

        public event Action NewGameChosen;
        public event Action<string> ContinueChosen;

        public RectTransform Root => root;

        void Awake()
        {
            menu = MenuWidgets.Panel(root, "Menu", new Vector2(80f, -120f), new Vector2(420f, 400f));
            slots = MenuWidgets.ScrollPanel(root, "Slots", new Vector2(540f, -120f), new Vector2(460f, 400f));
            options = MenuWidgets.Panel(root, "Options", new Vector2(540f, -120f), new Vector2(460f, 400f));

            MenuWidgets.Label(root, new Vector2(80f, -50f), 700f, "ENDLESS DESCENT", 34)
                .color = new Color(0.92f, 0.84f, 0.58f);

            status = MenuWidgets.Label(root, new Vector2(80f, -96f), 700f, string.Empty, 13);
            status.color = new Color(0.66f, 0.63f, 0.58f);

            float y = 0f;
            y = Item(y, "New game", () => { Hide(); NewGameChosen?.Invoke(); });
            y = Item(y, "Continue", ShowSlots);
            y = Item(y, "Options", ShowOptions);
            Item(y, "Quit", Quit);

            optionsScreen = gameObject.AddComponent<OptionsScreen>();
            optionsScreen.Build(options);
            optionsScreen.Closed += () => options.gameObject.SetActive(false);

            slots.gameObject.SetActive(false);
            options.gameObject.SetActive(false);
        }

        public void SetStatus(string text)
        {
            if (status != null)
                status.text = text;
        }

        // The front end owns the cursor while it is up. It has to claim it rather than assume it,
        // coming back here from the world arrives with PlayerLook's lock still on, and a locked
        // cursor puts every UI raycast off screen
        void OnEnable() => Claim();

        public void Show()
        {
            root.gameObject.SetActive(true);
            Claim();
        }

        public void Hide() => root.gameObject.SetActive(false);

        static void Claim()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        float Item(float y, string caption, Action onClick)
        {
            Text label = MenuWidgets.Button(menu, new Vector2(0f, y), new Vector2(400f, 44f), onClick,
                new Color(0.12f, 0.12f, 0.16f, 0.95f));

            label.text = caption;
            label.fontSize = 18;
            return y - 52f;
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

            foreach (GameObject row in slotRows)
                Destroy(row);

            slotRows.Clear();

            // Kept with the rows so it goes when they do, it was added again on every refresh
            Text heading = MenuWidgets.Label(slots, new Vector2(12f, -12f), 400f, "SAVED GAMES", 18);
            heading.color = new Color(0.95f, 0.85f, 0.5f);
            slotRows.Add(heading.gameObject);

            List<string> found = SaveSystem.ListSlots();
            float y = -50f;

            if (found.Count == 0)
            {
                slotRows.Add(MenuWidgets.Label(slots, new Vector2(12f, y), 420f,
                    "Nothing saved yet.").gameObject);
                return;
            }

            foreach (string slot in found)
            {
                string captured = slot;

                Text row = MenuWidgets.Button(slots, new Vector2(12f, y), new Vector2(420f, 34f),
                    () => { Hide(); ContinueChosen?.Invoke(captured); });

                row.text = Describe(slot);
                slotRows.Add(row.transform.parent.gameObject);
                y -= 40f;
            }

            MenuWidgets.Fit(slots, -y);
        }

        // Read off the file rather than kept in a second index, so a save deleted by hand does not
        // leave a row that opens nothing
        static string Describe(string slot)
        {
            GameSave save = SaveSystem.Load(slot);

            if (save == null)
                return $"{slot}  (unreadable)";

            int day = (int)(save.WorldMinutes / WorldClock.MinutesPerDay);
            return $"{slot}   :   day {day}";
        }

        // Application.Quit does nothing in the editor, which is correct, stopping play mode from
        // here would mean a runtime assembly referencing UnityEditor, and whether an asmdef gets
        // that reference in an editor compile is exactly the kind of thing the out of tree compile
        // check cannot see
        static void Quit() => Application.Quit();
    }
}
