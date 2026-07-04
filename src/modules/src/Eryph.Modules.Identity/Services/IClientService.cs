using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.Identity.Services;

public interface IClientService
{
    ValueTask<IReadOnlyList<ClientApplicationDescriptor>> List(Guid tenantId, CancellationToken cancellationToken);
    ValueTask<ClientApplicationDescriptor?> Get(string clientId, Guid tenantId, CancellationToken cancellationToken);

    /// <remarks>
    /// Mutating the system client is intentionally permitted here (the startup seeder uses it to
    /// reconcile the system client's certificate). Callers exposed to users must block the system
    /// client themselves, as the client endpoints do.
    /// </remarks>
    ValueTask<ClientApplicationDescriptor> Update(ClientApplicationDescriptor descriptor,
        CancellationToken cancellationToken);

    ValueTask Delete(string clientId, Guid tenantId, CancellationToken cancellationToken);

    ValueTask<ClientApplicationDescriptor> Add(ClientApplicationDescriptor descriptor, bool hashedSecret,
        CancellationToken cancellationToken);
}
