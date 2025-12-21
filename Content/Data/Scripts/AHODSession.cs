using Sandbox.Game.Entities;
using VRage.Game.Components;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using VRage.ModAPI;

namespace AHOD
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class AHODSession : MySessionComponentBase
    {
        private Logger lg;
        AHODConfig config;

        public override void LoadData()
        {
            InitConfig();
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            lg.File("AHODSession loaded.", 2);
        }
        public void OnEntityAdd(IMyEntity entity)
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            /// This is a hack to initialize group logic on the first
            /// entity added, because the grid group system is not
            /// available at LoadData time.
            BindGroupLogic();

            MyCubeGrid grid = entity as MyCubeGrid;
            if (grid != null)
            {
                /// The group logic binding did not trigger for this cubegrid,
                /// so we add it manually.
                lg.File($"GroupLogic binding triggered by cubegrid: {entity.DisplayName}, adding eventhandler manually.", 2);
                /// The ctor of MyGridGroupsDefaultEventHandler will add references to this object
                /// in the MyGridGroupData object, so we do not save a reference here.
                new Grid(grid.GetGridGroup(linkTypeEnum: GridLinkTypeEnum.Mechanical), config, lg, true);
            }
        }

        protected override void UnloadData()
        {
            lg.File("AHODSession unloaded.", 2);
        }

        Grid CreateEventHandler(IMyGridGroupData groupData)
        {
            return new Grid(groupData, config, lg);
        }
        private void BindGroupLogic()
        {
            MyGridGroupsHelper helper = new MyGridGroupsHelper();
            helper.AddGridGroupLogic(GridLinkTypeEnum.Mechanical, CreateEventHandler);
        }
        private void InitConfig()
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
    }
}
