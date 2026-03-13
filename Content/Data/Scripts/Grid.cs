using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRage.ModAPI;

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
        HashSet<MyCubeBlock> EfficiencyTargets = new HashSet<MyCubeBlock>();
        /// <summary>
        /// Dictionary for keeping references of created event handlers
        /// </summary>
        Dictionary<IMyCubeBlock, Action> FunctionalityEventHandlers = new Dictionary<IMyCubeBlock, Action>();
        /// <summary>
        /// The current efficiency of the grid, from 0.0 to 1.0.
        /// </summary>
        public float Efficiency { get; private set; } = 1f;
        /// <summary>
        /// The unique identifier for this grid.
        /// </summary>
        public long GridId => guid.GetHashCode();
        /// <summary>
        /// Callback invoked when the grid is released.
        /// </summary>
        public Action<Grid> OnReleasedCallback = null;
        /// <summary>
        /// Indicates whether this grid object has been released.
        /// </summary>
        public  bool IsReleased { get; private set; } = false;
        /// <summary>
        /// Indicates whether this grid is active (has player-built blocks).
        /// Non-active grids do not apply efficiency changes.
        /// </summary>
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
        private float currentAppliedEfficiency = 1f;
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
            string hex = String.Format("{0:x10}", GridId);
            lg.Context = hex.Substring(hex.Length - 5, 5);
            lg.File($"Creating new Grid instance for GridGroup.", 2);

            GridGroup.GetGrids(cubeGrids);
            lg.File($"Scanning all {cubeGrids.Count} CubeGrids in group to initialize bed counts and efficiency.", 2);
            foreach (IMyCubeGrid cubeGrid in cubeGrids)
            {
                lg.File($"Initial CubeGrid added: {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) to Grid instance.", 3);
                bool temp = lg.Enabled;
                lg.Enabled = false;
                SubscribeCubeGrid(cubeGrid);
                RegisterGrid(cubeGrid);
                if (!IsActive)
                {
                    if (IsPlayerOwned(cubeGrid))
                    {
                        IsActive = true;
                    }
                }
                lg.Enabled = temp;
            }
            Update();
        }

        protected override void OnGridAdded(IMyCubeGrid cubeGrid, IMyGridGroupData prevGroup)
        {
            lg.File($"New CubeGrid added: {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) to Grid instance.", 2);
            SubscribeCubeGrid(cubeGrid);
            RegisterGrid(cubeGrid);
            cubeGrids.Add(cubeGrid);
            if (!IsActive)
            {
                if (IsPlayerOwned(cubeGrid))
                {
                    IsActive = true;
                }
            }
            Update();
        }

        protected override void OnGridRemoved(IMyCubeGrid cubeGrid, IMyGridGroupData nextGroup)
        {
            if(cubeGrids.Remove(cubeGrid))
            {
                lg.File($"Removing CubeGrid {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) from Grid instance.", 2);
                UnsubscribeCubeGrid(cubeGrid);
                UnregisterGrid(cubeGrid);
                Update();
            }
            else if (cubeGrid.MarkedForClose)
            {
                lg.File($"Skipping removal of already removed CubeGrid {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}).", 3);
            }
            else
            {
                lg.File($"Warning: Tried to remove CubeGrid {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) from Grid instance, but it was not found.", 2);
            }

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
        /// Called when the Grid instance is being released by the modding API.
        /// </summary>
        protected override void OnReleased()
        {
            lg.File("Automatic release of Grid instance invoked by GridGroupData release.", 2);
            Release();
        }
        /// <summary>
        /// Manually releases the Grid instance, can be called when the modding API does not
        /// automatically release it.
        /// </summary>
        public void ManualRelease()
        {
            if (IsClosed)
            {
                lg.File("Warning: Tried to manually release Grid instance, but it is already closed.", 1);
                return;
            }
            lg.File("Manual release of Grid instance invoked.", 2);
            Release();
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
        /// Callback for when a CubeGrid is marked for close.
        /// </summary>
        /// <param name="entity">Entity being closed</param>
        private void CubeGridMarkedForClose(IMyEntity entity)
        {
            IMyCubeGrid cubeGrid = entity as IMyCubeGrid;
            if(cubeGrids.Remove(cubeGrid))
            {
                lg.File($"CubeGrid {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) marked for close, removing from Grid instance.", 2);
                UnsubscribeCubeGrid(cubeGrid);
                UnregisterGrid(cubeGrid);
                Update();
            }
            else
            {
                lg.File($"Warning: CubeGrid {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) marked for close, but was not found in Grid instance.", 2);
            }
        }
        private void Release()
        {
            if (IsReleased)
            {
                lg.File("This instance is already released, ignoring release request.", 3);
                return;
            }
            lg.File("Releasing Grid instance, unsubscribing from all CubeGrids.", 2);
            foreach (IMyCubeGrid cubeGrid in cubeGrids)
            {
                UnsubscribeCubeGrid(cubeGrid);
                UnregisterGrid(cubeGrid);
            }
            cubeGrids.Clear();
            if (BlockCounts.Count > 0 || RequiredCounts.Count > 0 || EfficiencyTargets.Count > 0)
            {
                lg.File($"Warning: Grid being released still has tracked data: BlockCounts:{BlockCounts.Count}, RequiredCounts:{RequiredCounts.Count}, EfficiencyTargets:{EfficiencyTargets.Count}. Clearing data.", 1);
            }
            if (FunctionalityEventHandlers.Count > 0)
            {
                lg.File($"Warning: Grid being released still has {FunctionalityEventHandlers.Count} subscribed event handlers!", 1);
            }
            BlockCounts.Clear();
            RequiredCounts.Clear();
            EfficiencyTargets.Clear();
            FunctionalityEventHandlers.Clear();
            Efficiency = 1f;
            currentAppliedEfficiency = 1f;
            IsActive = false;
            IsReleased = true;
            OnReleasedCallback?.Invoke(this);
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
            cubeGrid.OnMarkForClose += CubeGridMarkedForClose;
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
            cubeGrid.OnMarkForClose -= CubeGridMarkedForClose;
        }
        /// <summary>
        /// Subscribe to block functionality change events
        /// </summary>
        /// <param name="block">Block to subscribe to</param>
        private void SubscribeBlock(IMyCubeBlock block)
        {
            lg.File($"Subscribing to CubeBlock {block.DisplayName} (ID: {block.EntityId}) events.", 3);
            Action handler = () => { UpdateFunctionality(block); Update(); };
            FunctionalityEventHandlers.Add(block, handler);
            block.SlimBlock.ComponentStack.IsFunctionalChanged += handler;
        }
        /// <summary>
        /// Unsubscribe from block functionality change events
        /// </summary>
        /// <param name="block">Block to unsubscribe from</param>
        private void UnsubscribeBlock(IMyCubeBlock block)
        {
            lg.File($"Unsubscribing from CubeBlock {block.DisplayName} (ID: {block.EntityId}) events.", 3);
            Action handler;
            if (FunctionalityEventHandlers.TryGetValue(block, out handler))
            {
                block.SlimBlock.ComponentStack.IsFunctionalChanged -= handler;
                FunctionalityEventHandlers.Remove(block);
            }
            else
            {
                lg.File($"Warning: could not unsubscribe event handlers from {block.BlockDefinition.SubtypeId} '{block.DisplayName}' (ID: {block.EntityId})", 1);
            }
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
                RegisterBlock(block.FatBlock);
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
                UnregisterBlock(block.FatBlock);
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
            foreach (var block in EfficiencyTargets)
            {
                RemoveProductivityEfficiency(block, currentAppliedEfficiency);
                ApplyProductivityEfficiency(block, Efficiency);
            }
            currentAppliedEfficiency = Efficiency;
        }
        private void ApplyProductivityEfficiency(MyCubeBlock block, float efficiency)
        {
            lg.File($"Applying productivity efficiency {efficiency:P0} to block {block.BlockDefinition.DisplayNameText}.", 4);
            float additiveEfficiency = efficiency - 1f;
            block.UpgradeValues["Productivity"] += additiveEfficiency;
            block.CommitUpgradeValues();
            IMyTerminalBlock terminalBlock = block as IMyTerminalBlock;
            terminalBlock?.SetDetailedInfoDirty();
        }
        private void RemoveProductivityEfficiency(MyCubeBlock block, float efficiency)
        {
            lg.File($"Removing previously applied efficiency {efficiency:P0} from block {block.BlockDefinition.DisplayNameText}.", 4);
            float additiveEfficiency = efficiency - 1f;
            block.UpgradeValues["Productivity"] -= additiveEfficiency;
            block.CommitUpgradeValues();
            IMyTerminalBlock terminalBlock = block as IMyTerminalBlock;
            terminalBlock?.SetDetailedInfoDirty();
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
            if (minEff < 0.1f)
            {
                lg.File($"Minimum efficiency {minEff:P0} is below 10%, setting to 10%.", 3);
                minEff = 0.1f;
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
            lg.File($"Registering {blocks.Count} tracked blocks from CubeGrid {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}).", 3);
            foreach (var block in blocks)
            {
                RegisterBlock(block.FatBlock);
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
            lg.File($"Unregistering {blocks.Count} tracked blocks from CubeGrid {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}).", 3);
            foreach (var block in blocks)
            {
                UnregisterBlock(block.FatBlock);
            }
        }
        /// <summary>
        /// Registers a block into the grid's tracking system, if the subtype is tracked.
        /// </summary>
        /// <param name="block">Block to register</param>
        private void RegisterBlock(IMyCubeBlock block)
        {
            string subTypeId = block.BlockDefinition.SubtypeId;
            lg.File($"Registering block {subTypeId}.", 4);
            if (config.GroupOfBlockSubtype.ContainsKey(subTypeId))
            {
                SubscribeBlock(block);
                if (block.IsFunctional)
                {
                    SetFunctional(block);
                }
                else
                {
                    lg.File($"Added block {subTypeId} (ID: {block.EntityId}) was not functional, count stays the same.", 4);
                }
            }
        }
        /// <summary>
        /// Unregisters a block from the grid's tracking system, if the subtype is tracked.
        /// </summary>
        /// <param name="block">Block to unregister</param>
        public void UnregisterBlock(IMyCubeBlock block)
        {
            string subTypeId = block.BlockDefinition.SubtypeId;
            lg.File($"Unregistering block {subTypeId}.", 4);
            if (config.GroupOfBlockSubtype.ContainsKey(subTypeId))
            {
                UnsubscribeBlock(block);
                if (block.IsFunctional)
                {
                    SetNotFunctional(block);
                }
                else
                {
                    lg.File($"Removed block {subTypeId} (ID: {block.EntityId}) was not functional, count stays the same.", 4);
                }
            }
        }
        /// <summary>
        /// Adds or removes the given block from the block counts, depending on
        /// if the block is currently functional or not, respectively.
        /// </summary>
        /// <param name="block">The block that just changed its's functional state</param>
        private void UpdateFunctionality(IMyCubeBlock block)
        {
            if (block.IsFunctional)
            {
                SetFunctional(block);
            }
            else
            {
                SetNotFunctional(block);
            }
        }
        /// <summary>
        /// Add the given block and it's possible requirements to the block counts
        /// </summary>
        /// <param name="block">Block to add</param>
        private void SetFunctional(IMyCubeBlock block)
        {
            string subTypeId = block.BlockDefinition.SubtypeId;
            string groupName = config.GroupOfBlockSubtype[subTypeId];
            lg.File($"Detected {subTypeId} (ID: {block.EntityId}) becoming functional.", 4);
            ChangeGroupCount(groupName, 1);
            if (config.EfficiencyRequirements.ContainsKey(groupName))
            {
                if (EfficiencyTargets.Add(block as MyCubeBlock))
                {
                    ApplyProductivityEfficiency(block as MyCubeBlock, currentAppliedEfficiency);
                }
                else
                {
                    lg.File($"Warning: Tried to register block {subTypeId} to efficiency targets, but it was already present.", 2);
                }
                foreach (var req in config.EfficiencyRequirements[groupName])
                {
                    ChangeRequirement(req.Key, req.Value);
                }
            }
        }
        /// <summary>
        /// Remove the given block and it's possible requirements from the block counts
        /// </summary>
        /// <param name="block">Block to remove</param>
        private void SetNotFunctional(IMyCubeBlock block)
        {
            string subTypeId = block.BlockDefinition.SubtypeId;
            string groupName = config.GroupOfBlockSubtype[subTypeId];
            lg.File($"Detected {subTypeId} (ID: {block.EntityId}) becoming unfunctional.", 4);
            ChangeGroupCount(groupName, -1);
            if (config.EfficiencyRequirements.ContainsKey(groupName))
            {
                if (EfficiencyTargets.Remove(block as MyCubeBlock))
                {
                    RemoveProductivityEfficiency(block as MyCubeBlock, currentAppliedEfficiency);
                }
                else
                {
                    lg.File($"Warning: Tried to unregister block {subTypeId} from efficiency targets, but it was not found.", 2);
                }
                foreach (var req in config.EfficiencyRequirements[groupName])
                {
                    ChangeRequirement(req.Key, -req.Value);
                }
            }
        }
        /// <summary>
        /// Determines if the given block is player built.
        /// </summary>
        /// <param name="newBlock">Block to check</param>
        /// <returns>True, if the block is player built.</returns>
        private bool IsPlayerBuilt(IMySlimBlock newBlock)
        {
            return IsPlayerIdentityId(newBlock.BuiltBy);
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
            return IsPlayerIdentityId(cubeGrid.BigOwners[0]);
        }
        /// <summary>
        /// Determines if the given identity ID belongs to a player character.
        /// </summary>
        /// <param name="identityId">Identity ID to check</param>
        /// <returns>True, if the identity ID belongs to a player character.</returns>
        private bool IsPlayerIdentityId(long identityId)
        {
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(identityId);
            if (faction == null)
            {
                return true;
            }
            if (!faction.AcceptHumans || faction.IsEveryoneNpc())
            {
                return false;
            }
            if (!string.IsNullOrEmpty(faction.PrivateInfo))
            {
                return true;
            }
            lg.File($"WARNING: Could not determine if identity ID {identityId} is player controlled. Assuming it is.", 2);
            return true;
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