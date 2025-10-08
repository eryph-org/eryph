using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Modules.Controller.Serializers;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.ChangeTracking.Catlets;

internal class CatletSpecificationChangeHandler : IChangeHandler<CatletSpecificationChange>
{
    private readonly ChangeTrackingConfig _config;
    private readonly IFileSystem _fileSystem;
    private readonly IStateStore _stateStore;

    public CatletSpecificationChangeHandler(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        IStateStore stateStore)
    {
        _config = config;
        _fileSystem = fileSystem;
        _stateStore = stateStore;
    }

    public async Task HandleChangeAsync(
        CatletSpecificationChange change,
        CancellationToken cancellationToken = default)
    {
        var id = change.Id;
        var path = Path.Combine(_config.CatletSpecificationsConfigPath, $"{id}.json");

        var specification = await _stateStore.For<CatletSpecification>()
            .GetByIdAsync(id, cancellationToken);
        if (specification is null)
        {
            _fileSystem.File.Delete(path);
            return;
        }

        var specificationConfig = new CatletSpecificationConfigModel
        {   
            ProjectId = specification.ProjectId,
            Name = specification.Name,
            LatestId = specification.LatestId,
        };

        var json = CatletSpecificationConfigModelJsonSerializer.Serialize(specificationConfig);
        await _fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }
}
