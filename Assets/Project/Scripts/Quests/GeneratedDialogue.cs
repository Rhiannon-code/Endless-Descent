using System.Collections.Generic;
using EndlessDescent.Data;
using EndlessDescent.Simulation;

namespace EndlessDescent.Quests
{
    // Towns build their own cast, three to six people who have never been written, so
    // most of the people you can talk to will never have an authored tree. This gives them one made
    // out of what is actually true about them, what they are living through, what they want done,
    // and whether you are carrying it
    public static class GeneratedDialogue
    {
        public static DialogueDefinition For(NpcActor npc, QuestContext context, Quest offer, bool trades)
        {
            List<DialogueLineData> lines = new List<DialogueLineData>();
            List<DialogueChoiceData> root = new List<DialogueChoiceData>();

            root.Add(new DialogueChoiceData { Text = "How are things?", GoTo = "situation" });

            lines.Add(new DialogueLineData
            {
                Id = "situation",
                Text = "{situation}",
                Choices = new[] { new DialogueChoiceData { Text = "I see.", GoTo = "root" } }
            });

            if (offer != null)
            {
                root.Add(new DialogueChoiceData { Text = "Is there work?", GoTo = "work" });

                lines.Add(new DialogueLineData
                {
                    Id = "work",
                    Text = $"{offer.Reason}\n\n{offer.Complication}",
                    Choices = new[]
                    {
                        new DialogueChoiceData
                        {
                            Text = "I will do it.",
                            GoTo = "taken",
                            Action = DialogueAction.OfferQuest,
                            QuestId = offer.Id
                        },
                        new DialogueChoiceData { Text = "Not today.", GoTo = "root" }
                    }
                });

                lines.Add(new DialogueLineData
                {
                    Id = "taken",
                    Text = "Then it is yours. Do not be slow about it.",
                    Choices = new[] { new DialogueChoiceData { Text = "Understood.", GoTo = "root" } }
                });
            }

            // Only offered when this person is actually the one the current stage points at, so it
            // never appears as a line that does nothing when it is chosen
            if (Awaits(npc, context))
            {
                root.Add(new DialogueChoiceData
                {
                    Text = "About that job.",
                    GoTo = "handed",
                    Action = DialogueAction.TurnInQuest
                });

                lines.Add(new DialogueLineData
                {
                    Id = "handed",
                    Text = "You have it. Good. I had half stopped expecting it.",
                    Choices = new[] { new DialogueChoiceData { Text = "Think nothing of it.", GoTo = "root" } }
                });
            }

            if (trades)
                root.Add(new DialogueChoiceData { Text = "Show me what you have.", Action = DialogueAction.OpenTrade });

            root.Add(new DialogueChoiceData { Text = "Good day.", Action = DialogueAction.Leave });

            lines.Add(new DialogueLineData
            {
                Id = "root",
                Text = "{npc}, of {town}.",
                Choices = root.ToArray()
            });

            DialogueDefinition tree = UnityEngine.ScriptableObject.CreateInstance<DialogueDefinition>();
            tree.name = $"Dialogue_{npc?.Id}";
            tree.Configure($"dialogue.generated.{npc?.Id}", "root", lines.ToArray());
            return tree;
        }

        static bool Awaits(NpcActor npc, QuestContext context)
        {
            if (npc?.Definition == null || context?.Journal == null)
                return false;

            foreach (Quest quest in context.Journal.Active)
            {
                QuestStage stage = quest.CurrentStage;
                if (stage == null)
                    continue;

                foreach (QuestObjective objective in stage.Objectives)
                {
                    if (objective.IsComplete || objective.Data.Npc != npc.Definition)
                        continue;

                    if (objective.Data.Kind == QuestObjectiveKind.TalkToNpc || objective.Data.NeedsHandover)
                        return true;
                }
            }

            return false;
        }
    }
}
