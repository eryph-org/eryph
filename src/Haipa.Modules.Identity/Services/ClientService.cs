using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Modules.Identity.Models;
using Haipa.Modules.Identity.Models.V1;
using IdentityServer4;
using IdentityServer4.Models;

namespace Haipa.Modules.Identity.Services
{
    public class ClientService<TModel> : IClientService<TModel> where TModel : IClientApiModel
    {
        private readonly IIdentityServerClientService _identityServerService;

        public ClientService(IIdentityServerClientService identityServerClient)
        {
            _identityServerService = identityServerClient;

        }

        public IQueryable<TModel> QueryClients()
        {
            return ClientApiMapper.Mapper.ProjectTo<TModel>(_identityServerService.QueryClients());
        }

        public async Task<TModel> GetClient(string clientId)
        {
            return (await _identityServerService.GetClient(clientId)).ToApiModel<TModel>();
        }

        public async Task DeleteClient(TModel client)
        {
            var identityServerClient = await _identityServerService.GetClient(client.ClientId);

            if (identityServerClient != null)
                await _identityServerService.DeleteClient(identityServerClient);
        }

        public async Task UpdateClient(TModel client)
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

        public Task AddClient(TModel client)
        {
            return _identityServerService.AddClient(client.ToIdentityServerModel());
        }

    }
}
