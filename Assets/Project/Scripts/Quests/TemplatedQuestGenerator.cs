using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.Quests
{
    public static class TemplatedQuestGenerator
    {
        // A template that cannot fill reason, complication AND consequence from live world
        // state does not generate. Refusing is the feature, it is what stops these being fetch quests
        public static bool TryGenerate(QuestTemplateDefinition template, NpcActor giver, QuestContext context,
            int seed, out Quest quest, out string refusal)
        {
            quest = null;

            if (template == null || giver == null || context == null || !context.IsUsable)
            {
                refusal = "missing template, giver or world state";
                return false;
            }

            if (!giver.IsAlive)
            {
                refusal = $"{giver.DisplayName} is dead";
                return false;
            }

            if (giver.Definition == null || !giver.Definition.HasMotive(template.ServesMotive))
            {
                refusal = $"{giver.DisplayName} has no {template.ServesMotive} motive to justify this";
                return false;
            }

            DeterministicRandom rng = new DeterministicRandom(seed);

            NpcActor target = PickTarget(giver, context, ref rng);
            if (target == null)
            {
                refusal = "no third party available to complicate it";
                return false;
            }

            QuestObjectiveData objective = BuildObjective(template, target, ref rng);
            if (objective == null)
            {
                refusal = "template has no candidate item or enemy";
                return false;
            }

            List<QuestConsequenceData> consequences = BuildConsequences(template, giver, ref rng);
            if (consequences.Count == 0)
            {
                refusal = "template produces no consequence";
                return false;
            }

            string subject = objective.Item != null ? objective.Item.DisplayName
                : objective.Enemy != null ? objective.Enemy.DisplayName
                : "the task";

            quest = new Quest
            {
                Id = $"generated.{template.Id}.{giver.Id}.{seed}",
                Title = Fill(template.TitleFormat, giver, target, subject, objective.Count),
                Reason = Fill(template.ReasonFormat, giver, target, subject, objective.Count),
                Complication = Fill(template.ComplicationFormat, giver, target, subject, objective.Count),
                Giver = giver.Definition,
                Faction = giver.Definition.Faction,
                IsGenerated = true
            };

            QuestStages.Compose(quest, template, giver, target, objective, ref rng);
            quest.OnComplete.AddRange(consequences);

            // A giver who is dead cannot be told it is done, and the last stage is telling them.
            quest.FailsIfGiverDies = true;
            quest.DueDay = QuestStages.DeadlineFor(template, Today(context), ref rng);

            refusal = null;
            return true;
        }

        static int Today(QuestContext context) => context.Clock != null ? context.Clock.Day : 0;

        static NpcActor PickTarget(NpcActor giver, QuestContext context, ref DeterministicRandom rng)
        {
            List<NpcActor> candidates = new List<NpcActor>();

            if (giver.Definition?.Relations != null)
            {
                foreach (NpcDefinition relation in giver.Definition.Relations)
                {
                    NpcActor actor = context.Npcs.Find(relation);
                    if (actor != null && actor.IsAlive && actor != giver)
                        candidates.Add(actor);
                }
            }

            if (candidates.Count == 0)
            {
                List<NpcActor> living = new List<NpcActor>();
                context.Npcs.CollectLiving(living);

                foreach (NpcActor actor in living)
                {
                    if (actor != giver)
                        candidates.Add(actor);
                }
            }

            return candidates.Count == 0 ? null : candidates[rng.NextInt(candidates.Count)];
        }

        static QuestObjectiveData BuildObjective(QuestTemplateDefinition template, NpcActor target, ref DeterministicRandom rng)
        {
            int count = rng.NextInt(Mathf.Min(template.CountRange.x, template.CountRange.y),
                Mathf.Max(template.CountRange.x, template.CountRange.y) + 1);
            count = Mathf.Max(1, count);

            switch (template.Objective)
            {
                case QuestObjectiveKind.KillEnemy:
                    EnemyDefinition enemy = PickEnemy(template, ref rng);
                    if (enemy == null)
                        return null;

                    return new QuestObjectiveData
                    {
                        Kind = QuestObjectiveKind.KillEnemy,
                        Enemy = enemy,
                        Count = count,
                        Description = $"Kill {count}x {enemy.DisplayName}"
                    };

                case QuestObjectiveKind.TalkToNpc:
                    // Where they live, so the journal says where to go rather than only who to find
                    LocationDefinition home = target.Definition != null ? target.Definition.Home : null;

                    return new QuestObjectiveData
                    {
                        Kind = QuestObjectiveKind.TalkToNpc,
                        Npc = target.Definition,
                        Count = 1,
                        Description = home != null
                            ? $"Speak to {target.DisplayName} in {home.DisplayName}"
                            : $"Speak to {target.DisplayName}"
                    };

                default:
                    ItemDefinition item = PickItem(template, ref rng);
                    if (item == null)
                        return null;

                    return new QuestObjectiveData
                    {
                        Kind = QuestObjectiveKind.CollectItem,
                        Item = item,
                        Count = count,
                        Description = $"Collect {count}x {item.DisplayName}"
                    };
            }
        }

        static ItemDefinition PickItem(QuestTemplateDefinition template, ref DeterministicRandom rng)
        {
            IReadOnlyList<ItemDefinition> items = template.CandidateItems;
            return items == null || items.Count == 0 ? null : items[rng.NextInt(items.Count)];
        }

        static EnemyDefinition PickEnemy(QuestTemplateDefinition template, ref DeterministicRandom rng)
        {
            IReadOnlyList<EnemyDefinition> enemies = template.CandidateEnemies;
            return enemies == null || enemies.Count == 0 ? null : enemies[rng.NextInt(enemies.Count)];
        }

        static List<QuestConsequenceData> BuildConsequences(QuestTemplateDefinition template, NpcActor giver, ref DeterministicRandom rng)
        {
            List<QuestConsequenceData> consequences = new List<QuestConsequenceData>();

            int gold = rng.NextInt(Mathf.Min(template.GoldReward.x, template.GoldReward.y),
                Mathf.Max(template.GoldReward.x, template.GoldReward.y) + 1);

            if (gold > 0)
                consequences.Add(new QuestConsequenceData { Kind = QuestConsequenceKind.GiveGold, Amount = gold });

            if (template.StandingReward != 0 && giver.Definition?.Faction != null)
            {
                consequences.Add(new QuestConsequenceData
                {
                    Kind = QuestConsequenceKind.FactionStanding,
                    Faction = giver.Definition.Faction,
                    Amount = template.StandingReward
                });
            }

            return consequences;
        }

        static string Fill(string format, NpcActor giver, NpcActor target, string subject, int count)
        {
            if (string.IsNullOrEmpty(format))
                return string.Empty;

            return format
                .Replace("{giver}", giver.DisplayName)
                .Replace("{target}", target.DisplayName)
                .Replace("{item}", subject)
                .Replace("{location}", giver.CurrentLocation != null ? giver.CurrentLocation.DisplayName : "somewhere")
                .Replace("{count}", count.ToString());
        }
    }
}
