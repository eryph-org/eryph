using System.Threading;
using Dbosoft.OVN.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

public interface IOVSControl
{
    EitherAsync<Error, Unit> AddBridge(string bridgeName, CancellationToken cancellationToken);
    EitherAsync<Error, Unit> RemoveBridge(string bridgeName, CancellationToken cancellationToken);
    EitherAsync<Error, Unit> AddPort(string bridgeName, string portName, CancellationToken cancellationToken);
    EitherAsync<Error, Unit> RemovePort(string bridgeName, string portName, CancellationToken cancellationToken);
    EitherAsync<Error, Seq<Bridge>> GetBridges(CancellationToken cancellationToken);
    EitherAsync<Error, Seq<BridgePort>> GetPorts(CancellationToken cancellationToken);
    EitherAsync<Error, OVSTableRecord> GetOVSTable(CancellationToken cancellationToken);
    EitherAsync<Error, Unit> UpdateBridgeMapping(string bridgeMappings, CancellationToken cancellationToken);
    EitherAsync<Error, Unit> UpdateBridgePort(string bridgeName, int? tag, string? vlanMode,  CancellationToken cancellationToken);

}