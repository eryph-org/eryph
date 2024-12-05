using System;
using Eryph.VmManagement.Data;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public static class OperationalStatusConverter
{
    public static Option<VirtualMachineOperationalStatus> Convert(
        VirtualMachineOperationalStatus[] operationalStatus) =>
        from _ in Some(unit)
        // Explicitly check that the enum values are defined. This is a
        // precaution as the values might be extracted from the Powershell
        // using AutoMapper.
        let primaryStatus = operationalStatus.Length >= 1
            ? Optional(operationalStatus[0]).Filter(Enum.IsDefined)
            : None
        let secondaryStatus = operationalStatus.Length >= 2
            ? Optional(operationalStatus[1]).Filter(Enum.IsDefined)
            : None
        from result in Convert(primaryStatus, secondaryStatus)
        select result;

    public static Option<VirtualMachineOperationalStatus> Convert(
        Option<VirtualMachineOperationalStatus> primaryStatus,
        Option<VirtualMachineOperationalStatus> secondaryStatus) =>
        // We just assume that the secondary status is more relevant
        // than the primary status. According to the documentation for
        // the WMI class Msvm_ComputerSystem, primary status and secondary
        // status have distinct values but the Hyper-V Cmdlets use the
        // same enum.
        secondaryStatus | primaryStatus;
}
