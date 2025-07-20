using System;
using Eryph.Core;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Inventory;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

internal class OvsPortCommands<RT> where RT : struct,
    HasCancel<RT>,
    HasHyperVOvsPortManager<RT>,
    HasOVSControl<RT>,
    HasLogger<RT>,
    HasPowershell<RT>
{
    private OvsPortCommands() { }

    public static Aff<RT, Unit> syncOvsPorts(
        TypedPsObject<VirtualMachineInfo> vmInfo,
        VMPortChange change) =>
        change is VMPortChange.Nothing ? unitAff : forceSyncPorts(vmInfo, change);

    public static Aff<RT, Unit> syncOvsPorts(Guid vmId, VMPortChange change) =>
        change is VMPortChange.Nothing ? unitAff : forceSyncPorts(vmId, change);

    private static Aff<RT, Unit> forceSyncPorts(Guid vmId, VMPortChange change) =>
        from psEngine in default(RT).Powershell
        from vmInfo in VmQueries.GetVmInfo(psEngine, vmId).ToAff()
        from _ in forceSyncPorts(vmInfo, change)
        select unit;

    private static Aff<RT, Unit> forceSyncPorts(
        TypedPsObject<VirtualMachineInfo> vmInfo,
        VMPortChange change) =>
        from psEngine in default(RT).Powershell
        let getAdaptersCommand = PsCommandBuilder.Create()
            .AddCommand("Get-VMNetworkAdapter")
            .AddParameter("VM", vmInfo.PsObject)
        from allAdapters in psEngine.GetObjectsAsync<VMNetworkAdapter>(getAdaptersCommand).ToAff()
        let adapters = allAdapters
            .Map(a => a.Value)
            .Filter(a => a.SwitchName == EryphConstants.OverlaySwitchName)
        from portNames in adapters
            // Do not use GetConfiguredPortName() as we need to be backwards
            // compatible with older port names.
            .Map(a  => getPortName(a.Id))
            .SequenceSerial()
        from _ in change is VMPortChange.Add
            ? addPorts(portNames)
            : removePorts(portNames)
        select unit;

    private static Aff<RT, string> getPortName(string adapterId) =>
        from portManager in default(RT).HyperVOvsPortManager
        from optionalPortName in portManager.GetPortName(adapterId).ToAff()
        from portName in optionalPortName.ToAff(
            Error.New($"The Hyper-V network adapter '{adapterId}' does not exist."))
        select portName;

    private static Aff<RT, Unit> addPorts(Seq<string> portNames) =>
        from _ in retry(
            Schedule.NoDelayOnFirst
            & Schedule.spaced(TimeSpan.FromSeconds(1))
            & Schedule.upto(TimeSpan.FromSeconds(60)),
            from _ in portNames.Map(addPort).SequenceSerial()
            select unit)
        select unit;

    private static Aff<RT, Unit> addPort(string portName) =>
        from ovsControl in default(RT).OVS
        from ct in cancelToken<RT>()
        from optionalInterface in ovsControl.GetInterface(portName, ct).ToAff()
        from @interface in optionalInterface.Match(
            Some: i =>
                from _1 in logDebug("Interface for port '{PortName}' found. No need to add the port.", portName)
                select i,
            None: () =>
                from _1 in logDebug("Interface for port '{PortName}' not found. Adding port...", portName)
                from _2 in ovsControl.AddPortWithIFaceId("br-int", portName, ct).ToAff()
                from oi in ovsControl.GetInterface(portName, ct).ToAff()
                from i in oi.ToEff(Error.New($"Port '{portName}' was not created successfully."))
                select i)
        from _1 in @interface.LinkState == "up"
            ? from _1 in logDebug("Interface on port '{PortName}' is up.", portName)
              select unit
            : from _1 in logDebug("Interface on port '{PortName}' is not up. OVS Error state: {OvsError}",
                  portName, @interface.Error)
              from _2 in ovsControl.RemovePort("br-int", portName, ct).ToAff()
              from _3 in FailAff<RT, Unit>(
                  Error.New($"Interface on port '{portName}' is not up. OVS Error state: '{@interface.Error}'."))
              select unit
        select unit;

    private static Aff<RT, Unit> removePorts(Seq<string> portNames) =>
        // This logic uses the ovs-vsctl command. We have observed that
        // it sometimes fails due to connection issues with the OVS database.
        // Hence, we just retry the commands up to 3 times.
        from _ in retry(
            Schedule.NoDelayOnFirst
            & Schedule.spaced(TimeSpan.FromSeconds(1))
            & Schedule.upto(TimeSpan.FromSeconds(60))
            & Schedule.recurs(3),
            from _ in portNames.Map(removePort).SequenceSerial()
            select unit)
        select unit;

    private static Aff<RT, Unit> removePort(string portName) =>
        from ovsControl in default(RT).OVS
        from ct in cancelToken<RT>()
        from optionalInterface in ovsControl.GetInterface(portName, ct).ToAff()
        from _  in optionalInterface.Match(
            Some: _ => from _1 in logDebug("Interface on port '{PortName}' found. Removing port...", portName)
                       from _2 in ovsControl.RemovePort("br-int", portName, ct).ToAff()
                       select unit,
            None: () => logDebug("Port '{PortName}' not found, nothing to remove.", portName))
        select unit;

    private static Aff<RT, Unit> logDebug(string message, params object?[] args) =>
        Logger<RT>.logDebug<OvsPortCommands<RT>>(message, args);
}
