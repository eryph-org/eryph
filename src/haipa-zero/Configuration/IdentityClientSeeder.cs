using System.Threading;
using System.Threading.Tasks;
using Haipa.Configuration;
using Haipa.Configuration.Model;
using Haipa.Modules.Identity;
using Haipa.Modules.Identity.Models;
using Haipa.Modules.Identity.Services;
using Haipa.Runtime.Zero.Configuration.Clients;
using JetBrains.Annotations;

namespace Haipa.Runtime.Zero.Configuration
{
    [UsedImplicitly]
    internal class IdentityClientSeeder : IConfigSeeder<IdentityModule>
    {
        private readonly IIdentityServerClientService _clientService;
        private readonly IConfigReaderService<ClientConfigModel> _clientConfigService;

        public IdentityClientSeeder(IIdentityServerClientService clientService, IConfigReaderService<ClientConfigModel> clientConfigService)
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