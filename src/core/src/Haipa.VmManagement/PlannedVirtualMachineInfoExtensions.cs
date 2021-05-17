using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines;
using Haipa.Primitives.Resources.Machines.Config;
using Haipa.VmManagement.Data.Planned;

namespace Haipa.VmManagement
{
    public static class PlannedVirtualMachineInfoExtensions
    {
        public static VirtualMachineConfig ToVmConfig(this PlannedVirtualMachineInfo plannedVM)
        {
            return new VirtualMachineConfig
            {
                Cpu = new VirtualMachineCpuConfig
                {
                    Count = (int) plannedVM.ProcessorCount
                },
                Memory = new VirtualMachineMemoryConfig
                {
                   Startup = (int) Math.Ceiling(plannedVM.MemoryStartup / 1024d / 1024),
                   Maximum = (int)Math.Ceiling(plannedVM.MemoryMaximum / 1024d / 1024),
                   Minimum = (int)Math.Ceiling(plannedVM.MemoryMinimum / 1024d / 1024)
                },

                Drives = ConvertPlannedDrivesToConfig(plannedVM),
                NetworkAdapters = ConvertImageNetAdaptersToConfig(plannedVM)
            };

        }

        private static List<VirtualMachineDriveConfig> ConvertPlannedDrivesToConfig(PlannedVirtualMachineInfo plannedVM)
        {
            var result = plannedVM.HardDrives.Select(
                hardDiskDriveInfo => new VirtualMachineDriveConfig
                {
                    Name = Path.GetFileNameWithoutExtension(hardDiskDriveInfo.Path), 
                    Size = (int) Math.Ceiling(hardDiskDriveInfo.Size / 1024d / 1024 / 1024), 
                    Type = VirtualMachineDriveType.VHD
                }).ToList();
            
            result.AddRange(plannedVM.DVDDrives.Select(dvdDriveInfo => new VirtualMachineDriveConfig
            {
                Type = VirtualMachineDriveType.DVD
            }));

            return result;
        }

        private static List<VirtualMachineNetworkAdapterConfig> ConvertImageNetAdaptersToConfig(PlannedVirtualMachineInfo plannedVM)
        {
            return plannedVM.NetworkAdapters.Select(
                adapterInfo => new VirtualMachineNetworkAdapterConfig
                {
                    Name = adapterInfo.Name
                }).ToList();

        }
    }
}