using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eryph.ConfigModel.Catlets;
using Eryph.VmManagement.Data.Planned;

namespace Eryph.VmManagement
{
    public static class PlannedVirtualMachineInfoExtensions
    {
        public static VirtualCatletConfig ToVmConfig(this TypedPsObject<PlannedVirtualMachineInfo> plannedVM)
        {
            return new VirtualCatletConfig
            {
                Cpu = new VirtualCatletCpuConfig
                {
                    Count = (int) plannedVM.Value.ProcessorCount
                },
                Memory = new VirtualCatletMemoryConfig
                {
                    Startup = (int) Math.Ceiling(plannedVM.Value.MemoryStartup / 1024d / 1024),
                    //max and min memory is not imported from template
                },

                Drives = ConvertPlannedDrivesToConfig(plannedVM).ToArray(),
                NetworkAdapters = ConvertImageNetAdaptersToConfig(plannedVM).ToArray()
            };
        }

        private static IEnumerable<VirtualCatletDriveConfig> ConvertPlannedDrivesToConfig(TypedPsObject<PlannedVirtualMachineInfo> plannedVM)
        {
            var result = plannedVM.GetList(x=>x.HardDrives)
                .Map(x=>x.Cast<PlannedHardDiskDriveInfo>().Value).Select(
                hardDiskDriveInfo => new VirtualCatletDriveConfig
                {
                    Name = Path.GetFileNameWithoutExtension(hardDiskDriveInfo.Path),
                    Size = (int) Math.Ceiling(hardDiskDriveInfo.Size / 1024d / 1024 / 1024),
                    Type = VirtualCatletDriveType.VHD
                }).ToList();

            result.AddRange(plannedVM.GetList(x=>x.DVDDrives).Select(
                dvdDriveInfo => new VirtualCatletDriveConfig
            {
                Type = VirtualCatletDriveType.DVD
            }));

            return result;
        }

        private static IEnumerable<VirtualCatletNetworkAdapterConfig> ConvertImageNetAdaptersToConfig(
            PlannedVirtualMachineInfo plannedVM)
        {
            return plannedVM.NetworkAdapters.Select(
                adapterInfo => new VirtualCatletNetworkAdapterConfig
                {
                    Name = adapterInfo.Name
                }).ToList();
        }
    }
}