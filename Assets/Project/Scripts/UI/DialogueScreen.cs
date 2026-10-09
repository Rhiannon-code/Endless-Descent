using System.Collections.Generic;
using EndlessDescent.Data;
using EndlessDescent.Quests;
using EndlessDescent.Simulation;
using EndlessDescent.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // A conversation, rather than the single key press that used to stand in for one. It installs
    // itself onto whatever HUD exists, the way the board and the trade screen do, so no scene has to
    // be rebuilt to gain it
    [DisallowMultipleComponent]
    public class DialogueScreen : MonoBehaviour, IDialogueScreen
    {
        GameObject panel;
        Text speaker;
        Text body;
        readonly List<GameObject> rows = new List<GameObject>();

        DialogueRunner runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            Attach();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        static void Attach()
        {
            foreach (PlayerHud hud in FindObjectsByType<PlayerHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (hud.GetComponentInChildren<DialogueScreen>(true) == null)
                    hud.gameObject.AddComponent<DialogueScreen>();
        }

        void OnDestroy()
        {
            if (ReferenceEquals(DialogueScreens.Current, this))
                DialogueScreens.Current = null;
        }

        void Awake()
        {
            DialogueScreens.Current = this;

            panel = new GameObject("Dialogue", typeof(RectTransform));
            panel.transform.SetParent(transform, false);

            RectTransform root = panel.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(820f, 460f);

            panel.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.97f);

            speaker = MenuWidgets.Label(root, new Vector2(20f, -16f), 700f, string.Empty, 18);
            speaker.color = new Color(0.95f, 0.85f, 0.5f);

            body = MenuWidgets.Label(root, new Vector2(20f, -50f), 780f, string.Empty, 14);
            body.alignment = TextAnchor.UpperLeft;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.rectTransform.sizeDelta = new Vector2(780f, 120f);

            panel.SetActive(false);
        }

        public void Open(DialogueRunner conversation)
        {
            runner = conversation;

            if (runner == null || runner.IsOver)
                return;

            panel.SetActive(true);
            ScreenFocus.Take();
            Rebuild();
        }

        public void Close()
        {
            if (!panel.activeSelf)
                return;

            runner = null;
            panel.SetActive(false);
            ScreenFocus.Release();
        }

        void Update()
        {
            if (panel.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }

        void Rebuild()
        {
            foreach (GameObject row in rows)
                Destroy(row);

            rows.Clear();

            if (runner == null || runner.IsOver)
            {
                Close();
                return;
            }

            speaker.text = runner.Speaker != null ? runner.Speaker.DisplayName : "Someone";
            body.text = runner.Text;

            float y = -180f;

            for (int i = 0; i < runner.Choices.Count; i++)
            {
                int index = i;

                Text caption = MenuWidgets.Button((RectTransform)panel.transform, new Vector2(20f, y),
                    new Vector2(780f, 30f), () => Choose(index), new Color(0.12f, 0.12f, 0.16f, 0.95f));

                caption.text = runner.TextOf(runner.Choices[i]);
                caption.fontSize = 14;

                rows.Add(caption.transform.parent.gameObject);
                y -= 34f;
            }
        }

        EndlessDescent.Player.CharacterSheet sheet;

        EndlessDescent.Player.CharacterSheet Sheet
        {
            get
            {
                if (sheet == null)
                    sheet = GameObject.FindGameObjectWithTag("Player")?.GetComponent<EndlessDescent.Player.CharacterSheet>();

                return sheet;
            }
        }

        // The action is taken before the screen is rebuilt, because opening a shop closes this and a
        // rebuild afterwards would put a dead conversation back on top of it
        void Choose(int index)
        {
            NpcActor talking = runner?.Speaker;
            DialogueAction action = runner.Choose(index);

            // Taking work on and handing it back are the formal exchanges, which is what Etiquette is
            if (action == DialogueAction.OfferQuest || action == DialogueAction.TurnInQuest)
                Sheet?.Use(EndlessDescent.Data.SkillId.Etiquette);

            if (action == DialogueAction.Leave)
            {
                Close();
                return;
            }

            if (action == DialogueAction.OpenTrade)
            {
                Close();
                OpenTrade(talking);
                return;
            }

            Rebuild();
        }

        static void OpenTrade(NpcActor talking)
        {
            Merchant merchant = talking != null ? talking.GetComponent<Merchant>() : null;

            if (merchant == null)
                return;

            TradeScreens.Current?.Open(merchant);
        }
    }
}
