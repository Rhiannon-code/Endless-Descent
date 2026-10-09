using System;
using EndlessDescent.Data;

namespace EndlessDescent.Quests
{
    [Serializable]
    public struct ObjectiveRecord
    {
        public int Kind;
        public string Description;
        public string ItemId;
        public string NpcId;
        public string EnemyId;
        public int Count;
    }

    [Serializable]
    public struct ConsequenceRecord
    {
        public int Kind;
        public string FactionId;
        public string ItemId;
        public string NpcId;
        public string QuestId;
        public string Text;
        public int Amount;
    }

    [Serializable]
    public struct StageRecord
    {
        public string Title;
        public string Summary;
        public ObjectiveRecord[] Objectives;
        public ConsequenceRecord[] OnEnter;
    }

    // A generated quest written down whole. Regenerating it from template, giver and seed needed the
    // giver's town to be standing, and on Continue no town is, so every generated quest was dropped.
    // Generated quests never name a location or a property, so neither is kept
    [Serializable]
    public struct QuestRecord
    {
        public string Title;
        public string Reason;
        public string Complication;
        public string GiverId;
        public string FactionId;
        public bool FailsIfGiverDies;
        public StageRecord[] Stages;
        public ConsequenceRecord[] OnComplete;
        public ConsequenceRecord[] OnFail;

        public static QuestRecord From(Quest quest)
        {
            StageRecord[] stages = new StageRecord[quest.Stages.Count];

            for (int i = 0; i < stages.Length; i++)
            {
                QuestStage stage = quest.Stages[i];
                ObjectiveRecord[] objectives = new ObjectiveRecord[stage.Objectives.Count];

                for (int o = 0; o < objectives.Length; o++)
                {
                    QuestObjectiveData data = stage.Objectives[o].Data;

                    objectives[o] = new ObjectiveRecord
                    {
                        Kind = (int)data.Kind,
                        Description = data.Description,
                        ItemId = data.Item != null ? data.Item.Id : null,
                        NpcId = data.Npc != null ? data.Npc.Id : null,
                        EnemyId = data.Enemy != null ? data.Enemy.Id : null,
                        Count = data.Count
                    };
                }

                stages[i] = new StageRecord
                {
                    Title = stage.Title, Summary = stage.Summary, Objectives = objectives, OnEnter = Write(stage.OnEnter)
                };
            }

            return new QuestRecord
            {
                Title = quest.Title,
                Reason = quest.Reason,
                Complication = quest.Complication,
                GiverId = quest.Giver != null ? quest.Giver.Id : null,
                FactionId = quest.Faction != null ? quest.Faction.Id : null,
                FailsIfGiverDies = quest.FailsIfGiverDies,
                Stages = stages,
                OnComplete = Write(quest.OnComplete),
                OnFail = Write(quest.OnFail)
            };
        }

        public Quest ToQuest(string id, GameDatabase database)
        {
            Quest quest = new Quest
            {
                Id = id,
                Title = Title,
                Reason = Reason,
                Complication = Complication,
                Giver = database.Npc(GiverId),
                Faction = database.Faction(FactionId),
                IsGenerated = true,
                FailsIfGiverDies = FailsIfGiverDies
            };

            foreach (StageRecord record in Stages)
            {
                QuestStage stage = new QuestStage { Title = record.Title, Summary = record.Summary };

                if (record.Objectives != null)
                {
                    foreach (ObjectiveRecord objective in record.Objectives)
                    {
                        stage.Objectives.Add(new QuestObjective
                        {
                            Data = new QuestObjectiveData
                            {
                                Kind = (QuestObjectiveKind)objective.Kind,
                                Description = objective.Description,
                                Item = database.Item(objective.ItemId),
                                Npc = database.Npc(objective.NpcId),
                                Enemy = database.Enemy(objective.EnemyId),
                                Count = objective.Count
                            }
                        });
                    }
                }

                Read(record.OnEnter, stage.OnEnter, database);
                quest.Stages.Add(stage);
            }

            Read(OnComplete, quest.OnComplete, database);
            Read(OnFail, quest.OnFail, database);
            return quest;
        }

        static ConsequenceRecord[] Write(System.Collections.Generic.List<QuestConsequenceData> consequences)
        {
            ConsequenceRecord[] records = new ConsequenceRecord[consequences.Count];

            for (int i = 0; i < records.Length; i++)
            {
                QuestConsequenceData data = consequences[i];

                records[i] = new ConsequenceRecord
                {
                    Kind = (int)data.Kind,
                    FactionId = data.Faction != null ? data.Faction.Id : null,
                    ItemId = data.Item != null ? data.Item.Id : null,
                    NpcId = data.Npc != null ? data.Npc.Id : null,
                    QuestId = data.QuestId,
                    Text = data.Text,
                    Amount = data.Amount
                };
            }

            return records;
        }

        static void Read(ConsequenceRecord[] records, System.Collections.Generic.List<QuestConsequenceData> into,
            GameDatabase database)
        {
            if (records == null)
                return;

            foreach (ConsequenceRecord record in records)
            {
                into.Add(new QuestConsequenceData
                {
                    Kind = (QuestConsequenceKind)record.Kind,
                    Faction = database.Faction(record.FactionId),
                    Item = database.Item(record.ItemId),
                    Npc = database.Npc(record.NpcId),
                    QuestId = record.QuestId,
                    Text = record.Text,
                    Amount = record.Amount
                });
            }
        }
    }
}
