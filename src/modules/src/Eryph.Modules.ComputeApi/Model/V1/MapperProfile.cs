using AutoMapper;

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
                .MapFrom(y => y.Project.TenantId))
            ;

            CreateMap<StateDb.Model.Catlet, Catlet>();
            CreateMap<StateDb.Model.CatletDrive, CatletDrive>();
            CreateMap<StateDb.Model.CatletNetworkAdapter, CatletNetworkAdapter>();
            CreateMap<StateDb.Model.VirtualDisk, VirtualDisk>().ForMember(x => x.Path,
                o => { o.MapFrom(s => userRole == "Admin" ? s.Path : null); });

            CreateMap<StateDb.Model.Catlet, Catlet>()
                .ForMember(x => x.Networks, m => m.MapFrom(y => y.ReportedNetworks));

        }
    }
}