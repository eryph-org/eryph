using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.EntityFramework.Entities;

namespace Haipa.IdentityDb
{
    public interface IClientRepository
    {
        Task<Client> GetClientAsync(int id);
        IQueryable<Client> QueryClients();

        Task<int> SaveAllChangesAsync();

        Task<int> AddClientAsync(Client client);
        Task AddClientsAsync(IEnumerable<Client> clients);
        Task UpdateClientAsync(Client client);
        Task RemoveClientAsync(Client client);
        Task<(int? Id, string ClientId)> GetClientIdAsync(string clientId);
        Task<Client> GetTrackedClientAsync(string clientId);
        void RemoveClientRelations(Client client);
    }
}