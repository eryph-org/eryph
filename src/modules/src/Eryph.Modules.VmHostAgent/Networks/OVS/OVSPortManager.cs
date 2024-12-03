using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Dbosoft.OVN.Windows;
using Eryph.Core;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Networking;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

class OVSPortManager(
    IHyperVOvsPortManager portManager,
    IPowershellEngine engine,
    ILogger log,
    ISystemEnvironment sysEnvironment)
    : IOVSPortManager
{
    public EitherAsync<Error, Unit> SyncPorts(
        TypedPsObject<VirtualMachineInfo> vmInfo,
        VMPortChange change) =>
        change is VMPortChange.Nothing
            ? RightAsync<Error, Unit>(unit)
            : ForceSyncPorts(vmInfo, change);

    private EitherAsync<Error, Unit> ForceSyncPorts(
        TypedPsObject<VirtualMachineInfo> vmInfo,
        VMPortChange change) =>
        from allAdapters in NetworkAdapterQuery.GetNetworkAdapters(vmInfo, engine)
            .ToAsync()
        let adapters = allAdapters
            .Map(a => a.Value)
            .Filter(a => a.SwitchName == EryphConstants.OverlaySwitchName)
        from portNames in adapters
            .Map(a => portManager.GetPortName(a.Id))
            .SequenceSerial()
        from _ in change is VMPortChange.Add
            ? AddPorts(vmInfo.Value.Id, portNames).ToAsync()
            : RemovePorts(portNames).ToAsync()
        select unit;

    public EitherAsync<Error, Unit> SyncPorts(Guid vmId, VMPortChange change)
    {
        if (change == VMPortChange.Nothing)
            return Unit.Default;


        return GetVMs<VirtualMachineInfo>(vmId)
            .Bind(SingleOrFailure)
            .Bind(vmInfo => SyncPorts(vmInfo, change));
    }


    private async Task<Either<Error, Unit>> AddPorts(Guid vmId, Seq<string> portNames)
    {
        var ovsControl = new OVSControl(sysEnvironment);

        var cancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var portsRequired = new LanguageExt.HashSet<string>(portNames);

        while (!cancelToken.IsCancellationRequested && portsRequired.Count > 0)
        {

            foreach (var portName in portsRequired.AsParallel())
            {
                try
                {
                    var portIsOk = await ovsControl.GetInterface(portName)
                        .BindLeft(left =>
                            {
                                log.LogDebug("Port {portName} not found. Error: {message}",
                                    portName, left.Message);

                                return ovsControl.AddPortWithIFaceId("br-int", portName, cancelToken.Token)
                                    .Bind(_ => ovsControl.GetInterface(portName));

                            }
                        ).Bind(inf =>
                        {
                            if (inf.LinkState.FirstOrDefault() == "up") return true;
                            log.LogDebug("Interface on port {portName} is not up. OVS Error state: {ovsError}",
                                portName,
                                string.Join(',', inf.Error));
                            return ovsControl.RemovePort("br-int", portName, cancelToken.Token).Map(_ => false);

                        }).Match(
                            r => r,
                            l =>
                            {
                                log.LogDebug(
                                    "Update of port {portName} failed. Retrying in 1 second. Message: {message}"
                                    , portName, l.Message);
                                return false;
                            }
                        );

                    if (!portIsOk)
                        continue;

                    portsRequired = portsRequired.Remove(portName);
                }
                catch (OperationCanceledException)
                {

                }

                if (portsRequired.Count > 0)
                    await Task.Delay(1000, cancelToken.Token);
            }
        }

        return portsRequired.Count == 0
            ? Unit.Default
            : Error.New($"Failed to add all ports of VM {vmId} to OVS.");

    }

    private async Task<Either<Error, Unit>> RemovePorts(Seq<string> portNames)
    {
        var ovsControl = new OVSControl(sysEnvironment);

        var cancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        foreach (var portName in portNames)
        {
            await ovsControl.GetInterface(portName)
                .Bind(_ =>
                {
                    log.LogDebug("Interface on port {portName} found. Removing port",
                        portName);
                    return ovsControl.RemovePort("br-int", portName, cancelToken.Token);

                }).IfLeft(
                    l =>
                    {
                        log.LogDebug(
                            "No need to remove interface of port {portName}, as it was not found."
                            , portName);
                    }
                );

        }
        return Unit.Default;
    }


    private static EitherAsync<Error, TypedPsObject<T>> SingleOrFailure<T>(Seq<TypedPsObject<T>> sequence)
    {
        return sequence.HeadOrNone().ToEither(Errors.SequenceEmpty).ToAsync();
    }

    private EitherAsync<Error, Seq<TypedPsObject<T>>> GetVMs<T>(Guid vmId)
    {
        return engine.GetObjectsAsync<T>(new PsCommandBuilder()
            .AddCommand("Get-VM").AddParameter("Id", vmId)).ToError().ToAsync();
    }
}