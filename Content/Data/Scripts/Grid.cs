using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace AHOD
{
    /// <summary>
    /// Represents a grid group and its associated data for bed
    /// management and efficiency calculation.
    /// </summary>
    /// <details>
    /// When this file uses
    /// the word "grid" by itself, it is referring to this custom
    /// Grid class, which can contain many in-game cube grids as
    /// part of a in-game grid group.
    /// </details>
    public class Grid : MyGridGroupsDefaultEventHandler
    {
        /// <summary>
        /// The current number of beds in the grid.
        /// </summary>
        public int BedCount { get; private set; } = 0;
        /// <summary>
        /// The current number of beds required by the grid.
        public int RequiredBedCount { get; private set; } = 0;
        /// <summary>
        /// The current efficiency of the grid, from 0.0 to 1.0.
        /// </summary>
        public float Efficiency { get; private set; } = 1f;
        /// <summary>
        /// The unique identifier for this grid.
        /// </summary>
        public long GridId => guid.GetHashCode();

        public bool IsActive {
                get
                {
                    return _isActive;
                }
                private set
                {
                    if (!_isActive && value)
                    {
                        lg.File("Grid is now active.", 2);
                    }
                    else if (_isActive && !value)
                    {
                        lg.File("Grid is now inactive.", 2);
                    }
                }
            }
        private bool _isActive = false;
        List<IMyCubeGrid> cubeGrids = new List<IMyCubeGrid>();
        AHODConfig config;
        Logger lg;
        Guid guid = Guid.Empty;
        /// <summary>
        /// Initializes a new instance of the Grid class.
        /// </summary>
        /// <param name="gridGroup">Modding api grid group object</param>
        /// <param name="config">Mod config class</param>
        /// <param name="logger">Logger to copy logging options from. The class creates its own logger object.</param>
        /// </summary>
        public Grid(IMyGridGroupData gridGroup, AHODConfig config, Logger logger)
        : base(gridGroup)
        {
            this.config = config;
            lg = new Logger()
            {
                AvoidDuplicates = logger.AvoidDuplicates,
                DebugLevel = logger.DebugLevel,
                Enabled = logger.Enabled,
                FileLogging = logger.FileLogging,
                OnScreenLogging = logger.OnScreenLogging
            };
            lg.Context = $"{GridId:x4}";
            lg.File($"Creating new Grid instance for GridGroup.", 2);

            GridGroup.GetGrids(cubeGrids);
            lg.File($"Scanning all {cubeGrids.Count} cube grids in group to initialize bed counts and efficiency.", 2);
            foreach (IMyCubeGrid cubeGrid in cubeGrids)
            {
                lg.File($"Initial CubeGrid added: {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) to Grid instance.", 3);
                SubscribeCubeGrid(cubeGrid);
                ChangeBedCount(CountBeds(cubeGrid));
                ChangeRequiredBedCount(CountRequiredBeds(cubeGrid));
                if (!IsActive)
                {
                    if (IsPlayerOwned(cubeGrid))
                    {
                        IsActive = true;
                    }
                }
            }
            Update();
        }

        protected override void OnGridAdded(IMyCubeGrid cubeGrid, IMyGridGroupData prevGroup)
        {
            lg.File($"New CubeGrid added: {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) to Grid instance.", 2);
            SubscribeCubeGrid(cubeGrid);
            ChangeBedCount(CountBeds(cubeGrid));
            ChangeRequiredBedCount(CountRequiredBeds(cubeGrid));
            Update();
            cubeGrids.Add(cubeGrid);
            if (!IsActive)
            {
                if (IsPlayerOwned(cubeGrid))
                {
                    IsActive = true;
                }
            }
        }

        protected override void OnGridRemoved(IMyCubeGrid cubeGrid, IMyGridGroupData nextGroup)
        {
            lg.File($"Removing CubeGrid {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) from Grid instance.", 2);
            UnsubscribeCubeGrid(cubeGrid);
            ChangeBedCount(-CountBeds(cubeGrid));
            ChangeRequiredBedCount(-CountRequiredBeds(cubeGrid));
            Update();
            cubeGrids.Remove(cubeGrid);
        }

        protected override void OnReleased()
        {
            lg.File("GridGroup released, unsubscribing from all CubeGrids.", 2);
            foreach (IMyCubeGrid cubeGrid in cubeGrids)
            {
                UnsubscribeCubeGrid(cubeGrid);
            }
            cubeGrids.Clear();
            BedCount = 0;
            RequiredBedCount = 0;
            Efficiency = 1f;
            IsActive = false;
        }
        /// <summary>
        /// Gets a unique identifier for this grid instance. If not already assigned, generates a new one.
        /// </summary>
        /// <returns>New Guid</returns>
        protected override Guid GetGuid()
        {
            if (guid == Guid.Empty)
            {
                guid = Guid.NewGuid();
            }
            return guid;
        }
        /// <summary>
        /// Updates the grid's efficiency based on current bed counts.
        /// </summary>
        public void Update()
        {
            float oldEfficiency = Efficiency;
            RecalculateEfficency();
            if (oldEfficiency != Efficiency)
            {
                lg.File($"Efficiency updated: {Efficiency:P0}", 3);
                lg.OnScreen($"Efficiency updated: {Efficiency:P0}", durationMs: 2000, level: 3, color: "White", force: true);
                if (!IsActive)
                {
                    lg.File("Grid is not active, skipping efficiency application.", 3);
                    return;
                }
                ApplyNewEfficiency();
            }
        }
        /// <summary>
        /// Subscribes to cube grid events.
        /// </summary>
        /// <param name="cubeGrid">Cube grid to subscribe to</param>
        private void SubscribeCubeGrid(IMyCubeGrid cubeGrid)
        {
            lg.File($"Subscribing to CubeGrid {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) events.", 3);
            cubeGrid.OnBlockAdded += AddBlock;
            cubeGrid.OnBlockRemoved += RemoveBlock;
        }
        /// <summary>
        /// Unsubscribes from cube grid events.
        /// </summary>
        /// <param name="cubeGrid">Cube grid to unsubscribe from</param>
        private void UnsubscribeCubeGrid(IMyCubeGrid cubeGrid)
        {
            lg.File($"Unsubscribing from CubeGrid {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) events.", 3);
            cubeGrid.OnBlockAdded -= AddBlock;
            cubeGrid.OnBlockRemoved -= RemoveBlock;
        }
        /// <summary>
        /// Adds a block to the grid's bed calculations and updates efficiency.
        /// </summary>
        /// <param name="block">Block to add</param>
        public void AddBlock(IMySlimBlock block)
        {
            if (!IsActive)
            {
                if (IsPlayerBuilt(block))
                {
                    IsActive = true;
                }
            }
            lg.File($"Adding block {block?.FatBlock?.BlockDefinition.SubtypeId}", 3);
            if (IsBed(block))
            {
                ChangeBedCount(1);
            }
            else if (RequiresBeds(block))
            {
                foreach (BedRequirement br in config.BedRequirements)
                {
                    IMyCubeBlock cb = block.FatBlock;
                    if (cb.BlockDefinition.SubtypeId == br.SubtypeId)
                    {
                        ChangeRequiredBedCount(br.Beds);
                        break;
                    }
                }
            }
            Update();
        }
        /// <summary>
        /// Removes a block from the grid's bed calculations and updates efficiency.
        /// </summary>
        /// <param name="block">Block to remove</param>
        public void RemoveBlock(IMySlimBlock block)
        {
            lg.File($"Removing block {block?.FatBlock?.BlockDefinition.SubtypeId}", 3);
            if (IsBed(block))
            {
                ChangeBedCount(-1);
            }
            else if (RequiresBeds(block))
            {
                foreach (BedRequirement br in config.BedRequirements)
                {
                    IMyCubeBlock cb = block.FatBlock;
                    if (cb.BlockDefinition.SubtypeId == br.SubtypeId)
                    {
                        ChangeRequiredBedCount(-br.Beds);
                        break;
                    }
                }
            }
            Update();
        }
        /// <summary>
        /// Changes the bed count by the specified amount. Does not
        /// automatically update efficiency; call Update() after
        /// making changes.
        /// </summary>
        /// <param name="amount">Amount to change, can be negative</param>
        public void ChangeBedCount(int amount)
        {
            lg.File($"Changing BedCount by {amount}, new value {BedCount + amount}.", 3);
            BedCount += amount;
            if (BedCount < 0)
            {
                lg.File($"Warning: BedCount went below zero. Resetting to zero.", 1);
                lg.OnScreen($"Warning: BedCount went below zero. Resetting to zero.", durationMs: 2000, level: 2, color: "Red");
                BedCount = 0;
            }
        }
        /// <summary>
        /// Changes the required bed count by the specified amount.
        /// Does not automatically update efficiency; call Update()
        /// after making changes.
        /// </summary>
        /// <param name="amount">Amount to change, can be negative</param>
        public void ChangeRequiredBedCount(int amount)
        {
            lg.File($"Changing RequiredBedCount by {amount}, new value {RequiredBedCount + amount}.", 3);
            RequiredBedCount += amount;
            if (RequiredBedCount < 0)
            {
                lg.File($"Warning: RequiredBedCount went below zero. Resetting to zero.", 1);
                lg.OnScreen($"Warning: RequiredBedCount went below zero. Resetting to zero.", durationMs: 2000, level: 2, color: "Red");
                RequiredBedCount = 0;
            }
        }
        /// <summary>
        /// Applies the new efficiency to the grid's systems.
        /// </summary>
        private void ApplyNewEfficiency()
        {
            //TODO: Apply efficiency to grid systems
        }
        /// <summary>
        /// Recalculates the grid's efficiency based on current bed
        /// counts.
        /// </summary>
        private void RecalculateEfficency()
        {
            if (RequiredBedCount == 0)
            {
                Efficiency = 1f;
            }
            else
            {
                Efficiency = (float)BedCount / RequiredBedCount;
                if (Efficiency > 1f)
                {
                    Efficiency = 1f;
                }
            }
            Efficiency = RoundEfficiency(Efficiency);
        }
        /// <summary>
        /// Counts the total number of beds required by all blocks in
        /// the given cube grid.
        /// Costly operation; should be used sparingly.
        /// </summary>
        /// <param name="cubeGrid">The cube grid to check</param>
        /// <returns>Number of required beds in the given grid</returns>
        private int CountRequiredBeds(IMyCubeGrid cubeGrid)
        {
            int sum = 0;
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            cubeGrid.GetBlocks(blocks, b => RequiresBeds(b));
            foreach (IMySlimBlock block in blocks)
            {
                IMyCubeBlock cb = block.FatBlock;
                foreach (BedRequirement br in config.BedRequirements)
                {
                    if (cb.BlockDefinition.SubtypeId == br.SubtypeId)
                    {
                        lg.File($"Block {cb.DisplayName} requires {br.Beds} beds.", 4);
                        sum += br.Beds;
                        break;
                    }
                }
            }
            return sum;
        }
        /// <summary>
        /// Counts the total number of beds in the given cube grid.
        /// Costly operation; should be used sparingly.
        /// </summary>
        /// <param name="cubeGrid">>The cube grid to check</param>
        /// <returns>Number of beds in the given grid</returns>
        private int CountBeds(IMyCubeGrid cubeGrid)
        {
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            cubeGrid.GetBlocks(blocks, b => IsBed(b));
            return blocks.Count;
        }
        /// <summary>
        /// Determines if the given block is player built.
        /// </summary>
        /// <param name="newBlock">Block to check</param>
        /// <returns>True, if the block is player built.</returns>
        /// TODO: Consider faction ownership for multiplayer scenarios.
        private bool IsPlayerBuilt(IMySlimBlock newBlock)
        {
            return newBlock.BuiltBy == MyAPIGateway.Session.Player.IdentityId;
        }
        /// <summary>
        /// Determines if the given cubegrid is player owned.
        /// </summary>
        /// <param name="cubeGrid">Cubegrid to check</param>
        /// <returns>True, if the cube grid is player owned.</returns>
        /// TODO: Consider faction ownership for multiplayer scenarios.
        private bool IsPlayerOwned(IMyCubeGrid cubeGrid)
        {
            if (cubeGrid.BigOwners.Contains(MyAPIGateway.Session.Player.IdentityId))
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// Determines if the given block requires beds.
        /// </summary>
        /// <param name="block">Block to check</param>
        /// <returns>True if the block requires beds, false otherwise</returns>
        private bool RequiresBeds(IMySlimBlock block)
        {
            IMyCubeBlock cb = block.FatBlock;
            if (cb == null)
            {
                return false;
            }
            foreach (BedRequirement br in config.BedRequirements)
            {
                if (cb.BlockDefinition.SubtypeId == br.SubtypeId)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Determines if the given block is a bed.
        /// </summary>
        /// <param name="block">Block to check</param>
        /// <returns>True if the block is a bed, false otherwise</returns>
        private bool IsBed(IMySlimBlock block)
        {
            IMyCubeBlock cb = block.FatBlock;
            if (cb == null)
            {
                return false;
            }
            foreach (string subtypeId in config.BedSubtypeIds)
            {
                if (cb.BlockDefinition.SubtypeId == subtypeId)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Rounds the efficiency to the nearest defined increment.
        /// </summary>
        /// <param name="efficiency">Efficiency to round</param>
        /// <returns>Rounded Efficiency</returns>
        /// TODO: Make the rounding increment configurable
        private float RoundEfficiency(float efficiency)
        {
            //nearest percent
            //(float)System.Math.Round(efficiency, 2);

            //nearest 5 percent
            return (float)System.Math.Round(efficiency * 20f) / 20;
        }
    }
}