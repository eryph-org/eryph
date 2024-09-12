using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Runtime.Zero.HttpSys;
using JetBrains.Annotations;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Runtime.Zero;

public class ZeroStartupModule
{
    [UsedImplicitly]
    public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
    {
        container.Register<ISSLEndpointManager, SSLEndpointManager>(Lifestyle.Singleton);
    }

    [UsedImplicitly]
    public void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.AddHostedService<ZeroStartupService>();
        options.AddLogging();
    }
}
