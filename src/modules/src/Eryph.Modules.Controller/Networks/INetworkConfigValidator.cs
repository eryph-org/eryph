using System;
using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Network;

namespace Eryph.Modules.Controller.Networks;

public interface INetworkConfigValidator
{
    ProjectNetworksConfig NormalizeConfig(ProjectNetworksConfig config);

    IAsyncEnumerable<string> ValidateChanges(Guid projectId,
        ProjectNetworksConfig config,
        NetworkProvidersConfiguration providerConfig);

    IEnumerable<string> ValidateConfig(ProjectNetworksConfig config, NetworkProvidersConfiguration providerConfig);
}