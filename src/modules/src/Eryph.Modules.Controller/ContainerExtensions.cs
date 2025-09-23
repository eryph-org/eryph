using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.Networks;
using SimpleInjector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller;

public static class ContainerExtensions
{
    public static void AddStateDbDataServices(
        this Container container)
    {
        container.Register<IVirtualMachineDataService, VirtualMachineDataService>(Lifestyle.Scoped);
        container.Register<IVirtualMachineMetadataService, VirtualMachineMetadataService>(Lifestyle.Scoped);
        container.Register<IVMHostMachineDataService, VMHostMachineDataService>(Lifestyle.Scoped);

        container.Register<ICatletIpManager, CatletIpManager>(Lifestyle.Scoped);
        container.Register<IProviderIpManager, ProviderIpManager>(Lifestyle.Scoped);
        container.Register<IIpPoolManager, IpPoolManager>(Lifestyle.Scoped);
        container.Register<INetworkConfigValidator, NetworkConfigValidator>(Lifestyle.Scoped);
        container.Register<INetworkConfigRealizer, NetworkConfigRealizer>(Lifestyle.Scoped);
        container.Register<IDefaultNetworkConfigRealizer, DefaultNetworkConfigRealizer>(Lifestyle.Scoped);
        container.Register<INetworkProvidersConfigRealizer, NetworkProvidersConfigRealizer>(Lifestyle.Scoped);
    }
}
