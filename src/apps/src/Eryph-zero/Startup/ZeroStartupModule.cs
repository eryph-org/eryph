using System;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.ModuleCore.Startup;
using Eryph.Runtime.Zero.HttpSys;
using Eryph.Security.Cryptography;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Runtime.Zero.Startup;

/// <summary>
/// This module performs some necessary startup actions for eryph-zero.
/// We use the module with <see cref="IStartupHandler"/>s and
/// <see cref="Microsoft.Extensions.Hosting.IHostedService"/>s to avoid
/// timeouts when eryph-zero starts as a Windows service.
/// </summary>
public class ZeroStartupModule
{
    [UsedImplicitly]
    public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
    {
        container.RegisterSingleton(serviceProvider.GetRequiredService<IEryphOvnPathProvider>);
        container.RegisterSingleton(serviceProvider.GetRequiredService<IHostSettingsProvider>);
        container.RegisterSingleton(serviceProvider.GetRequiredService<INetworkProviderManager>);
        container.RegisterSingleton(serviceProvider.GetRequiredService<IControllerSettingsManager>);
        container.RegisterSingleton(serviceProvider.GetRequiredService<IVmHostAgentConfigurationManager>);

        container.RegisterSingleton(serviceProvider.GetRequiredService<ICertificateGenerator>);
        container.RegisterSingleton(serviceProvider.GetRequiredService<ICertificateKeyService>);
        container.RegisterSingleton(serviceProvider.GetRequiredService<ICertificateStoreService>);
        container.RegisterSingleton<ISslEndpointManager, SslEndpointManager>();
        container.RegisterSingleton<ISslEndpointRegistry, WinHttpSslEndpointRegistry>();
    }

    [UsedImplicitly]
    public void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.AddLogging();
        // This handler must be executed first as it ensures that Hyper-V is
        // available and responds to WMI queries. Otherwise, other code can
        // fail during service start after a reboot.
        options.AddStartupHandler<EnsureHyperVAndOvnStartupHandler>();
        options.AddStartupHandler<EnsureConfigurationStartupHandler>();
        options.AddHostedService<SslEndpointService>();
    }
}
