
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace AHOD
{
    public class AHODConfig
    {
        const string VariableId = nameof(AHODSession);
        const string FileName = "Config.ini";
        const string IniSection = "Config";
        public int DebugLevel = 1;
        public List<BedRequirement> BedRequirements = new List<BedRequirement>();
        public List<string> BedSubtypeIds = new List<string>();
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
            // Default values
            BedRequirements = new List<BedRequirement>
            {
                new BedRequirement() { SubtypeId = "LargeRefinery", Beds = 5 },
                new BedRequirement() { SubtypeId = "LargeRefineryIndustrial", Beds = 5 },
                new BedRequirement() { SubtypeId = "LargePrototechRefinery", Beds = 10 },
                new BedRequirement() { SubtypeId = "Blast Furnace", Beds = 3 },
                new BedRequirement() { SubtypeId = "LargeAssembler", Beds = 5 },
                new BedRequirement() { SubtypeId = "BasicAssembler", Beds = 2 },
                new BedRequirement() { SubtypeId = "LargeAssemblerIndustrial", Beds = 5 },
                new BedRequirement() { SubtypeId = "LargePrototechAssembler", Beds = 10 },
            };

            BedSubtypeIds = new List<string>
            {
                "LargeBlockBed",
                "LargeBlockHalfBed",
                "LargeBlockHalfBedOffset",
                "LargeBlockInsetBed",
                "LargeBlockBedFree",
            };
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
        }

        void ApplyConfig(MyIni iniParser)
        {
            string bedReqStr = iniParser.Get(IniSection, nameof(BedRequirements)).ToString("");
            lg.File("Parsing BedRequirements: " + bedReqStr, 3);
            BedRequirements = ParseBedRequirements(bedReqStr);
            BedSubtypeIds = ParseSubtypes(iniParser.Get(IniSection, nameof(BedSubtypeIds)).ToString(""));
            DebugLevel = iniParser.Get(IniSection, nameof(DebugLevel)).ToInt32(1);
        }

        void PopulateIniParser(MyIni iniParser)
        {
            iniParser.Set(IniSection, nameof(BedRequirements), EncodeBedRequirements(BedRequirements));
            iniParser.Set(IniSection, nameof(BedSubtypeIds), String.Join(";", BedSubtypeIds));
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

        string EncodeBedRequirements(List<BedRequirement> reqs)
        {
            string result = "";
            bool first = true;
            foreach (BedRequirement req in reqs)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    result += ";";
                }
                result += $"{req.SubtypeId}:{req.Beds}";
            }
            return result;
        }

        List<string> ParseSubtypes(string datastring)
        {
            List<string> types = new List<string>();
            if (datastring == "")
            {
                return types;
            }
            foreach (string type in datastring.Split(';'))
            {
                types.Add(type);
            }
            lg.File($"Parsed {types.Count} bed subtypes.", 3);
            return types;
        }

        List<BedRequirement> ParseBedRequirements(string datastring)
        {
            List<BedRequirement> reqs = new List<BedRequirement>();
            if (datastring == "")
            {
                lg.File("No bed requirements specified in config.", 3);
                return reqs;
            }
            foreach (string kvpair in datastring.Split(';'))
            {
                lg.File($"Parsing bed requirement: {kvpair}", 4);
                if (!kvpair.Contains(":"))
                {
                    lg.File($"ERROR: Invalid bed requirement list item '{kvpair}', the correct format is: 'Subtype:NubmerOfBeds'.", 0);
                    continue;
                }
                string subtype = kvpair.Split(':')[0];
                string numBeds = kvpair.Split(':')[1];
                if (numBeds.All(Char.IsDigit))
                {
                    reqs.Add(new BedRequirement() { SubtypeId = subtype, Beds = int.Parse(numBeds) });
                }
                else
                {
                    lg.File($"ERROR: Invalid number of beds '{numBeds}', must be an integer.", 0);
                }
            }
            lg.File($"Parsed {reqs.Count} bed requirements.", 3);
            return reqs;
        }
    }

    public class BedRequirement
    {
        public string SubtypeId;
        public int Beds;
    }
}