using System;
using System.IO;
using System.Linq;
using AutoMapper;
using Eryph.Core;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Model;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi.Model.V1
{
    public class MapperProfile : Profile
    {

        public MapperProfile()
        {
            CreateMap<StateDb.Model.ReportedNetwork, CatletNetwork>();

            CreateMap<StateDb.Model.VirtualNetwork, VirtualNetwork>()
                .ForMember(x => x.ProviderName, x => x.MapFrom(y => y.NetworkProvider));

            CreateMap<StateDb.Model.Catlet, Catlet>();
            CreateMap<StateDb.Model.CatletDrive, CatletDrive>()
                .ForMember(
                    d => d.AttachedDiskId,
                    o => o.MapFrom(drive => Optional(drive.AttachedDisk)
                        .Filter(disk => !disk.Deleted)
                        .Map(disk => disk.Id)
                        .ToNullable()));
            CreateMap<StateDb.Model.CatletNetworkAdapter, CatletNetworkAdapter>();

            CreateMap<(StateDb.Model.Catlet Catlet, CatletNetworkPort Port), CatletNetwork>()
                .ConstructUsing((src, _) =>
                {
                    var ipV4Addresses = src.Port.IpAssignments.Map(assignment => assignment.IpAddress!).ToList();
                    var routerIp = src.Port.Network.RouterPort?.IpAssignments.FirstOrDefault()?.IpAddress;
                    var subnets = src.Port.IpAssignments.Map(x => x.Subnet!.IpNetwork!).ToList();
                    var dnsServers = src.Port.IpAssignments.Map(x => x.Subnet)
                        .Cast<VirtualNetworkSubnet>()
                        .Map(x => x.DnsServersV4!).ToList();

                    var reportedNetworks = src.Catlet.ReportedNetworks.ToSeq();
                    // Try to find the reported network by the MAC address as a fallback.
                    // This is necessary for backwards compatibility with old port names.
                    var reportedNetwork = reportedNetworks.Find(x => x.PortName == src.Port.OvsName)
                                          | reportedNetworks.Find(x => x.MacAddress == src.Port.MacAddress);

                    // When no IP addresses have been reported at all, we assume that the catlet
                    // is not running. Hence, we return the configured network settings instead.
                    var isIpReported = reportedNetwork.Map(n => n.IpV4Addresses.Count > 0).IfNone(false);

                    FloatingNetworkPort? floatingPort = null;
                    if (src.Port.FloatingPort != null)
                    {
                        var floatingPortIp = src.Port.FloatingPort.IpAssignments.Map(x => x.IpAddress!);
                        floatingPort = new FloatingNetworkPort()
                        {
                            Name = src.Port.FloatingPort.Name,
                            Subnet = src.Port.FloatingPort.SubnetName,
                            Provider = src.Port.FloatingPort.ProviderName!,
                            IpV4Addresses = floatingPortIp.ToList(),
                            IpV4Subnets = src.Port.FloatingPort.IpAssignments.Map(x => x.Subnet!).Map(x => x.IpNetwork!).ToList(),
                        };
                    }

                    return new CatletNetwork
                    {
                        Name = src.Port.Network.Name,
                        Provider = src.Port.Network.NetworkProvider,
                        IpV4Addresses = isIpReported
                            ? reportedNetwork.ToSeq().SelectMany(n => n.IpV4Addresses).ToList()
                            : ipV4Addresses,
                        IPv4DefaultGateway = isIpReported
                            ? reportedNetwork.Bind(n => Optional(n.IPv4DefaultGateway)).IfNoneUnsafe((string?)null)
                            : routerIp,
                        IpV4Subnets = isIpReported
                            ? reportedNetwork.ToSeq().SelectMany(n => n.IpV4Subnets).ToList()
                            : subnets,
                        DnsServerAddresses = isIpReported
                            ? reportedNetwork.ToSeq().SelectMany(n => n.DnsServerAddresses).ToList()
                            : dnsServers,
                        FloatingPort = floatingPort,
                    };
                });

            var memberMap = CreateMap<ProjectRoleAssignment, ProjectMemberRole>();
            memberMap.ForMember(x => x.MemberId, o => o.MapFrom(dest => dest.IdentityId));
            memberMap.ForMember(x => x.RoleName,
                o => o.MapFrom((src, _) => RoleNames.GetRoleName(src.RoleId)));

            CreateMap<StateDb.Model.VirtualDisk, VirtualDisk>()
                .ForMember(x => x.Path, o => o.MapFrom((s, _, _, context) =>
                {
                    var authContext = context.GetAuthContext();
                    var isSuperAdmin = authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole);
                    return isSuperAdmin && !string.IsNullOrEmpty(s.Path) && !string.IsNullOrEmpty(s.FileName)
                        ? Path.Combine(s.Path, s.FileName)
                        : null;
                }))
                .ForMember(x => x.ParentPath, o => o.MapFrom((s, _, _, context) =>
                {
                    var authContext = context.GetAuthContext();
                    var isSuperAdmin = authContext.IdentityRoles.Contains(EryphConstants.SuperAdminRole);
                    return isSuperAdmin ? s.ParentPath : null;
                }))
                .ForMember(d => d.Location, o => o.MapFrom(s => s.StorageIdentifier))
                .ForMember(d => d.AttachedCatlets, o => o.MapFrom(s => s.AttachedDrives))
                .ForMember(
                    d => d.Gene,
                    o =>
                    {
                        o.PreCondition(s => s is { GeneSet: not null, GeneName: not null, GeneArchitecture: not null });
                        o.MapFrom(s => new VirtualDiskGeneInfo()
                        {
                            GeneSet = s.GeneSet!,
                            Name = s.GeneName!,
                            Architecture = s.GeneArchitecture!,
                        });
                    });
            CreateMap<StateDb.Model.CatletDrive, VirtualDiskAttachedCatlet>();

            CreateMap<StateDb.Model.Gene, Gene>()
                .Include<StateDb.Model.Gene, GeneWithUsage>();
            CreateMap<StateDb.Model.Gene, GeneWithUsage>();
        }
    }
}