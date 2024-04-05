using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.ZeroState.VirtualMachines;

internal class ZeroStateCatletMetadataChangeHandler : IZeroStateChangeHandler<ZeroStateCatletMetadataChange>
{
    private readonly IZeroStateConfig _config;
    private readonly IFileSystem _fileSystem;
    private readonly IStateStore _stateStore;

    public ZeroStateCatletMetadataChangeHandler(
        IZeroStateConfig config,
        IFileSystem fileSystem,
        IStateStore stateStore)
    {
        _config = config;
        _fileSystem = fileSystem;
        _stateStore = stateStore;
    }

    public async Task HandleChangeAsync(
        ZeroStateCatletMetadataChange change,
        CancellationToken cancellationToken = default)
    {
        foreach (var metadataId in change.Ids)
        {
            await HandleChangeAsync(metadataId, cancellationToken);
        }
    }

    private async Task HandleChangeAsync(
        Guid metadataId,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_config.VirtualMachinesConfigPath, $"{metadataId}.json");

        var metadata = await _stateStore.For<CatletMetadata>()
            .GetByIdAsync(metadataId, cancellationToken);
        if (metadata is null)
        {
            _fileSystem.File.Delete(path); 
            return;
        }

        var realMetadata = JsonSerializer.Deserialize<Resources.Machines.CatletMetadata>(metadata.Metadata);

        var json = JsonSerializer.Serialize(realMetadata);
        await _fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }
}
