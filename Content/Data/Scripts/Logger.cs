using Sandbox.ModAPI;
using VRage.Utils;

namespace AHOD
{
    public class Logger
    {
        public bool Enabled { get; set; } = true;
        public bool FileLogging { get; set; } = true;
        public bool OnScreenLogging { get; set; } = true;
        public int DebugLevel { get; set; } = 1;
        public Logger()
        {

        }

        public void Message(string msg, int level = 1)
        {
            if (level > DebugLevel)
            {
                return;
            }
            if (!Enabled)
            {
                return;
            }
            if (FileLogging)
            {
                MyLog.Default?.WriteLine("AHOD: " + msg);
            }
            if (OnScreenLogging)
            {
                if (MyAPIGateway.Utilities == null && FileLogging)
                {
                    MyLog.Default?.WriteLine("AHOD: MyAPIGateway.Utilities not initialized yet, writing given message here: " + msg);
                }
                MyAPIGateway.Utilities.ShowNotification(msg, 1000, "White");
            }
        }

    }
}