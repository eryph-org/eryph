using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static LanguageExt.Prelude;

namespace Eryph.ZeroState
{
    internal abstract class ZeroStateProjectSeederBase : IZeroStateSeeder
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _configPath;

        protected ZeroStateProjectSeederBase(
            IFileSystem fileSystem,
            string configPath)
        {
            _fileSystem = fileSystem;
            _configPath = configPath;
        }

        public async Task SeedAsync(CancellationToken stoppingToken = default)
        {
            var files = _fileSystem.Directory.EnumerateFiles(Path.Combine(_configPath, "*.json"));
            foreach (var file in files)
            {
                // TODO error handling (LanguageExt to collect errors?)
                var content = await _fileSystem.File.ReadAllTextAsync(file, Encoding.UTF8, stoppingToken);
                var projectId = Guid.Parse(_fileSystem.Path.GetFileNameWithoutExtension(file));
                await SeedProjectAsync(projectId, content, stoppingToken);
            }
        }

        protected abstract Task SeedProjectAsync(
            Guid projectId,
            string json,
            CancellationToken cancellationToken = default);
    }
}
