using System;
using System.Collections.Generic;

namespace AHOD
{
    public class EfficiencyRequirementsConfigField : ConfigField<Dictionary<string, Dictionary<string, float>>>
    {
        public EfficiencyRequirementsConfigField(string name, string section, Logger lg)
            : base(name, section, lg)
        {
        }

        protected override Dictionary<string, Dictionary<string, float>> Deserialize(string iniStr)
        {
            Dictionary<string, Dictionary<string, float>> efficiencyRequirements = new Dictionary<string, Dictionary<string, float>>();
            if (iniStr.Trim() == "")
            {
                Valid = true;
                return efficiencyRequirements;
            }
            var requirementEntries = iniStr.Split(new char[] { '|' });
            foreach (var requirementEntry in requirementEntries)
            {
                var parts = requirementEntry.Split(new char[] { ':' });
                if (parts.Length != 2)
                {
                    lg.File($"WARNING: Invalid efficiency requirement entry '{requirementEntry}'. Expected format 'GroupName1:ReqGroup1-Count1,ReqGroup2-Count2|GroupName2:...'", 0);
                    Valid = false;
                    return efficiencyRequirements;
                }
                var groupName = parts[0].Trim();
                if (groupName.Length == 0)
                {
                    lg.File($"WARNING: Empty group name in efficiency requirement entry '{requirementEntry}'.", 0);
                    Valid = false;
                    return efficiencyRequirements;
                }
                var reqParts = parts[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (reqParts.Length == 0)
                {
                    lg.File($"WARNING: No requirements defined for group '{groupName}' in efficiency requirement entry '{requirementEntry}'.", 0);
                    Valid = false;
                    return efficiencyRequirements;
                }
                var reqDict = new Dictionary<string, float>();
                foreach (var reqPart in reqParts)
                {
                    var reqPair = reqPart.Split(new char[] { '-' });
                    float count = 0;
                    if (reqPair.Length != 2 || !float.TryParse(reqPair[1].Trim(), out count))
                    {
                        lg.File($"WARNING: Invalid requirement '{reqPart}' in entry '{requirementEntry}'. Expected format 'ReqGroup-Count'", 0);
                        Valid = false;
                        return efficiencyRequirements;
                    }
                    var reqGroup = reqPair[0].Trim();
                    if (reqGroup.Length == 0)
                    {
                        lg.File($"WARNING: Empty required group name in requirement '{reqPart}' in entry '{requirementEntry}'.", 0);
                        Valid = false;
                        return efficiencyRequirements;
                    }
                    reqDict[reqGroup] = (float)(Math.Round(count * 100) / 100f);
                }
                efficiencyRequirements[groupName] = reqDict;
            }
            Valid = true;
            return efficiencyRequirements;
        }

        protected override string Serialize(Dictionary<string, Dictionary<string, float>> efficiencyRequirements)
        {
            List<string> requirementEntries = new List<string>();
            foreach (var reqDef in efficiencyRequirements)
            {
                List<string> reqParts = new List<string>();
                foreach (var req in reqDef.Value)
                {
                    string reqPart = req.Key + "=" + req.Value.ToString("0.00");
                    reqParts.Add(reqPart);
                }
                string entry = reqDef.Key + ":" + string.Join(",", reqParts);
                requirementEntries.Add(entry);
            }
            return string.Join(";", requirementEntries);
        }

        protected override Dictionary<string, Dictionary<string, float>> DefaultValue()
        {
            return new Dictionary<string, Dictionary<string, float>>()
                {
                    {
                        "Refineries", new Dictionary<string, float>()
                        {
                            { "Beds", 5 },
                        }
                    },
                };
        }
    }
}