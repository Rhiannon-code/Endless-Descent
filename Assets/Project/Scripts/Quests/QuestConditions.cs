using EndlessDescent.Data;
using EndlessDescent.Simulation;

namespace EndlessDescent.Quests
{
    public static class QuestConditions
    {
        public static bool Evaluate(QuestConditionData condition, QuestContext context)
        {
            if (condition == null || context == null)
                return false;

            switch (condition.Kind)
            {
                case QuestConditionKind.MinFactionStanding:
                    return context.Factions != null && context.Factions.StandingOf(condition.Faction) >= condition.Amount;

                case QuestConditionKind.MaxFactionStanding:
                    return context.Factions != null && context.Factions.StandingOf(condition.Faction) <= condition.Amount;

                case QuestConditionKind.QuestCompleted:
                    return context.Journal != null && context.Journal.IsCompleted(condition.QuestId);

                case QuestConditionKind.QuestNotCompleted:
                    return context.Journal == null || !context.Journal.IsCompleted(condition.QuestId);

                case QuestConditionKind.QuestActive:
                    return context.Journal != null && context.Journal.IsActive(condition.QuestId);

                case QuestConditionKind.QuestNotActive:
                    return context.Journal == null || !context.Journal.IsActive(condition.QuestId);

                case QuestConditionKind.HasItem:
                    return context.Inventory != null && context.Inventory.Has(condition.Item, UnityEngine.Mathf.Max(1, condition.Amount));

                case QuestConditionKind.MinGold:
                    return context.Wallet != null && context.Wallet.Gold >= condition.Amount;

                case QuestConditionKind.HourBetween:
                    return context.Clock != null && IsHourBetween(context.Clock.Hour, condition.FromHour, condition.ToHour);

                case QuestConditionKind.NpcAlive:
                    NpcActor actor = context.Npcs != null ? context.Npcs.Find(condition.Npc) : null;
                    return actor != null && actor.IsAlive;

                default:
                    return false;
            }
        }

        public static bool EvaluateAll(System.Collections.Generic.IReadOnlyList<QuestConditionData> conditions, QuestContext context)
        {
            if (conditions == null)
                return true;

            foreach (QuestConditionData condition in conditions)
            {
                if (!Evaluate(condition, context))
                    return false;
            }

            return true;
        }

        static bool IsHourBetween(int hour, int from, int to)
        {
            return from <= to ? hour >= from && hour <= to : hour >= from || hour <= to;
        }
    }
}
