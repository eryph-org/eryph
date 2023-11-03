using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.Modules.Identity;
using Eryph.Modules.Identity.Services;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eryph.Runtime.Zero.Configuration.Clients
{
    [UsedImplicitly]
    internal class IdentityClientSeeder : IConfigSeeder<IdentityModule>
    {
        private readonly IConfigReaderService<ClientConfigModel> _clientConfigService;
        private readonly IClientService _clientService;
        private readonly ILogger<IdentityClientSeeder> _logger;

        public IdentityClientSeeder(IConfigReaderService<ClientConfigModel> clientConfigService, ILogger<IdentityClientSeeder> logger,
            IClientService clientService)
        {
            _clientConfigService = clientConfigService;
            _logger = logger;
            _clientService = clientService;
        }

        public async Task Execute(CancellationToken stoppingToken)
        {
            foreach (var clientConfigModel in _clientConfigService.GetConfig())
            {
                var clientDescriptor = clientConfigModel.ToDescriptor();

                try
                {
                   await _clientService.Add(clientDescriptor, true, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "failed to load client {clientId}", clientConfigModel.ClientId);

                }
            }
        }
    }
}