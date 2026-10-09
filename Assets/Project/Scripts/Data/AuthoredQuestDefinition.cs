using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    [CreateAssetMenu(menuName = "Endless Descent/Quests/Authored Quest", fileName = "Quest")]
    public class AuthoredQuestDefinition : ScriptableObject
    {
        [SerializeField] string id = "quest.unnamed";
        [SerializeField] string title = "Unnamed";
        [SerializeField] NpcDefinition giver;
        [SerializeField] FactionDefinition faction;

        [Header("Why this is not a fetch quest (ADR 0003)")]
        [SerializeField, TextArea] string reason;
        [SerializeField, TextArea] string complication;

        [Header("Flow")]
        [SerializeField] QuestConditionData[] availability;
        [SerializeField] QuestStageData[] stages;
        [SerializeField] QuestConsequenceData[] onComplete;
        [SerializeField] QuestConsequenceData[] onFail;

        [Header("Failure")]
        [SerializeField, Min(0)] int deadlineDays;
        [SerializeField] bool failsIfGiverDies = true;

        public string Id => id;
        public string Title => title;
        public NpcDefinition Giver => giver;
        public FactionDefinition Faction => faction;
        public string Reason => reason;
        public string Complication => complication;
        public IReadOnlyList<QuestConditionData> Availability => availability;
        public IReadOnlyList<QuestStageData> Stages => stages;
        public IReadOnlyList<QuestConsequenceData> OnComplete => onComplete;
        public IReadOnlyList<QuestConsequenceData> OnFail => onFail;
        public int DeadlineDays => deadlineDays;
        public bool FailsIfGiverDies => failsIfGiverDies;

        // A quest without a reason and a consequence is exactly the fetch quest this
        // project exists to avoid, so the asset is invalid without both
        public bool IsWellFormed(out string problem)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                problem = $"'{id}' has no reason";
                return false;
            }

            if (string.IsNullOrWhiteSpace(complication))
            {
                problem = $"'{id}' has no complication";
                return false;
            }

            if (onComplete == null || onComplete.Length == 0)
            {
                problem = $"'{id}' has no consequence";
                return false;
            }

            if (stages == null || stages.Length == 0)
            {
                problem = $"'{id}' has no stages";
                return false;
            }

            problem = null;
            return true;
        }
    }
}
