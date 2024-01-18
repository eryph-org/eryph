using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Runtime.Uninstaller
{
    internal class ProgressNavigationState
    {
        public ProgressNavigationState(
            bool removeAppData,
            bool removeVirtualMachines,
            string? uninstallReason,
            string? feedback)
        {
            RemoveAppData = removeAppData;
            RemoveVirtualMachines = removeVirtualMachines;
            UninstallReason = uninstallReason;
            Feedback = feedback;
        }

        public bool RemoveAppData { get; }

        public bool RemoveVirtualMachines { get; }

        public string? UninstallReason { get; }

        public string? Feedback { get; }
    }
}
