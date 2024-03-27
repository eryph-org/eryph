﻿using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.ZeroState.VirtualMachines
{
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
                var metadata = await _stateStore.For<CatletMetadata>()
                    .GetByIdAsync(metadataId, cancellationToken);

                var json = JsonSerializer.Serialize(metadata);
                var path = Path.Combine(_config.VirtualMachinesConfigPath, $"{metadataId}.json");
                await _fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
            }
        }
    }
}