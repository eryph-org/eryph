using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.VmManagement.Data.Planned;
using Eryph.VmManagement.Storage;
using LanguageExt;

namespace Eryph.VmManagement
{
    public static class PlannedVirtualMachineInfoExtensions
    {
        public static async Task<VirtualCatletConfig> ToVmConfig(this TypedPsObject<PlannedVirtualMachineInfo> plannedVM, IPowershellEngine engine, string imageName)
        {
            return new VirtualCatletConfig
            {
                Image = imageName,
                Cpu = new VirtualCatletCpuConfig
                {
                    Count = (int) plannedVM.Value.ProcessorCount
                },
                Memory = new VirtualCatletMemoryConfig
                {
                    Startup = (int) Math.Ceiling(plannedVM.Value.MemoryStartup / 1024d / 1024),
                    //max and min memory is not imported from template
                },

                Drives = (await ConvertPlannedDrivesToConfig(engine, plannedVM)).ToArray(),
                NetworkAdapters = ConvertImageNetAdaptersToConfig(plannedVM).ToArray()
            };
        }

        private static async Task<IEnumerable<VirtualCatletDriveConfig>> ConvertPlannedDrivesToConfig(IPowershellEngine engine, 
            TypedPsObject<PlannedVirtualMachineInfo> plannedVM)
        {
            var result = await plannedVM.GetList(x => x.HardDrives)
                .Map(x =>

                    from plannedDrive in x.CastSafe<PlannedHardDiskDriveInfo>().ToAsync().ToError()
                    from optionalDrive in VhdQuery.GetVhdInfo(engine, plannedDrive.Value.Path).ToAsync().ToError()
                    select new VirtualCatletDriveConfig
                    {
                        Name = Path.GetFileNameWithoutExtension(plannedDrive.Value.Path),
                        Size = optionalDrive.Match(None: () => 0,
                            Some: d => (int)Math.Ceiling(d.Value.Size / 1024d / 1024 / 1024)),
                        Type = VirtualCatletDriveType.VHD
                    }).TraverseSerial(l => l)
                .IfLeft(l => Enumerable.Empty<VirtualCatletDriveConfig>().ToSeq()).Map(s => s.ToList());

            result.AddRange(plannedVM.GetList(x=>x.DVDDrives).Select(
                _ => new VirtualCatletDriveConfig
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