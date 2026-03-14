
using System;
using System.Collections.Generic;
using System.Linq;

namespace AHOD
{
    public class BlockGroupsConfigField : ConfigField<Dictionary<string, HashSet<string>>>
    {
        public BlockGroupsConfigField(string name, string section, Logger lg)
            : base(name, section, lg)
        {
        }

        protected override Dictionary<string, HashSet<string>> Deserialize(string iniStr)
        {
            Dictionary<string, HashSet<string>> blockGroups = new Dictionary<string, HashSet<string>>();
            if (iniStr.Trim() == "")
            {
                Valid = true;
                return blockGroups;
            }
            var groupEntries = iniStr.Split(new char[] { '|' });
            foreach (var groupEntry in groupEntries)
            {
                var parts = groupEntry.Split(new char[] { ':' });
                if (parts.Length != 2)
                {
                    lg.File($"WARNING: Invalid block group entry '{groupEntry}'. Expected format 'GroupName1:SubtypeId1,SubtypeId2,...;GroupName2:...'", 0);
                    Valid = false;
                    return blockGroups;
                }
                var groupName = parts[0].Trim();
                if (groupName.Length == 0)
                {
                    lg.File($"WARNING: Empty group name in block group entry '{groupEntry}'.", 0);
                    Valid = false;
                    return blockGroups;
                }
                var subtypeIds = parts[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim())
                                        .ToHashSet();
                if (subtypeIds.Count == 0)
                {
                    lg.File($"WARNING: No subtype IDs defined for group '{groupName}' in block group entry '{groupEntry}'.", 0);
                    Valid = false;
                    return blockGroups;
                }
                blockGroups[groupName] = subtypeIds;
            }
            Valid = true;
            return blockGroups;
        }

        protected override string Serialize(Dictionary<string, HashSet<string>> blockGroups)
        {
            List<string> groupEntries = new List<string>();
            foreach (var group in blockGroups)
            {
                string entry = group.Key + ":" + string.Join(",", group.Value);
                groupEntries.Add(entry);
            }
            return string.Join(";", groupEntries);
        }

        protected override Dictionary<string, HashSet<string>> DefaultValue()
        {
            return new Dictionary<string, HashSet<string>>()
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
    }
}