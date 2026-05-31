using System.Threading;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.Runtime.Zero;

/// <summary>
/// ACCEPTED WORKAROUND (not valid for real production split use). This bridges the
/// host-agent's named-pipe REBUILD_NETWORKS/VALIDATE_CHANGES to the controller by reaching
/// directly into the controller module's container — an in-process shortcut that only works
/// because eryph-zero co-hosts both modules. It was intentionally NOT unified onto the bus:
/// the network-sync flow legitimately differs per packaging (eryph-zero is admin-less /
/// command-based auto network build; the split runtime needs a different flow), so a single
/// bus contract is premature. The standalone agent uses <c>UnavailableNetworkSyncService</c>
/// instead. Revisit when the split-runtime network-sync flow is designed (see the related
/// AgentControlService cross-wire in NetworkModule, the same accepted-workaround category).
/// </summary>
internal class NetworkSyncServiceBridgeService : INetworkSyncService
{
    private readonly IModuleHost<ControllerModule> _controllerModule;

    public NetworkSyncServiceBridgeService(IModuleHost<ControllerModule> controllerModule)
    {
        _controllerModule = controllerModule;
    }

    public EitherAsync<Error, Unit> SyncNetworks(CancellationToken cancellationToken)
    {
        return _controllerModule.Services.GetRequiredService<Container>()
            .GetInstance<INetworkSyncService>()
            .SyncNetworks(cancellationToken);
    }

    public EitherAsync<Error, string[]> ValidateChanges(NetworkProvider[] networkProviders)
    {
        return _controllerModule.Services.GetRequiredService<Container>()
            .GetInstance<INetworkSyncService>()
            .ValidateChanges(networkProviders);
    }
}
