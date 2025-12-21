using Sandbox.ModAPI;
using VRage.Utils;

namespace AHOD
{
    public class Logger
    {
        /// <summary>
        /// Debug level for logging
        /// 0 - Errors Only
        /// 1 - Standard Info for Deployed Use
        /// 2 - Standard Info For Development
        /// 3 - Standard Info for Debugging
        /// 4 - Detailed Debugging
        /// 5 - Excessive Debugging
        /// </summary>
        public int DebugLevel { get; set; } = 1;
        /// <summary>
        /// Maximum debug level supported
        /// </summary>
        public const int MaxDebugLevel = 5;
        /// <summary>
        /// Enable or disable all logging. Force does not override this.
        /// </summary>
        public bool Enabled { get; set; } = true;
        /// <summary>
        /// Enable or disable file logging. Force overrides this.
        /// </summary>
        public bool FileLogging { get; set; } = true;
        /// <summary>
        /// Enable or disable on screen logging. Force overrides this.
        /// </summary>
        public bool OnScreenLogging { get; set; } = true;
        /// <summary>
        /// Avoid logging duplicate messages in a row. Force overrides this.
        /// </summary>
        public bool AvoidDuplicates { get; set; } = true;
        /// <summary>
        /// Custon context string to add to log messages.
        /// </summary>
        public string Context { get; set; } = "";

        private string lastMsg = "";
        public Logger()
        {

        }
        /// <summary>
        /// Logs a message on screen. If the screen is not available, logs to file instead.
        /// </summary>
        /// <param name="msg">Message to show</param>
        /// <param name="level">Log level</param>
        /// <param name="durationMs">Duration of the message</param>
        /// <param name="color">Color of the message font</param>
        /// <param name="force">Ignore duplicate avoidance, on screen message enable, and debug level</param>
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
                MyLog.Default?.WriteLine($"AHOD:[{Context}] on-screen notifications not initialized yet, writing given message here: " + msg);
            }
            MyAPIGateway.Utilities.ShowNotification($"[{Context}] " + msg, durationMs, color);
        }
        /// <summary>
        /// Logs a message to file. If file logging is not available, does nothing.
        /// </summary>
        /// <param name="msg">Message to log</param>
        /// <param name="level">Log level</param>
        /// <param name="force">Ignore duplicate avoidance, file logging enable, and debug level</param>
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
            MyLog.Default?.WriteLine($"AHOD: [{Context}] " + msg);
        }
        /// <summary>
        /// Checks if the given message is a duplicate of the last logged message. If not, updates the last message.
        /// </summary>
        /// <param name="msg">Message</param>
        /// <returns>True if the message is duplicate</returns>
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