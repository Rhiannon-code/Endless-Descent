using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    public sealed class WorldWaterData
    {
        public readonly int TileX;
        public readonly int TileY;

        // Standing water, one entry per distinct level in this tile. Vertices are a flat footprint,
        // the level is carried separately because the renderer levels the mesh to it
        public readonly List<(float Level, Vector3[] Vertices, int[] Triangles)> Still;

        // Running water, which slopes, so its heights are in the vertices
        public readonly Vector3[] Flowing;
        public readonly int[] FlowingTriangles;

        public bool Any => Still.Count > 0 || Flowing.Length > 0;

        WorldWaterData(int tileX, int tileY,
            List<(float, Vector3[], int[])> still, Vector3[] flowing, int[] flowingTriangles)
        {
            TileX = tileX;
            TileY = tileY;
            Still = still;
            Flowing = flowing;
            FlowingTriangles = flowingTriangles;
        }

        const float LevelGrouping = 0.25f;
        const float MinDepthMetres = 0.35f;

        public static WorldWaterData Build(WorldSurface surface, int tileX, int tileY, int resolution)
        {
            float cellKm = WorldTiles.TileKm / resolution;
            float cellMetres = cellKm * WorldSurface.MetresPerKm;

            Dictionary<int, Cells> still = new Dictionary<int, Cells>();
            Cells flowing = new Cells();

            float[,] corner = new float[resolution + 1, resolution + 1];

            for (int y = 0; y <= resolution; y++)
            for (int x = 0; x <= resolution; x++)
                corner[y, x] = surface.Height(WorldTiles.OriginKm(tileX, tileY)
                                              + new Vector2(x * cellKm, y * cellKm));

            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                Vector2 km = WorldTiles.OriginKm(tileX, tileY)
                             + new Vector2((x + 0.5f) * cellKm, (y + 0.5f) * cellKm);

                float ground = Mathf.Max(
                    Mathf.Max(corner[y, x], corner[y, x + 1]),
                    Mathf.Max(corner[y + 1, x], corner[y + 1, x + 1]));

                // The sea is not built here. One infinite surface at sea level covers every coast in
                // the world at once, and a mesh per tile would only be a worse copy of it
                float level = surface.WaterMetres(km);

                if (level > 0f && ground < level - MinDepthMetres)
                {
                    int key = Mathf.RoundToInt(level / LevelGrouping);

                    if (!still.TryGetValue(key, out Cells cells))
                        still[key] = cells = new Cells(resolution) { Level = key * LevelGrouping };

                    cells.Wet(x, y);
                    continue;
                }

                // Only asked where there is no standing water, a river running into a lake is the
                // lake, and two surfaces in one cell z-fight
                float channel = surface.ChannelMetres(km);

                if (channel > 0f && ground < channel - MinDepthMetres)
                    flowing.Quad(x, y, 1, cellMetres, channel - WorldTiles.FloorMetres);
            }

            List<(float, Vector3[], int[])> built = new List<(float, Vector3[], int[])>();

            foreach (Cells cells in still.Values)
            {
                cells.Emit(cellMetres);
                built.Add((cells.Level, cells.Vertices.ToArray(), cells.Triangles.ToArray()));
            }

            return new WorldWaterData(tileX, tileY, built,
                flowing.Vertices.ToArray(), flowing.Triangles.ToArray());
        }

        sealed class Cells
        {
            public float Level;
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();

            readonly bool[,] wet;
            readonly int resolution;

            public Cells(int resolution)
            {
                this.resolution = resolution;
                wet = new bool[resolution, resolution];
            }

            public Cells()
            {
                resolution = 0;
                wet = null;
            }

            public void Wet(int x, int y) => wet[y, x] = true;

            public void Emit(float cellMetres)
            {
                for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    if (!wet[y, x])
                        continue;

                    int run = 0;
                    while (x + run < resolution && wet[y, x + run]) run++;

                    Quad(x, y, run, cellMetres, 0f);
                    x += run - 1;
                }
            }

            public void Quad(int x, int y, int cells, float cellMetres, float height)
            {
                float x0 = x * cellMetres, x1 = x0 + cells * cellMetres;
                float y0 = y * cellMetres, y1 = y0 + cellMetres;
                int at = Vertices.Count;

                Vertices.Add(new Vector3(x0, height, y0));
                Vertices.Add(new Vector3(x0, height, y1));
                Vertices.Add(new Vector3(x1, height, y1));
                Vertices.Add(new Vector3(x1, height, y0));

                Triangles.Add(at); Triangles.Add(at + 1); Triangles.Add(at + 2);
                Triangles.Add(at); Triangles.Add(at + 2); Triangles.Add(at + 3);
            }
        }
    }
}
