using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    [CreateAssetMenu(menuName = "Endless Descent/Quests/Quest Template", fileName = "QuestTemplate")]
    public class QuestTemplateDefinition : ScriptableObject
    {
        [SerializeField] string id = "template.unnamed";
        [SerializeField] NpcMotive servesMotive = NpcMotive.Duty;
        [SerializeField] QuestObjectiveKind objective = QuestObjectiveKind.CollectItem;

        [Header("Text. Tokens: {giver} {target} {item} {location} {count}")]
        [SerializeField] string titleFormat = "{giver} needs {item}";
        [SerializeField, TextArea] string reasonFormat = "{giver} needs {item} because of a debt coming due.";
        [SerializeField, TextArea] string complicationFormat = "{target} has a prior claim on it.";

        [Header("Shape")]
        [SerializeField] Vector2Int stageRange = new Vector2Int(2, 3);
        [SerializeField] Vector2Int deadlineDays = new Vector2Int(0, 0);

        [Header("Scale")]
        [SerializeField] Vector2Int countRange = new Vector2Int(3, 6);
        [SerializeField] Vector2Int goldReward = new Vector2Int(40, 120);
        [SerializeField] int standingReward = 4;
        [SerializeField] ItemDefinition[] candidateItems;
        [SerializeField] EnemyDefinition[] candidateEnemies;

        public string Id => id;
        public NpcMotive ServesMotive => servesMotive;
        public QuestObjectiveKind Objective => objective;
        public string TitleFormat => titleFormat;
        public string ReasonFormat => reasonFormat;
        public string ComplicationFormat => complicationFormat;
        public Vector2Int StageRange => stageRange;
        public Vector2Int DeadlineDays => deadlineDays;
        public Vector2Int CountRange => countRange;
        public Vector2Int GoldReward => goldReward;
        public int StandingReward => standingReward;
        public IReadOnlyList<ItemDefinition> CandidateItems => candidateItems;
        public IReadOnlyList<EnemyDefinition> CandidateEnemies => candidateEnemies;
    }
}
