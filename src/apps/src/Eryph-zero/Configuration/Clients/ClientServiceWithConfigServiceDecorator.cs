using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.Modules.Identity.Services;

namespace Eryph.Runtime.Zero.Configuration.Clients
{
    internal class ClientServiceWithConfigServiceDecorator : IClientService
    {
        private readonly IConfigWriterService<ClientConfigModel> _configService;
        private readonly IClientService _decoratedService;

        public ClientServiceWithConfigServiceDecorator(IClientService decoratedService,
            IConfigWriterService<ClientConfigModel> configService)
        {
            _decoratedService = decoratedService;
            _configService = configService;
        }

        public ValueTask<IEnumerable<ClientApplicationDescriptor>> List(Guid tenantId, CancellationToken cancellationToken)
        {
            return _decoratedService.List(tenantId, cancellationToken);
        }

        public ValueTask<ClientApplicationDescriptor> Get(string clientId, Guid tenantId, CancellationToken cancellationToken)
        {
            return _decoratedService.Get(clientId, tenantId, cancellationToken);
        }

        public async ValueTask<ClientApplicationDescriptor> Update(ClientApplicationDescriptor descriptor, CancellationToken cancellationToken)
        {
            var newDescriptor = await _decoratedService.Update(descriptor, cancellationToken);
            await _configService.Update(newDescriptor.FromDescriptor(), "");
            return newDescriptor;
        }

        public async ValueTask Delete(string clientId, Guid tenantId, CancellationToken cancellationToken)
        {
            var client = await _decoratedService.Get(clientId, tenantId, cancellationToken);
            await _decoratedService.Delete(clientId, EryphConstants.DefaultTenantId, cancellationToken);
            await _configService.Delete(client.FromDescriptor(), "");
        }

        public async ValueTask<ClientApplicationDescriptor> Add(ClientApplicationDescriptor descriptor, bool hashedSecret, CancellationToken cancellationToken)
        {
            var newDescriptor = await _decoratedService.Add(descriptor, hashedSecret, cancellationToken);
            await _configService.Add(newDescriptor.FromDescriptor(), "");
            return newDescriptor;
        }
    }
}