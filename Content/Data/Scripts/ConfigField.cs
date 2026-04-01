using VRage.Game.ModAPI.Ingame.Utilities;

namespace AHOD
{
    public abstract class ConfigField<T>
    {
        public string Name { get; private set; }
        public string Section { get; private set; }
        public bool Valid { get; protected set; } = false;
        public bool IsPresent { get; protected set; } = false;
        public T Value;
        public string SerializedValue { get; private set; }
        public string IniString { get; private set; }
        protected Logger lg;

        public ConfigField(string name, string section, Logger lg)
        {
            Name = name;
            Section = section;
            this.lg = lg;
            Value = DefaultValue();
        }

        public void Load(MyIni iniParser)
        {
            IsPresent = iniParser.ContainsKey(Section, Name);
            if (IsPresent)
            {
                IniString = iniParser.Get(Section, Name).ToString();
                Value = Deserialize(IniString);
                if (!Valid)
                {
                    Value = DefaultValue();
                }
            }
            else
            {
                Valid = true;
                Value = DefaultValue();
            }
        }

        public void Save(MyIni iniParser)
        {
            SerializedValue = Serialize(Value);
            iniParser.Set(Section, Name, SerializedValue);
        }

        public void SetDefault()
        {
            Value = DefaultValue();
        }

        protected abstract T Deserialize(string iniStr);

        protected abstract string Serialize(T value);

        protected abstract T DefaultValue();
    }
}