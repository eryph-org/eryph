using System.Linq;
using AutoMapper;
using Haipa.Modules.Identity.Models;
using Haipa.Modules.Identity.Models.V1;
using IdentityServer4.Models;
using Client = Haipa.Modules.Identity.Models.V1.Client;

namespace Haipa.Modules.Identity.Services
{
    public class ClientApiMapperProfile : Profile
    {
        public ClientApiMapperProfile()
        {
            CreateMap<IdentityServer4.Models.Client, Client>()
                .ForMember(x => x.Id, opt => opt.MapFrom(x => x.ClientId))
                .ForMember(x => x.Name, opt => opt.MapFrom(x => x.ClientName))
                .ForMember(x => ((IClientApiModel) x).Certificate,
                    c => c.MapFrom(x => x.ClientSecrets.FirstOrDefault().Value))
                .ReverseMap();

        }
    }
}