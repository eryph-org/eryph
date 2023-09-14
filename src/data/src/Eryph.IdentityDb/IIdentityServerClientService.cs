using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.IdentityDb.Entities;

namespace Eryph.IdentityDb
{
    public interface IIdentityServerClientService
    {
        IQueryable<ClientApplicationEntity> QueryClients();
        Task<ClientApplicationEntity> GetClient(string clientId);
        Task DeleteClient(ClientApplicationEntity client);
        Task UpdateClient(ClientApplicationEntity client);
        Task AddClient(ClientApplicationEntity client);
        Task AddClients(IEnumerable<ClientApplicationEntity> clients);
    }
}