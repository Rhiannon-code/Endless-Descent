using System;
using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Simulation
{
    [Serializable]
    public struct FactionRegistryState
    {
        public string[] FactionIds;
        public int[] Standings;
    }

    [DisallowMultipleComponent]
    public class FactionRegistry : MonoBehaviour, ISaveable
    {
        [SerializeField] GameDatabase database;
        [SerializeField] string saveKey = "factions";

        readonly Dictionary<string, int> standing = new Dictionary<string, int>();

        public event Action<FactionDefinition, int> StandingChanged;

        public string SaveKey => saveKey;

        void Awake()
        {
            if (database?.Factions == null)
                return;

            foreach (FactionDefinition faction in database.Factions)
            {
                if (faction != null)
                    standing[faction.Id] = faction.StartingStanding;
            }
        }

        public int StandingOf(FactionDefinition faction)
        {
            if (faction == null)
                return 0;

            return standing.TryGetValue(faction.Id, out int value) ? value : faction.StartingStanding;
        }

        public string RankOf(FactionDefinition faction) => faction == null ? string.Empty : faction.RankTitleFor(StandingOf(faction));

        // Gaining standing with a faction costs standing with its rivals, so quest consequences
        // cannot make the player universally beloved
        public void Modify(FactionDefinition faction, int delta)
        {
            if (faction == null || delta == 0)
                return;

            Set(faction, StandingOf(faction) + delta);

            if (faction.Rivals == null)
                return;

            int bleed = Mathf.RoundToInt(-delta * faction.RivalBleed);
            if (bleed == 0)
                return;

            foreach (FactionDefinition rival in faction.Rivals)
            {
                if (rival != null)
                    Set(rival, StandingOf(rival) + bleed);
            }
        }

        void Set(FactionDefinition faction, int value)
        {
            int clamped = Mathf.Clamp(value, -100, 100);
            standing[faction.Id] = clamped;
            StandingChanged?.Invoke(faction, clamped);
        }

        public string CaptureJson()
        {
            List<string> ids = new List<string>();
            List<int> values = new List<int>();

            foreach (KeyValuePair<string, int> pair in standing)
            {
                ids.Add(pair.Key);
                values.Add(pair.Value);
            }

            return JsonUtility.ToJson(new FactionRegistryState { FactionIds = ids.ToArray(), Standings = values.ToArray() });
        }

        public void RestoreJson(string json)
        {
            FactionRegistryState state = JsonUtility.FromJson<FactionRegistryState>(json);
            if (state.FactionIds == null || state.Standings == null)
                return;

            int count = Mathf.Min(state.FactionIds.Length, state.Standings.Length);
            for (int i = 0; i < count; i++)
                standing[state.FactionIds[i]] = state.Standings[i];
        }
    }
}
