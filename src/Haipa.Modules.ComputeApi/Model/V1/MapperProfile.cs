using AutoMapper;
using Haipa.StateDb.Model;

namespace Haipa.Modules.ComputeApi.Model.V1
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            string userRole = null;

            CreateMap<StateDb.Model.MachineNetwork, MachineNetwork>();

            CreateMap<StateDb.Model.Machine, Machine>();
            CreateMap<StateDb.Model.VirtualMachine, VirtualMachine>()
                .ForMember(x => x.Name, x => x.MapFrom(y => y.Machine.Name))
                .ForMember(x => x.Networks, x => x.MapFrom(y => y.Machine.Networks));
            CreateMap<StateDb.Model.VirtualMachineDrive, VirtualMachineDrive>();
            CreateMap<StateDb.Model.VirtualMachineNetworkAdapter, VirtualMachineNetworkAdapter>();
            CreateMap<StateDb.Model.VirtualDisk, VirtualDisk>().ForMember(x => x.Path, o =>
            {
                o.MapFrom(s=> userRole == "Admin" ? s.Path : null );
            });
        }
    }
}