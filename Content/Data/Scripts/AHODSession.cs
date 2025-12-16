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
        private Dictionary<long, Grid> grids = new Dictionary<long, Grid>();
        private Logger lg;
        AHODConfig config;

        public override void LoadData()
        {
            Init();
            MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
            lg.File("AHODSession loaded.", 2);
        }

        protected override void UnloadData()
        {
            lg.File("Unloading AHODSession.", 2);
            MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;
            ClearGrids();
            lg.File("AHODSession unloaded.", 2);
        }

        private void EntityAdded(IMyEntity ent)
        {
            //TODO: If grid is created as non player owned, then later gets a player owner, we won't track it.
            if (IsPlayerOwnedGrid(ent))
            {
                IMyCubeGrid cubeGrid = ent as IMyCubeGrid;
                lg.File($"Entity added: {cubeGrid.DisplayName}", 2);
                RegisterGrid(cubeGrid);
            }
        }

        private void GridClose(IMyEntity ent)
        {
            lg.File($"Closing grid: {ent.DisplayName}", 2);
            RemoveGrid(ent as IMyCubeGrid);
        }

        private void ClearGrids()
        {
            lg.File("Clearing all tracked grids.", 2);
            foreach (KeyValuePair<long, Grid> pair in grids)
            {
                IMyCubeGrid cubeGrid = pair.Value?.CubeGrid;
                if (cubeGrid != null)
                {
                    RemoveGrid(pair.Value.CubeGrid);
                }
                else
                {
                    lg.File($"Warning: Attempted to remove null grid with EntityId {pair.Key}", 1);
                }
            }
        }

        private void RegisterGrid(IMyCubeGrid cubeGrid)
        {
            lg.File($"Registering grid {cubeGrid.DisplayName} for tracking.", 2);
            Grid grid = new Grid(cubeGrid, config, lg);
            grid.ScanGrid();
            grid.Update();

            grids.Add(cubeGrid.EntityId, grid);

            cubeGrid.OnClose += GridClose;
            cubeGrid.OnBlockAdded += grid.AddBlock;
            cubeGrid.OnBlockRemoved += grid.RemoveBlock;
        }

        private void RemoveGrid(IMyCubeGrid cubeGrid)
        {
            lg.File($"Removing grid {cubeGrid.DisplayName} from tracking.", 2);
            if (grids.ContainsKey(cubeGrid.EntityId))
            {
                Grid grid = grids[cubeGrid.EntityId];
                cubeGrid.OnClose -= GridClose;
                cubeGrid.OnBlockAdded -= grid.AddBlock;
                cubeGrid.OnBlockRemoved -= grid.RemoveBlock;
                grids.Remove(cubeGrid.EntityId);
            }
            else
            {
                lg.File($"Warning: Attempted to remove grid that is not tracked: {cubeGrid.DisplayName}", 1);
            }
        }

        private void Init()
        {
            //TODO: What debug level to use before config is loaded? Logging might be needed to debug config loading.
            lg = new Logger() { DebugLevel = 3 };
            lg.File("Init start.", 2);
            config = new AHODConfig(lg);
            //TODO: Remove export before load in release build
            config.Export();
            config.Load();
            lg.File("Init done.", 2);
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
