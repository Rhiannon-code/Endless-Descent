using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    // Walking a line across country that a person could actually walk. A straight line between two
    // points on a map goes through lakes, over mountains and into closed woodland; this goes round
    // them, which is what "direct" has to mean if it is to be offered as a choice beside the roads
    // The search itself lives in WorldRoute, which the roads use too, one router, one field, so a
    // way the empire could pave is a way a traveller could walk
    public static class WorldPaths
    {
        // A traveller climbs what a road never would, and stops at what nobody scrambles up with a
        // pack on. Thirty degrees is the cap, the great ranges stand well past it and are walls
        public const float MaxGrade = 0.58f;

        const float ClimbKmPerMetre = 0.012f;
        const float WaterKm = 400f;

        // Fording a watercourse away from a crossing: wading, or walking the bank until it shallows
        // A road or track already over it is a ford or a causeway and costs nothing extra
        const float FordKm = 6f;
        const float BridgeKm = 0.35f;

        // Closed woodland is roughly a third again as far as open ground, which is what makes a
        // route skirt a wood rather than walk the length of it
        const float CoverKm = 0.55f;

        public static Vector2[] CrossCountry(Vector2 from, Vector2 to, WorldSurface surface)
        {
            if (surface == null || surface.Route == null)
                return new[] { from, to };

            Vector2[] path = surface.Route.Plan(from, to,
                new WorldRoute.Terms(ClimbKmPerMetre, MaxGrade, WaterKm, FordKm, CoverKm,
                    km => Bridged(surface, km)));

            return path != null && path.Length >= 2 ? path : new[] { from, to };
        }

        // Somebody has already built the crossing here
        static bool Bridged(WorldSurface surface, Vector2 km)
        {
            return Carries(surface.Roads, km) || Carries(surface.Tracks, km);
        }

        static bool Carries(WorldRoads way, Vector2 km) =>
            way != null && way.Nearest(km).DistanceKm < BridgeKm;

        // Water you cannot walk through. A stream a foot deep is a ford and roads cross them all the
        // time, a lake is a wall. The line between the two is how far up you it comes
        public const float WadeMetres = 1.2f;

        public static bool Wet(WorldSurface surface, Vector2 km)
        {
            if (surface == null)
                return false;

            float level = surface.WaterMetres(km);

            return level > 0f
                ? level - surface.Height(km) > WadeMetres
                : surface.Height(km) < -WadeMetres;
        }

        public static float Length(IReadOnlyList<Vector2> path)
        {
            float total = 0f;

            for (int i = 0; i < path.Count - 1; i++)
                total += Vector2.Distance(path[i], path[i + 1]);

            return total;
        }
    }
}
