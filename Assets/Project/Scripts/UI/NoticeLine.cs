using EndlessDescent.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // The last few things the game said, bottom centre, each fading after a few seconds. On unscaled
    // time, so a message posted while a menu has the world paused still shows and still goes
    [DisallowMultipleComponent]
    public class NoticeLine : MonoBehaviour
    {
        const int Lines = 4;
        const float ShowSeconds = 6f;
        const float FadeSeconds = 1f;

        readonly Text[] labels = new Text[Lines];
        readonly float[] until = new float[Lines];

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
                if (hud.GetComponentInChildren<NoticeLine>(true) == null)
                    hud.gameObject.AddComponent<NoticeLine>();
        }

        void Awake()
        {
            for (int i = 0; i < Lines; i++)
            {
                Text label = MenuWidgets.Label(transform, Vector2.zero, 1000f, string.Empty, 15);
                label.alignment = TextAnchor.MiddleCenter;

                RectTransform rect = label.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 150f + i * 22f);

                label.enabled = false;
                labels[i] = label;
            }
        }

        void OnEnable() => Notice.Posted += Add;
        void OnDisable() => Notice.Posted -= Add;

        // Newest at the bottom, everything older moves up a line and the oldest drops off
        void Add(string text)
        {
            for (int i = Lines - 1; i > 0; i--)
            {
                labels[i].text = labels[i - 1].text;
                labels[i].enabled = labels[i - 1].enabled;
                until[i] = until[i - 1];
            }

            labels[0].text = text;
            labels[0].enabled = true;
            until[0] = Time.unscaledTime + ShowSeconds;

            for (int i = 0; i < Lines; i++)
                Tint(i);
        }

        // Colour is only written while a line is fading or going, not every frame
        void Update()
        {
            for (int i = 0; i < Lines; i++)
            {
                if (!labels[i].enabled)
                    continue;

                if (Time.unscaledTime >= until[i])
                    labels[i].enabled = false;
                else if (until[i] - Time.unscaledTime < FadeSeconds)
                    Tint(i);
            }
        }

        void Tint(int i)
        {
            float alpha = Mathf.Clamp01((until[i] - Time.unscaledTime) / FadeSeconds);
            labels[i].color = new Color(0.92f, 0.90f, 0.80f, alpha);
        }
    }
}
