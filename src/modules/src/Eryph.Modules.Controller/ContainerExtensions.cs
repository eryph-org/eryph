using Eryph.Modules.Controller.Components;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.Networks;
using SimpleInjector;

namespace Eryph.Modules.Controller;

public static class ContainerExtensions
{
    public static void AddStateDbDataServices(
        this Container container)
    {
        container.Register<ICatletDataService, CatletDataService>(Lifestyle.Scoped);
        container.Register<ICatletMetadataService, CatletMetadataService>(Lifestyle.Scoped);
        container.Register<IVMHostMachineDataService, VMHostMachineDataService>(Lifestyle.Scoped);

        container.Register<ICatletIpManager, CatletIpManager>(Lifestyle.Scoped);
        container.Register<IProviderIpManager, ProviderIpManager>(Lifestyle.Scoped);
        container.Register<IIpPoolManager, IpPoolManager>(Lifestyle.Scoped);
        container.Register<INetworkConfigValidator, NetworkConfigValidator>(Lifestyle.Scoped);
        container.Register<INetworkConfigRealizer, NetworkConfigRealizer>(Lifestyle.Scoped);
        container.Register<IDefaultNetworkConfigRealizer, DefaultNetworkConfigRealizer>(Lifestyle.Scoped);
        container.Register<INetworkProvidersConfigRealizer, NetworkProvidersConfigRealizer>(Lifestyle.Scoped);

        // Realizing a network resolves the site of its environment, and seeding realizes the catalog
        // that answers with. Both read the state database only — never the authored configuration —
        // so they belong to every container which gets the services above, including eryph-zero's
        // minimal warmup host. They are internal, so a host cannot register them itself.
        container.Register<ISiteResolver, SiteResolver>(Lifestyle.Scoped);
        container.Register<IEnvironmentsConfigRealizer, EnvironmentsConfigRealizer>(Lifestyle.Scoped);
    }
}
