using System.Collections.Generic;
using EndlessDescent.Core;
using EndlessDescent.Data;
using EndlessDescent.Quests;
using EndlessDescent.Simulation;
using UnityEngine;

namespace EndlessDescent.World
{
    // Work posted in a town. Every posting is generated against somebody who actually lives here and
    // wants something for a reason of their own, so the board is the town's own business
    // rather than a list of errands. What is on it turns over every week
    [DisallowMultipleComponent]
    public class QuestBoard : MonoBehaviour, IInteractable
    {
        [SerializeField, Min(1)] int postings = 5;
        [SerializeField, Min(1)] int daysPerTurnover = 7;

        public sealed class Posting
        {
            public QuestTemplateDefinition Template;
            public NpcActor Giver;
            public int Seed;
            public Quest Offer;
        }

        readonly List<Posting> board = new List<Posting>();

        QuestJournal journal;
        PlaceLoader place;
        NpcDirectory npcs;

        public IReadOnlyList<Posting> Postings => board;
        public string TownName { get; private set; } = "the parish";

        public string Prompt => "Read the postings";

        public bool CanInteract(GameObject actor) => true;

        public void Interact(GameObject actor)
        {
            Refresh();
            QuestBoardScreens.Current?.Open(this);
        }

        public void Refresh()
        {
            board.Clear();

            if (journal == null) journal = FindFirstObjectByType<QuestJournal>();
            if (place == null) place = FindFirstObjectByType<PlaceLoader>();
            if (npcs == null) npcs = FindFirstObjectByType<NpcDirectory>();

            if (journal == null || journal.Database == null || place == null || place.Region == null)
                return;

            IReadOnlyList<QuestTemplateDefinition> templates = journal.Database.QuestTemplates;
            if (templates == null || templates.Count == 0)
                return;

            int town = NearestTown();
            if (town < 0)
                return;

            RegionLocation here = place.Region.Locations[town];
            TownName = here.DisplayName;

            IReadOnlyList<NpcDefinition> residents = here.Residents;
            if (residents == null || residents.Count == 0)
                return;

            // A week's postings are the same postings all week, and a different set next week
            int week = WorldClock.Instance != null ? WorldClock.Instance.Day / Mathf.Max(1, daysPerTurnover) : 0;
            int seed = town * 7919 + week * 104729;
            DeterministicRandom rng = new DeterministicRandom(seed);

            for (int attempt = 0; attempt < 60 && board.Count < postings; attempt++)
            {
                NpcActor giver = npcs != null ? npcs.Find(residents[rng.NextInt(residents.Count)]) : null;
                if (giver == null)
                    continue;

                QuestTemplateDefinition template = templates[rng.NextInt(templates.Count)];
                int questSeed = seed + attempt * 31;

                // A template that cannot fill reason, complication and consequence from live world
                // state refuses, and refusing is the feature
                if (!journal.TryPreview(template, giver, questSeed, out Quest offer))
                    continue;

                if (journal.IsActive(offer.Id) || journal.IsCompleted(offer.Id) || Listed(offer.Id))
                    continue;

                board.Add(new Posting { Template = template, Giver = giver, Seed = questSeed, Offer = offer });
            }
        }

        bool Listed(string questId)
        {
            foreach (Posting posting in board)
                if (posting.Offer != null && posting.Offer.Id == questId) return true;

            return false;
        }

        public bool Take(int index)
        {
            if (index < 0 || index >= board.Count || journal == null)
                return false;

            Posting posting = board[index];

            if (journal.StartGenerated(posting.Template, posting.Giver, posting.Seed) == null)
                return false;

            board.RemoveAt(index);
            return true;
        }

        int NearestTown()
        {
            Vector2 here = WorldOrigin.KmAt(transform.position);
            int nearest = -1;
            float best = float.MaxValue;

            for (int i = 0; i < place.Region.Locations.Count; i++)
            {
                RegionLocation location = place.Region.Locations[i];
                if (!location.IsSettlement)
                    continue;

                float km = Vector2.Distance(here, location.Position);

                if (km >= best)
                    continue;

                best = km;
                nearest = i;
            }

            return nearest;
        }
    }
}
