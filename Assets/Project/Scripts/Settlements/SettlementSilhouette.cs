using EndlessDescent.Data;
using UnityEngine;

namespace EndlessDescent.Settlements
{
    // A town seen from kilometres off, one mesh of its building blocks and ramparts, always drawn. Every
    // box sits a little inside the real thing, so wherever the town is drawn it hides this, and wherever
    // SettlementVisibility has culled the town this stands in for it
    public static class SettlementSilhouette
    {
        const float Inset = 0.4f;
        const float Footing = 2f;

        public static Mesh Build(SettlementPlan plan, SettlementDefinition definition)
        {
            BoxMesh mesh = new BoxMesh();

            foreach (Building building in plan.Buildings)
            {
                float height = building.Storeys * definition.StoreyHeight - Inset + Footing;

                mesh.Add(new Vector3(building.Centre.x, building.Ground - Footing + height * 0.5f, building.Centre.y),
                    new Vector3(building.Size.x - Inset * 2f, height, building.Size.y - Inset * 2f),
                    Quaternion.Euler(0f, building.Yaw, 0f));
            }

            foreach ((Vector3 centre, Vector3 size) in SettlementGenerator.RampartPieces(plan, definition))
            {
                mesh.Add(centre - Vector3.up * (Footing + Inset) * 0.5f,
                    new Vector3(size.x - Inset, size.y - Inset + Footing, size.z - Inset), Quaternion.identity);
            }

            return mesh.ToMesh("Silhouette");
        }
    }
}
