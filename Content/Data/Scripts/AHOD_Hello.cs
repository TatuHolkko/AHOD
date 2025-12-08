using Sandbox.ModAPI;
using VRage.Utils;
using VRage.Game.Components;
using System.Collections.Generic;
using VRage.ModAPI;
using VRage.Game.ModAPI;

namespace AHOD
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class AHOD_Hello : MySessionComponentBase
    {

        public override void UpdateBeforeSimulation()
        {
            if (!_init)
            {
                MyLog.Default.WriteLine("AHOD: Initializing...");
                InitGrids();
                MyLog.Default.WriteLine("AHOD: Initialization done.");
                _init = true;
            }
        }

        private void InitGrids()
        {
            HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities, e => true);
            MyAPIGateway.Utilities.ShowNotification($"AHOD: found {entities.Count} entities.", 20000, "White");
            MyLog.Default.WriteLine($"AHOD: found {entities.Count} entities.");
            foreach (IMyEntity ent in entities)
            {
                var grid = ent as IMyCubeGrid;
                if (grid == null)
                {
                    continue;
                }
                string name = grid.DisplayName;
                MyAPIGateway.Utilities.ShowNotification($"AHOD: found grid: {name}", 2000, "White");
                MyLog.Default.WriteLine($"AHOD: Found grid: {name}");
            }
        }

        private HashSet<IMyCubeGrid> _grids;
        private bool _init = false;
    }
}
