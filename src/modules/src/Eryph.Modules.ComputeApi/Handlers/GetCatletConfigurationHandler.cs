using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Mvc;

using Catlet = Eryph.StateDb.Model.Catlet;

namespace Eryph.Modules.ComputeApi.Handlers
{

    internal class GetCatletConfigurationHandler : IGetRequestHandler<Catlet, 
        CatletConfiguration>
    {
        private readonly IStateStore _stateStore;

        public GetCatletConfigurationHandler(IStateStore stateStore)
        {
            _stateStore = stateStore;
        }

        public async Task<ActionResult<CatletConfiguration>> HandleGetRequest(Func<ISingleResultSpecification<Catlet>> specificationFunc, CancellationToken cancellationToken)
        {
            var vCatletSpec = specificationFunc();

            var repo = _stateStore.For<Catlet>();
            var vCatletIdResult= await repo.GetBySpecAsync(vCatletSpec, cancellationToken);

            if (vCatletIdResult == null)
                return new NotFoundResult();

            var vCatlet = await repo.GetBySpecAsync(new CatletSpecs
                .GetForConfig(vCatletIdResult.Id), cancellationToken);

            if (vCatlet == null)
                return new NotFoundResult();

            var config = new CatletConfig
            {
                Name = vCatlet.Name,
                Project = vCatlet.Project.Name != "default" ? vCatlet.Project.Name: null,
                Version = "1.0",
                Environment = vCatlet.Environment != "default" ? vCatlet.Environment : null,
                Location = vCatlet.StorageIdentifier
            };


            var driveConfigs = new List<CatletDriveConfig>();
            var alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
            for (var index = 0; index < vCatlet.Drives.Count; index++)
            {
                var drive = vCatlet.Drives[index];
                var driveName = $"sd{alpha[index]}".ToLowerInvariant();
                
                var driveConfig = new CatletDriveConfig();

                if (drive.AttachedDisk != null)
                {
                    driveConfig.Name = Path.GetFileNameWithoutExtension(drive.AttachedDisk.FileName);
                    driveConfig.Size = (int)
                        Math.Ceiling(drive.AttachedDisk.SizeBytes.GetValueOrDefault() / 1024d / 1024 / 1024);
                }

                driveConfig.Type = drive.Type != CatletDriveType.VHD ?  drive.Type : null;
                driveConfig.Name ??= driveName;
                driveConfigs.Add(driveConfig);


            }

            config.Drives = driveConfigs.ToArray();
            

            if (vCatlet.NetworkPorts != null)
            {
                var networks = new List<CatletNetworkConfig>();
                foreach (var vCatletNetworkPort in vCatlet.NetworkPorts)
                {
                    var network = new CatletNetworkConfig
                    {
                        Name = vCatletNetworkPort.Network.Name
                    };

                    foreach (var ipAssignment in vCatletNetworkPort.IpAssignments)
                    {
                        if(ipAssignment.Subnet is not VirtualNetworkSubnet subnet)
                            continue;

                        string poolName = null;
                        if (ipAssignment is IpPoolAssignment ipPoolAssignment)
                        {
                            await _stateStore.LoadPropertyAsync(ipPoolAssignment, 
                                x=>x.Pool, cancellationToken);

                            poolName = ipPoolAssignment.Pool.Name;
                        }

                        var subnetConfig = new CatletSubnetConfig
                        {
                            Name = subnet.Name,
                            IpPool = poolName
                        };

                        // ignore entries with defaults
                        if (subnetConfig.IpPool == "default" && subnetConfig.Name == "default")
                            continue;

                        var addressFamily = IPAddress.Parse(ipAssignment.IpAddress).AddressFamily;

                        switch (addressFamily)
                        {
                            case AddressFamily.InterNetwork:
                                network.SubnetV4 = subnetConfig;
                                break;
                            case AddressFamily.InterNetworkV6:
                                network.SubnetV6 = subnetConfig;
                                break;
                        }

                    }


                    // ignore network with only default values
                    if(network.Name == "default" && network.AdapterName == null
                       && network.SubnetV4 == null && network.SubnetV6 == null
                       )
                        continue;

                    networks.Add(network);
                }

                config.Networks = networks.ToArray();

            }

            config.Cpu = new CatletCpuConfig
            {
                Count = vCatlet.CpuCount
            };

            config.Memory = new CatletMemoryConfig
            {
                Startup = (int)Math.Ceiling(vCatlet.StartupMemory / 1024d / 1024),

                Maximum = vCatlet.MaximumMemory != 0 ? (int)Math.Ceiling(vCatlet.MaximumMemory / 1024d / 1024)
                    : null,
                Minimum = vCatlet.MinimumMemory != 0 ? (int)Math.Ceiling(vCatlet.MaximumMemory / 1024d / 1024)
                    : null,

            };

            //remove all settings also configured in image
            var metadataEntity = await _stateStore.Read<CatletMetadata>().GetByIdAsync(vCatlet.MetadataId, cancellationToken);
            VirtualCatletMetadata metadata = null;
            if (metadataEntity != null)
                metadata = JsonSerializer.Deserialize<VirtualCatletMetadata>(metadataEntity.Metadata);

            if (metadata != null)
            {
                config.Fodder = metadata.Fodder;
                if (config.Hostname == config.Name)
                    config.Hostname = null;

                if (config.Fodder != null)
                {
                    foreach (var fodderConfig in config.Fodder)
                    {
                        if (!fodderConfig.Secret.GetValueOrDefault()) continue;

                        fodderConfig.Content = "#REDACTED";

                    }

                    if (config.Fodder.Length == 0)
                        config.Fodder = null;

                }

                if (metadata.ParentConfig != null)
                {
                    config.Parent = metadata.ParentConfig.Parent;

                    if (config.Cpu?.Count.GetValueOrDefault() == 0 || config.Cpu?.Count == metadata.ParentConfig.Cpu?.Count)
                        config.Cpu.Count = null;

                    if (config.Memory.Startup.GetValueOrDefault() == 0 || config.Memory.Startup == metadata.ParentConfig.Memory?.Startup)
                        config.Memory.Startup = null;

                    if (config.Memory.Minimum.GetValueOrDefault() == 0 || config.Memory.Minimum == metadata.ParentConfig.Memory?.Minimum)
                        config.Memory.Minimum = null;


                    if (config.Memory.Maximum.GetValueOrDefault() == 0 || config.Memory.Maximum == metadata.ParentConfig.Memory?.Maximum)
                        config.Memory.Maximum = null;


                    var driveConfigReduced = new List<CatletDriveConfig>();
                    foreach (var driveConfig in config.Drives)
                    {
                        var parentConfig = metadata.ParentConfig.Drives.FirstOrDefault(x=>x.Name == driveConfig.Name);

                        if (parentConfig != null)
                        {
                            if(driveConfig.Location == null || driveConfig.Location == parentConfig.Location)
                                driveConfig.Location = null;
                            if (driveConfig.Type == null || driveConfig.Type == parentConfig.Type)
                                driveConfig.Type = null;
                            if (driveConfig.Size.GetValueOrDefault() == 0 || driveConfig.Size == parentConfig.Size)
                                driveConfig.Size = null;

                            if(driveConfig.Type == null && driveConfig.Size == null
                               && driveConfig.Location == null && driveConfig.Source == null
                              )
                                continue;
                        }

                        driveConfigReduced.Add(driveConfig);
                    }
                    config.Drives = driveConfigReduced.ToArray();
                }
            }

            if (config.Networks?.Length == 0)
                config.Networks = null;

            if (config.Drives?.Length == 0)
                config.Drives = null;



            if (config.Cpu.Count == null)
                config.Cpu = null;

            if (config.Memory?.Startup.GetValueOrDefault() == 0 && config.Memory?.Maximum.GetValueOrDefault() ==0
                && config.Memory?.Minimum.GetValueOrDefault() == 0)
                config.Memory = null;


            var configString = ConfigModelJsonSerializer.Serialize(config);

            var result = new CatletConfiguration
            {
                Configuration = JsonSerializer.Deserialize<JsonElement>(configString)
            };

            return result;
        }
    }
}
