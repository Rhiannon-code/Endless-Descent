using System;
using UnityEngine;

namespace EndlessDescent.Core
{
    public enum SpaceKind { Overworld, Interior, Dungeon }

    // Which space the player is in, as far as drawing goes. Interiors and dungeons sit under
    // the ground they belong to, so the camera looks up through the world at everything above, whatever
    // is not in the current space stops drawing
    public static class CurrentSpace
    {
        public static SpaceKind Kind { get; private set; }
        public static bool Outdoors => Kind == SpaceKind.Overworld;

        public static event Action<SpaceKind> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Kind = SpaceKind.Overworld;
            Changed = null;
        }

        public static void Enter(SpaceKind kind)
        {
            if (kind == Kind)
                return;

            Kind = kind;
            Changed?.Invoke(kind);
        }
    }
}
