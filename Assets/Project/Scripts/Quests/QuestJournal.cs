using System;
using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Items;
using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.Quests
{
    [Serializable]
    public struct QuestProgressState
    {
        public string QuestId;
        public string TemplateId;
        public string GiverId;
        public int Seed;
        public bool Generated;
        public int Stage;
        public int Status;
        public int[] Progress;
        public int DueDay;
        public bool HasDeadline;
        public QuestRecord Record;
    }

    [Serializable]
    public struct QuestJournalState
    {
        public QuestProgressState[] Active;
        public string[] Completed;
    }

    [DisallowMultipleComponent]
    public class QuestJournal : MonoBehaviour, ISaveable
    {
        [SerializeField] GameDatabase database;
        [SerializeField] FactionRegistry factions;
        [SerializeField] NpcDirectory npcs;
        [SerializeField] Inventory inventory;
        [SerializeField] Wallet wallet;
        [SerializeField] string saveKey = "quests";

        readonly List<Quest> active = new List<Quest>();
        readonly HashSet<string> completed = new HashSet<string>();
        readonly Dictionary<string, QuestProgressState> origins = new Dictionary<string, QuestProgressState>();

        // Work that has been shown to the player but not taken. A board or a conversation has to be
        // able to describe a generated quest before it exists, and then start that exact one,
        // regenerating from the template would quietly produce a different job to the one on offer
        readonly Dictionary<string, QuestProgressState> offers = new Dictionary<string, QuestProgressState>();

        QuestContext context;

        public event Action<Quest> QuestStarted;
        public event Action<Quest> QuestAdvanced;
        public event Action<Quest> QuestCompleted;
        public event Action<Quest> QuestFailed;

        public IReadOnlyList<Quest> Active => active;
        public QuestContext Context => context;
        public GameDatabase Database => database;
        public string SaveKey => saveKey;

        void Awake()
        {
            context = new QuestContext
            {
                Factions = factions,
                Npcs = npcs,
                Inventory = inventory,
                Wallet = wallet,
                Journal = this,
                Clock = null
            };
        }

        void Start()
        {
            context.Clock = WorldClock.Instance;

            if (inventory != null)
                inventory.Changed += RefreshItemObjectives;

            if (context.Clock != null)
                context.Clock.DayChanged += OnDayChanged;
        }

        void OnDestroy()
        {
            if (inventory != null)
                inventory.Changed -= RefreshItemObjectives;

            if (context.Clock != null)
                context.Clock.DayChanged -= OnDayChanged;
        }

        public int Today => context.Clock != null ? context.Clock.Day : 0;

        // A deadline is only a deadline if something checks it. Giver death is checked on the same
        // tick because both are the same thing from the player's side: the work is gone
        void OnDayChanged(int day)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Quest quest = active[i];

                if (quest.DueDay.HasValue && day > quest.DueDay.Value)
                    Fail(quest, "The time it was wanted by has passed.");
                else if (quest.FailsIfGiverDies && IsGiverDead(quest))
                    Fail(quest, $"{quest.Giver.DisplayName} is dead.");
            }
        }

        bool IsGiverDead(Quest quest) => npcs != null && npcs.IsDead(quest.Giver);

        public bool IsCompleted(string questId) => !string.IsNullOrEmpty(questId) && completed.Contains(questId);
        public bool IsActive(string questId) => Find(questId) != null;

        public Quest Find(string questId)
        {
            foreach (Quest quest in active)
            {
                if (quest.Id == questId)
                    return quest;
            }

            return null;
        }

        public bool CanOffer(AuthoredQuestDefinition definition)
        {
            if (definition == null || IsCompleted(definition.Id) || IsActive(definition.Id))
                return false;

            if (!definition.IsWellFormed(out string problem))
            {
                Debug.LogWarning($"Quest rejected: {problem}", this);
                return false;
            }

            return QuestConditions.EvaluateAll(definition.Availability, context);
        }

        public Quest StartById(string questId)
        {
            AuthoredQuestDefinition definition = database != null ? database.Quest(questId) : null;
            return definition == null ? null : Start(definition);
        }

        public Quest Start(AuthoredQuestDefinition definition)
        {
            if (!CanOffer(definition))
                return null;

            Quest quest = Quest.FromAuthored(definition);

            if (quest != null && definition.DeadlineDays > 0)
                quest.DueDay = Today + definition.DeadlineDays;

            return Start(quest, default);
        }

        // A board shows work before anybody takes it, so the generator has to be able to produce a
        // quest without starting one
        public bool TryPreview(QuestTemplateDefinition template, NpcActor giver, int seed, out Quest quest) =>
            TemplatedQuestGenerator.TryGenerate(template, giver, context, seed, out quest, out _);

        // Shown, remembered, and startable later by id alone
        public Quest Offer(QuestTemplateDefinition template, NpcActor giver, int seed)
        {
            if (template == null || giver == null || !TryPreview(template, giver, seed, out Quest quest))
                return null;

            if (IsActive(quest.Id) || IsCompleted(quest.Id))
                return null;

            offers[quest.Id] = new QuestProgressState
            {
                QuestId = quest.Id,
                TemplateId = template.Id,
                GiverId = giver.Id,
                Seed = seed,
                Generated = true
            };

            return quest;
        }

        public Quest Accept(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return null;

            if (!offers.TryGetValue(questId, out QuestProgressState offer))
                return StartById(questId);

            offers.Remove(questId);

            QuestTemplateDefinition template = TemplateWithId(offer.TemplateId);
            NpcActor giver = npcs != null ? npcs.Find(offer.GiverId) : null;

            return template == null || giver == null ? null : StartGenerated(template, giver, offer.Seed);
        }

        QuestTemplateDefinition TemplateWithId(string templateId)
        {
            if (database?.QuestTemplates == null)
                return null;

            foreach (QuestTemplateDefinition template in database.QuestTemplates)
                if (template != null && template.Id == templateId) return template;

            return null;
        }

        public Quest StartGenerated(QuestTemplateDefinition template, NpcActor giver, int seed)
        {
            if (!TemplatedQuestGenerator.TryGenerate(template, giver, context, seed, out Quest quest, out string refusal))
            {
                Debug.Log($"Generated quest refused: {refusal}", this);
                return null;
            }

            return Start(quest, new QuestProgressState
            {
                QuestId = quest.Id,
                TemplateId = template.Id,
                GiverId = giver.Id,
                Seed = seed,
                Generated = true
            });
        }

        Quest Start(Quest quest, QuestProgressState origin)
        {
            if (quest == null)
                return null;

            quest.Status = QuestStatus.Active;
            active.Add(quest);

            if (origin.Generated)
                origins[quest.Id] = origin;

            EnterStage(quest);
            QuestStarted?.Invoke(quest);
            RefreshItemObjectives();
            return quest;
        }

        public void ReportKill(EnemyDefinition enemy) => Report(QuestObjectiveKind.KillEnemy, o => o.Enemy == enemy, 1);

        // Talking to somebody is also the moment a parcel changes hands: that is the whole
        // difference between delivering something and having it in your pack. Money is NOT moved
        // here, taking sixty gold off a player for walking up to somebody is a theft, so a payment
        // waits for the turn-in line to be chosen (TurnIn)
        public void ReportTalk(NpcDefinition npc)
        {
            Handover(npc, false);
            Report(QuestObjectiveKind.TalkToNpc, o => o.Npc == npc, 1);
        }

        // Said out loud, in the conversation, goods, money and the report together
        public void TurnIn(NpcDefinition npc)
        {
            Handover(npc, true);
            Report(QuestObjectiveKind.TalkToNpc, o => o.Npc == npc, 1);
        }
        // Matched by name, the same way QuestDirections finds a location in the region
        public void ReportArrival(string place) =>
            Report(QuestObjectiveKind.ReachLocation, o => o.Location != null && o.Location.DisplayName == place, 1);
        // An objective that names a place is only done by clearing that place. One that names none is
        // "the nearest", which any dungeon answers
        public void ReportDungeonCleared(string place) =>
            Report(QuestObjectiveKind.ClearDungeon, o => o.Location == null || o.Location.DisplayName == place, 1);

        // What the player is carrying only ever satisfies a Collect. Deliver deliberately does not
        // read the pack, it is finished by Handover, at the person it is for
        public void RefreshItemObjectives()
        {
            if (inventory == null)
                return;

            foreach (Quest quest in active)
            {
                QuestStage stage = quest.CurrentStage;
                if (stage == null)
                    continue;

                foreach (QuestObjective objective in stage.Objectives)
                {
                    if (objective.Data.Kind == QuestObjectiveKind.CollectItem)
                        objective.Progress = inventory.CountOf(objective.Data.Item);
                }
            }

            CheckCompletion();
        }

        // Goods and money actually leave you here, which is what stops a delivery quest being a
        // collect quest that happens to name somebody
        public bool Handover(NpcDefinition npc, bool includingMoney)
        {
            if (npc == null)
                return false;

            bool moved = false;

            foreach (Quest quest in active)
            {
                QuestStage stage = quest.CurrentStage;
                if (stage == null)
                    continue;

                foreach (QuestObjective objective in stage.Objectives)
                {
                    if (objective.IsComplete || objective.Data.Npc != npc || !objective.Data.NeedsHandover)
                        continue;

                    if (objective.Data.Kind == QuestObjectiveKind.DeliverItem)
                    {
                        if (inventory == null || !inventory.Has(objective.Data.Item, objective.Data.Count))
                            continue;

                        inventory.Remove(objective.Data.Item, objective.Data.Count);
                    }
                    else
                    {
                        if (!includingMoney || wallet == null || !wallet.TrySpend(objective.Data.Count))
                            continue;
                    }

                    objective.Progress = objective.Data.Count;
                    moved = true;
                }
            }

            if (moved)
                CheckCompletion();

            return moved;
        }

        // Taken on is not the same as stuck with. Walking away costs the standing the quest would
        // have paid, through the same OnFail the deadline uses
        public bool Abandon(string questId)
        {
            Quest quest = Find(questId);

            if (quest == null)
                return false;

            Fail(quest, "You gave it up.");
            return true;
        }

        public void Fail(Quest quest, string reason)
        {
            if (quest == null || !active.Contains(quest))
                return;

            quest.Status = QuestStatus.Failed;
            quest.FailureReason = reason;

            QuestConsequences.ApplyAll(quest.OnFail, context);

            active.Remove(quest);
            origins.Remove(quest.Id);
            QuestFailed?.Invoke(quest);
        }

        // A stage can hand you the parcel, move the money or change what somebody is telling people,
        // as it opens rather than when the whole quest ends
        void EnterStage(Quest quest)
        {
            QuestStage stage = quest.CurrentStage;

            if (stage != null && stage.OnEnter.Count > 0)
                QuestConsequences.ApplyAll(stage.OnEnter, context);
        }

        void Report(QuestObjectiveKind kind, Func<QuestObjectiveData, bool> matches, int amount)
        {
            foreach (Quest quest in active)
            {
                QuestStage stage = quest.CurrentStage;
                if (stage == null)
                    continue;

                foreach (QuestObjective objective in stage.Objectives)
                {
                    if (objective.Data.Kind == kind && matches(objective.Data) && !objective.IsComplete)
                        objective.Progress += amount;
                }
            }

            CheckCompletion();
        }

        void CheckCompletion()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Quest quest = active[i];
                QuestStage stage = quest.CurrentStage;

                if (stage == null || !stage.IsComplete)
                    continue;

                if (quest.CurrentStageIndex < quest.Stages.Count - 1)
                {
                    quest.CurrentStageIndex++;
                    EnterStage(quest);
                    QuestAdvanced?.Invoke(quest);
                    continue;
                }

                quest.Status = QuestStatus.Completed;
                QuestConsequences.ApplyAll(quest.OnComplete, context);

                active.RemoveAt(i);
                completed.Add(quest.Id);
                origins.Remove(quest.Id);
                QuestCompleted?.Invoke(quest);
            }
        }

        public string CaptureJson()
        {
            List<QuestProgressState> states = new List<QuestProgressState>();

            foreach (Quest quest in active)
            {
                origins.TryGetValue(quest.Id, out QuestProgressState origin);

                origin.QuestId = quest.Id;
                origin.Generated = quest.IsGenerated;
                origin.Stage = quest.CurrentStageIndex;
                origin.Status = (int)quest.Status;
                origin.Progress = FlattenProgress(quest);
                origin.HasDeadline = quest.DueDay.HasValue;
                origin.DueDay = quest.DueDay ?? 0;

                if (quest.IsGenerated)
                    origin.Record = QuestRecord.From(quest);

                states.Add(origin);
            }

            string[] completedIds = new string[completed.Count];
            completed.CopyTo(completedIds);

            return JsonUtility.ToJson(new QuestJournalState { Active = states.ToArray(), Completed = completedIds });
        }

        public void RestoreJson(string json)
        {
            active.Clear();
            completed.Clear();
            origins.Clear();

            QuestJournalState state = JsonUtility.FromJson<QuestJournalState>(json);

            if (state.Completed != null)
            {
                foreach (string id in state.Completed)
                    completed.Add(id);
            }

            if (state.Active == null)
                return;

            foreach (QuestProgressState entry in state.Active)
            {
                Quest quest = !entry.Generated ? Quest.FromAuthored(database?.Quest(entry.QuestId))
                    : entry.Record.Stages?.Length > 0 && database != null ? entry.Record.ToQuest(entry.QuestId, database)
                    : null;
                if (quest == null)
                    continue;

                quest.Status = (QuestStatus)entry.Status;
                quest.CurrentStageIndex = entry.Stage;
                quest.DueDay = entry.HasDeadline ? entry.DueDay : (int?)null;
                ApplyProgress(quest, entry.Progress);

                active.Add(quest);

                if (entry.Generated)
                    origins[quest.Id] = entry;
            }
        }

        static int[] FlattenProgress(Quest quest)
        {
            List<int> progress = new List<int>();

            foreach (QuestStage stage in quest.Stages)
            {
                foreach (QuestObjective objective in stage.Objectives)
                    progress.Add(objective.Progress);
            }

            return progress.ToArray();
        }

        static void ApplyProgress(Quest quest, int[] progress)
        {
            if (progress == null)
                return;

            int index = 0;
            foreach (QuestStage stage in quest.Stages)
            {
                foreach (QuestObjective objective in stage.Objectives)
                {
                    if (index < progress.Length)
                        objective.Progress = progress[index];

                    index++;
                }
            }
        }
    }
}
