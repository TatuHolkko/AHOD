using Sandbox.ModAPI;
using VRage.Utils;

namespace AHOD
{
    public class Logger
    {
        public const int MaxDebugLevel = 5;
        public bool Enabled { get; set; } = true;
        public bool FileLogging { get; set; } = true;
        public bool OnScreenLogging { get; set; } = true;
        public bool AvoidDuplicates { get; set; } = true;
        public int DebugLevel { get; set; } = 1;
        private string lastMsg = "";
        public Logger()
        {

        }

        public void OnScreen(string msg, int level = 1, int durationMs = 1000, string color = "White", bool force = false)
        {
            if (!Enabled)
            {
                return;
            }
            if (!force)
            {
                if (!OnScreenLogging || level > DebugLevel || IsDuplicate(msg))
                {
                    return;
                }
            }
            if (MyAPIGateway.Utilities == null && FileLogging)
            {
                MyLog.Default?.WriteLine("AHOD: MyAPIGateway.Utilities not initialized yet, writing given message here: " + msg);
            }
            MyAPIGateway.Utilities.ShowNotification(msg, durationMs, color);
        }

        public void File(string msg, int level = 1, bool force = false)
        {
            if (!Enabled)
            {
                return;
            }
            if (!force)
            {
                if (!FileLogging || level > DebugLevel || IsDuplicate(msg))
                {
                    return;
                }
            }
            MyLog.Default?.WriteLine("AHOD: " + msg);
        }

        public void Message(string msg, int level = 1)
        {
            File(msg, level);
            OnScreen(msg, level);
        }

        bool IsDuplicate(string msg)
        {
            if (!AvoidDuplicates)
            {
                return false;
            }
            if (msg == lastMsg)
            {
                return true;
            }
            lastMsg = msg;
            return false;
        }
    }
}