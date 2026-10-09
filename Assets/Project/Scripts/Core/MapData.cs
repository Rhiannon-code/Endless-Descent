using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Core
{
    public enum MapMarkerKind { Room, Corridor, Street, Square, Building, Entrance, Exit, Boss, Water }

    // One footprint on the local map, in world XZ. Dungeons and settlements are drawn by the same
    // screen because both describe themselves this way rather than the screen knowing either
    public struct MapMarker
    {
        public Vector2 Centre;
        public Vector2 Size;
        public float Rotation;
        public int Level;
        public string Label;
        public int Purpose;
        public MapMarkerKind Kind;
    }

    public interface IMapSource
    {
        string PlaceName { get; }
        int LevelCount { get; }
        void CollectMarkers(List<MapMarker> into);
    }
}
