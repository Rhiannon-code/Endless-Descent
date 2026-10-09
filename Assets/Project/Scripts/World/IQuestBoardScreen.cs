namespace EndlessDescent.World
{
    // The board says what is posted; the screen is whatever shows it. Same shape as ITradeScreen, for
    // the same reason, the world should not know what the UI looks like
    public interface IQuestBoardScreen
    {
        void Open(QuestBoard board);
    }

    // Set by the screen when the HUD installs it, so the board does not search the scene for it
    public static class QuestBoardScreens
    {
        public static IQuestBoardScreen Current { get; set; }
    }
}
