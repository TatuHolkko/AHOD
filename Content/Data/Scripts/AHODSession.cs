using Sandbox.Game.Entities;
using VRage.Game.Components;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using System.Linq;

namespace AHOD
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class AHODSession : MySessionComponentBase
    {
        private Logger lg;
        AHODConfig config;
        /// <summary>
        /// List of grids that have not been released yet. Grid objects may not
        /// get released automatically on session unload, so we need to track them
        /// and release them manually.
        /// </summary>
        HashSet<Grid> grids = new HashSet<Grid>();
        /// <summary>
        /// Flag to indicate if grids are being released manually during session unload.
        /// Setting this to true skips automatic removal of grids from the session grid list,
        /// so that we can use a use a for-each loop without modifying the collection.
        /// </summary>
        bool manualRelease = false;
        public override void LoadData()
        {
            //TODO: What debug level to use before config is loaded? Logging might be needed to debug config loading.
            lg = new Logger() { DebugLevel = 4 };
            lg.Context = "MAIN";
            lg.File("Loading AHODSession.", 2);
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
            lg.File("Unloading AHODSession.", 2);
            if (grids.Count > 0)
            {
                lg.File($"Releasing {grids.Count} grids manually.", 2);
                manualRelease = true;
                foreach (Grid grid in grids)
                {
                    grid.ManualRelease();
                }
                manualRelease = false;
                grids.Clear();
            }
            lg.File("AHODSession unloaded.", 2);
        }
        /// <summary>
        /// Callback for when a grid is released. Removes the grid from the session's grid list.
        /// </summary>
        /// <param name="grid">The Grid object to be released.</param>
        void OnGridReleased(Grid grid)
        {
            string hex = string.Format("{0:x10}", grid.GridId);
            lg.File($"Grid [{hex.Substring(hex.Length - 5, 5)}] released.", 3);
            if (manualRelease)
            {
                lg.File($"Grid [{hex.Substring(hex.Length - 5, 5)}] release invoked by session unload, skipping removal from session grid list.", 2);
                return;
            }
            if (!grids.Remove(grid))
            {
                lg.File($"WARNING: Grid [{hex.Substring(hex.Length - 5, 5)}] not found in session grid list on release.", 1);
            }
            else
            {
                grid.OnReleasedCallback -= OnGridReleased;
            }
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
            Grid grid = new Grid(groupData, config, lg);
            grids.Add(grid);
            grid.OnReleasedCallback += OnGridReleased;
            return grid;
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
            config = new AHODConfig(lg);
            config.Load();
        }
    }
}
