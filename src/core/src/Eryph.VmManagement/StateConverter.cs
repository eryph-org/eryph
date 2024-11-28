using System;
using Eryph.VmManagement.Data;
using LanguageExt;

namespace Eryph.VmManagement
{
    public static class StateConverter
    {
        public static VirtualMachineState GetCriticalState(VirtualMachineState state)
        {
            var vmState = state;
            switch (state)
            {
                case VirtualMachineState.Running:
                    vmState = VirtualMachineState.RunningCritical;
                    break;
                case VirtualMachineState.Off:
                    vmState = VirtualMachineState.OffCritical;
                    break;
                case VirtualMachineState.Stopping:
                    vmState = VirtualMachineState.StoppingCritical;
                    break;
                case VirtualMachineState.Saved:
                    vmState = VirtualMachineState.SavedCritical;
                    break;
                case VirtualMachineState.Paused:
                    vmState = VirtualMachineState.PausedCritical;
                    break;
                case VirtualMachineState.Starting:
                    vmState = VirtualMachineState.StartingCritical;
                    break;
                case VirtualMachineState.Reset:
                    vmState = VirtualMachineState.ResetCritical;
                    break;
                case VirtualMachineState.Saving:
                    vmState = VirtualMachineState.SavingCritical;
                    break;
                case VirtualMachineState.Pausing:
                    vmState = VirtualMachineState.PausingCritical;
                    break;
                case VirtualMachineState.Resuming:
                    vmState = VirtualMachineState.ResumingCritical;
                    break;
                case VirtualMachineState.FastSaved:
                    vmState = VirtualMachineState.FastSavedCritical;
                    break;
                case VirtualMachineState.FastSaving:
                    vmState = VirtualMachineState.FastSavingCritical;
                    break;
            }

            return vmState;
        }

        public static VirtualMachineState ConvertVMState(
            ushort enabledStateNumber,
            Option<string> otherEnabledState,
            ushort healthStateNumber)
        {
            var computerState = (VMComputerSystemState) enabledStateNumber;
            if (computerState == VMComputerSystemState.Other)
                computerState = ConvertVMOtherState(otherEnabledState);

            var state = (VirtualMachineState) computerState;

            var healthState = (VMComputerSystemHealthState) healthStateNumber;
            if (healthState != VMComputerSystemHealthState.Ok)
                state = GetCriticalState(state);

            return state;
        }

        internal static VMComputerSystemState ConvertVMOtherState(
            Option<string> otherState) =>
            otherState.Match(
                s => s switch
                {
                    _ when string.Equals(s, "Quiescing", StringComparison.OrdinalIgnoreCase) => VMComputerSystemState.Pausing,
                    _ when string.Equals(s, "Resuming", StringComparison.OrdinalIgnoreCase) => VMComputerSystemState.Resuming,
                    _ when string.Equals(s, "Saving", StringComparison.OrdinalIgnoreCase) => VMComputerSystemState.Saving,
                    _ when string.Equals(s, "FastSaving", StringComparison.OrdinalIgnoreCase) => VMComputerSystemState.FastSaving,
                    _ => VMComputerSystemState.Unknown
                },
                () => VMComputerSystemState.Unknown);
    }
}
