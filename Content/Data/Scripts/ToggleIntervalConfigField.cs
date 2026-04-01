namespace AHOD
{
    public class ToggleIntervalConfigField : ConfigField<int>
    {
        public ToggleIntervalConfigField(string name, string section, Logger lg)
            : base(name, section, lg)
        {
        }

        protected override int Deserialize(string iniStr)
        {
            int interval = 0;
            if (int.TryParse(iniStr.Trim(), out interval))
            {
                Valid = true;
                return interval;
            }
            else
            {
                lg.File($"WARNING: Invalid toggle interval '{iniStr}'.", 0);
                Valid = false;
                return interval;
            }
        }

        protected override string Serialize(int value)
        {
            return value.ToString();
        }

        protected override int DefaultValue()
        {
            return 10;
        }
    }
}