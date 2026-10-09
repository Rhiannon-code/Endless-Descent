using UnityEngine;

namespace EndlessDescent.Data
{
    // The grid the world is realised on, and the only place that decides which world coordinate a
    // heightmap sample belongs to. The streamer and the seam check both go through it, because a
    // seam is exactly what you get when two pieces of code answer that question differently
    public static class WorldTiles
    {
        public const float TileKm = 1f;

        // The vertical band every terrain tile is scaled into. It has to be one band for the whole
        // world, neighbouring tiles with different vertical scales do not stitch. The surface spans
        // the -160 m ocean floor to about 2,400 m, and anything outside the band is clamped flat
        public const float FloorMetres = -175f;
        public const float CeilingMetres = 2450f;

        public static float Normalised(float metres) =>
            Mathf.Clamp01((metres - FloorMetres) / (CeilingMetres - FloorMetres));

        // Heights on a shared edge have to come out bit for bit identical, not merely close. With a
        // tile of one kilometre and a resolution of 2^n+1 the step is a negative power of two, so
        // the last sample of one tile lands on exactly the integer its neighbour starts from
        public static Vector2 SampleKm(int tileX, int tileY, int i, int j, int resolution) =>
            SampleKm(tileX, tileY, i, j, resolution, TileKm);

        public static Vector2 SampleKm(int tileX, int tileY, int i, int j, int resolution, float tileKm)
        {
            float step = tileKm / (resolution - 1);
            return new Vector2(tileX * tileKm + i * step, tileY * tileKm + j * step);
        }

        public static Vector2 OriginKm(int tileX, int tileY) =>
            new Vector2(tileX * TileKm, tileY * TileKm);

        public static Vector3 OriginMetres(int tileX, int tileY) =>
            new Vector3(tileX * TileKm * WorldSurface.MetresPerKm, 0f, tileY * TileKm * WorldSurface.MetresPerKm);

        public static Vector2Int TileAt(Vector3 metres) => new Vector2Int(
            Mathf.FloorToInt(metres.x / (TileKm * WorldSurface.MetresPerKm)),
            Mathf.FloorToInt(metres.z / (TileKm * WorldSurface.MetresPerKm)));

        // Where a splat texel sits, at its centre, not its corner, or the ground colour lands half a
        // texel off the height it belongs to
        public static Vector2 AlphaKm(int tileX, int tileY, int i, int j, int resolution)
        {
            float step = TileKm / resolution;
            return new Vector2(tileX * TileKm + (i + 0.5f) * step, tileY * TileKm + (j + 0.5f) * step);
        }
    }
}
