using System.Linq;
using AutoMapper;
using Haipa.Modules.Identity.Models.V1;
using IdentityServer4.Models;

namespace Haipa.Modules.Identity.Services
{
    public class ClientApiMapperProfile : Profile
    {
        public ClientApiMapperProfile()
        {
            CreateMap<Client, ClientApiModel>()
                .ForMember(x => x.X509CertificateBase64,
                    c => c.MapFrom(x => x.ClientSecrets.FirstOrDefault().Value))
                .ReverseMap();

        }
    }
}