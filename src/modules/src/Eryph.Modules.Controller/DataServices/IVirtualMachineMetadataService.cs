using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Resources.Machines;
using LanguageExt;

namespace Eryph.Modules.Controller.DataServices;

public interface IVirtualMachineMetadataService
{
    Task<Option<CatletMetadata>> GetMetadata(
        Guid id,
        CancellationToken cancellationToken = default);
    
    Task<Unit> SaveMetadata(
        CatletMetadata metadata,
        CancellationToken cancellationToken = default);

    Task<Unit> RemoveMetadata(
        Guid id,
        CancellationToken cancellationToken = default);
}