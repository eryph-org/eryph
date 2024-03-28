using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.ModuleCore.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.ZeroState.VirtualNetworks;

internal class ZeroStateVirtualNetworkChangeHandler : IZeroStateChangeHandler<ZeroStateVirtualNetworkChange>
{
    private readonly IZeroStateConfig _config;
    private readonly IFileSystem _fileSystem;
    private readonly IStateStore _stateStore;

    public ZeroStateVirtualNetworkChangeHandler(
        IZeroStateConfig config,
        IFileSystem fileSystem,
        IStateStore stateStore)
    {
        _config = config;
        _fileSystem = fileSystem;
        _stateStore = stateStore;
    }

    public async Task HandleChangeAsync(
        ZeroStateVirtualNetworkChange change,
        CancellationToken cancellationToken = default)
    {
        foreach (var projectId in change.ProjectIds)
        {
            await HandleChangeAsync(projectId, cancellationToken);
        }
    }

    private async Task HandleChangeAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_config.ProjectNetworksConfigPath, $"{projectId}.json");

        var project = await _stateStore.Read<Project>().GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            _fileSystem.File.Delete(path);
            return;
        }

        var networks = await _stateStore.For<VirtualNetwork>()
            .ListAsync(new VirtualNetworkSpecs.GetForProjectConfig(project.Id), cancellationToken);

        var networkConfig = networks.ToNetworksConfig(project.Name);
        var json = ConfigModelJsonSerializer.Serialize(networkConfig);
        await _fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }
}
