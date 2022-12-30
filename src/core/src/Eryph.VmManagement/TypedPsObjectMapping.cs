using System;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using AutoMapper;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Data.Planned;

namespace Eryph.VmManagement;

internal static class TypedPsObjectMapping
{
    private static IMapper _mapper;

    private static void EnsureMapper()
    {
        if (_mapper != null)
            return;

        Assembly powershellAssembly = null;

        Type GetPsType(string name)
        {
            if (powershellAssembly != null)
                return powershellAssembly.GetType($"Microsoft.HyperV.PowerShell.{name}", true);
            foreach (var hvAssembly in AppDomain.CurrentDomain.GetAssemblies()
                         .Where(a => a.FullName != null && a.FullName.Contains("HyperV")))
            {
                var tt = hvAssembly.GetType($"Microsoft.HyperV.PowerShell.{name}", false);
                if (tt == null) continue;
                powershellAssembly = hvAssembly;
                return tt;
            }

            throw new InvalidOperationException("could not find Hyper-V powershell objects");
        }

        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateProfile("HyperV", c =>
            {
                c.CreateMap(GetPsType("VirtualMachine"), typeof(VirtualMachineInfo));
                c.CreateMap(GetPsType("VirtualMachine"), typeof(PlannedVirtualMachineInfo));

                c.CreateMap(GetPsType("HardDiskDrive"), typeof(PlannedHardDiskDriveInfo))
                    .ForMember("Size", m => m.Ignore());

                c.CreateMap(GetPsType("HardDiskDrive"), typeof(VirtualMachineDeviceInfo));
                c.CreateMap(GetPsType("DvdDrive"), typeof(VirtualMachineDeviceInfo));
                c.CreateMap(GetPsType(nameof(VMNetworkAdapter)), typeof(VirtualMachineDeviceInfo));

                c.CreateMap(GetPsType(nameof(VMSwitch)), typeof(VMSwitch));
                c.CreateMap(GetPsType(nameof(VMNetworkAdapter)), typeof(VMNetworkAdapter));
                c.CreateMap(GetPsType(nameof(VMNetworkAdapter)), typeof(PlannedVMNetworkAdapter));
                c.CreateMap(GetPsType(nameof(VMNetworkAdapter)), typeof(HostVMNetworkAdapter));
                c.CreateMap(GetPsType(nameof(VMNetworkAdapterVlanSetting)), typeof(VMNetworkAdapterVlanSetting));


                c.CreateMap(GetPsType("VMProcessor"), typeof(VirtualMachineCpuData));
                c.CreateMap(GetPsType("VMMemory"), typeof(VirtualMachineMemoryData));
                c.CreateMap(GetPsType("VMFirmware"), typeof(VMFirmwareInfo));

            });
        });
        config.CompileMappings();

        _mapper = new Mapper(config);
    }

    public static T Map<T>(PSObject psObject)
    {
        EnsureMapper();

        // ReSharper disable once RedundantCast
        return _mapper.Map<T>((object) psObject);
    }
}