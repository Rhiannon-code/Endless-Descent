using UnityEngine;

namespace EndlessDescent.Data
{
    // The rectangle the world map covers, in kilometres. The exporter paints this rectangle and the
    // map page lays its markers over the same one, so a town sits where the picture drew it. Two
    // separately computed extents would put every marker slightly off its own coastline
    public static class WorldMapView
    {
        public const float MarginKm = 12f;

        public static Rect Bounds(WorldGeography geography)
        {
            Rect land = geography.Extent();

            float minX = land.xMin, minY = land.yMin;
            float maxX = land.xMax, maxY = land.yMax;

            foreach (OrdovanPlaces.Place place in OrdovanPlaces.All)
            {
                minX = Mathf.Min(minX, place.Km.x); maxX = Mathf.Max(maxX, place.Km.x);
                minY = Mathf.Min(minY, place.Km.y); maxY = Mathf.Max(maxY, place.Km.y);
            }

            return Rect.MinMaxRect(minX - MarginKm, minY - MarginKm, maxX + MarginKm, maxY + MarginKm);
        }

        // Where a place falls inside the map picture, 0..1 from its bottom left
        public static Vector2 Fraction(Rect bounds, Vector2 km) => new Vector2(
            (km.x - bounds.xMin) / bounds.width,
            (km.y - bounds.yMin) / bounds.height);
    }
}
