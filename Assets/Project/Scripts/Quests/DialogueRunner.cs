using System.Collections.Generic;
using EndlessDescent.Data;
using EndlessDescent.Simulation;

namespace EndlessDescent.Quests
{
    // Walks a dialogue tree against live world state. It holds no Unity objects and draws nothing,
    // so the screen is free to be replaced and the tree is testable without one
    public class DialogueRunner
    {
        readonly DialogueDefinition tree;
        readonly QuestContext context;
        readonly NpcActor speaker;
        readonly List<DialogueChoiceData> available = new List<DialogueChoiceData>();

        public DialogueRunner(DialogueDefinition definition, NpcActor npc, QuestContext state)
        {
            tree = definition;
            speaker = npc;
            context = state;
            Current = tree != null ? tree.Line(tree.RootId) : null;
            Rebuild();
        }

        public NpcActor Speaker => speaker;
        public DialogueLineData Current { get; private set; }
        public bool IsOver => Current == null;
        public IReadOnlyList<DialogueChoiceData> Choices => available;

        public string Text => Current == null ? string.Empty : Fill(Current.Text);

        public string TextOf(DialogueChoiceData choice) => choice == null ? string.Empty : Fill(choice.Text);

        // What the choice did, so the screen can open a shop or announce a quest without knowing how
        // the tree is put together
        public DialogueAction Choose(int index)
        {
            if (index < 0 || index >= available.Count)
                return DialogueAction.Leave;

            DialogueChoiceData choice = available[index];

            QuestConsequences.ApplyAll(choice.Effects, context);
            Act(choice);

            Current = choice.Ends ? null : tree.Line(choice.GoTo);
            Rebuild();

            return choice.Action;
        }

        void Act(DialogueChoiceData choice)
        {
            QuestJournal journal = context?.Journal;

            if (journal == null)
                return;

            switch (choice.Action)
            {
                case DialogueAction.OfferQuest:
                    journal.Accept(choice.QuestId);
                    break;

                // The only place money changes hands, because it is the only place the player said
                // so rather than merely walked up to somebody
                case DialogueAction.TurnInQuest:
                    if (speaker != null)
                        journal.TurnIn(speaker.Definition);
                    break;
            }
        }

        // A line whose own conditions have stopped being true is skipped rather than shown greyed
        // out, the person simply has nothing to say about it any more
        void Rebuild()
        {
            available.Clear();

            if (Current != null && !QuestConditions.EvaluateAll(Current.Requires, context))
                Current = null;

            if (Current?.Choices == null)
                return;

            foreach (DialogueChoiceData choice in Current.Choices)
            {
                if (choice != null && QuestConditions.EvaluateAll(choice.Requires, context))
                    available.Add(choice);
            }
        }

        string Fill(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("{npc}", speaker != null ? speaker.DisplayName : "they")
                .Replace("{situation}", speaker != null && !string.IsNullOrEmpty(speaker.Situation)
                    ? speaker.Situation
                    : "nothing worth saying")
                .Replace("{town}", speaker?.CurrentLocation != null
                    ? speaker.CurrentLocation.DisplayName
                    : "here");
        }
    }
}
