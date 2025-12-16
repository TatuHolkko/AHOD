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
        private Dictionary<long, Grid> activeGrids = new Dictionary<long, Grid>();
        private Dictionary<long, IMyCubeGrid> passiveGrids = new Dictionary<long, IMyCubeGrid>();
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
            UnregisterAll();
            lg.File("AHODSession unloaded.", 2);
        }

        private void EntityAdded(IMyEntity ent)
        {
            if (!IsGrid(ent))
            {
                return;
            }
            IMyCubeGrid cubeGrid = ent as IMyCubeGrid;
            if (IsPlayerOwnedGrid(cubeGrid))
            {
                RegisterActiveGrid(cubeGrid);
            }
            else
            {
                RegisterPassiveGrid(cubeGrid);
            }
        }

        private void ActiveGridClose(IMyEntity ent)
        {
            lg.File($"Closing grid: {ent.DisplayName}", 2);
            UnregisterActiveGrid(ent as IMyCubeGrid);
        }

        private void UnregisterAll()
        {
            lg.File($"Clearing all {activeGrids.Count} active tracked grids.", 2);
            foreach (KeyValuePair<long, Grid> pair in activeGrids)
            {
                IMyCubeGrid cubeGrid = pair.Value?.CubeGrid;
                if (cubeGrid != null)
                {
                    UnregisterActiveGrid(pair.Value.CubeGrid);
                }
                else
                {
                    lg.File($"Warning: Attempted to remove null grid with EntityId {pair.Key}", 1);
                }
            }
            activeGrids.Clear();

            lg.File($"Clearing all {passiveGrids.Count} passive tracked grids.", 2);
            foreach (KeyValuePair<long, IMyCubeGrid> pair in passiveGrids)
            {
                IMyCubeGrid cubeGrid = pair.Value;
                if (cubeGrid != null)
                {
                    UnregisterPassiveGrid(cubeGrid);
                }
                else
                {
                    lg.File($"Warning: Attempted to remove null passive grid with EntityId {pair.Key}", 1);
                }
            }
            passiveGrids.Clear();
        }

        private void RegisterActiveGrid(IMyCubeGrid cubeGrid)
        {
            lg.File($"Registering grid {cubeGrid.DisplayName} for tracking.", 2);
            Grid grid = new Grid(cubeGrid, config, lg);
            grid.ScanGrid();
            grid.Update();

            activeGrids.Add(cubeGrid.EntityId, grid);

            cubeGrid.OnClose += ActiveGridClose;
            cubeGrid.OnBlockAdded += grid.AddBlock;
            cubeGrid.OnBlockRemoved += grid.RemoveBlock;
        }

        private void UnregisterActiveGrid(IMyCubeGrid cubeGrid)
        {
            lg.File($"Removing grid {cubeGrid.DisplayName} from tracking.", 2);
            if (activeGrids.ContainsKey(cubeGrid.EntityId))
            {
                Grid grid = activeGrids[cubeGrid.EntityId];
                cubeGrid.OnClose -= ActiveGridClose;
                cubeGrid.OnBlockAdded -= grid.AddBlock;
                cubeGrid.OnBlockRemoved -= grid.RemoveBlock;
            }
            else
            {
                lg.File($"Warning: Attempted to remove grid that is not tracked: {cubeGrid.DisplayName}", 1);
            }
        }

        private void RegisterPassiveGrid(IMyCubeGrid cubeGrid)
        {
            lg.File($"Registering passive grid {cubeGrid.DisplayName} for tracking.", 3);
            passiveGrids.Add(cubeGrid.EntityId, cubeGrid);
            cubeGrid.OnBlockAdded += CheckActivate;
        }

        private void UnregisterPassiveGrid(IMyCubeGrid cubeGrid)
        {
            lg.File($"Removing passive grid {cubeGrid.DisplayName} from tracking.", 3);
            if (passiveGrids.ContainsKey(cubeGrid.EntityId))
            {
                cubeGrid.OnBlockAdded -= CheckActivate;
            }
            else
            {
                lg.File($"Warning: Attempted to remove passive grid that is not tracked: {cubeGrid.DisplayName}", 1);
            }
        }

        private void CheckActivate(IMySlimBlock block)
        {
            IMyCubeGrid cubeGrid = block.CubeGrid;
            if (IsPlayerOwnedGrid(cubeGrid))
            {
                lg.File($"Passive grid {cubeGrid.DisplayName} is now player owned, activating tracking.", 2);
                UnregisterPassiveGrid(cubeGrid);
                RegisterActiveGrid(cubeGrid);
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

        private bool IsGrid(IMyEntity ent)
        {
            IMyCubeGrid grid = ent as IMyCubeGrid;
            if (grid != null)
            {
                return true;
            }
            return false;
        }

        private bool IsPlayerOwnedGrid(IMyCubeGrid grid)
        {
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
