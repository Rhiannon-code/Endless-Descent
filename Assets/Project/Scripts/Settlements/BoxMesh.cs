using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EndlessDescent.Settlements
{
    // Boxes merged into one mesh. A greybox town is tens of thousands of them, and one GameObject with
    // a collider apiece is what made a city ten thousand objects to build and to draw
    public sealed class BoxMesh
    {
        static readonly Vector3[] Faces =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
        };

        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> triangles = new List<int>();

        public void Add(Vector3 centre, Vector3 size, Quaternion rotation)
        {
            Vector3 half = size * 0.5f;

            foreach (Vector3 face in Faces)
            {
                Vector3 along = face.x != 0f ? Vector3.forward : Vector3.right;
                Vector3 across = Vector3.Cross(face, along);

                Vector3 middle = Vector3.Scale(face, half);
                Vector3 a = Vector3.Scale(along, half);
                Vector3 b = Vector3.Scale(across, half);

                int start = vertices.Count;
                Vector3 normal = rotation * face;

                foreach (Vector3 corner in new[] { middle - a - b, middle + a - b, middle + a + b, middle - a + b })
                {
                    vertices.Add(centre + rotation * corner);
                    normals.Add(normal);

                    // In metres, so a material tiles at the same size whatever it is on
                    uv.Add(new Vector2(Vector3.Dot(corner, along), Vector3.Dot(corner, across)));
                }

                // Winding is chosen by testing each face against its normal rather than written out by
                // hand, a face wound the wrong way is invisible, which is how the plants went missing
                bool outward = Vector3.Dot(Vector3.Cross(vertices[start + 1] - vertices[start],
                    vertices[start + 2] - vertices[start]), normal) > 0f;

                if (outward)
                    triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                else
                    triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
        }

        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
