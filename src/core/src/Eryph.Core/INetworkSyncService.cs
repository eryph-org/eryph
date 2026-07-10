using System.Threading;
using Eryph.Core.Network;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core;

public interface INetworkSyncService
{
    public EitherAsync<Error, Unit> SyncNetworks(CancellationToken cancellationToken);

    /// <summary>
    /// Realizes and re-distributes the given network provider configuration instead of re-reading it
    /// from the provider manager. Used by the authoring path, where the just-authored value is not yet
    /// visible to a fresh read (it is written in the still-open authoring unit of work), so realizing
    /// against a re-read would apply the previous configuration.
    /// </summary>
    public EitherAsync<Error, Unit> SyncNetworks(
        NetworkProvidersConfiguration providerConfig, CancellationToken cancellationToken);

    public EitherAsync<Error, string[]> ValidateChanges(NetworkProvider[] networkProviders);
}
