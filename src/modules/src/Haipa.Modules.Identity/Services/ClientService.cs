using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Haipa.Modules.Identity.Models;
using Haipa.Modules.Identity.Models.V1;
using Haipa.Security.Cryptography;
using Org.BouncyCastle.OpenSsl;

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
            if (string.Equals(client.Id, "system-client"))
                throw new InvalidOperationException("It is not allowed to delete the build-in system-client");

            var identityServerClient = await _identityServerService.GetClient(client.Id);

            if (identityServerClient != null)
                await _identityServerService.DeleteClient(identityServerClient);
        }

        public async Task UpdateClient(TModel client)
        {
            if (string.Equals(client.Id, "system-client"))
                throw new InvalidOperationException("It is not allowed to change the build-in system-client");


            var identityServerClient = await _identityServerService.GetClient(client.Id);

            if (identityServerClient == null) return;

            identityServerClient.AllowedScopes = client.AllowedScopes;
            identityServerClient.Description = client.Description;
            identityServerClient.ClientName = client.Name;

            await _identityServerService.UpdateClient(identityServerClient);
        }

        public Task AddClient(TModel client)
        {
            return _identityServerService.AddClient(client.ToIdentityServerModel());
        }

    }
}
