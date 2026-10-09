namespace EndlessDescent.Simulation
{
    // Lets a merchant open the trade UI without Simulation referencing the UI assembly, UI already
    // depends on Simulation, so the interface lives on this side and the screen implements it
    public interface ITradeScreen
    {
        void Open(Merchant merchant);
    }

    // Set by the screen when the HUD installs it. Finding it meant walking every behaviour in the
    // scene on each press of E, a built town and a dungeon beneath it are thousands of them
    public static class TradeScreens
    {
        public static ITradeScreen Current { get; set; }
    }
}
