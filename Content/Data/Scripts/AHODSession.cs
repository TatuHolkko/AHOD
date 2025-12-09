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

        public override void UpdateBeforeSimulation()
        {
            if (!init)
            {
                lg = new Logger();
                lg.Message("Init start.");
                InitGrids();
                lg.Message("Init done.");
                init = true;
            }
        }

        private void InitGrids()
        {
            HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities, e => true);

            lg.Message($"Found {entities.Count} entities.");

            foreach (IMyEntity ent in entities)
            {
                var grid = ent as IMyCubeGrid;
                if (grid == null)
                {
                    continue;
                }
                string name = grid.DisplayName;

                lg.Message($"Found grid: {name}");
            }
        }

        private HashSet<IMyCubeGrid> grids;
        private bool init = false;

        private Logger lg;
    }
}
