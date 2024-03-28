using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Modules.Controller;
using Microsoft.Extensions.Logging;

namespace Eryph.ZeroState;

internal abstract class ZeroStateSeederBase : IConfigSeeder<ControllerModule>
{
    private readonly IFileSystem _fileSystem;
    private readonly string _configPath;

    protected ZeroStateSeederBase(
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
            // TODO error handling (LanguageExt to collect errors?)
            var content = await _fileSystem.File.ReadAllTextAsync(file, Encoding.UTF8, stoppingToken);
            var projectId = Guid.Parse(_fileSystem.Path.GetFileNameWithoutExtension(file));
            await SeedAsync(projectId, content, stoppingToken);
        }
    }

    protected abstract Task SeedAsync(Guid entityId, string json, CancellationToken cancellationToken = default);
}
