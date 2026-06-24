using System;
using System.IO.Abstractions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;

namespace Eryph.Modules.Controller.Seeding;

internal abstract class SeederBase(
    IFileSystem fileSystem,
    string configPath) : IConfigSeeder<ControllerModule>
{
    public async Task Execute(CancellationToken stoppingToken)
    {
        var files = fileSystem.Directory.EnumerateFiles(configPath, "*.json");
        foreach (var file in files)
            try
            {
                fileSystem.File.Copy(file, $"{file}.bak", true);
                var content = await fileSystem.File.ReadAllTextAsync(file, Encoding.UTF8, stoppingToken);
                var projectId = Guid.Parse(fileSystem.Path.GetFileNameWithoutExtension(file));
                await SeedAsync(projectId, content, stoppingToken);
            }
            catch (Exception ex)
            {
                throw new SeederException($"Failed to seed database from file '{file}'", ex);
            }
    }

    protected abstract Task SeedAsync(Guid entityId, string json, CancellationToken cancellationToken = default);
}
