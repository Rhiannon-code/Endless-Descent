using EndlessDescent.Data;
using EndlessDescent.Quests;
using UnityEngine;

namespace EndlessDescent.World
{
    // Where a quest wants you to go. Daggerfall told you nothing and its modern version added optional
    // markers, which is the single change people credit with making its side work playable
    // The journal knows who and what, only the region knows where
    public static class QuestDirections
    {
        public readonly struct Heading
        {
            public readonly string Task;
            public readonly string Place;
            public readonly Vector2 Km;

            public Heading(string task, string place, Vector2 km)
            {
                Task = task;
                Place = place;
                Km = km;
            }
        }

        public static bool TryFind(Quest quest, RegionMap region, Vector2 hereKm, out Heading heading)
        {
            heading = default;

            if (quest == null || region == null)
                return false;

            QuestObjectiveData objective = CurrentObjective(quest);
            if (objective == null)
                return false;

            int where = Where(objective, quest, region, hereKm);

            if (where < 0)
                return false;

            RegionLocation location = region.Locations[where];
            heading = new Heading(objective.Description, location.DisplayName, location.Position);
            return true;
        }

        // The first thing on the current stage still to be done
        public static QuestObjectiveData CurrentObjective(Quest quest)
        {
            QuestStage stage = quest?.CurrentStage;

            if (stage == null)
                return null;

            foreach (QuestObjective candidate in stage.Objectives)
                if (!candidate.IsComplete) return candidate.Data;

            return null;
        }

        static int Where(QuestObjectiveData objective, Quest quest, RegionMap region, Vector2 hereKm)
        {
            switch (objective.Kind)
            {
                case QuestObjectiveKind.TalkToNpc:
                    return TownOf(objective.Npc, region);

                case QuestObjectiveKind.ReachLocation:
                    return Named(objective.Location, region);

                case QuestObjectiveKind.ClearDungeon:
                    return objective.Location != null ? Named(objective.Location, region) : NearestDungeon(region, hereKm);

                // A thing to find or a thing to kill could be anywhere; what is known is who is waiting
                // for it, and that is worth more than nothing
                default:
                    return TownOf(quest.Giver, region);
            }
        }

        static int TownOf(NpcDefinition npc, RegionMap region)
        {
            if (npc == null)
                return -1;

            for (int i = 0; i < region.Locations.Count; i++)
            {
                NpcDefinition[] residents = region.Locations[i].Residents;

                if (residents == null)
                    continue;

                foreach (NpcDefinition resident in residents)
                    if (resident == npc) return i;
            }

            return -1;
        }

        static int Named(LocationDefinition location, RegionMap region)
        {
            if (location == null)
                return -1;

            for (int i = 0; i < region.Locations.Count; i++)
                if (region.Locations[i].DisplayName == location.DisplayName) return i;

            return -1;
        }

        static int NearestDungeon(RegionMap region, Vector2 hereKm)
        {
            int nearest = -1;
            float best = float.MaxValue;

            for (int i = 0; i < region.Locations.Count; i++)
            {
                RegionLocation location = region.Locations[i];

                if (location.IsSettlement || location.Dungeon == null)
                    continue;

                float km = Vector2.Distance(hereKm, location.Position);

                if (km >= best)
                    continue;

                best = km;
                nearest = i;
            }

            return nearest;
        }
    }
}
