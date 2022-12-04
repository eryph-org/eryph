using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Modules.Controller;

namespace Eryph.Runtime.Zero
{
    internal class ZeroAgentLocator : IPlacementCalculator, IStorageManagementAgentLocator
    {
        public string CalculateVMPlacement(CatletConfig? dataConfig)
        {
            return Environment.MachineName;
        }

        public string FindAgentForDataStore(string dataStore, string environment)
        {
            return Environment.MachineName;
        }
    }
}