using Sandbox.ModAPI;
using VRage.Utils;
using VRage.Game.Components;
using System.Collections.Generic;
using VRage.ModAPI;
using VRage.Game.ModAPI;
using System.Linq;

namespace AHOD
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class AHODSession : MySessionComponentBase
    {
        const int UpdateInterval = 100;
        private bool init = false;
        private Logger lg;
        private int tickCounter = 0;
        private HashSet<string> requiringSubtypes = new HashSet<string>();
        private HashSet<IMyCubeGrid> grids = new HashSet<IMyCubeGrid>();

        AHODConfig config;

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

        private void Init()
        {
            lg = new Logger(){ DebugLevel = Logger.MaxDebugLevel};
            lg.File("Init start.", 2);
            config = new AHODConfig(lg);
            //TODO: Remove export before load in release build
            config.Export();
            config.Load();
            foreach(BedRequirement req in config.BedRequirements)
            {
                requiringSubtypes.Add(req.SubtypeId);
                lg.File($"Requiring subtype added: {req.SubtypeId} requires {req.Beds} beds.", 3);
            }
            ScanExistingGrids();
            lg.File("Init done.", 2);
        }

        private void UpdateGrids()
        {
            ScanExistingGrids();
            foreach(IMyCubeGrid grid in grids)
            {
                int beds = CountBeds(grid);
                int bedsRequired = CountRequiredBeds(grid);
                lg.OnScreen($"Found {beds}/{bedsRequired} beds in {grid.DisplayName}", force: true);
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
            return config.BedRequirements.Find(r => r.SubtypeId == block.FatBlock.BlockDefinition.SubtypeId).Beds;
        }

        private bool IsRequiring(IMySlimBlock block)
        {
            return IsBlockCorrectType(block, requiringSubtypes);
        }

        private bool IsBed(IMySlimBlock block)
        {
            return IsBlockCorrectType(block, config.BedSubtypeIds);
        }

        private bool IsBlockCorrectType(IMySlimBlock block, IEnumerable<string> subtypelist)
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
    }
}
