using System;
using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Networks;
using Eryph.Core.Network;

namespace Eryph.Modules.Controller.Networks;

public interface INetworkConfigValidator
{
    ProjectNetworksConfig NormalizeConfig(ProjectNetworksConfig config);

    IAsyncEnumerable<string> ValidateChanges(Guid projectId,
        ProjectNetworksConfig config,
        NetworkProvider[] networkProviders);

    IEnumerable<string> ValidateConfig(ProjectNetworksConfig config, NetworkProvider[] networkProviders);
}