using Sandbox.ModAPI;
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
        private int tickCounter = 0;
        private HashSet<IMyCubeGrid> grids = new HashSet<IMyCubeGrid>();
        private Logger lg;
        AHODConfig config;

        public override void UpdateBeforeSimulation()
        {
            tickCounter++;
            if (tickCounter == UpdateInterval)
            {
                tickCounter = 0;
                UpdateGrids();
            }
        }

        public override void BeforeStart()
        {
            base.BeforeStart();
            Init();
        }

        private void Init()
        {
            lg = new Logger(){ DebugLevel = 3};
            lg.File("Init start.", 2);
            config = new AHODConfig(lg);
            //TODO: Remove export before load in release build
            config.Export();
            config.Load();
            lg.File("Init done.", 2);
        }

        private void UpdateGrids()
        {
            ScanExistingGrids();
            foreach(IMyCubeGrid grid in grids)
            {
                Grid g = new Grid(grid, config, lg);
                g.ScanGrid();
                g.Update();
            }
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
