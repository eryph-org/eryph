using System.Linq;
using System.Threading.Tasks;
using Eryph.Modules.Identity.Models;

namespace Eryph.Modules.Identity.Services
{
    public interface IClientService<TModel> where TModel : IClientApiModel
    {
        IQueryable<TModel> QueryClients();
        Task<TModel> GetClient(string clientId);
        Task DeleteClient(TModel client);
        Task UpdateClient(TModel client);
        Task AddClient(TModel client);
    }
}