
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Scripting;

namespace AHOD
{
    public class AHODConfig
    {
        const string VariableId = nameof(AHODSession);
        const string FileName = "Config.ini";
        const string IniSection = "Config";

        public List<BedRequirement> BedRequirements = new List<BedRequirement>();
        public List<string> BedSubtypeIds = new List<string>();

        Logger lg = new Logger();

        IMyModContext Mod;

        public AHODConfig()
        {
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

        public void Load(IMyModContext mod)
        {
            Mod = mod;

            SaveConfig(new MyIni(), true, true);
            return;

            if (MyAPIGateway.Session.IsServer)
            {
                LoadOnHost();
            }
            else
            {
                LoadOnClient();
            }

        }

        void LoadConfig(MyIni iniParser)
        {
            BedRequirements = ParseBedRequirements(iniParser.Get(IniSection, nameof(BedRequirements)).ToString(""));
            BedSubtypeIds = ParseSubtypes(iniParser.Get(IniSection, nameof(BedSubtypeIds)).ToString(""));
        }

        void SaveConfig(MyIni iniParser, bool toWorld = false, bool toFile = false)
        {
            iniParser.Set(IniSection, nameof(BedRequirements), EncodeBedRequirements(BedRequirements));
            iniParser.Set(IniSection, nameof(BedSubtypeIds), String.Join(";", BedSubtypeIds));

            if (toWorld)
            {
                string iniText = iniParser.ToString();
                MyAPIGateway.Utilities.SetVariable<string>(VariableId, iniText);
                lg.Message("Config saved to world.");
            }

            if (toFile)
            {
                string iniText = iniParser.ToString();
                using (TextWriter file = MyAPIGateway.Utilities.WriteFileInWorldStorage(FileName, typeof(AHODConfig)))
                {
                    file.Write(iniText);
                }
                lg.Message("Config saved to file.");
            }
        }

        void LoadOnHost()
        {
            string savePath = MyAPIGateway.Session?.CurrentPath;
            string gamePath = MyAPIGateway.Utilities?.GamePaths?.ModsPath;

            if (savePath == null || gamePath == null || savePath.StartsWith(MyAPIGateway.Utilities.GamePaths.ContentPath))
            {
                lg.Message("Delaying world config loading because of world creation bugs...");
                MyAPIGateway.Utilities.InvokeOnGameThread(LoadOnHost);
                return;
            }

            MyIni iniParser = new MyIni();

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

                    LoadConfig(iniParser);
                    lg.Message("World config loaded!");
                }
            }

            iniParser.Clear();

            SaveConfig(iniParser, true, true);
        }

        void LoadOnClient()
        {
            string text;
            if(!MyAPIGateway.Utilities.GetVariable<string>(VariableId, out text))
            {
                throw new Exception("No config found in sandbox.sbc!");
            }

            MyIni iniParser = new MyIni();
            MyIniParseResult result;
            if(!iniParser.TryParse(text, out result))
            {
                throw new Exception($"Config error: {result.ToString()}");
            }

            LoadConfig(iniParser);
            lg.Message("World config loaded!");
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
                types.Append(type);
            }
            return types;
        }

        List<BedRequirement> ParseBedRequirements(string datastring)
        {
            List<BedRequirement> reqs = new List<BedRequirement>();
            if (datastring == "")
            {
                return reqs;
            }
            foreach (string kvpair in datastring.Split(';'))
            {
                if (!kvpair.Contains(":"))
                {
                    lg.Message($"ERROR: Invalid bed requirement list item '{kvpair}', the correct format is: 'Subtype:NubmerOfBeds'.");
                    continue;
                }
                string subtype = kvpair.Split(':')[0];
                string numBeds = kvpair.Split(':')[1];
                if (numBeds.All(Char.IsDigit))
                {
                    reqs.Append(new BedRequirement() { SubtypeId = subtype, Beds = int.Parse(numBeds) });
                }
                else
                {
                    lg.Message($"ERROR: Invalid number of beds '{numBeds}', must be an integer.");
                }
            }
            return reqs;
        }
    }

    public class BedRequirement
    {
        public string SubtypeId;
        public int Beds;
    }
}