using System;
using System.Linq;
using AutoMapper;
using Eryph.Core;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Model;

namespace Eryph.Modules.ComputeApi.Model.V1
{
    public class MapperProfile : Profile
    {

        public MapperProfile()
        {
            string userRole = null;

            CreateMap<StateDb.Model.ReportedNetwork, CatletNetwork>();

            CreateMap<StateDb.Model.VirtualNetwork, VirtualNetwork>()
            .ForMember(x => x.ProviderName,
                x => x.MapFrom(y => y.NetworkProvider))
            .ForMember(x => x.ProjectId, x => x.MapFrom(y => y.ProjectId))
            .ForMember(x => x.TenantId, x => x
                .MapFrom(y => y.Project.TenantId));

            

            CreateMap<StateDb.Model.Catlet, Catlet>().ForMember(x => x.Networks, m =>
            {
                m.MapAtRuntime();
                /*
                m.MapFrom((catlet,_) =>
                {
                    return catlet.NetworkPorts.Map(port =>
                    {
                        var ipV4Addresses = port.IpAssignments?.Map(assignment => assignment.IpAddress) 
                                            ?? Array.Empty<string>();
                        var routerIp = port.Network.RouterPort.IpAssignments?.FirstOrDefault()?.IpAddress;
                        var subnets = port.IpAssignments?.Map(x => x.Subnet.IpNetwork) ?? Array.Empty<string>();
                        var dnsServers = port.IpAssignments?.Map(x => x.Subnet).Cast<VirtualNetworkSubnet>()
                            .Map(x => x.DnsServersV4) ?? Array.Empty<string>();

                        var reportedNetwork = catlet.ReportedNetworks.FirstOrDefault(x => 
                            x.IpV4Addresses.SequenceEqual(ipV4Addresses));

                        FloatingNetworkPort floatingPort = null;
                        if (port.FloatingPort != null)
                        {
                            var floatingPortIp = port.FloatingPort.IpAssignments?.Map(x=>x.IpAddress);
                            floatingPort = new FloatingNetworkPort()
                            {
                                Name = port.FloatingPort.Name,
                                Subnet = port.FloatingPort.SubnetName,
                                Provider = port.FloatingPort.ProviderName,
                                IpV4Addresses = floatingPortIp,
                                IpV4Subnets = port.FloatingPort.IpAssignments?.Map(x => x.Subnet).Map(x=>x.IpNetwork)
                            };
                        }

                        return new CatletNetwork
                        {
                            Name = port.Network.Name,
                            Provider = port.Network.NetworkProvider,
                            IpV4Addresses = reportedNetwork?.IpV4Addresses ?? ipV4Addresses,
                            //IpV6Addresses = reportedNetwork?.IpV6Addresses ?? Enumerable.Empty<string>(),
                            IPv4DefaultGateway = reportedNetwork?.IPv4DefaultGateway ?? routerIp,
                            //IPv6DefaultGateway = reportedNetwork?.IPv6DefaultGateway,
                            IpV4Subnets = reportedNetwork?.IpV4Subnets ?? subnets,
                            //IpV6Subnets = reportedNetwork?.IpV6Subnets ?? Enumerable.Empty<string>(),
                            DnsServerAddresses = reportedNetwork?.DnsServerAddresses ?? dnsServers,
                            FloatingPort = floatingPort
                        };
                    });


                });
                */
            });
            CreateMap<StateDb.Model.CatletDrive, CatletDrive>();
            CreateMap<StateDb.Model.CatletNetworkAdapter, CatletNetworkAdapter>();
            CreateMap<StateDb.Model.VirtualDisk, VirtualDisk>().ForMember(x => x.Path,
                o => { o.MapFrom(s => userRole == "Admin" ? s.Path : null); });

            var memberMap = CreateMap<ProjectRoleAssignment, ProjectMemberRole>();
            memberMap
                .ForMember(x => x.MemberId,
                    o =>
                        o.MapFrom(dest => dest.IdentityId));

            memberMap.ForMember(x => x.ProjectName,
                    o => 
                        o.MapFrom(s => s.Project.Name));

            memberMap.ForMember(x => x.RoleName,
                    o =>
                        o.MapFrom((src,m) =>
                        {
                            if(src.RoleId == EryphConstants.BuildInRoles.Owner)
                                return "owner";
                            if (src.RoleId == EryphConstants.BuildInRoles.Contributor)
                                return "contributor";
                            if (src.RoleId == EryphConstants.BuildInRoles.Reader)
                                return "reader";

                            return "";
                        }));

        }
    }
}