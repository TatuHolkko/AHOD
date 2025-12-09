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
            grids = new HashSet<IMyCubeGrid>();
            HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities, e => IsPlayerOwnedGrid(e));

            lg.Message($"Found {entities.Count} entities.");

            foreach (IMyEntity ent in entities)
            {
                IMyCubeGrid grid = ent as IMyCubeGrid;
                if (grid == null)
                {
                    continue;
                }
                string name = grid.DisplayName;
                lg.Message($"Found grid: {name}");
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
            return false;
        }

        private HashSet<IMyCubeGrid> grids;
        private bool init = false;

        private Logger lg;
    }
}
