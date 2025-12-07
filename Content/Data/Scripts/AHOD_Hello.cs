using Sandbox.ModAPI;
using VRage.Utils;
using VRage.Game.Components;

namespace AHOD
{
    // This tells SE to load your component and call UpdateBeforeSimulation
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class AHOD_Hello : MySessionComponentBase
    {
        public override void LoadData()
        {
            MyLog.Default.WriteLine("AHOD: Hello World – LoadData()");
        }

        public override void UpdateBeforeSimulation()
        {
            // Only run once
            if (!_printed)
            {
                MyLog.Default.WriteLine("AHOD: Hello World – UpdateBeforeSimulation()");
                _printed = true;
            }
        }

        private bool _printed = false;
    }
}
