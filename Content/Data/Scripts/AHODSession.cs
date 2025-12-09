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

        public static readonly HashSet<string> BedSubtypes = new HashSet<string>
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
                lg.Message($"Found {beds} beds in {grid.DisplayName}");
            }
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
            grids = new HashSet<IMyCubeGrid>();
            lg.Message("Init start.");
            ScanExistingGrids();
            lg.Message("Init done.");
        }

        private void ScanExistingGrids()
        {
            grids.Clear();
            HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities, e => IsPlayerOwnedGrid(e));

            //lg.Message($"Found {entities.Count} player owned grids.");

            foreach (IMyEntity ent in entities)
            {
                IMyCubeGrid grid = ent as IMyCubeGrid;
                //lg.Message($"Found grid: {grid.DisplayName}");
                grids.Add(grid);
            }
        }

        private bool IsBed(IMySlimBlock block)
        {
            IMyCubeBlock cb = block.FatBlock;
            if (cb == null)
            {
                return false;
            }
            return BedSubtypes.Contains(cb.BlockDefinition.SubtypeId);
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
        private HashSet<IMyCubeGrid> grids;
    }
}
