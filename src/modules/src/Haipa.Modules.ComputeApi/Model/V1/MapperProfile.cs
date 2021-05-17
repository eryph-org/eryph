using AutoMapper;

namespace Haipa.Modules.ComputeApi.Model.V1
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            string userRole = null;

            CreateMap<StateDb.Model.MachineNetwork, MachineNetwork>();

            CreateMap<StateDb.Model.Machine, Machine>();
            CreateMap<StateDb.Model.VirtualMachine, VirtualMachine>();
            CreateMap<StateDb.Model.VirtualMachineDrive, VirtualMachineDrive>();
            CreateMap<StateDb.Model.VirtualMachineNetworkAdapter, VirtualMachineNetworkAdapter>();
            CreateMap<StateDb.Model.VirtualDisk, VirtualDisk>().ForMember(x => x.Path,
                o => { o.MapFrom(s => userRole == "Admin" ? s.Path : null); });
        }
    }
}