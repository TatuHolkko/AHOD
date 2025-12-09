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
            MyLog.Default.WriteLine("AHOD: " + msg);
            MyAPIGateway.Utilities.ShowNotification(msg, 5000, "White");
        }


        public bool fileLogging { get; set; } = true;
        public bool onScreenLogging { get; set; } = true;
    }
}