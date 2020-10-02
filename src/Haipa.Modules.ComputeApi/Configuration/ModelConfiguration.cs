
using Haipa.StateDb.Model;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Mvc;

namespace Haipa.Modules.Api.Configuration
{
    public class ODataModelConfiguration : IModelConfiguration
    {
        private static void ConfigureV1( ODataModelBuilder builder )
        {
            builder.Namespace = "Haipa";

            builder.EntitySet<Operation>("Operations");
            builder.EntitySet<OperationLogEntry>("OperationLogs");
            builder.EntitySet<OperationTask>("OperationTaks");
            builder.EntitySet<Machine>("Machines");
            builder.EntitySet<Agent>("Agents");
            builder.EntitySet<Network>("Networks");
            //builder.EntitySet<Subnet>("Subnets");

            //builder.EntityType<Network>().HasOptional(np => np.Subnets);
            //builder.EntityType<Network>().HasOptional(np => np.AgentNetworks);

            builder.EntityType<Subnet>().HasRequired(np => np.Network);
            builder.EntityType<Subnet>().Ignore(x => x.DnsServersInternal);
            builder.EntityType<Subnet>().CollectionProperty(x => x.DnsServerAddresses);
            
            builder.EntityType<Agent>().HasKey(t => t.Name);

            builder.EntityType<Machine>().HasRequired(np => np.Agent);
            builder.EntityType<Machine>().HasOptional(x => x.VM);
            builder.EntityType<Machine>().HasMany(x => x.Networks);

            builder.EntitySet<MachineNetwork>("MachineNetworks");
            builder.EntityType<MachineNetwork>().HasKey(x => x.MachineId).HasKey(x => x.AdapterName);
            builder.EntityType<MachineNetwork>().Ignore(x=>x.IpV4AddressesInternal);
            builder.EntityType<MachineNetwork>().Ignore(x => x.IpV6AddressesInternal);
            builder.EntityType<MachineNetwork>().CollectionProperty(x => x.IpV6Addresses);
            builder.EntityType<MachineNetwork>().CollectionProperty(x => x.IpV4Addresses);
            builder.EntityType<MachineNetwork>().Ignore(x=>x.IpV4SubnetsInternal);
            builder.EntityType<MachineNetwork>().Ignore(x => x.IpV6SubnetsInternal);
            builder.EntityType<MachineNetwork>().CollectionProperty(x => x.IpV4Subnets);
            builder.EntityType<MachineNetwork>().CollectionProperty(x => x.IpV6Subnets);
            builder.EntityType<MachineNetwork>().Ignore(x=>x.DnsServersInternal);
            builder.EntityType<MachineNetwork>().CollectionProperty(x => x.DnsServerAddresses);


            builder.EntityType<VirtualMachine>().HasMany(x => x.NetworkAdapters);

            builder.EntitySet<VirtualMachineNetworkAdapter>("VirtualMachineNetworkAdapters");
            builder.EntityType<VirtualMachineNetworkAdapter>().HasKey(x => x.MachineId).HasKey(x => x.Name);


            builder.EntityType<Machine>().Action("Start").ReturnsFromEntitySet<Operation>("Operations");
            builder.EntityType<Machine>().Action("Stop").ReturnsFromEntitySet<Operation>("Operations");

        }

        public void Apply( ODataModelBuilder builder, ApiVersion apiVersion )
        {
            switch ( apiVersion.MajorVersion )
            {
                default:
                    ConfigureV1( builder );
                    break;
            }
        }
    }
}
