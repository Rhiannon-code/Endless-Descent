using System;
using UnityEngine;

namespace EndlessDescent.Data
{
    public enum QuestObjectiveKind { KillEnemy, CollectItem, DeliverItem, TalkToNpc, ReachLocation, ClearDungeon, PayGold }

    // QuestActive and QuestNotActive are what dialogue gates on: "is there work" has to stop being
    // offered the moment it is taken, and "about that job" must not appear before it is
    public enum QuestConditionKind
    {
        MinFactionStanding, MaxFactionStanding, QuestCompleted, QuestNotCompleted,
        HasItem, MinGold, HourBetween, NpcAlive, QuestActive, QuestNotActive
    }

    public enum QuestConsequenceKind { FactionStanding, GiveItem, TakeItem, GiveGold, TakeGold, SetNpcSituation, UnlockProperty, StartQuest }

    [Serializable]
    public class QuestObjectiveData
    {
        public QuestObjectiveKind Kind = QuestObjectiveKind.TalkToNpc;
        public string Description;
        public ItemDefinition Item;

        // Who the objective is about. For deliver and pay this is who receives it, which is what
        // makes those two different from collect, handing something over needs somebody to hand it to
        public NpcDefinition Npc;
        public LocationDefinition Location;
        public EnemyDefinition Enemy;
        [Min(1)] public int Count = 1;

        public bool NeedsHandover =>
            Kind == QuestObjectiveKind.DeliverItem || Kind == QuestObjectiveKind.PayGold;
    }

    [Serializable]
    public class QuestConditionData
    {
        public QuestConditionKind Kind = QuestConditionKind.MinFactionStanding;
        public FactionDefinition Faction;
        public ItemDefinition Item;
        public NpcDefinition Npc;
        public string QuestId;
        public int Amount;
        [Range(0, 23)] public int FromHour;
        [Range(0, 23)] public int ToHour = 23;
    }

    [Serializable]
    public class QuestConsequenceData
    {
        public QuestConsequenceKind Kind = QuestConsequenceKind.GiveGold;
        public FactionDefinition Faction;
        public ItemDefinition Item;
        public NpcDefinition Npc;
        public PropertyDefinition Property;
        public string QuestId;
        [TextArea] public string Text;
        public int Amount;
    }

    [Serializable]
    public class QuestStageData
    {
        public string Title;
        [TextArea] public string Summary;
        public QuestObjectiveData[] Objectives;

        // Applied as the stage opens rather than when the quest ends, so a stage can put the parcel
        // in your hands, move the money, or change what somebody is telling people
        public QuestConsequenceData[] OnEnter;
    }
}
