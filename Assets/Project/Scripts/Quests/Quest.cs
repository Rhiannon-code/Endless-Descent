using System.Collections.Generic;
using EndlessDescent.Data;

namespace EndlessDescent.Quests
{
    public enum QuestStatus { Available, Active, Completed, Failed }

    public class QuestObjective
    {
        public QuestObjectiveData Data;
        public int Progress;

        public bool IsComplete => Data != null && Progress >= Data.Count;
        public string Description => Data?.Description;
    }

    public class QuestStage
    {
        public string Title;
        public string Summary;
        public readonly List<QuestObjective> Objectives = new List<QuestObjective>();
        public readonly List<QuestConsequenceData> OnEnter = new List<QuestConsequenceData>();

        public bool IsComplete
        {
            get
            {
                foreach (QuestObjective objective in Objectives)
                {
                    if (!objective.IsComplete)
                        return false;
                }

                return true;
            }
        }
    }

    public class Quest
    {
        public string Id;
        public string Title;
        public string Reason;
        public string Complication;
        public NpcDefinition Giver;
        public FactionDefinition Faction;
        public bool IsGenerated;
        public QuestStatus Status = QuestStatus.Available;
        public int CurrentStageIndex;

        // The day it lapses, or none. A quest that cannot be lost is a quest with no stakes, and
        // OnFail existed on every one of them with nothing able to reach it
        public int? DueDay;
        public bool FailsIfGiverDies;
        public string FailureReason;

        public readonly List<QuestStage> Stages = new List<QuestStage>();
        public readonly List<QuestConsequenceData> OnComplete = new List<QuestConsequenceData>();
        public readonly List<QuestConsequenceData> OnFail = new List<QuestConsequenceData>();

        public QuestStage CurrentStage =>
            CurrentStageIndex >= 0 && CurrentStageIndex < Stages.Count ? Stages[CurrentStageIndex] : null;

        public bool IsFinished => Status == QuestStatus.Completed || Status == QuestStatus.Failed;

        public int DaysLeft(int today) => DueDay.HasValue ? DueDay.Value - today : int.MaxValue;

        public static Quest FromAuthored(AuthoredQuestDefinition definition)
        {
            if (definition == null)
                return null;

            Quest quest = new Quest
            {
                Id = definition.Id,
                Title = definition.Title,
                Reason = definition.Reason,
                Complication = definition.Complication,
                Giver = definition.Giver,
                Faction = definition.Faction,
                IsGenerated = false,
                FailsIfGiverDies = definition.FailsIfGiverDies
            };

            if (definition.Stages != null)
            {
                foreach (QuestStageData stageData in definition.Stages)
                    quest.Stages.Add(StageFrom(stageData));
            }

            if (definition.OnComplete != null)
                quest.OnComplete.AddRange(definition.OnComplete);

            if (definition.OnFail != null)
                quest.OnFail.AddRange(definition.OnFail);

            return quest;
        }

        public static QuestStage StageFrom(QuestStageData data)
        {
            QuestStage stage = new QuestStage { Title = data.Title, Summary = data.Summary };

            if (data.Objectives != null)
            {
                foreach (QuestObjectiveData objective in data.Objectives)
                    stage.Objectives.Add(new QuestObjective { Data = objective });
            }

            if (data.OnEnter != null)
                stage.OnEnter.AddRange(data.OnEnter);

            return stage;
        }
    }
}
