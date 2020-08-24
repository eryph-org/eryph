using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.Models;

namespace Haipa.Modules.Identity.Services
{
    public interface IIdentityServerClientService
    {
        IQueryable<Client> QueryClients();
        Task<Client> GetClient(string clientId);
        Task DeleteClient(Client client);
        Task UpdateClient(Client client);
        Task AddClient(Client client);
        Task AddClients(IEnumerable<Client> clients);

    }
}