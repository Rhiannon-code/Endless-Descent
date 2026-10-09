using System.Collections.Generic;
using EndlessDescent.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // What the town has posted, with why each person wants it. The reason and the complication are on
    // the posting rather than hidden behind accepting it, a job you cannot judge is a chore
    [DisallowMultipleComponent]
    public class QuestBoardScreen : MonoBehaviour, IQuestBoardScreen
    {
        GameObject panel;
        Text header;
        readonly List<GameObject> rows = new List<GameObject>();

        QuestBoard board;

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
                if (hud.GetComponentInChildren<QuestBoardScreen>(true) == null)
                    hud.gameObject.AddComponent<QuestBoardScreen>();
        }

        void OnDestroy()
        {
            if (ReferenceEquals(QuestBoardScreens.Current, this))
                QuestBoardScreens.Current = null;
        }

        void Awake()
        {
            QuestBoardScreens.Current = this;

            panel = new GameObject("QuestBoard", typeof(RectTransform));
            panel.transform.SetParent(transform, false);

            RectTransform root = panel.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(860f, 560f);

            panel.AddComponent<Image>().color = new Color(0.06f, 0.05f, 0.04f, 0.97f);

            header = MenuWidgets.Label(root, new Vector2(20f, -16f), 700f, "POSTINGS", 18);
            MenuWidgets.Button(root, new Vector2(760f, -14f), new Vector2(80f, 26f), Close).text = "Close";

            panel.SetActive(false);
        }

        public void Open(QuestBoard posted)
        {
            board = posted;
            Rebuild();
            panel.SetActive(true);
            ScreenFocus.Take();
        }

        public void Close()
        {
            if (!panel.activeSelf)
                return;

            panel.SetActive(false);
            board = null;
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

            if (board == null)
                return;

            header.text = $"POSTINGS: {board.TownName.ToUpperInvariant()}";

            RectTransform root = (RectTransform)panel.transform;
            float y = -56f;

            if (board.Postings.Count == 0)
            {
                rows.Add(MenuWidgets.Label(root, new Vector2(20f, y), 800f,
                    "Nothing posted this week. Somebody will want something before long.").gameObject);
                return;
            }

            for (int i = 0; i < board.Postings.Count; i++)
            {
                QuestBoard.Posting posting = board.Postings[i];
                int index = i;

                Text take = MenuWidgets.Button(root, new Vector2(20f, y), new Vector2(820f, 92f),
                    () => Accept(index), new Color(0.12f, 0.11f, 0.09f, 0.95f));

                take.text = $"{posting.Offer.Title}   :   {posting.Giver.DisplayName}";
                take.fontSize = 14;
                take.alignment = TextAnchor.UpperLeft;

                Text why = MenuWidgets.Label(take.rectTransform, new Vector2(0f, -24f), 800f, posting.Offer.Reason, 11);
                why.color = new Color(0.78f, 0.74f, 0.64f);

                Text snag = MenuWidgets.Label(take.rectTransform, new Vector2(0f, -52f), 800f,
                    $"They are not saying: {posting.Offer.Complication}", 11);
                snag.color = new Color(0.72f, 0.62f, 0.52f);

                rows.Add(take.transform.parent.gameObject);
                y -= 100f;
            }
        }

        void Accept(int index)
        {
            if (board != null && board.Take(index))
                Rebuild();
        }
    }
}
