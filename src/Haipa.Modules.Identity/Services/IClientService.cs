using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Modules.Identity.Models;

namespace Haipa.Modules.Identity.Services
{
    public interface IClientService
    {
        IQueryable<ClientEntityDTO> QueryClients();
        Task<ClientEntityDTO> GetClient(string clientId);
        Task DeleteClient(ClientEntityDTO client);
        Task UpdateClient(ClientEntityDTO client);
        Task AddClient(ClientEntityDTO client);
    }
}