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
using Eryph.Core;
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
using CatletMetadata = Eryph.Resources.Machines.CatletMetadata;

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

        public async Task<ActionResult<CatletConfiguration>> HandleGetRequest(
            Func<ISingleResultSpecification<Catlet>> specificationFunc,
            CancellationToken cancellationToken)
        {
            var catletSpec = specificationFunc();

            var repo = _stateStore.For<Catlet>();
            var catletIdResult= await repo.GetBySpecAsync(catletSpec, cancellationToken);

            if (catletIdResult == null)
                return new NotFoundResult();

            var catlet = await repo.GetBySpecAsync(new CatletSpecs
                .GetForConfig(catletIdResult.Id), cancellationToken);

            if (catlet == null)
                return new NotFoundResult();

            var config = new CatletConfig
            {
                Name = catlet.Name,
                Project = catlet.Project.Name != "default" ? catlet.Project.Name: null,
                Version = "1.0",
                Environment = catlet.Environment != "default" ? catlet.Environment : null,
                Location = catlet.StorageIdentifier
            };


            var driveConfigs = new List<CatletDriveConfig>();
            var alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
            for (var index = 0; index < catlet.Drives.Count; index++)
            {
                var drive = catlet.Drives[index];
                var driveName = $"sd{alpha[index]}".ToLowerInvariant();
                
                var driveConfig = new CatletDriveConfig();

                if (drive.AttachedDisk != null)
                {
                    driveConfig.Name = drive.AttachedDisk.Name;

                    driveConfig.Size = (int)Math.Ceiling(
                        drive.AttachedDisk.SizeBytes.GetValueOrDefault() / 1024d / 1024 / 1024);

                    // When the drive's parent is a gene disk and their sizes are equal,
                    // this means the drive size has not been changed and should be omitted.
                    // This check handles the case when the catlet gene in the gene pool has no
                    // disk sizes specified in the config.
                    if (drive.AttachedDisk.Parent is not null
                        && drive.AttachedDisk.Parent.StorageIdentifier?.StartsWith("gene:") == true
                        && drive.AttachedDisk.Parent.SizeBytes == drive.AttachedDisk.SizeBytes)
                    {
                        driveConfig.Size = null;
                    }
                }

                driveConfig.Type = drive.Type != CatletDriveType.VHD ?  drive.Type : null;
                driveConfig.Name ??= driveName;
                driveConfigs.Add(driveConfig);


            }

            config.Drives = driveConfigs.ToArray();

            var networkPorts = await _stateStore.For<CatletNetworkPort>().ListAsync(
                new CatletNetworkPortSpecs.GetByCatletMetadataId(catlet.MetadataId),
                cancellationToken);

            if (networkPorts.Count > 0)
            {
                var networks = new List<CatletNetworkConfig>();
                foreach (var catletNetworkPort in networkPorts)
                {
                    var network = new CatletNetworkConfig
                    {
                        Name = catletNetworkPort.Network.Name
                    };

                    foreach (var ipAssignment in catletNetworkPort.IpAssignments)
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
                Count = catlet.CpuCount
            };

            config.Memory = new CatletMemoryConfig
            {
                Startup = (int)Math.Ceiling(catlet.StartupMemory / 1024d / 1024),

                Maximum = catlet.MaximumMemory != 0 ? (int)Math.Ceiling(catlet.MaximumMemory / 1024d / 1024)
                    : null,
                Minimum = catlet.MinimumMemory != 0 ? (int)Math.Ceiling(catlet.MaximumMemory / 1024d / 1024)
                    : null,

            };

            //remove all settings also configured in image
            var metadataEntity = await _stateStore.Read<StateDb.Model.CatletMetadata>().GetByIdAsync(catlet.MetadataId, cancellationToken);
            CatletMetadata metadata = null;
            if (metadataEntity != null)
                metadata = JsonSerializer.Deserialize<CatletMetadata>(metadataEntity.Metadata);

            if (metadata != null)
            {
                config.Fodder = metadata.Fodder;
                config.Variables = metadata.Variables;
                if (config.Hostname == config.Name)
                    config.Hostname = null;

                if (config.Fodder != null)
                {
                    foreach (var fodderConfig in config.Fodder)
                    {
                        if (fodderConfig.Secret.GetValueOrDefault())
                        {
                            fodderConfig.Content = "#REDACTED";
                        }

                        if (fodderConfig.Variables != null)
                        {
                            foreach (var variableConfig in fodderConfig.Variables)
                            {
                                if (variableConfig.Secret.GetValueOrDefault())
                                {
                                    variableConfig.Value = "#REDACTED";
                                }
                            }

                            if (fodderConfig.Variables.Length == 0)
                            {
                                fodderConfig.Variables = null;
                            }
                        }
                    }

                    if (config.Fodder.Length == 0)
                    {
                        config.Fodder = null;
                    }
                }

                if (config.Variables != null)
                {
                    foreach (var variableConfig in config.Variables)
                    {
                        if (variableConfig.Secret.GetValueOrDefault())
                        {
                            variableConfig.Value = "#REDACTED";
                        }
                    }

                    if (config.Variables.Length == 0)
                    {
                        config.Variables = null;
                    }
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
                        var parentConfig = metadata.ParentConfig.Drives?.FirstOrDefault(x=>x.Name == driveConfig.Name);

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

            // for reverse generation of capabilities we have to
            // check if the capability is set enabled or configured on the parent

            var parentCaps = metadata?.ParentConfig?.Capabilities ?? Array.Empty<CatletCapabilityConfig>();
            var capabilities = new List<CatletCapabilityConfig>();
            if (catlet.Features.Any(x =>
                    x == CatletFeature.NestedVirtualization ||
                    parentCaps.Any(c => c.Name == EryphConstants.Capabilities.NestedVirtualization)))
            {
                // nested virtualization is either on or off, so we can just check if it is set to on
                var featureOff = catlet.Features.All(x => x != CatletFeature.NestedVirtualization);

                var nestedVirtualizationCap = new CatletCapabilityConfig
                {
                    Name = EryphConstants.Capabilities.NestedVirtualization,
                    Details = featureOff
                                ? new[] { EryphConstants.CapabilityDetails.Disabled } : null
                };
                capabilities.Add(nestedVirtualizationCap);
            }

            if (catlet.Features.Any(x =>
                    x == CatletFeature.SecureBoot ||
                    parentCaps.Any(c => c.Name == EryphConstants.Capabilities.SecureBoot)))
            {
                var featureOff = catlet.Features.All(x => x != CatletFeature.SecureBoot);
                    
                var details = new List<string>();
                if(!string.IsNullOrWhiteSpace(catlet.SecureBootTemplate))
                    details.Add($"template:{catlet.SecureBootTemplate}");

                if(featureOff)
                    details.Add(EryphConstants.CapabilityDetails.Disabled);

                var secureBootCap = new CatletCapabilityConfig
                {
                    Name = EryphConstants.Capabilities.SecureBoot,
                    Details = details.ToArray()
                };

                capabilities.Add(secureBootCap);
            }

            // reduce capabilities to only those that are not set same on parent
            foreach (var capabilityConfig in capabilities.ToArray())
            {
                var parentCap = parentCaps
                    .FirstOrDefault(x => x.Name == capabilityConfig.Name);
                if (parentCap == null) continue;
                var parentDetails = parentCap.Details?.Map(x => x.ToLowerInvariant()).Order().ToArray() ?? Array.Empty<string>();
                var capDetails = capabilityConfig.Details?.Map(x => x.ToLowerInvariant()).Order().ToArray() ?? Array.Empty<string>();
                if(parentDetails.SequenceEqual(capDetails))
                    capabilities.Remove(capabilityConfig);
            }
            if(capabilities.Count > 0)
                config.Capabilities = capabilities.ToArray();
            
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
