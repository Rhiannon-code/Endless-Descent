using EndlessDescent.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // The dark a transition happens in, with a caption that keeps moving so a long preparation reads as
    // work being done rather than a hang. Puts itself on whatever HUD the scene has
    [DisallowMultipleComponent]
    public class TransitionOverlay : MonoBehaviour
    {
        Image shade;
        Text caption;

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
            {
                if (hud.GetComponentInChildren<TransitionOverlay>(true) != null)
                    continue;

                GameObject overlay = new GameObject("TransitionOverlay", typeof(RectTransform));
                overlay.transform.SetParent(hud.transform, false);
                overlay.transform.SetAsLastSibling();
                overlay.AddComponent<TransitionOverlay>();
            }
        }

        void Awake()
        {
            RectTransform rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            shade = gameObject.AddComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0f);
            shade.raycastTarget = false;
            shade.enabled = false;

            caption = MenuWidgets.Label(transform, Vector2.zero, 900f, string.Empty, 20);
            caption.alignment = TextAnchor.MiddleCenter;
            caption.color = new Color(0.85f, 0.82f, 0.72f);
            caption.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            caption.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            caption.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            caption.rectTransform.anchoredPosition = new Vector2(0f, -40f);
            caption.enabled = false;
        }

        void LateUpdate()
        {
            float dark = Transition.Blackout;

            shade.enabled = dark > 0f;
            shade.color = new Color(0f, 0f, 0f, dark);

            bool showCaption = dark > 0.95f && !string.IsNullOrEmpty(Transition.Caption);
            caption.enabled = showCaption;

            if (showCaption)
                caption.text = Transition.Caption + new string('.', 1 + (int)(Time.unscaledTime * 2f) % 3);
        }
    }
}
