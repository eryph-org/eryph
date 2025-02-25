using System;
using System.Threading;
using Dbosoft.OVN.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

public interface IOVSControl
{
    EitherAsync<Error, Unit> AddBridge(
        string bridgeName,
        CancellationToken cancellationToken);

    EitherAsync<Error, Unit> RemoveBridge(
        string bridgeName,
        CancellationToken cancellationToken);

    EitherAsync<Error, Unit> AddPort(
        string bridgeName,
        OvsInterfaceUpdate interfaceUpdate,
        CancellationToken cancellationToken);

    EitherAsync<Error, Unit> RemovePort(
        string bridgeName,
        string portName,
        CancellationToken cancellationToken);

    EitherAsync<Error, Unit> AddBond(
        string bridgeName,
        string portName,
        Seq<OvsInterfaceUpdate> interfaceUpdates,
        string bondMode,
        CancellationToken cancellationToken);

    EitherAsync<Error, Seq<OvsBridge>> GetBridges(CancellationToken cancellationToken);

    EitherAsync<Error, Seq<OvsBridgePort>> GetPorts(CancellationToken cancellationToken);

    EitherAsync<Error, Seq<OvsInterface>> GetInterfaces(CancellationToken cancellationToken);

    EitherAsync<Error, OVSTableRecord> GetOVSTable(CancellationToken cancellationToken);

    EitherAsync<Error, Unit> UpdateBridgeMapping(
        string bridgeMappings,
        CancellationToken cancellationToken);
    
    EitherAsync<Error, Unit> UpdateBridgePort(
        string bridgeName,
        Option<int> tag,
        Option<string> vlanMode, 
        CancellationToken cancellationToken);
    
    EitherAsync<Error, Unit> UpdateBondPort(
        string portName,
        string bondMode,
        CancellationToken cancellationToken);
}
