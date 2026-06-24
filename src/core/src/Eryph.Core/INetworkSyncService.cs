using System.Threading;
using Eryph.Core.Network;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core;

public interface INetworkSyncService
{
    public EitherAsync<Error, Unit> SyncNetworks(CancellationToken cancellationToken);

    public EitherAsync<Error, string[]> ValidateChanges(NetworkProvider[] networkProviders);
}
