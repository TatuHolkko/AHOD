
using System;
using System.Collections.Generic;
using System.IO;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace AHOD
{
    public class AHODConfig
    {
        const string VariableId = nameof(AHODSession);
        const string FileName = "Config.ini";
        const string IniSection = "AHOD";
        public bool Valid = true;
        public int DebugLevel = 1;
        public Dictionary<string, Dictionary<string, float>> EfficiencyRequirements => efficiencyReqField.Value;
        public Dictionary<string, HashSet<string>> BlockGroups => blockGroupsField.Value;
        Dictionary<string, string> groupOfBlockType = new Dictionary<string, string>();
        ConfigField<Dictionary<string, Dictionary<string, float>>> efficiencyReqField;
        ConfigField<Dictionary<string, HashSet<string>>> blockGroupsField;
        ConfigField<int> debugLevelField;
        Logger lg;

        public AHODConfig(Logger logger = null)
        {
            if (logger != null)
            {
                lg = logger;
            }
            else
            {
                lg = new Logger();
            }
            efficiencyReqField = new EfficiencyRequirementsConfigField(nameof(EfficiencyRequirements), IniSection, lg);
            blockGroupsField = new BlockGroupsConfigField(nameof(BlockGroups), IniSection, lg);
            debugLevelField = new DebugLevelConfigField(nameof(DebugLevel), IniSection, lg);
            CreateBlockGroupMappings();
            Valid = ValidateLoadedConfig();
            if (!Valid)
            {
                throw new Exception("AHOD: Default config is invalid!");
            }
        }

        public void Export()
        {
            MyIni iniParser = new MyIni();
            SaveToIniParser(iniParser);
            ExportToFile(iniParser);
            ExportToSBC(iniParser);
        }

        public void Load()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                lg.File("Loading config on host...", 2);
                LoadOnHost();
            }
            else
            {
                lg.File("Loading config on client...", 2);
                LoadOnClient();
            }
            Valid = ValidateLoadedConfig();
            if (!Valid)
            {
                throw new Exception("AHOD: Loaded config is invalid!");
            }
            CreateBlockGroupMappings();
        }

        public bool IsTrackedBlock(IMySlimBlock slimBlock)
        {
            if (slimBlock == null || slimBlock.FatBlock == null)
            {
                return false;
            }
            return groupOfBlockType.ContainsKey(slimBlock.FatBlock.BlockDefinition.SubtypeId);
        }
        /// <summary>
        /// Try to determine the type of a given block
        /// </summary>
        /// <param name="block">Block to inspect</param>
        /// <returns>The block type, if it can be determined, null otherwise</returns>
        public string BlockTypeOf(IMyCubeBlock block)
        {
            if (block.BlockDefinition.SubtypeId.Length > 0)
            {
                return block.BlockDefinition.SubtypeId;
            }
            else
            {
                string baseTypeString = block.BlockDefinition.TypeIdString;
                if (baseTypeString != null && baseTypeString.Length > "MyObjectBuilder_".Length)
                {
                    return baseTypeString.Substring("MyObjectBuilder_".Length);
                }
                else
                {
                    lg.File($"Warning: Can not recognize type of given block.", 1);
                    return null;
                }
            }
        }
        /// <summary>
        /// Try to determine the group of a given block
        /// </summary>
        /// <param name="block">Block to inspect</param>
        /// <returns>Group name if it exists, null otherwise</returns>
        public string GroupOf(IMyCubeBlock block)
        {
            string group = null;
            groupOfBlockType.TryGetValue(BlockTypeOf(block), out group);
            return group;
        }
        /// <summary>
        /// Formulate an identification string of a block, useful for logging
        /// </summary>
        /// <param name="cubeBlock">Block to identify</param>
        /// <param name="level">Level of detail</param>
        /// <returns>An identification string</returns>
        public string BlockID(IMyCubeBlock cubeBlock, int level = 3)
        {
            if (cubeBlock == null)
            {
                return "[null block]";
            }
            string name = "";
            string dispName = "";
            if (cubeBlock.DisplayName != null)
            {
                dispName = cubeBlock.DisplayName.Length > 0 ? $" '{cubeBlock.DisplayName}'": "" ;
            }
            string type = BlockTypeOf(cubeBlock);
            string hex = $"{cubeBlock.EntityId:x10}";
            string entId = $"{hex.Substring(hex.Length - 5, 5)}";
            if (level == 0)
            {
                name = entId;
            }
            else if (level == 1)
            {
                name = type;
            }
            else if (level == 2)
            {
                name = $"{type}{dispName}";
            }
            else if (level == 3)
            {
                name = $"{type}{dispName} ({entId})";
            }
            else
            {
                name = "Unknown id level";
            }
            return $"[{name}]";
        }
        /// <summary>
        /// Formulate an identification string of a block
        /// </summary>
        /// <param name="cubeBlock">Block to identify</param>
        /// <param name="level">Level of detail</param>
        /// <returns>An identification string</returns>
        public string BlockID(IMySlimBlock slimBlock, int level = 3)
        {
            string name = "";
            if (slimBlock == null)
            {
                name = "[null block]";
            }
            else if (slimBlock.FatBlock == null)
            {
                name = $"[{slimBlock.BlockDefinition.DisplayNameText}]";
            }
            else
            {
                return BlockID(slimBlock.FatBlock, level);
            }

            return name;
        }
        public bool IsInfoBlock(IMyCubeBlock block)
        {
            if (block == null)
            {
                return false;
            }
            IMyTerminalBlock tblock = block as IMyTerminalBlock;
            if (tblock == null)
            {
                return false;
            }
            if (IsTrackedBlock(tblock.SlimBlock))
            {
                if (GroupOf(block) == "Beds")
                {
                    return true;
                }
            }
            return false;
        }

        void CreateBlockGroupMappings()
        {
            groupOfBlockType.Clear();
            foreach (var group in BlockGroups)
            {
                foreach (var subtype in group.Value)
                {
                    // Duplicates are not checked here, ValidateConfig does that
                    groupOfBlockType[subtype] = group.Key;
                }
            }
        }

        bool ValidateLoadedConfig()
        {
            bool valid = true;
            HashSet<string> allBlockTypes = new HashSet<string>();
            foreach (var group in BlockGroups)
            {
                foreach (var blockType in group.Value)
                {
                    if (allBlockTypes.Contains(blockType))
                    {
                        lg.File($"WARNING: Block type '{blockType}' is defined in multiple groups, which is not supported.", 0);
                        valid = false;
                    }
                    allBlockTypes.Add(blockType);
                }
            }
            foreach (var reqDef in EfficiencyRequirements)
            {
                if (!BlockGroups.ContainsKey(reqDef.Key))
                {
                    lg.File($"WARNING: Efficiency requirement created for an undefined group '{reqDef.Key}'.", 0);
                    valid = false;
                    continue;
                }
                foreach (var req in reqDef.Value)
                {
                    if (!BlockGroups.ContainsKey(req.Key))
                    {
                        lg.File($"WARNING: Efficiency requirement for group '{reqDef.Key}' references an undefined group '{req.Key}'.", 0);
                        valid = false;
                    }
                    if (req.Value < 0)
                    {
                        lg.File($"WARNING: Efficiency requirement for group '{reqDef.Key}' defines a negative required count '{req.Value}' for group '{req.Key}'.", 0);
                        valid = false;
                    }
                }
            }
            return valid;
        }

        bool LoadFromIniParser(MyIni iniParser)
        {
            bool allPresent = true;
            allPresent &= LoadConfigField(iniParser, blockGroupsField);
            allPresent &= LoadConfigField(iniParser, efficiencyReqField);
            allPresent &= LoadConfigField(iniParser, debugLevelField);
            return allPresent;
        }

        void SaveToIniParser(MyIni iniParser)
        {
            blockGroupsField.Save(iniParser);
            efficiencyReqField.Save(iniParser);
            debugLevelField.Save(iniParser);
        }

        bool LoadConfigField<T>(MyIni iniParser, ConfigField<T> field)
        {
            field.Load(iniParser);
            if (!field.Valid)
            {
                lg.File($"ERROR: Config field '{field.Section}.{field.Name}' is invalid: '{field.IniString}'", 0);
                throw new Exception("AHOD: Invalid config field loaded!");
            }
            return field.IsPresent;
        }

        void ExportToFile(MyIni iniParser)
        {
            using (TextWriter file = MyAPIGateway.Utilities.WriteFileInWorldStorage(FileName, typeof(AHODConfig)))
            {
                file.Write(iniParser.ToString());
            }
            lg.File("Config exported to world storage file.", 2);
        }

        void ExportToSBC(MyIni iniParser)
        {
            MyAPIGateway.Utilities.SetVariable<string>(VariableId, iniParser.ToString());
            lg.File("Config exported to sandbox.sbc.", 2);
        }

        void LoadOnHost()
        {
            string savePath = MyAPIGateway.Session?.CurrentPath;
            string gamePath = MyAPIGateway.Utilities?.GamePaths?.ModsPath;

            if (savePath == null || gamePath == null || savePath.StartsWith(MyAPIGateway.Utilities.GamePaths.ContentPath))
            {
                lg.File("Delaying world config loading because of world creation bugs...", 2);
                MyAPIGateway.Utilities.InvokeOnGameThread(LoadOnHost);
                return;
            }

            if (!LoadFromFile())
            {
                Export();
            }
        }

        void LoadOnClient()
        {
            LoadFromSBC();
        }

        bool LoadFromSBC()
        {
            lg.File("Trying to load config from sandbox.sbc...", 2);
            MyIni iniParser = new MyIni();
            string text;
            if (MyAPIGateway.Utilities.GetVariable<string>(VariableId, out text))
            {
                MyIniParseResult result;
                if (!iniParser.TryParse(text, out result))
                {
                    throw new Exception($"Config error: {result.ToString()}");
                }

                if (LoadFromIniParser(iniParser))
                {
                    lg.File("Config loaded from sandbox.sbc.", 2);
                    return true;
                }
                else
                {
                    lg.File("Config in sandbox.sbc is incomplete, using defaults for missing fields.", 2);
                    return false;
                }
            }
            else
            {
                lg.File("No config found in sandbox.sbc, using defaults.", 2);
                return false;
            }
        }

        bool LoadFromFile()
        {
            lg.File("Trying to load config from world storage file...", 2);
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(FileName, typeof(AHODConfig)))
            {
                using (TextReader file = MyAPIGateway.Utilities.ReadFileInWorldStorage(FileName, typeof(AHODConfig)))
                {
                    string text = file.ReadToEnd();

                    MyIni iniParser = new MyIni();
                    MyIniParseResult result;
                    if (!iniParser.TryParse(text, out result))
                    {
                        throw new Exception($"Config error: {result.ToString()}");
                    }

                    if (LoadFromIniParser(iniParser))
                    {
                        lg.File("Config loaded from world storage file.", 2);
                        return true;
                    }
                    else
                    {
                        lg.File("Config file incomplete, using defaults for missing fields.", 2);
                        return false;
                    }
                }
            }
            else
            {
                lg.File("No config file found, using defaults.", 2);
                return false;
            }
        }
    }
}