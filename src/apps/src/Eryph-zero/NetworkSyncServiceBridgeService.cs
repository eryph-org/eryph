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
