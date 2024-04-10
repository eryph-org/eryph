﻿using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.ZeroState.NetworkProviders;

internal class ZeroStateFloatingNetworkPortChangeHandler
    : IZeroStateChangeHandler<ZeroStateFloatingNetworkPortChange>
{
    private readonly IZeroStateConfig _config;
    private readonly IFileSystem _fileSystem;
    private readonly IStateStore _stateStore;

    public ZeroStateFloatingNetworkPortChangeHandler(
        IZeroStateConfig config,
        IFileSystem fileSystem,
        IStateStore stateStore)
    {
        _config = config;
        _fileSystem = fileSystem;
        _stateStore = stateStore;
    }

    public async Task HandleChangeAsync(
        ZeroStateFloatingNetworkPortChange change,
        CancellationToken cancellationToken = default)
    {
        var floatingPorts = await _stateStore.For<FloatingNetworkPort>()
            .ListAsync(new FloatingNetworkPortSpecs.GetForConfig(), cancellationToken);

        var floatingPortsConfig = floatingPorts
            .Map(p => new FloatingNetworkPortConfigModel()
            {
                Name = p.Name,
                MacAddress = p.MacAddress,
                ProviderName = p.ProviderName,
                SubnetName = p.SubnetName,
                PoolName = p.PoolName,
                IpAssignments = p.IpAssignments.Map(a =>
                    a is IpPoolAssignment pa
                        ? new IpAssignmentConfigModel()
                        {
                            IpAddress = pa.IpAddress,
                            SubnetName = pa.Subnet.Name,
                            PoolName = pa.Pool.Name,
                        }
                        : new IpAssignmentConfigModel()
                        {
                            IpAddress = a.IpAddress,
                            SubnetName = a.Subnet.Name,
                        }
                ).ToArray()
            }).ToArray();

        var wrapper = new FloatingNetworkPortsConfigModel()
        {
            FloatingPorts = floatingPortsConfig,
        };
        var json = JsonSerializer.Serialize(wrapper);
        var path = Path.Combine(_config.NetworksConfigPath, "ports.json");

        await _fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }
}