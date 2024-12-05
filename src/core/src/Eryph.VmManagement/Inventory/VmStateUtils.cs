using Eryph.VmManagement.Data;
using LanguageExt;

using static Eryph.VmManagement.Data.VirtualMachineOperationalStatus;
using static Eryph.VmManagement.Data.VirtualMachineState;

namespace Eryph.VmManagement.Inventory;

public static class VmStateUtils
{
    /// <summary>
    /// Checks if a virtual machine can be inventoried based on the
    /// provided state information.
    /// </summary>
    /// <remarks>
    /// We use this method to avoid inventorying VMs which are still
    /// being changed by Hyper-V or are in an error state where Hyper-V
    /// will not report useful information.
    /// </remarks>
    public static bool canBeInventoried(
        Option<VirtualMachineState> state,
        Option<VirtualMachineOperationalStatus> operationalStatus) =>
        operationalStatus
            .Map(s => s is Ok or PredictiveFailure)
            .IfNone(false)
        && state
            .Map(s => s is Running or Off or Stopping or Saved or Paused or Starting or Reset
                or Saving or Pausing or Resuming or FastSaved or FastSaving or ForceShutdown
                or ForceReboot or Hibernated)
            .IfNone(false);
}
