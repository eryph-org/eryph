using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Haipa.Modules.Identity.Models;
using IdentityServer4;
using IdentityServer4.Models;
using JetBrains.Annotations;

namespace Haipa.Modules.Identity.Services
{
    public class ClientService : IClientService
    {
        private readonly IIdentityServerClientService _identityServerService;
        private readonly MapperConfiguration _mapperConfiguration;

        public ClientService(IIdentityServerClientService identityServerClient)
        {
            _identityServerService = identityServerClient;
            _mapperConfiguration = new MapperConfiguration(cfg => cfg.AddProfile<ApiClientMapperProfile>());

        }

        public IQueryable<ClientEntityDTO> QueryClients()
        {
            return _identityServerService.QueryClients().ProjectTo<ClientEntityDTO>(_mapperConfiguration);
        }

        public async Task<ClientEntityDTO> GetClient(string clientId)
        {
            return ToApiModel(await _identityServerService.GetClient(clientId));
        }

        public async Task DeleteClient(ClientEntityDTO client)
        {
            var identityServerClient = await _identityServerService.GetClient(client.ClientId);

            if (identityServerClient != null)
                await _identityServerService.DeleteClient(identityServerClient);
        }

        public async Task UpdateClient(ClientEntityDTO client)
        {
            var identityServerClient = await _identityServerService.GetClient(client.ClientId);

            if (identityServerClient == null) return;

            identityServerClient.AllowedScopes = client.AllowedScopes;
            identityServerClient.Description = client.Description;
            identityServerClient.AllowedScopes = client.AllowedScopes;
            identityServerClient.ClientSecrets = new List<Secret>
            {
                new Secret
                {
                    Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
                    Value = client.X509CertificateBase64
                }
            };

            await _identityServerService.UpdateClient(identityServerClient);
        }

        public Task AddClient(ClientEntityDTO client)
        {
            return _identityServerService.AddClient(client.ToIdentityServerModel());
        }

        private static ClientEntityDTO ToApiModel([CanBeNull] Client client)
        {
            if (client == null)
                return null;

            return new ClientEntityDTO
            {
                ClientId = client.ClientId,
                Description = client.Description,
                AllowedScopes = client.AllowedScopes.ToArray(),
                X509CertificateBase64 = client.ClientSecrets.FirstOrDefault()?.Value
            };
        }
    }


    public class ApiClientMapperProfile : Profile
    {
        public ApiClientMapperProfile()
        {
            CreateMap<Client, ClientEntityDTO>()
                .ForMember(x => x.X509CertificateBase64,
                    c => c.MapFrom(x => x.ClientSecrets.FirstOrDefault().Value));
                
        }
    }
}
