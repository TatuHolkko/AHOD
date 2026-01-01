namespace AHOD
{
    public class DebugLevelConfigField : ConfigField<int>
    {
        public DebugLevelConfigField(string name, string section, Logger lg)
            : base(name, section, lg)
        {
        }

        protected override int Deserialize(string iniStr)
        {
            int level = 0;
            if (int.TryParse(iniStr.Trim(), out level))
            {
                Valid = true;
                return level;
            }
            else
            {
                lg.File($"WARNING: Invalid debug level '{iniStr}'.", 0);
                Valid = false;
                return level;
            }
        }

        protected override string Serialize(int value)
        {
            return value.ToString();
        }

        protected override int DefaultValue()
        {
            return 1;
        }
    }
}