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
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Mvc;
using VirtualCatlet = Eryph.StateDb.Model.VirtualCatlet;

namespace Eryph.Modules.ComputeApi.Handlers
{
    internal class GetVirtualCatletConfigurationHandler : IGetRequestHandler<VirtualCatlet, 
        VirtualCatletConfiguration>
    {
        private readonly IStateStore _stateStore;

        public GetVirtualCatletConfigurationHandler(IStateStore stateStore)
        {
            _stateStore = stateStore;
        }

        public async Task<ActionResult<VirtualCatletConfiguration>> HandleGetRequest(Func<ISingleResultSpecification<VirtualCatlet>> specificationFunc, CancellationToken cancellationToken)
        {
            var vCatletSpec = specificationFunc();

            var repo = _stateStore.For<VirtualCatlet>();
            var vCatletIdResult= await repo.GetBySpecAsync(vCatletSpec, cancellationToken);

            if (vCatletIdResult == null)
                return new NotFoundResult();

            var vCatlet = await repo.GetBySpecAsync(new VCatletSpecs
                .GetForConfig(vCatletIdResult.Id), cancellationToken);

            if (vCatlet == null)
                return new NotFoundResult();

            var config = new CatletConfig
            {
                Name = vCatlet.Name,
                Project = vCatlet.Project.Name != "default" ? vCatlet.Project.Name: null,
                Version = "1.0",
                Environment = vCatlet.Environment != "default" ? vCatlet.Environment : null,
                VCatlet = new VirtualCatletConfig
                {
                    Slug = vCatlet.StorageIdentifier,
                }
            };


            var driveConfigs = new List<VirtualCatletDriveConfig>();
            var alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
            for (var index = 0; index < vCatlet.Drives.Count; index++)
            {
                var drive = vCatlet.Drives[index];
                var driveName = $"sd{alpha[index]}".ToLowerInvariant();
                
                var driveConfig = new VirtualCatletDriveConfig();

                if (drive.AttachedDisk != null)
                {
                    driveConfig.Name = Path.GetFileNameWithoutExtension(drive.AttachedDisk.FileName);
                    driveConfig.Size = (int)
                        Math.Ceiling(drive.AttachedDisk.SizeBytes.GetValueOrDefault() / 1024d / 1024 / 1024);
                }

                driveConfig.Type = drive.Type != VirtualCatletDriveType.VHD ?  drive.Type : null;
                driveConfig.Name ??= driveName;
                driveConfigs.Add(driveConfig);


            }

            config.VCatlet.Drives = driveConfigs.ToArray();
            

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

            config.VCatlet.Cpu = new VirtualCatletCpuConfig
            {
                Count = vCatlet.CpuCount
            };

            config.VCatlet.Memory = new VirtualCatletMemoryConfig
            {
                Startup = (int)Math.Ceiling(vCatlet.StartupMemory / 1024d / 1024),

                Maximum = vCatlet.Features.Contains(VCatletFeature.DynamicMemory)
                    ? (int)Math.Ceiling(vCatlet.MaximumMemory / 1024d / 1024)
                    : null,
                Minimum = vCatlet.Features.Contains(VCatletFeature.DynamicMemory)
                    ? (int)Math.Ceiling(vCatlet.MaximumMemory / 1024d / 1024)
                    : null,

            };

            //remove all settings also configured in image
            var metadataEntity = await _stateStore.Read<VirtualMachineMetadata>().GetByIdAsync(vCatlet.MetadataId, cancellationToken);
            VirtualCatletMetadata metadata = null;
            if (metadataEntity != null)
                metadata = JsonSerializer.Deserialize<VirtualCatletMetadata>(metadataEntity.Metadata);

            if (metadata != null)
            {
                config.Raising = metadata.RaisingConfig;

                if (config.Raising != null)
                {
                    // remove default hostname config
                    if (config.Raising.Hostname == config.VCatlet.Slug)
                        config.Raising.Hostname = null;

                    foreach (var raisingConfig in config.Raising.Config)
                    {
                        if (!raisingConfig.Sensitive) continue;

                        raisingConfig.Content = "#REDACTED";

                    }
                }

                if (metadata.ImageConfig != null)
                {
                    config.VCatlet.Image = metadata.ImageConfig.Image;

                    if (config.VCatlet.Cpu.Count == metadata.ImageConfig.Cpu?.Count)
                        config.VCatlet.Cpu.Count = null;

                    if (config.VCatlet.Memory.Startup == metadata.ImageConfig.Memory?.Startup)
                        config.VCatlet.Memory.Startup = null;


                    var driveConfigReduced = new List<VirtualCatletDriveConfig>();
                    foreach (var driveConfig in config.VCatlet.Drives)
                    {
                        var imageConfig = metadata.ImageConfig.Drives.FirstOrDefault(x=>x.Name == driveConfig.Name);

                        if (imageConfig != null)
                        {
                            if(driveConfig.Slug == imageConfig.Slug)
                                driveConfig.Slug = null;
                            if (driveConfig.Type == null || driveConfig.Type == imageConfig.Type)
                                driveConfig.Type = null;
                            if (driveConfig.Size == imageConfig.Size)
                                driveConfig.Size = null;

                            if(driveConfig.Type == null && driveConfig.Size == null
                               && driveConfig.Slug == null && driveConfig.Template == null
                              )
                                continue;
                        }

                        driveConfigReduced.Add(driveConfig);
                    }
                    config.VCatlet.Drives = driveConfigReduced.ToArray();
                }
            }

            if (config.Networks?.Length == 0)
                config.Networks = null;

            if (config.VCatlet.Drives?.Length == 0)
                config.VCatlet.Drives = null;



            if (config.VCatlet.Cpu.Count == null)
                config.VCatlet.Cpu = null;

            if (config.VCatlet.Memory.Startup == null && config.VCatlet.Memory.Maximum == null
                && config.VCatlet.Memory.Minimum == null)
                config.VCatlet.Memory = null;


            var configString = ConfigModelJsonSerializer.Serialize(config);

            var result = new VirtualCatletConfiguration
            {
                Configuration = JsonSerializer.Deserialize<JsonElement>(configString)
            };

            return result;
        }
    }
}
