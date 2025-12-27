using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using Sandbox.Game.Entities.Character;

namespace AHOD
{
    /// <summary>
    /// Represents a grid group and its associated data for bed
    /// management and efficiency calculation.
    /// </summary>
    /// <details>
    /// When this file uses
    /// the word "grid" by itself, it is referring to this custom
    /// Grid class, which can contain many in-game CubeGrids as
    /// part of a in-game grid group.
    /// </details>
    public class Grid : MyGridGroupsDefaultEventHandler
    {
        Dictionary<string, int> BlockCounts = new Dictionary<string, int>();
        Dictionary<string, int> RequiredCounts = new Dictionary<string, int>();
        /// <summary>
        /// The current efficiency of the grid, from 0.0 to 1.0.
        /// </summary>
        public float Efficiency { get; private set; } = 1f;
        /// <summary>
        /// The unique identifier for this grid.
        /// </summary>
        public long GridId => guid.GetHashCode();

        public bool IsActive
        {
            get
            {
                return _isActive;
            }
            private set
            {
                if (!_isActive && value)
                {
                    lg.File("Grid is now active.", 2);
                    _isActive = true;
                }
                else if (_isActive && !value)
                {
                    lg.File("Grid is now inactive.", 2);
                    _isActive = false;
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
            lg.File($"Scanning all {cubeGrids.Count} CubeGrids in group to initialize bed counts and efficiency.", 2);
            foreach (IMyCubeGrid cubeGrid in cubeGrids)
            {
                lg.File($"Initial CubeGrid added: {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) to Grid instance.", 3);
                SubscribeCubeGrid(cubeGrid);
                RegisterGrid(cubeGrid);
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
            RegisterGrid(cubeGrid);
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
            UnregisterGrid(cubeGrid);
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
            BlockCounts.Clear();
            RequiredCounts.Clear();
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
        /// Subscribes to CubeGrid events.
        /// </summary>
        /// <param name="cubeGrid">CubeGrid to subscribe to</param>
        private void SubscribeCubeGrid(IMyCubeGrid cubeGrid)
        {
            lg.File($"Subscribing to CubeGrid {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) events.", 3);
            cubeGrid.OnBlockAdded += AddBlock;
            cubeGrid.OnBlockRemoved += RemoveBlock;
        }
        /// <summary>
        /// Unsubscribes from CubeGrid events.
        /// </summary>
        /// <param name="cubeGrid">CubeGrid to unsubscribe from</param>
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
            if (config.IsTrackedBlock(block))
            {
                RegisterBlock(block.FatBlock.BlockDefinition.SubtypeId);
                Update();
            }
        }
        /// <summary>
        /// Removes a block from the grid's bed calculations and updates efficiency.
        /// </summary>
        /// <param name="block">Block to remove</param>
        public void RemoveBlock(IMySlimBlock block)
        {
            lg.File($"Removing block {block?.FatBlock?.BlockDefinition.SubtypeId}", 3);
            if (config.IsTrackedBlock(block))
            {
                UnregisterBlock(block.FatBlock.BlockDefinition.SubtypeId);
                Update();
            }
        }
        /// <summary>
        /// Adds the specified amount to the block count for the given group.
        /// Does not automatically update efficiency; call Update()
        /// after making changes.
        /// </summary>
        /// <param name="groupName">Group name</param>
        /// <param name="amount">Amount to add (can be negative)</param>
        public void ChangeGroupCount(string groupName, int amount)
        {
            if (!BlockCounts.ContainsKey(groupName))
            {
                BlockCounts[groupName] = 0;
            }
            lg.File($"Changing BlockCount for group {groupName} by {amount}, new value {BlockCounts[groupName] + amount}.", 3);
            BlockCounts[groupName] += amount;
            if (BlockCounts[groupName] < 0)
            {
                lg.File($"Warning: BlockCount for group {groupName} went below zero. Resetting to zero.", 1);
                lg.OnScreen($"Warning: BlockCount for group {groupName} went below zero. Resetting to zero.", durationMs: 2000, level: 2, color: "Red");
                BlockCounts[groupName] = 0;
            }
            if (BlockCounts[groupName] == 0)
            {
                lg.File($"BlockCount for group {groupName} is now zero. Removing tracking element.", 4);
                BlockCounts.Remove(groupName);
            }
        }
        /// <summary>
        /// Adds the specified amount to the required count for the given group.
        /// Does not automatically update efficiency; call Update()
        /// after making changes.
        /// </summary>
        /// <param name="groupName">Group name</param>
        /// <param name="amount">Amount to add (can be negative)</param>
        public void ChangeRequirement(string groupName, int amount)
        {
            if (!RequiredCounts.ContainsKey(groupName))
            {
                RequiredCounts[groupName] = 0;
            }
            lg.File($"Changing RequiredCount for group {groupName} by {amount}, new value {RequiredCounts[groupName] + amount}.", 3);
            RequiredCounts[groupName] += amount;
            if (RequiredCounts[groupName] < 0)
            {
                lg.File($"Warning: RequiredCount for group {groupName} went below zero. Resetting to zero.", 1);
                lg.OnScreen($"Warning: RequiredCount for group {groupName} went below zero. Resetting to zero.", durationMs: 2000, level: 2, color: "Red");
                RequiredCounts[groupName] = 0;
            }
            if (RequiredCounts[groupName] == 0)
            {
                lg.File($"RequiredCount for group {groupName} is now zero. Removing tracking element.", 4);
                RequiredCounts.Remove(groupName);
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
            float minEff = 1f;
            foreach (var kvp in RequiredCounts)
            {
                string groupName = kvp.Key;
                int required = kvp.Value;
                int available = 0;
                if (BlockCounts.ContainsKey(groupName))
                {
                    available = BlockCounts[groupName];
                }
                if (required > 0)
                {
                    float groupEff = (float)available / (float)required;
                    lg.File($"Group {groupName}: {available}/{required}, efficiency {groupEff:P0}.", 4);
                    if (groupEff < minEff)
                    {
                        minEff = groupEff;
                    }
                }
            }
            Efficiency = RoundEfficiency(minEff);
        }
        /// <summary>
        /// Registers all relevant blocks from the given CubeGrid.
        /// </summary>
        /// <param name="cubeGrid">CubeGrid to register</param>
        private void RegisterGrid(IMyCubeGrid cubeGrid)
        {
            var blocks = new List<IMySlimBlock>();
            cubeGrid.GetBlocks(blocks, config.IsTrackedBlock);
            foreach (var block in blocks)
            {
                RegisterBlock(block.FatBlock.BlockDefinition.SubtypeId);
            }
        }
        /// <summary>
        /// Unregisters all tracked blocks from the given CubeGrid.
        /// </summary>
        /// <param name="cubeGrid">CubeGrid to unregister</param>
        private void UnregisterGrid(IMyCubeGrid cubeGrid)
        {
            var blocks = new List<IMySlimBlock>();
            cubeGrid.GetBlocks(blocks, config.IsTrackedBlock);
            foreach (var block in blocks)
            {
                UnregisterBlock(block.FatBlock.BlockDefinition.SubtypeId);
            }
        }
        /// <summary>
        /// Registers a block into the grid's tracking system, if the subtype is tracked.
        /// </summary>
        /// <param name="subTypeId">Block Subtype ID</param>
        private void RegisterBlock(string subTypeId)
        {
            if (config.GroupOfBlockSubtype.ContainsKey(subTypeId))
            {
                string groupName = config.GroupOfBlockSubtype[subTypeId];
                ChangeGroupCount(groupName, 1);
                if (config.EfficiencyRequirements.ContainsKey(groupName))
                {
                    foreach (var req in config.EfficiencyRequirements[groupName])
                    {
                        ChangeRequirement(req.Key, req.Value);
                    }
                }
            }
        }
        /// <summary>
        /// Unregisters a block from the grid's tracking system, if the subtype is tracked.
        /// </summary>
        /// <param name="subTypeId">Block Subtype ID</param>
        public void UnregisterBlock(string subTypeId)
        {
            if (config.GroupOfBlockSubtype.ContainsKey(subTypeId))
            {
                string groupName = config.GroupOfBlockSubtype[subTypeId];
                ChangeGroupCount(groupName, -1);
                if (config.EfficiencyRequirements.ContainsKey(groupName))
                {
                    foreach (var req in config.EfficiencyRequirements[groupName])
                    {
                        ChangeRequirement(req.Key, -req.Value);
                    }
                }
            }
        }
        /// <summary>
        /// Determines if the given block is player built.
        /// </summary>
        /// <param name="newBlock">Block to check</param>
        /// <returns>True, if the block is player built.</returns>
        /// TODO: Consider faction ownership for multiplayer scenarios.
        private bool IsPlayerBuilt(IMySlimBlock newBlock)
        {
            return IsPlayerEntityId(newBlock.BuiltBy);
        }
        /// <summary>
        /// Determines if the given cubegrid is player owned.
        /// </summary>
        /// <param name="cubeGrid">Cubegrid to check</param>
        /// <returns>True, if the CubeGrid is player owned.</returns>
        private bool IsPlayerOwned(IMyCubeGrid cubeGrid)
        {
            if (cubeGrid.BigOwners == null || cubeGrid.BigOwners.Count == 0)
            {
                return false;
            }
            return IsPlayerEntityId(cubeGrid.BigOwners[0]);
        }
        /// <summary>
        /// Determines if the given entity ID belongs to a player character.
        /// </summary>
        /// <param name="entityId">Entity ID to check</param>
        /// <returns>True, if the entity ID belongs to a player character.</returns>
        private bool IsPlayerEntityId(long entityId)
        {
            MyEntity entity = null;
            if (MyEntities.TryGetEntityById(entityId, out entity, allowClosed: true))
            {
                IMyCharacter character = entity as IMyCharacter;
                return character != null && character.IsPlayer;
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