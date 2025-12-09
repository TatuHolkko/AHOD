using Sandbox.ModAPI;
using VRage.Utils;
using VRage.Game.Components;
using System.Collections.Generic;
using VRage.ModAPI;
using VRage.Game.ModAPI;

namespace AHOD
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class AHODSession : MySessionComponentBase
    {
        const int UpdateInterval = 100;

        private static readonly Dictionary<string, int> BedRequirements = new Dictionary<string, int>
        {
            {"LargeRefinery", 5},
            {"LargeRefineryIndustrial", 5},
            {"LargePrototechRefinery", 10},
            {"Blast Furnace", 3},
            {"LargeAssembler", 5},
            {"BasicAssembler", 2},
            {"LargeAssemblerIndustrial", 5},
            {"LargePrototechAssembler", 10},
        };

        private static readonly HashSet<string> BedSubtypes = new HashSet<string>
        {
            "LargeBlockBed",
            "LargeBlockHalfBed",
            "LargeBlockHalfBedOffset",
            "LargeBlockInsetBed",
            "LargeBlockBedFree",
        };

        public override void UpdateBeforeSimulation()
        {
            if (!init)
            {
                Init();
                init = true;
            }

            tickCounter++;
            if (tickCounter == UpdateInterval)
            {
                tickCounter = 0;
                UpdateGrids();
            }
        }

        private void UpdateGrids()
        {
            ScanExistingGrids();
            foreach(IMyCubeGrid grid in grids)
            {
                int beds = CountBeds(grid);
                int bedsRequired = CountRequiredBeds(grid);
                lg.Message($"Found {beds}/{bedsRequired} beds in {grid.DisplayName}");
            }
        }

        private int CountRequiredBeds(IMyCubeGrid grid)
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks, b => IsRequiring(b));
            int sum = 0;
            foreach(IMySlimBlock b in blocks)
            {
                sum += RequiredBeds(b);
            }
            return sum;
        }

        private int CountBeds(IMyCubeGrid grid)
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks, b => IsBed(b));
            return blocks.Count;
        }



        private void Init()
        {
            lg = new Logger();
            lg.Message("Init start.");
            grids = new HashSet<IMyCubeGrid>();
            requiringSubtypes = new HashSet<string>();
            foreach(string subtype in BedRequirements.Keys)
            {
                requiringSubtypes.Add(subtype);
            }
            ScanExistingGrids();
            lg.Message("Init done.");
        }

        private void ScanExistingGrids()
        {
            grids.Clear();
            HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities, e => IsPlayerOwnedGrid(e));
            foreach (IMyEntity ent in entities)
            {
                IMyCubeGrid grid = ent as IMyCubeGrid;
                grids.Add(grid);
            }
        }

        private int RequiredBeds(IMySlimBlock block)
        {
            if (!IsRequiring(block))
            {
                return 0;
            }
            return BedRequirements[block.FatBlock.BlockDefinition.SubtypeId];
        }

        private bool IsRequiring(IMySlimBlock block)
        {
            return IsBlockCorrectType(block, requiringSubtypes);
        }

        private bool IsBed(IMySlimBlock block)
        {
            return IsBlockCorrectType(block, BedSubtypes);
        }

        private bool IsBlockCorrectType(IMySlimBlock block, HashSet<string> subtypelist)
        {
            IMyCubeBlock cb = block.FatBlock;
            if (cb == null)
            {
                return false;
            }
            return subtypelist.Contains(cb.BlockDefinition.SubtypeId);
        }

        private bool IsPlayerOwnedGrid(IMyEntity ent)
        {
            IMyCubeGrid grid = ent as IMyCubeGrid;
            if (grid == null)
            {
                return false;
            }
            if (grid.SmallOwners.Contains(MyAPIGateway.Session.Player.IdentityId))
            {
                return true;
            }
            if (grid.BigOwners.Contains(MyAPIGateway.Session.Player.IdentityId))
            {
                return true;
            }
            return false;
        }

        private bool init = false;
        private Logger lg;
        private int tickCounter = 0;
        private HashSet<string> requiringSubtypes;
        private HashSet<IMyCubeGrid> grids;
    }
}
