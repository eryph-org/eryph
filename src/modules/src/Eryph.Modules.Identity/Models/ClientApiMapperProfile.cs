using System.Linq;
using AutoMapper;
using Dbosoft.IdentityServer.Storage.Models;

namespace Eryph.Modules.Identity.Models
{
    public class ClientApiMapperProfile : Profile
    {
        public ClientApiMapperProfile()
        {
            CreateMap<Client, Models.V1.Client>()
                .ForMember(x => x.Id, opt => opt.MapFrom(x => x.ClientId))
                .ForMember(x => x.Name, opt => opt.MapFrom(x => x.ClientName))
                .ForMember(x => ((IClientApiModel) x).Certificate,
                    c => c.MapFrom(x => x.ClientSecrets.FirstOrDefault().Value))
                .ReverseMap();
        }
    }
}