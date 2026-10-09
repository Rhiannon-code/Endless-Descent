using System.Collections.Generic;
using System.Text;
using EndlessDescent.Quests;
using EndlessDescent.World;
using UnityEngine;
using UnityEngine.UI;

namespace EndlessDescent.UI
{
    [DisallowMultipleComponent]
    public class QuestJournalScreen : MonoBehaviour, IMenuPage
    {
        [SerializeField] QuestJournal journal;
        [SerializeField] Text body;
        [SerializeField] RectTransform root;

        readonly List<GameObject> buttons = new List<GameObject>();

        PlaceLoader place;
        RectTransform rows;

        // The body and its buttons move into a scrolling list the first time it is drawn. The page was
        // a fixed 460 px, and every quest past the third ran off the bottom of it
        RectTransform Rows()
        {
            if (rows != null)
                return rows;

            RectTransform text = body.rectTransform;
            rows = MenuWidgets.ScrollPanel(text.parent, "Entries", text.anchoredPosition, text.sizeDelta);

            text.SetParent(rows, false);
            MenuWidgets.Anchor(text, Vector2.zero, text.sizeDelta);
            return rows;
        }

        public string Title => "Journal";
        public RectTransform Root => root;

        public void Refresh() => Rebuild();

        void OnEnable()
        {
            if (journal == null)
                return;

            journal.QuestStarted += OnChanged;
            journal.QuestAdvanced += OnChanged;
            journal.QuestCompleted += OnChanged;
            journal.QuestFailed += OnChanged;
            Rebuild();
        }

        void OnDisable()
        {
            if (journal == null)
                return;

            journal.QuestStarted -= OnChanged;
            journal.QuestAdvanced -= OnChanged;
            journal.QuestCompleted -= OnChanged;
            journal.QuestFailed -= OnChanged;
        }

        void OnChanged(Quest quest) => Rebuild();

        // The reason and complication are shown, not just the objective list
        void Rebuild()
        {
            if (journal == null || body == null)
                return;

            foreach (GameObject button in buttons)
                Destroy(button);

            buttons.Clear();

            StringBuilder text = new StringBuilder();
            IReadOnlyList<Quest> active = journal.Active;

            if (active.Count == 0)
                text.AppendLine("No active quests.");

            if (place == null)
                place = FindFirstObjectByType<PlaceLoader>();

            RectTransform list = Rows();
            int today = journal.Today;
            float y = 0f;

            foreach (Quest quest in active)
            {
                int startedAt = Lines(text);
                text.AppendLine(quest.Title);

                // Which of how many, because a quest that silently moved on reads as one that broke
                if (quest.Stages.Count > 1)
                    text.AppendLine($"  Step {quest.CurrentStageIndex + 1} of {quest.Stages.Count}" +
                                    $"{Due(quest, today)}");
                else if (quest.DueDay.HasValue)
                    text.AppendLine($" {Due(quest, today).TrimStart()}");

                if (!string.IsNullOrEmpty(quest.Reason))
                    text.AppendLine($"  Why: {quest.Reason}");

                if (!string.IsNullOrEmpty(quest.Complication))
                    text.AppendLine($"  But: {quest.Complication}");

                QuestStage stage = quest.CurrentStage;
                if (stage != null)
                {
                    if (!string.IsNullOrEmpty(stage.Summary) && quest.Stages.Count > 1)
                        text.AppendLine($"  {stage.Summary}");

                    foreach (QuestObjective objective in stage.Objectives)
                    {
                        string tick = objective.IsComplete ? "x" : " ";
                        text.AppendLine($"  [{tick}] {objective.Description} ({objective.Progress}/{objective.Data.Count})");
                    }
                }

                // Where to go, which Daggerfall never told you and its modern version had to add
                if (place != null && QuestDirections.TryFind(quest, place.Region, place.HereKm,
                        out QuestDirections.Heading heading))
                {
                    float km = Vector2.Distance(place.HereKm, heading.Km);
                    text.AppendLine($"  -> {heading.Place}, {km:0} km");
                }

                string questId = quest.Id;

                Text drop = MenuWidgets.Button(list, new Vector2(640f, y), new Vector2(120f, 24f),
                    () => { journal.Abandon(questId); Rebuild(); }, new Color(0.22f, 0.12f, 0.12f, 0.95f));

                drop.text = "Give up";
                drop.fontSize = 12;
                buttons.Add(drop.transform.parent.gameObject);

                text.AppendLine();

                // The button has to sit beside the block of text this quest just wrote, and the only
                // thing that knows how tall that block is, is how many lines went into it
                y -= (Lines(text) - startedAt) * LineHeight;
            }

            body.text = text.ToString();

            float height = Lines(text) * LineHeight + 20f;
            body.rectTransform.sizeDelta = new Vector2(body.rectTransform.sizeDelta.x, height);
            MenuWidgets.Fit(list, height);
        }

        const float LineHeight = 16f;

        static int Lines(StringBuilder text)
        {
            int count = 0;

            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n') count++;

            return count;
        }

        static string Due(Quest quest, int today)
        {
            if (!quest.DueDay.HasValue)
                return string.Empty;

            int left = quest.DaysLeft(today);

            return left <= 0 ? ": due today"
                : left == 1 ? ": one day left"
                : $": {left} days left";
        }
    }
}
