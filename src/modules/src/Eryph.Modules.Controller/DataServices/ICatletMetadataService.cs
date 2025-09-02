using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.DataServices;

public interface ICatletMetadataService
{
    Task<CatletMetadata?> GetMetadata(
        Guid id,
        CancellationToken cancellationToken = default);

    Task MarkSecretDataHidden(
        Guid id,
        CancellationToken cancellationToken = default);

    Task RemoveMetadata(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddMetadata(
        CatletMetadata metadata,
        CancellationToken cancellationToken = default);
}
