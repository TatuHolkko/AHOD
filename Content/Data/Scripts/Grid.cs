using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace AHOD
{
    public class Grid
    {
        public IMyCubeGrid CubeGrid { get; }
        public int BedCount { get; set; } = 0;
        public int RequiredBedCount { get; set; } = 0;
        public float Efficiency { get; private set; } = 0f;
        AHODConfig config;
        Logger lg;
        public Grid(IMyCubeGrid grid, AHODConfig config, Logger logger)
        {
            CubeGrid = grid;
            this.config = config;
            lg = new Logger() {
                GridName = grid.DisplayName,
                AvoidDuplicates = logger.AvoidDuplicates,
                DebugLevel = logger.DebugLevel,
                Enabled = logger.Enabled,
                FileLogging = logger.FileLogging,
                OnScreenLogging = logger.OnScreenLogging
                };
        }

        public void Update()
        {
            float oldEfficiency = Efficiency;
            RecalculateEfficency();
            if (oldEfficiency != Efficiency)
            {
                lg.File($"Efficiency updated: {Efficiency:P0}", 2);
                lg.OnScreen($"Efficiency updated: {Efficiency:P0}", durationMs: 2000, level: 3, color: "White", force: true);
                ApplyNewEfficiency();
            }
        }

        public void AddBlock(IMySlimBlock block)
        {
            lg.File($"Adding block {block?.FatBlock?.BlockDefinition.SubtypeId}", 3);
            if (IsBed(block))
            {
                ChangeBedCount(1);
            }
            else if (RequiresBeds(block))
            {
                foreach (BedRequirement br in config.BedRequirements)
                {
                    IMyCubeBlock cb = block.FatBlock;
                    if (cb.BlockDefinition.SubtypeId == br.SubtypeId)
                    {
                        ChangeRequiredBedCount(br.Beds);
                        break;
                    }
                }
            }
            Update();
        }

        public void RemoveBlock(IMySlimBlock block)
        {
            lg.File($"Removing block {block?.FatBlock?.BlockDefinition.SubtypeId}", 3);
            if (IsBed(block))
            {
                ChangeBedCount(-1);
            }
            else if (RequiresBeds(block))
            {
                foreach (BedRequirement br in config.BedRequirements)
                {
                    IMyCubeBlock cb = block.FatBlock;
                    if (cb.BlockDefinition.SubtypeId == br.SubtypeId)
                    {
                        ChangeRequiredBedCount(-br.Beds);
                        break;
                    }
                }
            }
            Update();
        }

        public void ScanGrid()
        {
            BedCount = CountBeds();
            RequiredBedCount = CountRequiredBeds();
            lg.File($"Scanned: BC={BedCount}, RBC={RequiredBedCount}", 2);
        }

        public void ChangeBedCount(int amount)
        {
            lg.File($"Changing BedCount by {amount}, new value {BedCount + amount}.", 3);
            BedCount += amount;
            if (BedCount < 0)
            {
                lg.File($"Warning: BedCount went below zero. Resetting to zero.", 1);
                lg.OnScreen($"Warning: BedCount went below zero. Resetting to zero.", durationMs: 2000, level: 2, color: "Red");
                BedCount = 0;
            }
        }

        public void ChangeRequiredBedCount(int amount)
        {
            lg.File($"Changing RequiredBedCount by {amount}, new value {RequiredBedCount + amount}.", 3);
            RequiredBedCount += amount;
            if (RequiredBedCount < 0)
            {
                lg.File($"Warning: RequiredBedCount went below zero. Resetting to zero.", 1);
                lg.OnScreen($"Warning: RequiredBedCount went below zero. Resetting to zero.", durationMs: 2000, level: 2, color: "Red");
                RequiredBedCount = 0;
            }
        }

        private void ApplyNewEfficiency()
        {
            //TODO: Apply efficiency to grid systems
        }

        private void RecalculateEfficency()
        {
            if (RequiredBedCount == 0)
            {
                Efficiency = 1f;
            }
            else
            {
                Efficiency = (float)BedCount / RequiredBedCount;
                if (Efficiency > 1f)
                {
                    Efficiency = 1f;
                }
            }
            Efficiency = RoundEfficiency(Efficiency);
        }

        private int CountRequiredBeds()
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            CubeGrid.GetBlocks(blocks, b => RequiresBeds(b));
            int sum = 0;
            foreach (IMySlimBlock block in blocks)
            {
                IMyCubeBlock cb = block.FatBlock;
                foreach (BedRequirement br in config.BedRequirements)
                {
                    if (cb.BlockDefinition.SubtypeId == br.SubtypeId)
                    {
                        lg.File($"Block {cb.DisplayName} requires {br.Beds} beds.", 4);
                        sum += br.Beds;
                        break;
                    }
                }
            }
            return sum;
        }
        private int CountBeds()
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            CubeGrid.GetBlocks(blocks, b => IsBed(b));
            return blocks.Count;
        }

        private bool RequiresBeds(IMySlimBlock block)
        {
            IMyCubeBlock cb = block.FatBlock;
            if (cb == null)
            {
                return false;
            }
            foreach (BedRequirement br in config.BedRequirements)
            {
                if (cb.BlockDefinition.SubtypeId == br.SubtypeId)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsBed(IMySlimBlock block)
        {
            IMyCubeBlock cb = block.FatBlock;
            if (cb == null)
            {
                return false;
            }
            foreach (string subtypeId in config.BedSubtypeIds)
            {
                if (cb.BlockDefinition.SubtypeId == subtypeId)
                {
                    return true;
                }
            }
            return false;
        }

        private float RoundEfficiency(float efficiency)
        {
            //nearest percent
            //(float)System.Math.Round(efficiency, 2);

            //nearest 5 percent
            return (float)System.Math.Round(efficiency * 20f) / 20;
        }
    }
}