using System;
using Haipa.Modules.Controller;
using Haipa.Resources.Machines.Config;

namespace Haipa.Runtime.Zero
{
    internal class ZeroAgentPlacementCalculator : IPlacementCalculator
    {
        public string CalculateVMPlacement(MachineConfig dataConfig)
        {
            return Environment.MachineName;
        }
    }
}