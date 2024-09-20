using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.Modules.Identity.Services;

namespace Eryph.Runtime.Zero.Configuration.Clients;

internal class ClientServiceWithConfigServiceDecorator(
    IClientService decoratedService,
    IClientConfigService configService)
    : IClientService
{
    public ValueTask<IEnumerable<ClientApplicationDescriptor>> List(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return decoratedService.List(tenantId, cancellationToken);
    }

    public ValueTask<ClientApplicationDescriptor> Get(
        string clientId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return decoratedService.Get(clientId, tenantId, cancellationToken);
    }

    public async ValueTask<ClientApplicationDescriptor> Update(
        ClientApplicationDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var newDescriptor = await decoratedService.Update(descriptor, cancellationToken);
        await configService.Save(newDescriptor.FromDescriptor());
        return newDescriptor;
    }

    public async ValueTask Delete(string clientId, Guid tenantId, CancellationToken cancellationToken)
    {
        await decoratedService.Delete(clientId, tenantId, cancellationToken);
        configService.Delete(clientId);
    }

    public async ValueTask<ClientApplicationDescriptor> Add(
        ClientApplicationDescriptor descriptor,
        bool hashedSecret,
        CancellationToken cancellationToken)
    {
        var newDescriptor = await decoratedService.Add(descriptor, hashedSecret, cancellationToken);
        await configService.Save(newDescriptor.FromDescriptor());
        return newDescriptor;
    }
}
