using System;
using Eryph.Modules.Controller;
using Eryph.Resources.Machines.Config;

namespace Eryph.Runtime.Zero
{
    internal class ZeroAgentLocator : IPlacementCalculator, IStorageManagementAgentLocator
    {
        public string CalculateVMPlacement(MachineConfig? dataConfig)
        {
            return Environment.MachineName;
        }

        public string FindAgentForDataStore(string dataStore, string environment)
        {
            return Environment.MachineName;
        }
    }
}