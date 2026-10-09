using UnityEngine;

namespace EndlessDescent.Data
{
    public enum GameStartKind { None, NewCharacter, ContinueSave }

    public static class GameStart
    {
        // Raised when a character is created, so whatever is going to build the player out of it can
        // hear about it without the creator holding a reference to something in another scene
        public static event System.Action<CharacterBlueprint> Created;

        public static GameStartKind Kind { get; private set; }
        public static CharacterBlueprint Blueprint { get; private set; }
        public static string Slot { get; private set; }

        public static bool Requested => Kind != GameStartKind.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Kind = GameStartKind.None;
            Blueprint = default;
            Slot = null;
            Created = null;
        }

        public static void NewCharacter(CharacterBlueprint blueprint)
        {
            Kind = GameStartKind.NewCharacter;
            Blueprint = blueprint;
            Slot = null;
            Created?.Invoke(blueprint);
        }

        public static void Continue(string slot)
        {
            Kind = GameStartKind.ContinueSave;
            Blueprint = default;
            Slot = slot;
        }

        // Taken once, by whatever in the world scene acts on it, so a second scene load does not
        // silently re-roll the character. Subscribers are deliberately left alone, clearing them
        // here would quietly unhook the component that is mid-way through acting on this
        public static void Clear()
        {
            Kind = GameStartKind.None;
            Blueprint = default;
            Slot = null;
        }

        // Going back to the front end drops the subscribers as well, the components that were
        // listening belong to the world scene that is about to be unloaded, and a stale subscriber
        // to a static is how a destroyed object gets a callback
        public static void ClearForMenu() => ResetStatics();
    }
}
