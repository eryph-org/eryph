using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eryph.Resources.Machines;
using Eryph.Resources.Machines.Config;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Planned;

namespace Eryph.VmManagement
{
    public static class PlannedVirtualMachineInfoExtensions
    {
        public static VirtualMachineConfig ToVmConfig(this TypedPsObject<PlannedVirtualMachineInfo> plannedVM)
        {
            return new VirtualMachineConfig
            {
                Cpu = new VirtualMachineCpuConfig
                {
                    Count = (int) plannedVM.Value.ProcessorCount
                },
                Memory = new VirtualMachineMemoryConfig
                {
                    Startup = (int) Math.Ceiling(plannedVM.Value.MemoryStartup / 1024d / 1024),
                    Maximum = (int) Math.Ceiling(plannedVM.Value.MemoryMaximum / 1024d / 1024),
                    Minimum = (int) Math.Ceiling(plannedVM.Value.MemoryMinimum / 1024d / 1024)
                },

                Drives = ConvertPlannedDrivesToConfig(plannedVM),
                NetworkAdapters = ConvertImageNetAdaptersToConfig(plannedVM)
            };
        }

        private static List<VirtualMachineDriveConfig> ConvertPlannedDrivesToConfig(TypedPsObject<PlannedVirtualMachineInfo> plannedVM)
        {
            var result = plannedVM.GetList(x=>x.HardDrives)
                .Map(x=>x.Cast<PlannedHardDiskDriveInfo>().Value).Select(
                hardDiskDriveInfo => new VirtualMachineDriveConfig
                {
                    Name = Path.GetFileNameWithoutExtension(hardDiskDriveInfo.Path),
                    Size = (int) Math.Ceiling(hardDiskDriveInfo.Size / 1024d / 1024 / 1024),
                    Type = VirtualMachineDriveType.VHD
                }).ToList();

            result.AddRange(plannedVM.GetList(x=>x.DVDDrives).Select(
                dvdDriveInfo => new VirtualMachineDriveConfig
            {
                Type = VirtualMachineDriveType.DVD
            }));

            return result;
        }

        private static List<VirtualMachineNetworkAdapterConfig> ConvertImageNetAdaptersToConfig(
            PlannedVirtualMachineInfo plannedVM)
        {
            return plannedVM.NetworkAdapters.Select(
                adapterInfo => new VirtualMachineNetworkAdapterConfig
                {
                    Name = adapterInfo.Name
                }).ToList();
        }
    }
}