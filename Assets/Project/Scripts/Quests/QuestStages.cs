using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.Quests
{
    public static class QuestStages
    {
        public static void Compose(Quest quest, QuestTemplateDefinition template, NpcActor giver,
            NpcActor target, QuestObjectiveData work, ref DeterministicRandom rng)
        {
            int wanted = Mathf.Clamp(
                rng.NextInt(Mathf.Min(template.StageRange.x, template.StageRange.y),
                    Mathf.Max(template.StageRange.x, template.StageRange.y) + 1),
                1, 4);

            // The lead is the complication made walkable, the third party who has a prior claim is
            // also the person who knows where the thing is
            bool lead = wanted >= 3 && target?.Definition != null;
            bool toll = wanted >= 4 && Toll(template.ServesMotive) && target?.Definition != null;

            if (lead)
                quest.Stages.Add(Lead(target));

            if (toll)
                quest.Stages.Add(Toll(target, work));

            quest.Stages.Add(Work(quest, work));

            if (wanted >= 2 && giver?.Definition != null)
                quest.Stages.Add(Report(giver, work));
        }

        static bool Toll(NpcMotive motive) =>
            motive == NpcMotive.Greed || motive == NpcMotive.Debt || motive == NpcMotive.Ambition;

        static QuestStage Lead(NpcActor target)
        {
            string where = target.Definition.Home != null ? $" in {target.Definition.Home.DisplayName}" : string.Empty;

            return new QuestStage
            {
                Title = $"Find out from {target.DisplayName}",
                Summary = $"{target.DisplayName} has a claim of their own on this, which also means " +
                          "they know where it is. Ask them before anything else.",
                Objectives =
                {
                    new QuestObjective
                    {
                        Data = new QuestObjectiveData
                        {
                            Kind = QuestObjectiveKind.TalkToNpc,
                            Npc = target.Definition,
                            Count = 1,
                            Description = $"Speak to {target.DisplayName}{where}"
                        }
                    }
                }
            };
        }

        static QuestStage Toll(NpcActor target, QuestObjectiveData work)
        {
            int amount = Mathf.Max(10, work.Count * 12);

            return new QuestStage
            {
                Title = $"Settle with {target.DisplayName}",
                Summary = $"{target.DisplayName} will stand aside for {amount} gold and not a coin less.",
                Objectives =
                {
                    new QuestObjective
                    {
                        Data = new QuestObjectiveData
                        {
                            Kind = QuestObjectiveKind.PayGold,
                            Npc = target.Definition,
                            Count = amount,
                            Description = $"Pay {target.DisplayName} {amount} gold"
                        }
                    }
                }
            };
        }

        static QuestStage Work(Quest quest, QuestObjectiveData work) =>
            new QuestStage
            {
                Title = quest.Title,
                Summary = quest.Reason,
                Objectives = { new QuestObjective { Data = work } }
            };

        // Collecting something and being paid for it are not the same act. The last stage is the one
        // that hands it over, which is what makes the giver worth walking back to
        static QuestStage Report(NpcActor giver, QuestObjectiveData work)
        {
            bool carrying = work.Kind == QuestObjectiveKind.CollectItem && work.Item != null;
            string where = giver.Definition.Home != null ? $" in {giver.Definition.Home.DisplayName}" : string.Empty;

            QuestObjectiveData data = carrying
                ? new QuestObjectiveData
                {
                    Kind = QuestObjectiveKind.DeliverItem,
                    Npc = giver.Definition,
                    Item = work.Item,
                    Count = work.Count,
                    Description = $"Take {work.Count}x {work.Item.DisplayName} to {giver.DisplayName}{where}"
                }
                : new QuestObjectiveData
                {
                    Kind = QuestObjectiveKind.TalkToNpc,
                    Npc = giver.Definition,
                    Count = 1,
                    Description = $"Tell {giver.DisplayName}{where} it is done"
                };

            return new QuestStage
            {
                Title = $"Back to {giver.DisplayName}",
                Summary = carrying
                    ? $"{giver.DisplayName} is waiting on it, and waiting is the whole of their problem."
                    : $"{giver.DisplayName} asked to be told, not to hear it from somebody else.",
                Objectives = { new QuestObjective { Data = data } }
            };
        }

        public static int? DeadlineFor(QuestTemplateDefinition template, int today, ref DeterministicRandom rng)
        {
            int low = Mathf.Min(template.DeadlineDays.x, template.DeadlineDays.y);
            int high = Mathf.Max(template.DeadlineDays.x, template.DeadlineDays.y);

            if (high <= 0)
                return null;

            int days = rng.NextInt(Mathf.Max(1, low), high + 1);
            return today + days;
        }
    }
}
