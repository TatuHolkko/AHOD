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
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;

            ClearGrids();
            lg.File("AHODSession unloaded.", 2);
        }

        private void EntityAdded(IMyEntity ent)
        {
            if (IsPlayerOwnedGrid(ent))
            {
                IMyCubeGrid cubeGrid = ent as IMyCubeGrid;

                lg.File($"EntityAdded: New grid detected: {cubeGrid.DisplayName}", 2);
                lg.OnScreen($"EntityAdded: New grid detected: {cubeGrid.DisplayName}", durationMs: 2000, level: 3, color: "White");

                Grid grid = new Grid(cubeGrid, config, lg);
                grid.ScanGrid();
                grid.Update();

                grids.Add(cubeGrid.EntityId, grid);

                cubeGrid.OnMarkForClose += GridMarkedForClose;
                cubeGrid.OnBlockAdded += grid.AddBlock;
                cubeGrid.OnBlockRemoved += grid.RemoveBlock;
            }
        }

        private void GridMarkedForClose(IMyEntity ent)
        {
            lg.File($"Grid marked for close: {ent.DisplayName}", 2);
            RemoveGrid(ent as IMyCubeGrid);
        }

        private void ClearGrids()
        {
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

        private void RemoveGrid(IMyCubeGrid cubeGrid)
        {
            if (grids.ContainsKey(cubeGrid.EntityId))
            {
                Grid grid = grids[cubeGrid.EntityId];
                cubeGrid.OnMarkForClose -= GridMarkedForClose;
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
