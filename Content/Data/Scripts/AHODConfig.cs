
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
        public Dictionary<string, string> GroupOfBlockSubtype = new Dictionary<string, string>();

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
        }

        public bool IsTrackedBlock(IMySlimBlock slimBlock)
        {
            if (slimBlock == null || slimBlock.FatBlock == null)
            {
                return false;
            }
            return GroupOfBlockSubtype.ContainsKey(slimBlock.FatBlock.BlockDefinition.SubtypeId);
        }

        void CreateBlockGroupMappings()
        {
            GroupOfBlockSubtype.Clear();
            foreach (var group in BlockGroups)
            {
                foreach (var subtype in group.Value)
                {
                    // Duplicates are not checked here, ValidateConfig does that
                    GroupOfBlockSubtype[subtype] = group.Key;
                }
            }
        }

        bool ValidateLoadedConfig()
        {
            bool valid = true;
            HashSet<string> allSubtypes = new HashSet<string>();
            foreach (var group in BlockGroups)
            {
                foreach (var subtype in group.Value)
                {
                    if (allSubtypes.Contains(subtype))
                    {
                        lg.File($"WARNING: Block subtype '{subtype}' is defined in multiple groups, which is not supported.", 0);
                        valid = false;
                    }
                    allSubtypes.Add(subtype);
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