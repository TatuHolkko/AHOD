using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using System.Text;

namespace AHOD
{
    /// <summary>
    /// Represents a grid group and its associated data for block count
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
        Dictionary<string, float> RequiredCounts = new Dictionary<string, float>();
        HashSet<MyCubeBlock> EfficiencyTargets = new HashSet<MyCubeBlock>();
        HashSet<IMyTerminalBlock> InfoTargets = new HashSet<IMyTerminalBlock>();
        Dictionary<IMyFunctionalBlock, ToggleTarget> ToggleTargets = new Dictionary<IMyFunctionalBlock, ToggleTarget>();
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
            lg.File($"Scanning all {cubeGrids.Count} CubeGrids in group to initialize block counts and efficiency.", 2);
            if (cubeGrids.Count > 0 && cubeGrids[0].Physics == null)
            {
                lg.File($"Initial CubeGrid detected as non-physical: {cubeGrids[0].DisplayName} (ID: {cubeGrids[0].EntityId}), aborting init.", 2);
                cubeGrids.Clear();
                return;
            }
            foreach (IMyCubeGrid cubeGrid in cubeGrids)
            {
                lg.File($"Initial CubeGrid added: {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) to Grid instance.", 3);
                bool temp = lg.Enabled;
                lg.Enabled = lg.DebugLevel > 3;
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
            MarkInfoDirty();
        }

        protected override void OnGridAdded(IMyCubeGrid cubeGrid, IMyGridGroupData prevGroup)
        {
            lg.File($"New CubeGrid added: {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) to Grid instance.", 2);
            if (cubeGrid.Physics == null)
            {
                lg.File($"CubeGrid detected as non-physical: {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}), releasing Grid instance.", 2);
                Release();
                return;
            }
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
            MarkInfoDirty();
        }

        protected override void OnGridRemoved(IMyCubeGrid cubeGrid, IMyGridGroupData nextGroup)
        {
            if(cubeGrids.Remove(cubeGrid))
            {
                lg.File($"Removing CubeGrid {cubeGrid.DisplayName} (ID: {cubeGrid.EntityId}) from Grid instance.", 2);
                UnsubscribeCubeGrid(cubeGrid);
                UnregisterGrid(cubeGrid);
                Update();
                MarkInfoDirty();
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
        /// Updates the grid's efficiency based on current block counts.
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
            if (BlockCounts.Count > 0 || RequiredCounts.Count > 0 || EfficiencyTargets.Count > 0)
            {
                lg.File($"Warning: Grid being released still has tracked data: BlockCounts:{BlockCounts.Count}, RequiredCounts:{RequiredCounts.Count}, EfficiencyTargets:{EfficiencyTargets.Count}. Clearing data.", 1);
            }
            if (FunctionalityEventHandlers.Count > 0)
            {
                lg.File($"Warning: Grid being released still has {FunctionalityEventHandlers.Count} subscribed event handlers!", 1);
            }
            if (InfoTargets.Count > 0)
            {
                lg.File($"Warning: Grid being released still has {InfoTargets.Count} subscribed info targets!", 1);
            }
            if (ToggleTargets.Count > 0)
            {
                lg.File($"Warning: Grid being released still has {ToggleTargets.Count} subscribed toggle targets!", 1);
            }
            cubeGrids.Clear();
            BlockCounts.Clear();
            RequiredCounts.Clear();
            EfficiencyTargets.Clear();
            FunctionalityEventHandlers.Clear();
            InfoTargets.Clear();
            ToggleTargets.Clear();
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
        /// Subscribe to block events
        /// </summary>
        /// <param name="block">Block to subscribe to</param>
        private void SubscribeBlock(IMyCubeBlock block)
        {
            lg.File($"Subscribing to CubeBlock {config.BlockID(block, 1)} events.", 3);

            Action handler = () => {
                UpdateFunctionality(block);
                Update();
                MarkInfoDirty();
                };
            FunctionalityEventHandlers.Add(block, handler);
            block.SlimBlock.ComponentStack.IsFunctionalChanged += handler;

            if (config.IsInfoBlock(block))
            {
                SubscribeInfoTarget(block as IMyTerminalBlock);
            }
            if (config.IsToggleTarget(block))
            {
                IMyFunctionalBlock fBlock = block as IMyFunctionalBlock;
                SubscribeToggleTarget(fBlock);
                fBlock.RefreshCustomInfo();
                fBlock.SetDetailedInfoDirty();
            }
        }
        /// <summary>
        /// Unsubscribe from block events
        /// </summary>
        /// <param name="block">Block to unsubscribe from</param>
        private void UnsubscribeBlock(IMyCubeBlock block)
        {
            lg.File($"Unsubscribing from CubeBlock {config.BlockID(block, 1)} events.", 3);
            Action handler;
            if (FunctionalityEventHandlers.TryGetValue(block, out handler))
            {
                block.SlimBlock.ComponentStack.IsFunctionalChanged -= handler;
                FunctionalityEventHandlers.Remove(block);
            }
            else
            {
                lg.File($"Warning: could not unsubscribe event handlers from {config.BlockID(block)}", 1);
            }

            if (config.IsInfoBlock(block))
            {
                UnsubscribeInfoTarget(block as IMyTerminalBlock);
            }
            if (config.IsToggleTarget(block))
            {
                UnsubscribeToggleTarget(block as IMyFunctionalBlock);
            }
        }
        /// <summary>
        /// Make the terminal block receive the grid info
        /// in it's detailed info page
        /// </summary>
        /// <param name="block">Terminal block</param>
        private void SubscribeInfoTarget(IMyTerminalBlock block)
        {
            lg.File($"Subscribing info target {config.BlockID(block, 2)}");
            InfoTargets.Add(block);
            block.AppendingCustomInfo += SetInfo;
        }
        /// <summary>
        /// Stop a terminal block from receiving
        /// grid state info
        /// </summary>
        /// <param name="block">Terminal block</param>
        private void UnsubscribeInfoTarget(IMyTerminalBlock block)
        {
            lg.File($"Unsubscribing info target {config.BlockID(block, 2)}");
            if (!InfoTargets.Remove(block))
            {
                lg.File($"Warning: Tried to unsubscribe from info target {config.BlockID(block)}, but it was not found.", 1);
            }
            block.AppendingCustomInfo -= SetInfo;
        }
        private void SubscribeToggleTarget(IMyFunctionalBlock block)
        {
            lg.File($"Subscribing toggle target {config.BlockID(block, 2)}", 2);
            ToggleTargets.Add(block, new ToggleTarget()
            {
                UserEnabled = block.Enabled,
                Starved = false,
                Block = block,
                IgnoreHandler = false
            });
            block.EnabledChanged += EnabledChanged;
            block.AppendingCustomInfo += SetToggleTargetInfo;
        }
        private void UnsubscribeToggleTarget(IMyFunctionalBlock block)
        {
            lg.File($"Unsubscribing toggle target {config.BlockID(block, 2)}", 2);
            if (!ToggleTargets.Remove(block))
            {
                lg.File($"Warning: Tried to unsubscribe from toggle target {config.BlockID(block)}, but it was not found.", 1);
            }
            block.EnabledChanged -= EnabledChanged;
            block.AppendingCustomInfo -= SetToggleTargetInfo;
        }
        private void EnabledChanged(IMyTerminalBlock block)
        {
            ToggleTarget tt;
            if (ToggleTargets.TryGetValue(block as IMyFunctionalBlock, out tt))
            {
                lg.File($"Block {config.BlockID(block, 1)} changed enabled state to: {tt.Block.Enabled}", 3);
                if (tt.IgnoreHandler)
                {
                    tt.IgnoreHandler = false;
                    block.RefreshCustomInfo();
                    block.SetDetailedInfoDirty();
                    return;
                }
                tt.UserEnabled = tt.Block.Enabled;
                block.RefreshCustomInfo();
                block.SetDetailedInfoDirty();
            }
            else
            {
                lg.File($"Warning: {config.BlockID(block)} triggered EnabledChanged, but block is not tracked.", 1);
            }
        }
        /// <summary>
        /// Adds a block to the grid's block count calculations and updates efficiency.
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
            lg.File($"Adding block {config.BlockID(block, 1)}", 3);
            if (config.IsTrackedBlock(block))
            {
                RegisterBlock(block.FatBlock);
                Update();
                MarkInfoDirty();
            }
        }
        /// <summary>
        /// Removes a block from the grid's block count calculations and updates efficiency.
        /// </summary>
        /// <param name="block">Block to remove</param>
        public void RemoveBlock(IMySlimBlock block)
        {
            lg.File($"Removing block {config.BlockID(block, 1)}", 3);
            if (config.IsTrackedBlock(block))
            {
                UnregisterBlock(block.FatBlock);
                Update();
                MarkInfoDirty();
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
        public void ChangeRequirement(string groupName, float amount)
        {
            if (!RequiredCounts.ContainsKey(groupName))
            {
                RequiredCounts[groupName] = 0;
            }
            float previousValue = RequiredCounts[groupName];
            float newValue = RoundRequirement(previousValue + amount);
            lg.File($"Changing RequiredCount for group {groupName} by {amount}, new value {newValue}.", 3);
            if (newValue < 0)
            {
                lg.File($"Warning: RequiredCount for group {groupName} went below zero. Removing tracking element.", 1);
                lg.OnScreen($"Warning: RequiredCount for group {groupName} went below zero. Removing tracking element.", durationMs: 2000, level: 2, color: "Red");
                RequiredCounts.Remove(groupName);
            }
            else if (newValue == 0)
            {
                lg.File($"RequiredCount for group {groupName} is now zero. Removing tracking element.", 4);
                RequiredCounts.Remove(groupName);
            }
            else
            {
                RequiredCounts[groupName] = newValue;
            }
        }
        /// <summary>
        /// Request an info update on each info target block
        /// </summary>
        private void MarkInfoDirty()
        {
            lg.File("Marked info as dirty.", 3);
            foreach(IMyTerminalBlock block in InfoTargets)
            {
                block.RefreshCustomInfo();
                block.SetDetailedInfoDirty();
            }
        }
        /// <summary>
        /// Callback for the terminal block to call when the custom info
        /// needs updating
        /// </summary>
        /// <param name="block">Terminal block</param>
        /// <param name="sb">String builder</param>
        private void SetInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.Append("AHOD Status:\n");
            sb.Append($" Efficiency: {Efficiency:P0}");
            sb.Append("  Counts:\n");
            foreach(KeyValuePair<string, float> blockReq in RequiredCounts)
            {
                string groupName = blockReq.Key;
                float requiredCount = blockReq.Value;
                int currentCount= 0;
                BlockCounts.TryGetValue(groupName, out currentCount);
                float actualRequiredCount = (float)Math.Floor(requiredCount);
                sb.Append($"    -{groupName}: {currentCount}/{actualRequiredCount}");
                if (actualRequiredCount != requiredCount)
                {
                    sb.Append($" ({requiredCount})");
                }
                sb.Append("\n");
                foreach(KeyValuePair<string, int> blockCount in BlockCounts)
                {
                    string source = blockCount.Key;
                    Dictionary<string, float> requirements = null;
                    config.EfficiencyRequirements.TryGetValue(source, out requirements);
                    float relevantRequirement = 0;
                    requirements?.TryGetValue(groupName, out relevantRequirement);
                    if (relevantRequirement > 0)
                    {
                        int numSources = 0;
                        BlockCounts.TryGetValue(source, out numSources);
                        float total = relevantRequirement * numSources;
                        sb.Append($"      -{source} ({numSources}): {total}\n");
                    }
                }
            }
        }
        private void SetToggleTargetInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            ToggleTarget tt;
            if (!ToggleTargets.TryGetValue(block as IMyFunctionalBlock, out tt))
            {
                lg.File($"Warning: Tried to fill detailed info to a toggle target {config.BlockID(block)} that is not tracked.", 1);
                return;
            }
            sb.Append("AHOD Status:\n");
            if (tt.UserEnabled)
            {
                sb.Append(" User enabled\n");
            }
            else
            {
                sb.Append(" User disabled\n");
            }
            if (!tt.Starved)
            {
                sb.Append(" Needs met\n");
            }
            else
            {
                sb.Append(" Needs unmet\n");
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
        /// <summary>
        /// Apply the given productivity into a production block
        /// </summary>
        /// <param name="block">Production block</param>
        /// <param name="efficiency">Productivity</param>
        private void ApplyProductivityEfficiency(MyCubeBlock block, float efficiency)
        {
            lg.File($"Applying productivity efficiency {efficiency:P0} to block {config.BlockID(block, 2)}.", 4);
            float additiveEfficiency = efficiency - 1f;
            block.UpgradeValues["Productivity"] += additiveEfficiency;
            block.CommitUpgradeValues();
            IMyTerminalBlock terminalBlock = block as IMyTerminalBlock;
            terminalBlock?.SetDetailedInfoDirty();
        }
        /// <summary>
        /// Remove the given productivity from a production block
        /// </summary>
        /// <param name="block">Production block</param>
        /// <param name="efficiency">Productivity</param>
        private void RemoveProductivityEfficiency(MyCubeBlock block, float efficiency)
        {
            lg.File($"Removing previously applied efficiency {efficiency:P0} from block {config.BlockID(block, 2)}.", 4);
            float additiveEfficiency = efficiency - 1f;
            block.UpgradeValues["Productivity"] -= additiveEfficiency;
            block.CommitUpgradeValues();
            IMyTerminalBlock terminalBlock = block as IMyTerminalBlock;
            terminalBlock?.SetDetailedInfoDirty();
        }
        /// <summary>
        /// Recalculates the grid's efficiency based on current block
        /// counts.
        /// </summary>
        private void RecalculateEfficency()
        {
            float minEff = 1f;
            foreach (var kvp in RequiredCounts)
            {
                string groupName = kvp.Key;
                float required = kvp.Value;
                float requiredFloor = (float)Math.Floor(required);
                if (required > 0)
                {
                    int available = 0;
                    float groupEff = 1;
                    BlockCounts.TryGetValue(groupName, out available);
                    if (requiredFloor > 0)
                    {
                        groupEff = available / requiredFloor;
                    }
                    lg.File($"Group {groupName}: {available}/{required}, efficiency {groupEff:P0}.", 4);
                    if (groupEff < minEff)
                    {
                        minEff = groupEff;
                    }
                }
                else
                {
                    lg.File($"Warning: Required count for group {groupName} exist and is zero.", 2);
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
            lg.File($"Registering block {config.BlockID(block, 1)}.", 4);
            SubscribeBlock(block);
            if (block.IsFunctional)
            {
                SetFunctional(block);
            }
            else
            {
                lg.File($"Added block {config.BlockID(block, 1)} was not functional, count stays the same.", 4);
            }
        }
        /// <summary>
        /// Unregisters a block from the grid's tracking system, if the subtype is tracked.
        /// </summary>
        /// <param name="block">Block to unregister</param>
        public void UnregisterBlock(IMyCubeBlock block)
        {
            lg.File($"Unregistering block {config.BlockID(block, 1)}.", 4);
            UnsubscribeBlock(block);
            if (block.IsFunctional)
            {
                SetNotFunctional(block);
            }
            else
            {
                lg.File($"Removed block {config.BlockID(block, 1)} was not functional, count stays the same.", 4);
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
            string groupName = config.GroupOf(block);
            lg.File($"Detected {config.BlockID(block, 1)} becoming functional.", 4);
            ChangeGroupCount(groupName, 1);
            if (config.EfficiencyRequirements.ContainsKey(groupName))
            {
                if (config.BlockHasProductivity(block))
                {
                    lg.File($"Adding block {config.BlockID(block, 1)} to productivity targets.", 3);
                    if (EfficiencyTargets.Add(block as MyCubeBlock))
                    {
                        ApplyProductivityEfficiency(block as MyCubeBlock, currentAppliedEfficiency);
                    }
                    else
                    {
                        lg.File($"Warning: Tried to register block {config.BlockID(block)} to productivity targets, but it was already present.", 2);
                    }
                }
                else
                {
                    IMyFunctionalBlock fBlock = block as IMyFunctionalBlock;
                    if (fBlock != null)
                    {
                        ToggleTarget tt;
                        if (ToggleTargets.TryGetValue(block as IMyFunctionalBlock, out tt))
                        {
                            tt.UserEnabled = tt.Block.Enabled;
                        }
                    }
                    else
                    {
                        lg.File($"Warning: Could not cast {config.BlockID(block)} as functional block.", 1);
                    }
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
            string groupName = config.GroupOf(block);
            lg.File($"Detected {config.BlockID(block, 2)} becoming unfunctional.", 4);
            ChangeGroupCount(groupName, -1);
            if (config.EfficiencyRequirements.ContainsKey(groupName))
            {
                if (config.BlockHasProductivity(block))
                {
                    lg.File($"Removing block {config.BlockID(block, 1)} from productivity targets.", 3);
                    if (EfficiencyTargets.Remove(block as MyCubeBlock))
                    {
                        RemoveProductivityEfficiency(block as MyCubeBlock, currentAppliedEfficiency);
                    }
                    else
                    {
                        lg.File($"Warning: Tried to remove block {config.BlockID(block)} from productivity targets, but it was not found.", 1);
                    }
                }
                else
                {
                    IMyFunctionalBlock fBlock = block as IMyFunctionalBlock;
                    if (fBlock != null)
                    {
                        ToggleTarget tt;
                        if (ToggleTargets.TryGetValue(fBlock, out tt))
                        {
                            fBlock.Enabled = tt.UserEnabled;
                        }
                    }
                    else
                    {
                        lg.File($"Warning: Could not cast {config.BlockID(block)} as functional block.", 1);
                    }
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
        private float RoundRequirement(float value)
        {
            return (float)System.Math.Round(value * 100f) / 100f;
        }

        private class ToggleTarget
        {
            public bool UserEnabled;
            public bool Starved;
            public bool IgnoreHandler;
            public IMyFunctionalBlock Block;
        }
    }
}