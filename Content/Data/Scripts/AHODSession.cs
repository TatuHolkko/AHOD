using Sandbox.Game.Entities;
using VRage.Game.Components;
using System.Collections.Generic;
using VRage.Game.ModAPI;

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
            lg.File("AHODSession loaded.", 2);
        }

        public override void BeforeStart()
        {
            BindGroupLogic();
            InitializeExistingGroups();
        }

        protected override void UnloadData()
        {
            lg.File("AHODSession unloaded.", 2);
        }
        /// <summary>
        /// Create a Grid event handler for the given grid group.
        /// </summary>
        /// <details>
        /// The return value does not need to be saved by the caller,
        /// because the constructor of MyGridGroupsDefaultEventHandler
        /// (which Grid extends) saves references to the object by
        /// subscribing to events in the <ref>groupData</ref> parameter.
        /// A bit weird pattern, but that's how the API works.
        /// </details>
        /// <param name="groupData">Modding api grid group object</param>
        /// <returns></returns>
        Grid CreateEventHandler(IMyGridGroupData groupData)
        {
            return new Grid(groupData, config, lg);
        }
        /// <summary>
        /// Bind the grid group logic to create event handlers for new mechanical groups.
        /// </summary>
        private void BindGroupLogic()
        {
            MyGridGroupsHelper helper = new MyGridGroupsHelper();
            helper.AddGridGroupLogic(GridLinkTypeEnum.Mechanical, CreateEventHandler);
        }
        /// <summary>
        /// Initialize event handlers manually for existing mechanical grid groups.
        /// </summary>
        private void InitializeExistingGroups()
        {
            MyGridGroupsHelper helper = new MyGridGroupsHelper();
            List<IMyGridGroupData> groups = new List<IMyGridGroupData>();
            helper.GetGridGroups(GridLinkTypeEnum.Mechanical, groups);
            foreach (var group in groups)
            {
                CreateEventHandler(group);
            }
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
