using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Modules.Controller.Serializers;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.ChangeTracking.Catlets;

internal class CatletSpecificationChangeHandler(
    ChangeTrackingConfig config,
    IFileSystem fileSystem,
    IStateStore stateStore)
    : IChangeHandler<CatletSpecificationChange>
{
    public async Task HandleChangeAsync(
        CatletSpecificationChange change,
        CancellationToken cancellationToken = default)
    {
        var id = change.Id;
        var path = Path.Combine(config.CatletSpecificationsConfigPath, $"{id}.json");

        var specification = await stateStore.For<CatletSpecification>()
            .GetByIdAsync(id, cancellationToken);
        if (specification is null)
        {
            fileSystem.File.Delete(path);
            return;
        }

        var specificationConfig = new CatletSpecificationConfigModel
        {
            Id = specification.Id,
            ProjectId = specification.ProjectId,
            Name = specification.Name,
            Architectures = specification.Architectures.Map(a => a.Value).ToHashSet(),
        };

        var json = CatletSpecificationConfigModelJsonSerializer.Serialize(specificationConfig);
        await fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }
}
