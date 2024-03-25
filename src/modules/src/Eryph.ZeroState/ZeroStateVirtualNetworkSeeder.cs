using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Networks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using LanguageExt.Pipes;
using Microsoft.Extensions.Logging;

namespace Eryph.ZeroState
{
    internal class ZeroStateVirtualNetworkSeeder : ZeroStateProjectSeederBase
    {
        private readonly ILogger _logger;
        private readonly INetworkProviderManager _networkProviderManager;
        private readonly INetworkConfigRealizer _configRealizer;
        private readonly IStateStore _stateStore;

        public ZeroStateVirtualNetworkSeeder(
            ILogger logger,
            IFileSystem fileSystem,
            IZeroStateConfig config,
            INetworkProviderManager networkProviderManager,
            INetworkConfigRealizer configRealizer,
            IStateStore stateStore) : base(fileSystem, config.ProjectNetworksConfigPath, logger)
        {
            _logger = logger;
            _networkProviderManager = networkProviderManager;
            _configRealizer = configRealizer;
            _stateStore = stateStore;
        }

        protected override async Task SeedProjectAsync(Guid projectId, string json, CancellationToken cancellationToken = default)
        {
            var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(json);
            if (configDictionary == null)
                return;

            var config = ProjectNetworksConfigDictionaryConverter.Convert(configDictionary);
            var project = await _stateStore.For<Project>().GetBySpecAsync(
                new ProjectSpecs.GetByName(EryphConstants.DefaultTenantId, config.Project),
                cancellationToken);
            if (project is null)
            {
                _logger.LogWarning("Could not find project {ProjectName} during restore of networks", config.Project);
                return;
            }

            // TODO skip restore if project already has networks

            // TODO fix error handling
            var providerConfig = await _networkProviderManager.GetCurrentConfiguration()
                .IfLeft(l => l.ToErrorException().Rethrow<NetworkProvidersConfiguration>());
            await _configRealizer.UpdateNetwork(project.Id, config, providerConfig);
            await _stateStore.SaveChangesAsync(cancellationToken);
        }
    }
}
