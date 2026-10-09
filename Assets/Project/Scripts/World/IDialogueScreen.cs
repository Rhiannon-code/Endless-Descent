using EndlessDescent.Quests;

namespace EndlessDescent.World
{
    public interface IDialogueScreen
    {
        void Open(DialogueRunner runner);
        void Close();
    }

    // Set by the screen when the HUD installs it, so a conversation does not search the scene for it
    public static class DialogueScreens
    {
        public static IDialogueScreen Current { get; set; }
    }
}
