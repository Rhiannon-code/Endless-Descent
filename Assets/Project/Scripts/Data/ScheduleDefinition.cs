using System;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessDescent.Data
{
    public enum NpcActivity { Sleeping, Working, Eating, Socialising, Travelling, Worshipping, Idle }

    [CreateAssetMenu(menuName = "Endless Descent/World/Schedule", fileName = "Schedule")]
    public class ScheduleDefinition : ScriptableObject
    {
        [Serializable]
        public class Block
        {
            [Range(0, 23)] public int StartHour;
            public NpcActivity Activity = NpcActivity.Idle;
            public LocationDefinition Location;
        }

        [SerializeField] Block[] blocks;

        public IReadOnlyList<Block> Blocks => blocks;

        // Blocks run until the next one starts, so the active block is the latest whose start hour
        // has passed, before the first block the day wraps to the last
        public Block BlockFor(int hour)
        {
            if (blocks == null || blocks.Length == 0)
                return null;

            Block best = null;
            foreach (Block block in blocks)
            {
                if (block.StartHour <= hour && (best == null || block.StartHour > best.StartHour))
                    best = block;
            }

            if (best != null)
                return best;

            Block latest = blocks[0];
            foreach (Block block in blocks)
            {
                if (block.StartHour > latest.StartHour)
                    latest = block;
            }

            return latest;
        }
    }
}
