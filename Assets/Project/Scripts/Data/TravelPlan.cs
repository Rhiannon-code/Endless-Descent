using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    public enum TravelPace { Cautious, Reckless }
    public enum TravelLodging { Inn, Camping }
    public enum TravelWay { Paved, Dirt, Direct, Passage }

    public readonly struct TravelPlan
    {
        public const float RoadKmPerDay = 32f;
        public const float TrackKmPerDay = 25f;
        public const float CrossKmPerDay = 18f;
        public const float RecklessFactor = 0.75f;
        public const int InnGoldPerDay = 6;

        // A ship sails through the night, which is most of why it beats walking. The fare is the
        // master's, and it is not cheap: he is the only way across
        public const float SailKmPerDay = 110f;
        public const int PassageGold = 8;
        public const float PassageGoldPerKm = 0.12f;

        public readonly Vector2[] Path;
        public readonly float RoadKm;
        public readonly float CrossKm;
        public readonly float SeaKm;
        public readonly float Hours;
        public readonly int Gold;
        public readonly TravelPace Pace;
        public readonly TravelLodging Lodging;
        public readonly TravelWay Way;

        public float TotalKm => RoadKm + CrossKm + SeaKm;
        public int Days => Mathf.Max(1, Mathf.CeilToInt(Hours / 24f));
        public bool OnRoad => RoadKm > CrossKm;

        TravelPlan(Vector2[] path, float roadKm, float crossKm, float hours, int gold,
            TravelPace pace, TravelLodging lodging, TravelWay way, float seaKm = 0f)
        {
            Way = way;
            Path = path;
            RoadKm = roadKm;
            CrossKm = crossKm;
            SeaKm = seaKm;
            Hours = hours;
            Gold = gold;
            Pace = pace;
            Lodging = lodging;
        }

        public static TravelPlan For(WorldSurface surface, Vector2 fromKm, Vector2 toKm,
            TravelPace pace, TravelLodging lodging, TravelWay way)
        {
            // Where the ground runs out, no choice about roads is the question being asked
            if (surface?.Route != null && !surface.Route.Walkable(fromKm, toKm))
                return BySea(surface, fromKm, toKm, pace, lodging);

            WorldRoads network = surface == null ? null
                : way == TravelWay.Paved ? surface.Roads
                : way == TravelWay.Dirt ? surface.Tracks
                : null;

            float perDay = way == TravelWay.Paved ? RoadKmPerDay
                : way == TravelWay.Dirt ? TrackKmPerDay
                : CrossKmPerDay;

            // Cross country is walked, not ruled, a straight line goes through lakes and over
            // mountains, and "direct" is a choice about roads, not about drowning
            Vector2[] path = WorldPaths.CrossCountry(fromKm, toKm, surface);
            float roadKm = 0f;
            float crossKm = WorldPaths.Length(path);
            float days = crossKm / CrossKmPerDay;

            // The way is the player's choice, not an optimisation. A road is nearly always the longer
            // way round and often the quicker one, and sometimes neither, the panel shows the days
            // for each and lets them decide. Picking the faster one automatically meant choosing
            // "dirt track" silently gave you open country whenever the tracks went the long way
            if (network != null)
            {
                List<Vector2> viaWay = network.Route(fromKm, toKm, out float onWay, out float approach,
                    (a, b) => WorldPaths.CrossCountry(a, b, surface));

                if (onWay > 0f)
                {
                    path = viaWay.ToArray();
                    roadKm = onWay;
                    crossKm = approach;
                    days = onWay / perDay + approach / CrossKmPerDay;
                }
            }

            // Recklessly is faster because you do not stop when you should. What it costs you is not
            // priced here, that is for whatever eventually rolls against it
            if (pace == TravelPace.Reckless)
                days *= RecklessFactor;

            float hours = Mathf.Max(1f, days * 24f);
            int gold = lodging == TravelLodging.Inn
                ? Mathf.CeilToInt(days) * InnGoldPerDay
                : 0;

            return new TravelPlan(path, roadKm, crossKm, hours, gold, pace, lodging, way);
        }

        // Land to a harbour, over the water, and off the other end on foot. The two land legs are
        // routed like any other cross country journey, only the crossing itself is a straight line,
        // because a ship's is
        static TravelPlan BySea(WorldSurface surface, Vector2 fromKm, Vector2 toKm,
            TravelPace pace, TravelLodging lodging)
        {
            WorldRoute route = surface.Route;
            int here = route.Landmass(fromKm), there = route.Landmass(toKm);

            Vector2 depart = Harbour(route, here, fromKm, out bool sailsFrom);
            Vector2 arrive = Harbour(route, there, toKm, out bool sailsTo);

            // Nowhere on the Cold Reach is a harbour, so the ship puts in on the nearest shore it
            // can and the rest is walked
            if (!sailsTo) arrive = route.NearestLanding(there, depart);
            if (!sailsFrom) depart = route.NearestLanding(here, arrive);

            Vector2[] onto = WorldPaths.CrossCountry(fromKm, depart, surface);
            Vector2[] away = WorldPaths.CrossCountry(arrive, toKm, surface);

            List<Vector2> path = new List<Vector2>(onto);
            path.AddRange(away);

            float landKm = WorldPaths.Length(onto) + WorldPaths.Length(away);
            float seaKm = Vector2.Distance(depart, arrive);
            float days = landKm / CrossKmPerDay + seaKm / SailKmPerDay;

            if (pace == TravelPace.Reckless)
                days *= RecklessFactor;

            float hours = Mathf.Max(1f, days * 24f);
            int gold = PassageGold + Mathf.CeilToInt(seaKm * PassageGoldPerKm)
                       + (lodging == TravelLodging.Inn ? Mathf.CeilToInt(days) * InnGoldPerDay : 0);

            return new TravelPlan(path.ToArray(), 0f, landKm, hours, gold, pace, lodging,
                TravelWay.Passage, seaKm);
        }

        static Vector2 Harbour(WorldRoute route, int landmass, Vector2 near, out bool found)
        {
            found = false;
            Vector2 best = near;
            float span = float.MaxValue;

            foreach (OrdovanPlaces.Place place in OrdovanPlaces.All)
            {
                if (place.Tier != SettlementTier.Harbour && place.Tier != SettlementTier.Port
                    && place.Tier != SettlementTier.PortCity)
                    continue;

                if (route.Landmass(place.Km) != landmass)
                    continue;

                float span2 = Vector2.Distance(place.Km, near);

                if (span2 >= span)
                    continue;

                span = span2;
                best = place.Km;
                found = true;
            }

            return best;
        }

        public string Describe()
        {
            string made = Way == TravelWay.Passage
                ? $"{SeaKm:0} km of it by sea"
                : Way == TravelWay.Direct || RoadKm <= 0f
                    ? "cross country"
                    : $"{RoadKm:0} km by {(Way == TravelWay.Paved ? "road" : "track")}";

            return $"{TotalKm:0} km  ({made})   {Hours / 24f:0.0} days" +
                   (Gold > 0 ? $"   {Gold} gold" : "   no lodging");
        }
    }
}
