using System;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Network;

namespace Eryph.Modules.Controller.Networks;

public interface INetworkConfigRealizer
{
    Task UpdateNetwork(Guid projectId, ProjectNetworksConfig config, NetworkProvidersConfiguration providerConfig);
}