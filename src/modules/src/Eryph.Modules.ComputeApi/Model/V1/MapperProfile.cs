using System;
using System.Linq;
using AutoMapper;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Model;

namespace Eryph.Modules.ComputeApi.Model.V1
{
    public class MapperProfile : Profile
    {

        public MapperProfile()
        {
            CreateMap<StateDb.Model.ReportedNetwork, CatletNetwork>();

            CreateMap<StateDb.Model.VirtualNetwork, VirtualNetwork>()
            .ForMember(x => x.ProviderName,
                x => x.MapFrom(y => y.NetworkProvider))
            .ForMember(x => x.ProjectId, x => x.MapFrom(y => y.ProjectId))
            .ForMember(x => x.TenantId, x => x
                .MapFrom(y => y.Project.TenantId));

            CreateMap<StateDb.Model.Catlet, Catlet>();
            CreateMap<StateDb.Model.CatletDrive, CatletDrive>();
            CreateMap<StateDb.Model.CatletNetworkAdapter, CatletNetworkAdapter>();

            CreateMap<(StateDb.Model.Catlet Catlet, CatletNetworkPort Port), CatletNetwork>()
                .ConvertUsing((src, target) =>
                {
                    target ??= new CatletNetwork();
                    var ipV4Addresses = src.Port.IpAssignments?.Map(assignment => assignment.IpAddress)
                        ?? Array.Empty<string>();
                    var routerIp = src.Port.Network.RouterPort.IpAssignments?.FirstOrDefault()?.IpAddress;
                    var subnets = src.Port.IpAssignments?.Map(x => x.Subnet.IpNetwork) ?? Array.Empty<string>();
                    var dnsServers = src.Port.IpAssignments?.Map(x => x.Subnet).Cast<VirtualNetworkSubnet>()
                        .Map(x => x.DnsServersV4) ?? Array.Empty<string>();

                    var reportedNetwork = src.Catlet.ReportedNetworks.FirstOrDefault(x =>
                        x.IpV4Addresses.SequenceEqual(ipV4Addresses));

                    FloatingNetworkPort floatingPort = null;
                    if (src.Port.FloatingPort != null)
                    {
                        var floatingPortIp = src.Port.FloatingPort.IpAssignments?.Map(x => x.IpAddress);
                        floatingPort = new FloatingNetworkPort()
                        {
                            Name = src.Port.FloatingPort.Name,
                            Subnet = src.Port.FloatingPort.SubnetName,
                            Provider = src.Port.FloatingPort.ProviderName,
                            IpV4Addresses = floatingPortIp,
                            IpV4Subnets = src.Port.FloatingPort.IpAssignments?.Map(x => x.Subnet).Map(x => x.IpNetwork)
                        };
                    }

                    target.Name = src.Port.Network.Name;
                    target.Provider = src.Port.Network.NetworkProvider;
                    target.IpV4Addresses = reportedNetwork?.IpV4Addresses ?? ipV4Addresses;
                    //IpV6Addresses = reportedNetwork?.IpV6Addresses ?? Enumerable.Empty<string>(),
                    target.IPv4DefaultGateway = reportedNetwork?.IPv4DefaultGateway ?? routerIp;
                    //IPv6DefaultGateway = reportedNetwork?.IPv6DefaultGateway,
                    target.IpV4Subnets = reportedNetwork?.IpV4Subnets ?? subnets;
                    //IpV6Subnets = reportedNetwork?.IpV6Subnets ?? Enumerable.Empty<string>(),
                    target.DnsServerAddresses = reportedNetwork?.DnsServerAddresses ?? dnsServers;
                    target.FloatingPort = floatingPort;

                    return target;
                });

            var memberMap = CreateMap<ProjectRoleAssignment, ProjectMemberRole>();
            memberMap.ForMember(x => x.MemberId, o => o.MapFrom(dest => dest.IdentityId));
            memberMap.ForMember(x => x.ProjectName, o => o.MapFrom(s => s.Project.Name));
            memberMap.ForMember(x => x.RoleName,
                o => o.MapFrom((src,m) => RoleNames.GetRoleName(src.RoleId)));

            CreateMap<StateDb.Model.VirtualDisk, VirtualDisk>()
                .ForMember(d => d.Project, o => o.MapFrom(s => s.Project.Name))
                .ForMember(x => x.Path, o => o.MapFrom((s, _, _, context) =>
                {
                    var authContext = context.GetAuthContext();
                    var isSuperAdmin = authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole);
                    return isSuperAdmin ? s.Path : null;
                }))
                .ForMember(d => d.Location, o => o.MapFrom(s => s.StorageIdentifier));

            CreateMap<StateDb.Model.Gene, Gene>()
                .ForMember(x => x.Name, o => o.MapFrom(s => new GeneIdentifier(
                    new GeneSetIdentifier(
                        OrganizationName.New(s.GeneSet.Organization),
                        GeneSetName.New(s.GeneSet.Name),
                        TagName.New(s.GeneSet.Tag)),
                    GeneName.New(s.Name)).Value));
            CreateMap<StateDb.Model.GeneSet, GeneSet>();
        }
    }
}