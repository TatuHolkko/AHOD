using Sandbox.ModAPI;
using VRage.Utils;

namespace AHOD
{
    public class Logger
    {
        public Logger()
        {

        }

        public void Message(string msg)
        {
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

        public bool Enabled { get; set; } = true;
        public bool FileLogging { get; set; } = true;
        public bool OnScreenLogging { get; set; } = true;
    }
}