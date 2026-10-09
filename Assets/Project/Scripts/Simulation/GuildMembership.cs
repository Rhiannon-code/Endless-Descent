using System;
using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Simulation
{
    [Serializable]
    public struct GuildMembershipState
    {
        public string[] JoinedIds;
        public string[] RefusedIds;
    }

    // Which halls you belong to. Rank is not stored: it is read from the standing the faction system
    // already tracks, so doing work for a guild is what promotes you
    [DisallowMultipleComponent]
    public class GuildMembership : MonoBehaviour, ISaveable
    {
        [SerializeField] FactionRegistry factions;
        [SerializeField] string saveKey = "guilds.player";

        readonly HashSet<string> joined = new HashSet<string>();

        // A hidden circle that has turned you down does not reconsider. There is no appeal and no
        // second attempt, because a circle that gives you another go is a circle that gets everybody
        // hange
        readonly HashSet<string> refused = new HashSet<string>();

        public event Action Changed;

        public string SaveKey => saveKey;

        public bool IsMember(FactionDefinition guild) => guild != null && joined.Contains(guild.Id);

        public bool IsRefused(FactionDefinition guild) => guild != null && refused.Contains(guild.Id);

        public void Refuse(FactionDefinition guild)
        {
            if (guild == null || !refused.Add(guild.Id))
                return;

            Changed?.Invoke();
        }

        // Admission by test rather than by standing, the circles do not care what your reputation is,
        // only what you answered. Refusal is checked here so no caller can forget it
        public bool Admit(FactionDefinition guild)
        {
            if (guild == null || IsMember(guild) || IsRefused(guild))
                return false;

            joined.Add(guild.Id);
            factions?.Modify(guild, 20);
            Changed?.Invoke();
            return true;
        }

        public int RankIndex(FactionDefinition guild) =>
            guild == null || factions == null ? 0 : guild.RankIndexFor(factions.StandingOf(guild));

        public string RankTitle(FactionDefinition guild) =>
            guild == null || factions == null ? string.Empty : factions.RankOf(guild);

        public bool CanJoin(FactionDefinition guild) =>
            guild != null && guild.IsGuild && !IsMember(guild) && !IsRefused(guild) &&
            factions != null && factions.StandingOf(guild) >= guild.JoinAtStanding;

        public bool Join(FactionDefinition guild)
        {
            if (!CanJoin(guild))
                return false;

            joined.Add(guild.Id);
            factions.Modify(guild, 5);
            Changed?.Invoke();
            return true;
        }

        public bool CanUse(FactionDefinition guild, GuildService service) =>
            IsMember(guild) && RankIndex(guild) >= guild.RankRequiredFor(service);

        public string CaptureJson()
        {
            string[] ids = new string[joined.Count];
            joined.CopyTo(ids);

            string[] shut = new string[refused.Count];
            refused.CopyTo(shut);

            return JsonUtility.ToJson(new GuildMembershipState { JoinedIds = ids, RefusedIds = shut });
        }

        public void RestoreJson(string json)
        {
            joined.Clear();
            refused.Clear();

            GuildMembershipState state = JsonUtility.FromJson<GuildMembershipState>(json);

            if (state.JoinedIds != null)
                foreach (string id in state.JoinedIds) joined.Add(id);

            if (state.RefusedIds != null)
                foreach (string id in state.RefusedIds) refused.Add(id);

            Changed?.Invoke();
        }
    }
}
