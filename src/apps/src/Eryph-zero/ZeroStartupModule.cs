using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Runtime.Zero.Configuration.Clients;
using Eryph.Runtime.Zero.HttpSys;
using Eryph.Security.Cryptography;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Runtime.Zero;

public class ZeroStartupModule
{
    [UsedImplicitly]
    public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
    {
        container.Register(serviceProvider.GetRequiredService<IEryphOvsPathProvider>);
        container.Register<ICertificateGenerator, CertificateGenerator>();
        container.Register<ICertificateStoreService, WindowsCertificateStoreService>();
        container.Register<ICryptoIOServices, WindowsCryptoIOServices>();
        container.Register<IRSAProvider, RSAProvider>();
        container.Register<ISSLEndpointManager, SSLEndpointManager>();
        container.Register<ISSLEndpointRegistry, WinHttpSSLEndpointRegistry>();
        container.Register<ISystemClientGenerator, SystemClientGenerator>();
    }

    [UsedImplicitly]
    public void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.AddHostedService<ZeroStartupService>();
        options.AddLogging();
    }
}
