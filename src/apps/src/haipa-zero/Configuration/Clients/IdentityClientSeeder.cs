using System.Threading;
using System.Threading.Tasks;
using Haipa.Configuration;
using Haipa.Configuration.Model;
using Haipa.Modules.Identity;
using Haipa.Modules.Identity.Models;
using Haipa.Modules.Identity.Services;
using JetBrains.Annotations;

namespace Haipa.Runtime.Zero.Configuration.Clients
{
    [UsedImplicitly]
    internal class IdentityClientSeeder : IConfigSeeder<IdentityModule>
    {
        private readonly IConfigReaderService<ClientConfigModel> _clientConfigService;
        private readonly IIdentityServerClientService _clientService;

        public IdentityClientSeeder(IIdentityServerClientService clientService,
            IConfigReaderService<ClientConfigModel> clientConfigService)
        {
            _clientService = clientService;
            _clientConfigService = clientConfigService;
        }

        public Task Execute(CancellationToken stoppingToken)
        {
            return _clientService.AddClients(
                _clientConfigService.GetConfig()
                    .Map(x => x.ToApiModel().ToIdentityServerModel()));
        }
    }
}