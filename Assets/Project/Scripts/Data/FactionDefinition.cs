using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    [CreateAssetMenu(menuName = "Endless Descent/World/Faction", fileName = "Faction")]
    public class FactionDefinition : ScriptableObject
    {
        [SerializeField] string id = "faction.unnamed";
        [SerializeField] string displayName = "Unnamed";
        [SerializeField, TextArea] string description;
        [SerializeField, Range(-100, 100)] int startingStanding;
        [SerializeField] FactionDefinition[] rivals;
        [SerializeField, Range(0f, 1f)] float rivalBleed = 0.5f;
        [SerializeField] string[] rankTitles = { "Outsider", "Associate", "Member", "Trusted", "Inner Circle" };

        [Header("Guild")]
        [SerializeField] bool isGuild;
        [SerializeField, Range(-100, 100)] int joinAtStanding = -20;
        [SerializeField] GuildService[] services = new GuildService[0];
        [SerializeField] int[] serviceRequiresRank = new int[0];

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public int StartingStanding => startingStanding;
        public bool IsGuild => isGuild;
        public int JoinAtStanding => joinAtStanding;
        public GuildService[] Services => services;

        public int RankIndexFor(int standing)
        {
            if (rankTitles == null || rankTitles.Length == 0)
                return 0;

            int clamped = Mathf.Clamp(standing, -100, 100);
            return Mathf.Clamp((clamped + 100) * rankTitles.Length / 201, 0, rankTitles.Length - 1);
        }

        public int RankRequiredFor(GuildService service)
        {
            for (int i = 0; i < services.Length; i++)
            {
                if (services[i] != service)
                    continue;

                return serviceRequiresRank != null && i < serviceRequiresRank.Length ? serviceRequiresRank[i] : 0;
            }

            return int.MaxValue;
        }
        public IReadOnlyList<FactionDefinition> Rivals => rivals;
        public float RivalBleed => rivalBleed;

        public string RankTitleFor(int standing)
        {
            if (rankTitles == null || rankTitles.Length == 0)
                return string.Empty;

            int clamped = Mathf.Clamp(standing, -100, 100);
            int index = Mathf.Clamp((clamped + 100) * rankTitles.Length / 201, 0, rankTitles.Length - 1);
            return rankTitles[index];
        }
    }
}
