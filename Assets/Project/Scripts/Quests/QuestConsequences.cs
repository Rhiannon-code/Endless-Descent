using EndlessDescent.Data;
using EndlessDescent.Simulation;

namespace EndlessDescent.Quests
{
    public static class QuestConsequences
    {
        public static void Apply(QuestConsequenceData consequence, QuestContext context)
        {
            if (consequence == null || context == null)
                return;

            switch (consequence.Kind)
            {
                case QuestConsequenceKind.FactionStanding:
                    context.Factions?.Modify(consequence.Faction, consequence.Amount);
                    break;

                case QuestConsequenceKind.GiveItem:
                    context.Inventory?.Add(consequence.Item, UnityEngine.Mathf.Max(1, consequence.Amount));
                    break;

                case QuestConsequenceKind.TakeItem:
                    context.Inventory?.Remove(consequence.Item, UnityEngine.Mathf.Max(1, consequence.Amount));
                    break;

                case QuestConsequenceKind.GiveGold:
                    context.Wallet?.Add(consequence.Amount);
                    break;

                case QuestConsequenceKind.TakeGold:
                    context.Wallet?.TrySpend(consequence.Amount);
                    break;

                case QuestConsequenceKind.SetNpcSituation:
                    NpcActor actor = context.Npcs != null ? context.Npcs.Find(consequence.Npc) : null;
                    actor?.SetSituation(consequence.Text);
                    break;

                case QuestConsequenceKind.StartQuest:
                    context.Journal?.StartById(consequence.QuestId);
                    break;

                case QuestConsequenceKind.UnlockProperty:
                    break;
            }
        }

        public static void ApplyAll(System.Collections.Generic.IReadOnlyList<QuestConsequenceData> consequences, QuestContext context)
        {
            if (consequences == null)
                return;

            foreach (QuestConsequenceData consequence in consequences)
                Apply(consequence, context);
        }
    }
}
