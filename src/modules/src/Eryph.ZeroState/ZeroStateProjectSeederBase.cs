using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.ZeroState
{
    internal abstract class ZeroStateProjectSeederBase : IZeroStateSeeder
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _configPath;
        private readonly ILogger _logger;

        protected ZeroStateProjectSeederBase(
            IFileSystem fileSystem,
            string configPath,
            ILogger logger)
        {
            _fileSystem = fileSystem;
            _configPath = configPath;
            _logger = logger;
        }

        public async Task SeedAsync(CancellationToken stoppingToken = default)
        {
            try
            {
                var files = _fileSystem.Directory.EnumerateFiles(_configPath, "*.json");
                foreach (var file in files)
                {
                    // TODO error handling (LanguageExt to collect errors?)
                    var content = await _fileSystem.File.ReadAllTextAsync(file, Encoding.UTF8, stoppingToken);
                    var projectId = Guid.Parse(_fileSystem.Path.GetFileNameWithoutExtension(file));
                    await SeedProjectAsync(projectId, content, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during seeding");
            }
        }

        protected abstract Task SeedProjectAsync(
            Guid projectId,
            string json,
            CancellationToken cancellationToken = default);
    }
}
