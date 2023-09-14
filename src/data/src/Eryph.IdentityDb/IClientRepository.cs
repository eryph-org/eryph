using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.IdentityDb.Entities;

namespace Eryph.IdentityDb
{
    public interface IClientRepository
    {
        Task<ClientApplicationEntity> GetClientAsync(string id);
        IQueryable<ClientApplicationEntity> QueryClients();

        Task<int> SaveAllChangesAsync();

        Task<string> AddClientAsync(ClientApplicationEntity client);
        Task AddClientsAsync(IEnumerable<ClientApplicationEntity> clients);
        Task UpdateClientAsync(ClientApplicationEntity client);
        Task RemoveClientAsync(ClientApplicationEntity client);
        Task<ClientApplicationEntity> GetTrackedClientAsync(string clientId);
        void RemoveClientRelations(ClientApplicationEntity client);
    }
}