using System.Threading;
using Dbosoft.OVN.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

public interface IOVSControl
{
    EitherAsync<Error, Unit> AddBridge(string bridgeName, CancellationToken cancellationToken = default);
    EitherAsync<Error, Unit> RemoveBridge(string bridgeName, CancellationToken cancellationToken = default);
    EitherAsync<Error, Unit> AddPort(string bridgeName, string portName, CancellationToken cancellationToken = default);
    EitherAsync<Error, Unit> RemovePort(string bridgeName, string portName, CancellationToken cancellationToken = default);
    EitherAsync<Error, Seq<Bridge>> GetBridges(CancellationToken cancellationToken = default);
    EitherAsync<Error, Seq<BridgePort>> GetPorts(CancellationToken cancellationToken = default);
    EitherAsync<Error, OVSTableRecord> GetOVSTable(CancellationToken cancellationToken = default);
    EitherAsync<Error, Unit> UpdateBridgeMapping(string bridgeMappings, CancellationToken cancellationToken);
}