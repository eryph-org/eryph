using System.Linq;
using System.Threading.Tasks;
using Haipa.Modules.Identity.Models;
using Haipa.Modules.Identity.Services;

namespace Haipa.Runtime.Zero.ConfigStore.Clients
{
    internal class ClientServiceWithConfigServiceDecorator : IClientService
    {
        private readonly IClientService _decoratedService;
        private readonly IConfigWriterService<ClientConfigModel> _configService;

        public ClientServiceWithConfigServiceDecorator(IClientService decoratedService, IConfigWriterService<ClientConfigModel> configService)
        {
            _decoratedService = decoratedService;
            _configService = configService;
        }

        public IQueryable<ClientEntityDTO> QueryClients ()
        {
            return _decoratedService.QueryClients();
        }

        public Task<ClientEntityDTO> GetClient(string clientId)
        {
            return _decoratedService.GetClient(clientId);
        }

        public async Task DeleteClient(ClientEntityDTO client)
        {
            await _decoratedService.DeleteClient(client);
            await _configService.Delete(client.FromApiModel());
        }

        public async Task UpdateClient(ClientEntityDTO client)
        {
            await _decoratedService.UpdateClient(client);
            await _configService.Update(client.FromApiModel());
        }

        public async Task AddClient(ClientEntityDTO client)
        {
            await _decoratedService.AddClient(client);
            await _configService.Add(client.FromApiModel());
        }

    }
}