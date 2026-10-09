using System;
using System.Collections.Generic;
using EndlessDescent.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // Spellmaker, enchanting table, guild hall and trainer. Each used to cycle on E and only moved on
    // when you could not afford what was showing, so with gold in the purse you only ever got the first
    [DisallowMultipleComponent]
    public class ServiceScreen : MonoBehaviour, IServiceScreen
    {
        GameObject panel;
        RectTransform list;
        Text header;
        Text notice;

        readonly List<GameObject> rows = new List<GameObject>();

        Func<IReadOnlyList<ServiceOption>> options;

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
                if (hud.GetComponentInChildren<ServiceScreen>(true) == null)
                    hud.gameObject.AddComponent<ServiceScreen>();
        }

        void Awake()
        {
            ServiceScreens.Current = this;

            panel = new GameObject("Service", typeof(RectTransform));
            panel.transform.SetParent(transform, false);

            RectTransform root = panel.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(620f, 420f);

            panel.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.97f);

            header = MenuWidgets.Label(root, new Vector2(20f, -16f), 480f, string.Empty, 17);
            header.color = new Color(0.95f, 0.85f, 0.5f);

            notice = MenuWidgets.Label(root, new Vector2(20f, -42f), 580f, string.Empty, 12);
            notice.color = new Color(0.75f, 0.9f, 0.7f);

            MenuWidgets.Button(root, new Vector2(520f, -14f), new Vector2(80f, 26f), Close).text = "Close";

            list = MenuWidgets.ScrollPanel(root, "Options", new Vector2(20f, -72f), new Vector2(580f, 330f));

            panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (ReferenceEquals(ServiceScreens.Current, this))
                ServiceScreens.Current = null;
        }

        public void Open(string title, Func<IReadOnlyList<ServiceOption>> source)
        {
            options = source;
            header.text = title;
            notice.text = string.Empty;

            if (!panel.activeSelf)
            {
                panel.SetActive(true);
                ScreenFocus.Take();
            }

            Rebuild();
        }

        public void Close()
        {
            if (!panel.activeSelf)
                return;

            options = null;
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

            if (options == null)
                return;

            float y = 0f;

            foreach (ServiceOption option in options())
            {
                ServiceOption captured = option;

                Text label = MenuWidgets.Button(list, new Vector2(0f, y), new Vector2(570f, 28f), () => Choose(captured));
                label.text = option.Text;
                label.fontSize = 13;

                if (!option.Available)
                {
                    label.color = new Color(0.5f, 0.5f, 0.5f);
                    label.transform.parent.GetComponent<Button>().interactable = false;
                }

                rows.Add(label.transform.parent.gameObject);
                y -= 32f;
            }

            MenuWidgets.Fit(list, -y);
        }

        void Choose(ServiceOption option)
        {
            if (!option.Available || option.Choose == null)
                return;

            notice.text = option.Choose() ?? string.Empty;
            Rebuild();
        }
    }
}
