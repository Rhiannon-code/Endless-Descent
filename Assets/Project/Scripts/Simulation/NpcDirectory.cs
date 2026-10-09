using System;
using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Simulation
{
    [Serializable]
    public struct NpcDirectoryState
    {
        public NpcActorState[] Actors;
        public string[] Dead;
    }

    // The one place NPCs are looked up. The quest generator draws its cast from here, so a second
    // list of NPCs anywhere would let quests reference people the world does not have
    [DisallowMultipleComponent]
    public class NpcDirectory : MonoBehaviour, ISaveable
    {
        [SerializeField] string saveKey = "npcs";

        readonly List<NpcActor> actors = new List<NpcActor>();
        readonly Dictionary<string, NpcActor> byId = new Dictionary<string, NpcActor>();

        // A town is rebuilt every time it streams back in, so who has died is kept here rather than on
        // the people, or the dead would be standing in the square again on the next visit
        readonly HashSet<string> dead = new HashSet<string>();

        public IReadOnlyList<NpcActor> Actors => actors;
        public string SaveKey => saveKey;

        public bool IsDead(NpcDefinition npc) => npc != null && dead.Contains(npc.Id);

        public void Bury(NpcActor actor)
        {
            if (actor != null && !string.IsNullOrEmpty(actor.Id))
                dead.Add(actor.Id);
        }

        public void Register(NpcActor actor)
        {
            if (actor == null || string.IsNullOrEmpty(actor.Id) || byId.ContainsKey(actor.Id))
                return;

            if (dead.Contains(actor.Id))
            {
                actor.gameObject.SetActive(false);
                return;
            }

            actors.Add(actor);
            byId[actor.Id] = actor;
        }

        public void Unregister(NpcActor actor)
        {
            if (actor == null)
                return;

            actors.Remove(actor);

            if (byId.TryGetValue(actor.Id, out NpcActor listed) && listed == actor)
                byId.Remove(actor.Id);
        }

        public NpcActor Find(string id) => id != null && byId.TryGetValue(id, out NpcActor actor) ? actor : null;
        public NpcActor Find(NpcDefinition definition) => definition == null ? null : Find(definition.Id);

        public void CollectLiving(List<NpcActor> into)
        {
            into.Clear();

            foreach (NpcActor actor in actors)
            {
                if (actor.IsAlive)
                    into.Add(actor);
            }
        }

        public void ApplyHour(int hour)
        {
            foreach (NpcActor actor in actors)
                actor.ApplyHour(hour);
        }

        public string CaptureJson()
        {
            NpcActorState[] states = new NpcActorState[actors.Count];
            for (int i = 0; i < actors.Count; i++)
                states[i] = actors[i].Capture();

            string[] buried = new string[dead.Count];
            dead.CopyTo(buried);

            return JsonUtility.ToJson(new NpcDirectoryState { Actors = states, Dead = buried });
        }

        public void RestoreJson(string json)
        {
            NpcDirectoryState state = JsonUtility.FromJson<NpcDirectoryState>(json);

            dead.Clear();

            if (state.Dead != null)
                foreach (string id in state.Dead) dead.Add(id);

            if (state.Actors != null)
                foreach (NpcActorState actorState in state.Actors)
                    Find(actorState.NpcId)?.Restore(actorState);

            // Somebody standing in the loaded town who was already dead when this was saved
            for (int i = actors.Count - 1; i >= 0; i--)
            {
                if (!dead.Contains(actors[i].Id))
                    continue;

                actors[i].gameObject.SetActive(false);
                byId.Remove(actors[i].Id);
                actors.RemoveAt(i);
            }
        }
    }
}
