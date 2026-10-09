using UnityEngine;

namespace EndlessDescent.Data
{
    public static class WorldGeometry
    {
        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;

            if (lengthSquared < 1e-6f)
                return Vector2.Distance(p, a);

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
            return Vector2.Distance(p, a + ab * t);
        }

        public static float DistanceToPath(Vector2 p, Vector2[] path)
        {
            if (path == null || path.Length == 0)
                return float.MaxValue;

            if (path.Length == 1)
                return Vector2.Distance(p, path[0]);

            float best = float.MaxValue;

            for (int i = 0; i < path.Length - 1; i++)
                best = Mathf.Min(best, DistanceToSegment(p, path[i], path[i + 1]));

            return best;
        }

        public static float DistanceToEdge(Vector2 p, Vector2[] polygon)
        {
            if (polygon == null || polygon.Length < 2)
                return float.MaxValue;

            float best = float.MaxValue;

            for (int i = 0; i < polygon.Length; i++)
                best = Mathf.Min(best, DistanceToSegment(p, polygon[i], polygon[(i + 1) % polygon.Length]));

            return best;
        }

        public static bool Inside(Vector2 p, Vector2[] polygon)
        {
            if (polygon == null || polygon.Length < 3)
                return false;

            bool inside = false;

            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];

                if (a.y > p.y != b.y > p.y &&
                    p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        // Negative inside, positive outside, in the same units as the polygon
        public static float SignedDistance(Vector2 p, Vector2[] polygon) =>
            Inside(p, polygon) ? -DistanceToEdge(p, polygon) : DistanceToEdge(p, polygon);

        // Which side of an open path a point falls on, from the nearest segment, negative left of
        // travel, positive right. The escarpment needs this to know which side is the high one
        public static float Side(Vector2 p, Vector2[] path)
        {
            if (path == null || path.Length < 2)
                return 0f;

            float best = float.MaxValue;
            float side = 0f;

            for (int i = 0; i < path.Length - 1; i++)
            {
                float distance = DistanceToSegment(p, path[i], path[i + 1]);

                if (distance >= best)
                    continue;

                best = distance;
                Vector2 ab = path[i + 1] - path[i];
                Vector2 ap = p - path[i];
                side = ab.x * ap.y - ab.y * ap.x;
            }

            return side < 0f ? -1f : 1f;
        }

        public static float SmoothStep(float edge0, float edge1, float x)
        {
            if (Mathf.Abs(edge1 - edge0) < 1e-6f)
                return x < edge0 ? 0f : 1f;

            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
