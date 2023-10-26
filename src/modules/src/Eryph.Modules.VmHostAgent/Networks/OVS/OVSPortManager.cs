using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Eryph.Core;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Networking;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

class OVSPortManager : IOVSPortManager
{
    private readonly IPowershellEngine _engine;
    private readonly ILogger _log;
    private readonly ISysEnvironment _sysEnvironment;

    public OVSPortManager(IPowershellEngine engine, ILogger log, ISysEnvironment sysEnvironment)
    {
        _engine = engine;
        _log = log;
        _sysEnvironment = sysEnvironment;
    }


    public EitherAsync<Error, Unit> SyncPorts(TypedPsObject<VirtualMachineInfo> vmInfo, VMPortChange change)
    {
        if (change == VMPortChange.Nothing)
            return Unit.Default;

        var getNetworkAdapters = Prelude.fun(
            (TypedPsObject<VirtualMachineInfo> vm) => NetworkAdapterQuery.GetNetworkAdapters(vm, _engine).ToAsync()
                .Map(r => r
                    .Where(a => a.Value.SwitchName == EryphConstants.OverlaySwitchName)
                    .Select(a => a.Value))
        );

        var getPortNames = Prelude.fun(
            (Seq<VMNetworkAdapter> adapters) =>
                Prelude.RightAsync<Error, IEnumerable<string>>(VirtualNetworkQuery.FindOvsPortNames(adapters))
                    .Map(r => r.ToSeq()));


        return getNetworkAdapters(vmInfo)
            .Bind(getPortNames)
            .Bind(portNames => change == VMPortChange.Add
                ? AddPorts(vmInfo.Value.Id, portNames).ToAsync()
                : RemovePorts(portNames).ToAsync());

    }

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
        var ovsControl = new OVSControl(_sysEnvironment);

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
                                _log.LogDebug("Port {portName} not found. Error: {message}",
                                    portName, left.Message);

                                return ovsControl.AddPortWithIFaceId("br-int", portName, cancelToken.Token)
                                    .Bind(_ => ovsControl.GetInterface(portName));

                            }
                        ).Bind(inf =>
                        {
                            if (inf.LinkState.FirstOrDefault() == "up") return true;
                            _log.LogDebug("Interface on port {portName} is not up. OVS Error state: {ovsError}",
                                portName,
                                string.Join(',', inf.Error));
                            return ovsControl.RemovePort("br-int", portName, cancelToken.Token).Map(_ => false);

                        }).Match(
                            r => r,
                            l =>
                            {
                                _log.LogDebug(
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
        var ovsControl = new OVSControl(_sysEnvironment);

        var cancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        foreach (var portName in portNames)
        {
            await ovsControl.GetInterface(portName)
                .Bind(_ =>
                {
                    _log.LogDebug("Interface on port {portName} found. Removing port",
                        portName);
                    return ovsControl.RemovePort("br-int", portName, cancelToken.Token);

                }).IfLeft(
                    l =>
                    {
                        _log.LogDebug(
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
        return _engine.GetObjectsAsync<T>(new PsCommandBuilder()
            .AddCommand("Get-VM").AddParameter("Id", vmId)).ToError().ToAsync();
    }
}