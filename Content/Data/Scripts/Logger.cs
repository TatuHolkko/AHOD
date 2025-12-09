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
                MyLog.Default.WriteLine("AHOD: " + msg);
            }
            if (OnScreenLogging)
            {
                MyAPIGateway.Utilities.ShowNotification(msg, 5000, "White");
            }
        }

        public bool Enabled { get; set; } = true;
        public bool FileLogging { get; set; } = true;
        public bool OnScreenLogging { get; set; } = true;
    }
}