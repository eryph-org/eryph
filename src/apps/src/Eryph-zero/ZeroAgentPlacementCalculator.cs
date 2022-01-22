using System;
using Eryph.Modules.Controller;
using Eryph.Resources.Machines.Config;

namespace Eryph.Runtime.Zero
{
    internal class ZeroAgentPlacementCalculator : IPlacementCalculator
    {
        public string CalculateVMPlacement(MachineConfig dataConfig)
        {
            return Environment.MachineName;
        }
    }
}