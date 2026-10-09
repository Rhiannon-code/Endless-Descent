using EndlessDescent.Core;
using EndlessDescent.Items;
using EndlessDescent.Simulation;

namespace EndlessDescent.Quests
{
    // Quests read the simulation and write back through it; the simulation never knows quests
    // exist, this is the whole of the coupling, in one place
    public class QuestContext
    {
        public FactionRegistry Factions;
        public NpcDirectory Npcs;
        public Inventory Inventory;
        public Wallet Wallet;
        public QuestJournal Journal;
        public WorldClock Clock;

        public bool IsUsable => Factions != null && Npcs != null && Inventory != null && Wallet != null;
    }
}
