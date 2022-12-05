using AutoMapper;

namespace Eryph.Modules.ComputeApi.Model.V1
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            string userRole = null;

            CreateMap<StateDb.Model.VirtualNetwork, CatletNetwork>();

            CreateMap<StateDb.Model.Catlet, Catlet>();
            CreateMap<StateDb.Model.VirtualCatlet, VirtualCatlet>();
            CreateMap<StateDb.Model.VirtualMachineDrive, VirtualCatletDrive>();
            CreateMap<StateDb.Model.VirtualCatletNetworkAdapter, VirtualCatletNetworkAdapter>();
            CreateMap<StateDb.Model.VirtualDisk, VirtualDisk>().ForMember(x => x.Path,
                o => { o.MapFrom(s => userRole == "Admin" ? s.Path : null); });
        }
    }
}