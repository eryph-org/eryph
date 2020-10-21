using Haipa.StateDb.Model;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Mvc;

namespace Haipa.Modules.ComputeApi.Configuration
{
    public class ODataModelConfiguration : IModelConfiguration
    {
        private static void ConfigureV1( ODataModelBuilder builder )
        {
            builder.Namespace = "Haipa";

            builder.EntitySet<Machine>("Machines");

            builder.EntitySet<Agent>("Agents");
            builder.EntitySet<Network>("Networks");
            builder.EntitySet<VirtualDisk>("VirtualDisks");

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
            builder.EntityType<MachineNetwork>().HasKey(x=> x.Id);
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

            builder.EntitySet<VirtualMachine>("VirtualMachines");
            builder.EntityType<VirtualMachine>().HasKey(x=>x.Id);
            builder.EntityType<VirtualMachine>().HasRequired(x => x.Machine);

            builder.EntityType<VirtualMachine>().ContainsMany(x => x.Drives);
            builder.EntityType<VirtualMachine>().ContainsMany(x => x.NetworkAdapters);

            builder.EntityType<VirtualDisk>().ContainsMany(x => x.AttachedDrives);

            //builder.EntitySet<VirtualMachineNetworkAdapter>("VirtualMachineNetworkAdapters");
            //builder.EntityType<VirtualMachineNetworkAdapter>().HasKey(x => x.Id);

            //builder.EntitySet<VirtualMachineDrive>("VirtualMachineDrives");
            //builder.EntityType<VirtualMachineDrive>().HasKey(x => x.Id);
            //builder.EntityType<VirtualMachineDrive>().HasOptional(x => x.AttachedDisk);

            builder.EntitySet<VirtualDisk>("VirtualDisks");
            builder.EntityType<VirtualDisk>().HasKey(x => x.Id);
            builder.EntityType<VirtualDisk>().HasOptional(x => x.Parent);
            builder.EntityType<VirtualDisk>().HasMany(x => x.Childs);
            builder.EntityType<VirtualDisk>().ContainsMany(x=>x.AttachedDrives);

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
