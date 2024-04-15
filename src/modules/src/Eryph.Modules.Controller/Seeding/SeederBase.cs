using System;
using System.IO.Abstractions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;

namespace Eryph.Modules.Controller.Seeding;

internal abstract class SeederBase : IConfigSeeder<ControllerModule>
{
    private readonly IFileSystem _fileSystem;
    private readonly string _configPath;

    protected SeederBase(
        IFileSystem fileSystem,
        string configPath)
    {
        _fileSystem = fileSystem;
        _configPath = configPath;
    }

    public async Task Execute(CancellationToken stoppingToken)
    {
        var files = _fileSystem.Directory.EnumerateFiles(_configPath, "*.json");
        foreach (var file in files)
        {
            try
            {
                _fileSystem.File.Copy(file, $"{file}.bak", true);
                var content = await _fileSystem.File.ReadAllTextAsync(file, Encoding.UTF8, stoppingToken);
                var projectId = Guid.Parse(_fileSystem.Path.GetFileNameWithoutExtension(file));
                await SeedAsync(projectId, content, stoppingToken);
            }
            catch (Exception ex)
            {
                throw new SeederException($"Failed to seed database from file '{file}'", ex);
            }
        }
    }

    protected abstract Task SeedAsync(Guid entityId, string json, CancellationToken cancellationToken = default);
}
