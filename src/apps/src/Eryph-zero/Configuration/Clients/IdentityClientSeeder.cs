using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.IdentityDb;
using Eryph.Modules.Identity;
using Eryph.Modules.Identity.Models;
using Eryph.Modules.Identity.Services;
using JetBrains.Annotations;

namespace Eryph.Runtime.Zero.Configuration.Clients
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