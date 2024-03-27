﻿using System;
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
        private readonly INetworkConfigValidator _configValidator;
        private readonly IStateStore _stateStore;

        public ZeroStateVirtualNetworkSeeder(
            ILogger logger,
            IFileSystem fileSystem,
            IZeroStateConfig config,
            INetworkProviderManager networkProviderManager,
            INetworkConfigRealizer configRealizer,
            INetworkConfigValidator configValidator,
            IStateStore stateStore) : base(fileSystem, config.ProjectNetworksConfigPath, logger)
        {
            _logger = logger;
            _networkProviderManager = networkProviderManager;
            _configRealizer = configRealizer;
            _configValidator = configValidator;
            _stateStore = stateStore;
        }

        protected override async Task SeedProjectAsync(Guid projectId, string json, CancellationToken cancellationToken = default)
        {
            var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(json);
            if (configDictionary == null)
                return;

            var project = await _stateStore.For<Project>().GetBySpecAsync(
                new ProjectSpecs.GetById(EryphConstants.DefaultTenantId, projectId),
                cancellationToken);
            if (project is null)
            {
                _logger.LogWarning("Could not find project {ProjectId} during restore of networks", projectId);
                return;
            }

            var existingNetworks = await _stateStore.For<VirtualNetwork>().ListAsync(
                new VirtualNetworkSpecs.GetForProjectConfig(project.Id),
                cancellationToken);
            if (existingNetworks.Any())
                return;

            var config = ProjectNetworksConfigDictionaryConverter.Convert(configDictionary);
            var normalizedConfig = _configValidator.NormalizeConfig(config);

            // TODO fix error handling
            var providerConfig = await _networkProviderManager.GetCurrentConfiguration()
                .IfLeft(l => l.ToErrorException().Rethrow<NetworkProvidersConfiguration>());
            await _configRealizer.UpdateNetwork(project.Id, normalizedConfig, providerConfig);
            await _stateStore.SaveChangesAsync(cancellationToken);
        }
    }
}