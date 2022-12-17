using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using AutoMapper;
using Eryph.ConfigModel.Catlets;

using LanguageExt;

namespace Eryph.VmManagement
{
    public static class CatletConfigExtensions
    {
        public static Task<Either<PowershellFailure, CatletConfig>> MergeWithImageSettings(
            this CatletConfig machineConfig, Option<VirtualCatletConfig> optionalImageConfig)
        {
            if (string.IsNullOrWhiteSpace(machineConfig.VCatlet?.Image))
                return Prelude.RightAsync<PowershellFailure, CatletConfig>(machineConfig).ToEither();

            //copy machine config to a new object
            var mapper = new Mapper(new MapperConfiguration(c =>
            {
                c.CreateMap<CatletConfig, CatletConfig>();
                c.CreateMap<VirtualCatletConfig, VirtualCatletConfig>();
                c.CreateMap<VirtualCatletCpuConfig, VirtualCatletCpuConfig>();
                c.CreateMap<VirtualCatletMemoryConfig, VirtualCatletMemoryConfig>();
                c.CreateMap<VirtualCatletNetworkAdapterConfig, VirtualCatletNetworkAdapterConfig>();
                c.CreateMap<VirtualCatletDriveConfig, VirtualCatletDriveConfig>();
            }));
            var newConfig = mapper.Map<CatletConfig, CatletConfig>(machineConfig);

            return optionalImageConfig
                .Map(imageConfig =>
                {
                    //initialize machine config with image settings
                    newConfig.VCatlet =
                        mapper.Map<VirtualCatletConfig, VirtualCatletConfig>(imageConfig);

                    //merge drive settings configured both on image and vm config
                    newConfig.VCatlet.Drives.ToSeq()
                        .Iter(ihd =>
                            {
                                var vmHdConfig = machineConfig.VCatlet.Drives.FirstOrDefault(x => x.Name == ihd.Name);

                                // add a reference to image drive
                                if (vmHdConfig == null || string.IsNullOrEmpty(vmHdConfig.Template))
                                {
                                    ihd.Template = $"image:{machineConfig.VCatlet.Image}:{ihd.Name}";
                                }

                                if (vmHdConfig == null)
                                    return;

                                if (vmHdConfig.Size != 0) ihd.Size = vmHdConfig.Size;
                                if (!string.IsNullOrWhiteSpace(vmHdConfig.DataStore))
                                    ihd.DataStore = vmHdConfig.DataStore;
                                if (!string.IsNullOrWhiteSpace(vmHdConfig.Slug))
                                    ihd.Slug = vmHdConfig.Slug;
                            }
                        );

                    //add drives configured only on vm
                    var imageDriveNames = newConfig.VCatlet.Drives.Select(x => x.Name);
                    newConfig.VCatlet.Drives = newConfig.VCatlet.Drives.Append(machineConfig.VCatlet.Drives.Where(vmHd =>
                        !imageDriveNames.Any(x =>
                            string.Equals(x, vmHd.Name, StringComparison.InvariantCultureIgnoreCase)))).ToArray();

                    //merge network adapter settings configured both on image and vm config
                    newConfig.VCatlet.NetworkAdapters.ToSeq()
                        .Iter(iad =>
                            {
                                var vmAdapterConfig =
                                    machineConfig.VCatlet.NetworkAdapters.FirstOrDefault(x => x.Name == iad.Name);

                                if (vmAdapterConfig == null) return;
                                if (!string.IsNullOrWhiteSpace(vmAdapterConfig.MacAddress))
                                    iad.MacAddress = vmAdapterConfig.MacAddress;
                            }
                        );

                    //add network adapters configured only on vm
                    var imageAdapterNames = newConfig.VCatlet.NetworkAdapters.Select(x => x.Name);
                    newConfig.VCatlet.NetworkAdapters = newConfig.VCatlet.NetworkAdapters.Append(machineConfig.VCatlet.NetworkAdapters.Where(vmHd =>
                        !imageAdapterNames.Any(x =>
                            string.Equals(x, vmHd.Name, StringComparison.InvariantCultureIgnoreCase)))).ToArray();

                    //merge other settings
                    if (!string.IsNullOrWhiteSpace(machineConfig.VCatlet.DataStore))
                        newConfig.VCatlet.DataStore = machineConfig.VCatlet.DataStore;
                    if (!string.IsNullOrWhiteSpace(machineConfig.VCatlet.Slug))
                        newConfig.VCatlet.DataStore = machineConfig.VCatlet.Slug;

                    if (machineConfig.VCatlet.Cpu.Count.HasValue)
                        newConfig.VCatlet.Cpu.Count = machineConfig.VCatlet.Cpu.Count;
                    if (machineConfig.VCatlet.Memory.Maximum.HasValue)
                        newConfig.VCatlet.Memory.Maximum = machineConfig.VCatlet.Memory.Maximum;
                    if (machineConfig.VCatlet.Memory.Minimum.HasValue)
                        newConfig.VCatlet.Memory.Minimum = machineConfig.VCatlet.Memory.Minimum;
                    if (machineConfig.VCatlet.Memory.Startup.HasValue)
                        newConfig.VCatlet.Memory.Startup = machineConfig.VCatlet.Memory.Startup;


                    return Unit.Default.AsTask();
                }).Map(u => newConfig)
                .Match(
                    None: () => Prelude.RightAsync<PowershellFailure, CatletConfig>(machineConfig),
                    Some: Prelude.RightAsync<PowershellFailure, CatletConfig>).ToEither();
        }
    }
}