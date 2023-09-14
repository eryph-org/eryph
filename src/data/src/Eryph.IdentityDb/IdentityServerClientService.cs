using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Eryph.IdentityDb.Entities;

namespace Eryph.IdentityDb
{
    public class IdentityServerClientService : IIdentityServerClientService
    {
        private readonly IClientRepository _repository;

        static IdentityServerClientService()
        {

        }

        public IdentityServerClientService(IClientRepository repository)
        {
            _repository = repository;
        }

        public IQueryable<ClientApplicationEntity> QueryClients()
        {
            return _repository.QueryClients();
        }

        public async Task<ClientApplicationEntity> GetClient(string clientId)
        {
            return await _repository.GetClientAsync(clientId);
        }

        public async Task AddClient(ClientApplicationEntity client)
        {
            await _repository.AddClientAsync(client);
            await _repository.SaveAllChangesAsync();
        }

        public async Task AddClients(IEnumerable<ClientApplicationEntity> clients)
        {
            await _repository.AddClientsAsync(clients);
            await _repository.SaveAllChangesAsync();
        }

        public async Task DeleteClient(ClientApplicationEntity client)
        {
            var trackedClient = await _repository.GetTrackedClientAsync(client.ClientId);

            if (trackedClient == null)
                return;

            await _repository.RemoveClientAsync(trackedClient);
            await _repository.SaveAllChangesAsync();
        }


        public async Task UpdateClient(ClientApplicationEntity client)
        {
            var trackedClient = await _repository.GetTrackedClientAsync(client.ClientId);

            if (trackedClient == null)
                return;

            _repository.RemoveClientRelations(trackedClient);
            await _repository.UpdateClientAsync(trackedClient);
            await _repository.SaveAllChangesAsync();
        }
    }
}