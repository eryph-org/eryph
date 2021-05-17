using AutoMapper;

namespace Haipa.Modules.Identity.Services
{
    public static class ClientApiMapper
    {
        private static readonly MapperConfiguration MapperConfiguration =
            new MapperConfiguration(cfg => cfg.AddProfile<ClientApiMapperProfile>());

        public static readonly IMapper Mapper = MapperConfiguration.CreateMapper();
    }
}