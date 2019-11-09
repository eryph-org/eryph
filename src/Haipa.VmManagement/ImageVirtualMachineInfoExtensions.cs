using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haipa.VmConfig;
using Haipa.VmManagement.Data;

namespace Haipa.Modules.VmHostAgent
{
    public static class ImageVirtualMachineInfoExtensions
    {
        public static VirtualMachineConfig ToVmConfig(this ImageVirtualMachineInfo imageInfo)
        {
            return new VirtualMachineConfig
            {
                Cpu = new VirtualMachineCpuConfig
                {
                    Count = (int) imageInfo.ProcessorCount
                },
                Memory = new VirtualMachineMemoryConfig
                {
                   Startup = (int) Math.Ceiling(imageInfo.MemoryStartup / 1024d / 1024),
                   Maximum = (int)Math.Ceiling(imageInfo.MemoryMaximum / 1024d / 1024),
                   Minimum = (int)Math.Ceiling(imageInfo.MemoryMinimum / 1024d / 1024)
                },

                Drives = ConvertImageDrivesToConfig(imageInfo),
                NetworkAdapters = ConvertImageNetAdaptersToConfig(imageInfo)
            };

        }

        private static List<VirtualMachineDriveConfig> ConvertImageDrivesToConfig(ImageVirtualMachineInfo imageInfo)
        {
            var result = imageInfo.HardDrives.Select(
                hardDiskDriveInfo => new VirtualMachineDriveConfig
                {
                    Name = Path.GetFileNameWithoutExtension(hardDiskDriveInfo.Path), 
                    Size = (int) Math.Ceiling(hardDiskDriveInfo.Size / 1024d / 1024 / 1024), 
                    Type = VirtualMachineDriveType.VHD
                }).ToList();
            
            result.AddRange(imageInfo.DVDDrives.Select(dvdDriveInfo => new VirtualMachineDriveConfig
            {
                Type = VirtualMachineDriveType.DVD
            }));

            return result;
        }

        private static List<VirtualMachineNetworkAdapterConfig> ConvertImageNetAdaptersToConfig(ImageVirtualMachineInfo imageInfo)
        {
            return imageInfo.NetworkAdapters.Select(
                adapterInfo => new VirtualMachineNetworkAdapterConfig
                {
                    Name = adapterInfo.Name,
                    MacAddress = !adapterInfo.DynamicMacAddressEnabled ? adapterInfo.MacAddress: null,
                    SwitchName = adapterInfo.SwitchName
                }).ToList();

        }
    }
}