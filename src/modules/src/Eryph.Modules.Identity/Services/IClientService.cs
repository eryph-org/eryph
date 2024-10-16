using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.Identity.Services;

public interface IClientService
{
    ValueTask<IReadOnlyList<ClientApplicationDescriptor>> List(Guid tenantId, CancellationToken cancellationToken);
    ValueTask<ClientApplicationDescriptor?> Get(string clientId, Guid tenantId, CancellationToken cancellationToken);
    ValueTask<ClientApplicationDescriptor> Update(ClientApplicationDescriptor descriptor, CancellationToken cancellationToken);
    ValueTask Delete(string clientId, Guid tenantId, CancellationToken cancellationToken);
    ValueTask<ClientApplicationDescriptor> Add(ClientApplicationDescriptor descriptor, bool hashedSecret, CancellationToken cancellationToken);
}