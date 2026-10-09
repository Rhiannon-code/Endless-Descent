using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    public enum NpcMotive { Debt, Grief, Ambition, Fear, Devotion, Greed, Revenge, Duty }

    [CreateAssetMenu(menuName = "Endless Descent/World/NPC", fileName = "Npc")]
    public class NpcDefinition : ScriptableObject
    {
        [SerializeField] string id = "npc.unnamed";
        [SerializeField] string displayName = "Unnamed";
        [SerializeField] GameObject prefab;
        [SerializeField] FactionDefinition faction;
        [SerializeField] ScheduleDefinition schedule;
        [SerializeField] LocationDefinition home;

        [Header("Why they might ask you for something")]
        [SerializeField] NpcMotive[] motives = { NpcMotive.Duty };
        [SerializeField] NpcDefinition[] relations;
        [SerializeField, TextArea] string situation;
        [SerializeField] DialogueDefinition dialogue;

        [Header("Trade")]
        // Where they stand in a town, at the door of a building of this kind, so they can be found
        [SerializeField] RoomPurpose workplace = RoomPurpose.Townhouse;
        [SerializeField] bool isMerchant;
        [SerializeField, Min(0)] int startingGold = 250;

        public string Id => id;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public FactionDefinition Faction => faction;
        public ScheduleDefinition Schedule => schedule;
        public LocationDefinition Home => home;
        public IReadOnlyList<NpcMotive> Motives => motives;
        public IReadOnlyList<NpcDefinition> Relations => relations;
        public string Situation => situation;
        public DialogueDefinition Dialogue => dialogue;
        public bool IsMerchant => isMerchant;
        public RoomPurpose Workplace => workplace;
        public int StartingGold => startingGold;

        public bool HasMotive(NpcMotive motive)
        {
            if (motives == null)
                return false;

            foreach (NpcMotive m in motives)
            {
                if (m == motive)
                    return true;
            }

            return false;
        }
    }
}
