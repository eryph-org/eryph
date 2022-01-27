using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Eryph.Resources.Machines.Config;
using LanguageExt;

namespace Eryph.VmManagement
{
    public static class MachineConfigExtensions
    {
        public static Task<Either<PowershellFailure, MachineConfig>> MergeWithImageSettings(
            this MachineConfig machineConfig, Option<VirtualMachineConfig> optionalImageConfig)
        {
            if (string.IsNullOrWhiteSpace(machineConfig.Image.Name))
                return Prelude.RightAsync<PowershellFailure, MachineConfig>(machineConfig).ToEither();

            //copy machine config to a new object
            var mapper = new Mapper(new MapperConfiguration(c =>
            {
                c.CreateMap<MachineConfig, MachineConfig>();
                c.CreateMap<VirtualMachineConfig, VirtualMachineConfig>();
                c.CreateMap<VirtualMachineCpuConfig, VirtualMachineCpuConfig>();
                c.CreateMap<VirtualMachineMemoryConfig, VirtualMachineMemoryConfig>();
                c.CreateMap<VirtualMachineNetworkAdapterConfig, VirtualMachineNetworkAdapterConfig>();
                c.CreateMap<VirtualMachineDriveConfig, VirtualMachineDriveConfig>();
            }));
            var newConfig = mapper.Map<MachineConfig, MachineConfig>(machineConfig);

            return optionalImageConfig
                .Map(imageConfig =>
                {
                    //initialize machine config with image settings
                    newConfig.VM =
                        mapper.Map<VirtualMachineConfig, VirtualMachineConfig>(imageConfig);

                    //merge drive settings configured both on image and vm config
                    newConfig.VM.Drives.ToSeq()
                        .Iter(ihd =>
                            {
                                var vmHdConfig = machineConfig.VM.Drives.FirstOrDefault(x => x.Name == ihd.Name);

                                if (vmHdConfig == null) return;

                                if (vmHdConfig.Size != 0) ihd.Size = vmHdConfig.Size;
                                if (!string.IsNullOrWhiteSpace(vmHdConfig.DataStore))
                                    ihd.DataStore = vmHdConfig.DataStore;
                                if (!string.IsNullOrWhiteSpace(vmHdConfig.ShareSlug))
                                    ihd.ShareSlug = vmHdConfig.ShareSlug;
                            }
                        );

                    //add drives configured only on vm
                    var imageDriveNames = newConfig.VM.Drives.Select(x => x.Name);
                    newConfig.VM.Drives.AddRange(machineConfig.VM.Drives.Where(vmHd =>
                        !imageDriveNames.Any(x =>
                            string.Equals(x, vmHd.Name, StringComparison.InvariantCultureIgnoreCase))));

                    //merge network adapter settings configured both on image and vm config
                    newConfig.VM.NetworkAdapters.ToSeq()
                        .Iter(iad =>
                            {
                                var vmAdapterConfig =
                                    machineConfig.VM.NetworkAdapters.FirstOrDefault(x => x.Name == iad.Name);

                                if (vmAdapterConfig == null) return;
                                if (!string.IsNullOrWhiteSpace(vmAdapterConfig.MacAddress))
                                    iad.MacAddress = vmAdapterConfig.MacAddress;
                            }
                        );

                    //add network adapters configured only on vm
                    var imageAdapterNames = newConfig.VM.NetworkAdapters.Select(x => x.Name);
                    newConfig.VM.NetworkAdapters.AddRange(machineConfig.VM.NetworkAdapters.Where(vmHd =>
                        !imageAdapterNames.Any(x =>
                            string.Equals(x, vmHd.Name, StringComparison.InvariantCultureIgnoreCase))));

                    //merge other settings
                    if (!string.IsNullOrWhiteSpace(machineConfig.VM.DataStore))
                        newConfig.VM.DataStore = machineConfig.VM.DataStore;
                    if (!string.IsNullOrWhiteSpace(machineConfig.VM.Slug))
                        newConfig.VM.DataStore = machineConfig.VM.Slug;

                    if (machineConfig.VM.Cpu.Count.HasValue)
                        newConfig.VM.Cpu.Count = machineConfig.VM.Cpu.Count;
                    if (machineConfig.VM.Memory.Maximum.HasValue)
                        newConfig.VM.Memory.Maximum = machineConfig.VM.Memory.Maximum;
                    if (machineConfig.VM.Memory.Minimum.HasValue)
                        newConfig.VM.Memory.Minimum = machineConfig.VM.Memory.Minimum;
                    if (machineConfig.VM.Memory.Startup.HasValue)
                        newConfig.VM.Memory.Startup = machineConfig.VM.Memory.Startup;


                    return Unit.Default.AsTask();
                }).Map(u => newConfig)
                .Match(
                    None: () => Prelude.RightAsync<PowershellFailure, MachineConfig>(machineConfig),
                    Some: Prelude.RightAsync<PowershellFailure, MachineConfig>).ToEither();
        }
    }
}