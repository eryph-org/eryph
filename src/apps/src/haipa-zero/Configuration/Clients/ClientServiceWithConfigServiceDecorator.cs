using System.Linq;
using System.Threading.Tasks;
using Haipa.Configuration;
using Haipa.Configuration.Model;
using Haipa.Modules.Identity.Models;
using Haipa.Modules.Identity.Services;

namespace Haipa.Runtime.Zero.Configuration.Clients
{
    internal class ClientServiceWithConfigServiceDecorator<TModel> : IClientService<TModel>
        where TModel : IClientApiModel
    {
        private readonly IConfigWriterService<ClientConfigModel> _configService;
        private readonly IClientService<TModel> _decoratedService;

        public ClientServiceWithConfigServiceDecorator(IClientService<TModel> decoratedService,
            IConfigWriterService<ClientConfigModel> configService)
        {
            _decoratedService = decoratedService;
            _configService = configService;
        }

        public IQueryable<TModel> QueryClients()
        {
            return _decoratedService.QueryClients();
        }

        public Task<TModel> GetClient(string clientId)
        {
            return _decoratedService.GetClient(clientId);
        }

        public async Task DeleteClient(TModel client)
        {
            await _decoratedService.DeleteClient(client);
            await _configService.Delete(client.FromApiModel());
        }

        public async Task UpdateClient(TModel client)
        {
            await _decoratedService.UpdateClient(client);
            await _configService.Update(client.FromApiModel());
        }

        public async Task AddClient(TModel client)
        {
            await _decoratedService.AddClient(client);
            await _configService.Add(client.FromApiModel());
        }
    }
}