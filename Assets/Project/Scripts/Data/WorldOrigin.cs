using System;
using UnityEngine;

namespace EndlessDescent.Data
{
    // Unity positions are measured from a point that follows the player. Places reach 279 km
    // from the world's own origin, where a float position moves in 3 cm steps and physics jitters. So
    // anything that means a spot on the map converts through here; anything measured from something
    // else in the scene does not need to
    public static class WorldOrigin
    {
        // World metres of Unity's origin, whole kilometres, and never vertical
        public static Vector3 Offset { get; private set; }

        // Raised after every scene position has moved by minus this much
        public static event Action<Vector3> Shifted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Offset = Vector3.zero;
            Shifted = null;
        }

        public static Vector3 ToWorld(Vector3 unity) => unity + Offset;
        public static Vector3 ToUnity(Vector3 world) => world - Offset;

        public static Vector2 KmAt(Vector3 unity) =>
            new Vector2(unity.x + Offset.x, unity.z + Offset.z) / WorldSurface.MetresPerKm;

        public static Vector3 UnityAt(Vector2 km, float height) => new Vector3(
            km.x * WorldSurface.MetresPerKm - Offset.x, height, km.y * WorldSurface.MetresPerKm - Offset.z);

        public static void Recentre(Vector3 by)
        {
            Offset += by;
            Shifted?.Invoke(by);
        }
    }
}
