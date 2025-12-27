
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public int DebugLevel = 1;
        public bool Valid = true;
        public Dictionary<string, Dictionary<string, int>> EfficiencyRequirements = new Dictionary<string, Dictionary<string, int>>();
        public Dictionary<string, HashSet<string>> BlockGroups = new Dictionary<string, HashSet<string>>();
        public Dictionary<string, string> GroupOfBlockSubtype = new Dictionary<string, string>();
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
            SetDefaultBlockConfigs();
            Valid = ValidateConfig();
            if (!Valid)
            {
                lg.File("WARNING: Config is invalid!", 0);
            }
        }

        public void Export()
        {
            MyIni iniParser = new MyIni();
            PopulateIniParser(iniParser);
            ExportToFile(iniParser);
            ExportToSBC(iniParser);
        }

        public void Load()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                LoadOnHost();
            }
            else
            {
                LoadOnClient();
            }
            Valid = ValidateConfig();
        }

        public Dictionary<string, int> GetEfficiencyRequirements(string groupName)
        {
            if (EfficiencyRequirements.ContainsKey(groupName))
            {
                return EfficiencyRequirements[groupName];
            }
            return new Dictionary<string, int>();
        }

        public string GetBlockGroup(string subtypeId)
        {
            if (GroupOfBlockSubtype.ContainsKey(subtypeId))
            {
                return GroupOfBlockSubtype[subtypeId];
            }
            return null;
        }

        public bool IsTrackedBlock(IMySlimBlock slimBlock)
        {
            if (slimBlock == null || slimBlock.FatBlock == null)
            {
                return false;
            }
            return GroupOfBlockSubtype.ContainsKey(slimBlock.FatBlock.BlockDefinition.SubtypeId);
        }

        void SetDefaultBlockConfigs()
        {
            SetDefaultBlockGroups();
            SetDefaultEfficiencyRequirements();
            CreateBlockGroupMappings();
        }

        void SetDefaultBlockGroups()
        {
            BlockGroups = new Dictionary<string, HashSet<string>>()
                {
                    {
                        "Beds", new HashSet<string>()
                        {
                            "LargeBlockBed",
                            "LargeBlockHalfBed",
                            "LargeBlockHalfBedOffset",
                            "LargeBlockInsetBed",
                            "LargeBlockBedFree",
                        }
                    },
                    {
                        "Refineries", new HashSet<string>()
                        {
                            "LargeRefinery",
                            "LargeRefineryIndustrial",
                        }
                    },
                };
        }

        void SetDefaultEfficiencyRequirements()
        {
            EfficiencyRequirements = new Dictionary<string, Dictionary<string, int>>()
                {
                    {
                        "Refineries", new Dictionary<string, int>()
                        {
                            { "Beds", 5 },
                        }
                    },
                };
        }

        void CreateBlockGroupMappings()
        {
            foreach (var group in BlockGroups)
            {
                foreach (var subtype in group.Value)
                {
                    // Duplicates are not checked here, ValidateConfig does that
                    GroupOfBlockSubtype[subtype] = group.Key;
                }
            }
        }

        bool ValidateConfig()
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

        void ApplyConfig(MyIni iniParser)
        {
            string groupStr = iniParser.Get(IniSection, nameof(BlockGroups)).ToString("");
            lg.File("Parsing Eff.Requirements: " + groupStr, 4);
            BlockGroups = ParseBlockGroups(groupStr);

            string reqStr = iniParser.Get(IniSection, nameof(EfficiencyRequirements)).ToString("");
            lg.File("Parsing Block Groups: " + reqStr, 4);
            EfficiencyRequirements = ParseEfficiencyRequirements(reqStr);

            DebugLevel = iniParser.Get(IniSection, nameof(DebugLevel)).ToInt32(1);
        }

        void PopulateIniParser(MyIni iniParser)
        {
            iniParser.Set(IniSection, nameof(BlockGroups), EncodeBlockGroups(BlockGroups));
            iniParser.Set(IniSection, nameof(EfficiencyRequirements), EncodeEfficiencyRequirements(EfficiencyRequirements));
            iniParser.Set(IniSection, nameof(DebugLevel), DebugLevel);
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

            MyIni iniParser = new MyIni();
            lg.File("Loading config from file...", 2);
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(FileName, typeof(AHODConfig)))
            {
                using (TextReader file = MyAPIGateway.Utilities.ReadFileInWorldStorage(FileName, typeof(AHODConfig)))
                {
                    string text = file.ReadToEnd();

                    MyIniParseResult result;
                    if (!iniParser.TryParse(text, out result))
                    {
                        throw new Exception($"Config error: {result.ToString()}");
                    }

                    ApplyConfig(iniParser);
                    lg.File("Config loaded from file.");
                }
            }
            else
            {
                lg.File("No config file found, creating default config.");
                PopulateIniParser(iniParser);
                ExportToFile(iniParser);
            }
        }

        void LoadOnClient()
        {
            lg.File("Loading config from sandbox.sbc...", 2);
            MyIni iniParser = new MyIni();
            string text;
            if (!MyAPIGateway.Utilities.GetVariable<string>(VariableId, out text))
            {
                lg.File("No config found in sandbox.sbc, creating one from defaults.");
                PopulateIniParser(iniParser);
                ExportToSBC(iniParser);
                return;
            }

            MyIniParseResult result;
            if (!iniParser.TryParse(text, out result))
            {
                throw new Exception($"Config error: {result.ToString()}");
            }

            ApplyConfig(iniParser);
            lg.File("Config loaded from sandbox.sbc.");
        }
        string EncodeBlockGroups(Dictionary<string, HashSet<string>> blockGroups)
        {
            List<string> groupEntries = new List<string>();
            foreach (var group in blockGroups)
            {
                string entry = group.Key + ":" + string.Join(",", group.Value);
                groupEntries.Add(entry);
            }
            return string.Join(";", groupEntries);
        }
        string EncodeEfficiencyRequirements(Dictionary<string, Dictionary<string, int>> efficiencyRequirements)
        {
            List<string> requirementEntries = new List<string>();
            foreach (var reqDef in efficiencyRequirements)
            {
                List<string> reqParts = new List<string>();
                foreach (var req in reqDef.Value)
                {
                    string reqPart = req.Key + "=" + req.Value.ToString();
                    reqParts.Add(reqPart);
                }
                string entry = reqDef.Key + ":" + string.Join(",", reqParts);
                requirementEntries.Add(entry);
            }
            return string.Join(";", requirementEntries);
        }
        public Dictionary<string, HashSet<string>> ParseBlockGroups(string input)
        {
            var blockGroups = new Dictionary<string, HashSet<string>>();
            if (input == null || input.Trim() == "")
            {
                return blockGroups;
            }
            var groupEntries = input.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var groupEntry in groupEntries)
            {
                var parts = groupEntry.Split(new char[] { ':' }, 2);
                if (parts.Length != 2)
                {
                    Valid = false;
                    lg.File($"WARNING: Invalid block group entry '{groupEntry}'. Expected format 'GroupName1:SubtypeId1,SubtypeId2,...;GroupName2:...'", 0);
                    continue;
                }
                var groupName = parts[0].Trim();
                var subtypeIds = parts[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim())
                                        .ToHashSet();
                blockGroups[groupName] = subtypeIds;
            }
            return blockGroups;
        }

        Dictionary<string, Dictionary<string, int>> ParseEfficiencyRequirements(string input)
        {
            var efficiencyRequirements = new Dictionary<string, Dictionary<string, int>>();
            if (input == null || input.Trim() == "")
            {
                return efficiencyRequirements;
            }
            var requirementEntries = input.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var requirementEntry in requirementEntries)
            {
                var parts = requirementEntry.Split(new char[] { ':' }, 2);
                if (parts.Length != 2)
                {
                    Valid = false;
                    lg.File($"WARNING: Invalid efficiency requirement entry '{requirementEntry}'. Expected format 'GroupName1:ReqGroup1=Count1,ReqGroup2=Count2;GroupName2:...'", 0);
                    continue;
                }
                var groupName = parts[0].Trim();
                var reqParts = parts[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var reqDict = new Dictionary<string, int>();
                foreach (var reqPart in reqParts)
                {
                    var reqPair = reqPart.Split(new char[] { '=' });
                    int count = 0;
                    if (reqPair.Length != 2 || !int.TryParse(reqPair[1].Trim(), out count))
                    {
                        Valid = false;
                        lg.File($"WARNING: Invalid requirement '{reqPart}' in entry '{requirementEntry}'. Expected format 'ReqGroup=Count'", 0);
                        continue;
                    }
                    var reqGroup = reqPair[0].Trim();
                    reqDict[reqGroup] = count;
                }
                efficiencyRequirements[groupName] = reqDict;
            }
            return efficiencyRequirements;
        }
    }
}