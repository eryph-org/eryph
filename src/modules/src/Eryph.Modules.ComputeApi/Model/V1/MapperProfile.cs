using AutoMapper;

namespace Eryph.Modules.ComputeApi.Model.V1
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            string userRole = null;

            CreateMap<StateDb.Model.VirtualNetwork, CatletNetwork>();

            CreateMap<StateDb.Model.VirtualNetwork, VirtualNetwork>()
            .ForMember(x => x.ProviderName,
                x => x.MapFrom(y => y.NetworkProvider))
            .ForMember(x => x.ProjectId, x => x.MapFrom(y => y.ProjectId))
            .ForMember(x => x.TenantId, x => x
                .MapFrom(y => y.Project.TenantId))
            ;

            CreateMap<StateDb.Model.Catlet, Catlet>();
            CreateMap<StateDb.Model.VirtualCatlet, VirtualCatlet>();
            CreateMap<StateDb.Model.VirtualCatletDrive, VirtualCatletDrive>();
            CreateMap<StateDb.Model.VirtualCatletNetworkAdapter, VirtualCatletNetworkAdapter>();
            CreateMap<StateDb.Model.VirtualDisk, VirtualDisk>().ForMember(x => x.Path,
                o => { o.MapFrom(s => userRole == "Admin" ? s.Path : null); });
        }
    }
}