using EndlessDescent.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    // A quest changing state with nothing on screen to say so is indistinguishable from nothing
    // happening, which is how a loop stops reading as a loop
    [DisallowMultipleComponent]
    public class QuestToast : MonoBehaviour
    {
        const float HoldSeconds = 5f;

        Text line;
        QuestJournal journal;
        float until;

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
                if (hud.GetComponentInChildren<QuestToast>(true) == null)
                    hud.gameObject.AddComponent<QuestToast>();
        }

        void Awake()
        {
            line = MenuWidgets.Label(transform, Vector2.zero, 900f, string.Empty, 15);
            line.alignment = TextAnchor.MiddleCenter;
            line.color = new Color(0.92f, 0.88f, 0.72f);

            RectTransform rect = line.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -80f);

            line.enabled = false;
        }

        void Start()
        {
            journal = FindFirstObjectByType<QuestJournal>();

            if (journal == null)
                return;

            journal.QuestStarted += OnStarted;
            journal.QuestAdvanced += OnAdvanced;
            journal.QuestCompleted += OnCompleted;
        }

        void OnDestroy()
        {
            if (journal == null)
                return;

            journal.QuestStarted -= OnStarted;
            journal.QuestAdvanced -= OnAdvanced;
            journal.QuestCompleted -= OnCompleted;
        }

        void OnStarted(Quest quest) => Say($"Taken: {quest.Title}");
        void OnAdvanced(Quest quest) => Say(quest.CurrentStage != null ? quest.CurrentStage.Title : quest.Title);
        void OnCompleted(Quest quest) => Say($"Done: {quest.Title}");

        void Say(string what)
        {
            line.text = what;
            line.enabled = true;
            until = Time.unscaledTime + HoldSeconds;
        }

        void Update()
        {
            if (line.enabled && Time.unscaledTime > until)
                line.enabled = false;
        }
    }
}
