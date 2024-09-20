using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.VmHostAgent;
using Eryph.Runtime.Zero.Configuration.Clients;
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
        container.RegisterSingleton(serviceProvider.GetRequiredService<IEryphOvsPathProvider>);
        container.RegisterSingleton(serviceProvider.GetRequiredService<IHostSettingsProvider>);
        container.RegisterSingleton(serviceProvider.GetRequiredService<INetworkProviderManager>);
        container.RegisterSingleton(serviceProvider.GetRequiredService<IVmHostAgentConfigurationManager>);

        container.Register(serviceProvider.GetRequiredService<IConfigWriterService<ClientConfigModel>>);
        container.Register(serviceProvider.GetRequiredService<IConfigReaderService<ClientConfigModel>>);
        container.RegisterSingleton<ICertificateGenerator, CertificateGenerator>();
        container.RegisterSingleton<ICertificateKeyService, WindowsCertificateKeyService>();
        container.RegisterSingleton<ICertificateStoreService, WindowsCertificateStoreService>();
        container.RegisterSingleton<ICryptoIOServices, WindowsCryptoIOServices>();
        container.RegisterSingleton<ISslEndpointManager, SslEndpointManager>();
        container.RegisterSingleton<ISslEndpointRegistry, WinHttpSslEndpointRegistry>();
        container.Register<ISystemClientGenerator, SystemClientGenerator>();
    }

    [UsedImplicitly]
    public void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.AddLogging();
        // This handler must be executed first as it ensures that Hyper-V is
        // available and responds to WMI queries. Otherwise, other code can
        // fail during service start after a reboot.
        options.AddStartupHandler<EnsureHyperVAndOvsStartupHandler>();
        options.AddStartupHandler<EnsureConfigurationStartupHandler>();
        options.AddStartupHandler<SystemClientStartupHandler>();
        options.AddHostedService<SslEndpointService>();
    }
}
