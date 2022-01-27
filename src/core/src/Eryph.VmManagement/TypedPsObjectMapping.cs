using System;
using System.Management.Automation;
using AutoMapper;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;

namespace Eryph.VmManagement;

internal static class TypedPsObjectMapping
{
    private static IMapper _mapper;

    private static void EnsureMapper(PSObject psoObject)
    {
        if (_mapper != null)
            return;

        var powershellAssembly = psoObject.BaseObject.GetType().Assembly;

        Type GetPsType(string name)
        {
            return powershellAssembly.GetType($"Microsoft.HyperV.PowerShell.{name}", true);
        }

        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateProfile("Powershell", c =>
            {
                c.CreateMap(GetPsType("VirtualMachine"), typeof(VirtualMachineInfo));
                c.CreateMap(GetPsType("HardDiskDrive"), typeof(VirtualMachineDeviceInfo));
                c.CreateMap(GetPsType("DvdDrive"), typeof(VirtualMachineDeviceInfo));
                c.CreateMap(GetPsType(nameof(VMNetworkAdapter)), typeof(VirtualMachineDeviceInfo));

                c.CreateMap(GetPsType(nameof(VMSwitch)), typeof(VMSwitch));
                c.CreateMap(GetPsType(nameof(VMNetworkAdapter)), typeof(VMNetworkAdapter));
                c.CreateMap(GetPsType(nameof(VMNetworkAdapter)), typeof(HostVMNetworkAdapter));
                c.CreateMap(GetPsType(nameof(VMNetworkAdapterVlanSetting)), typeof(VMNetworkAdapterVlanSetting));

            });
        });

        _mapper = new Mapper(config);
    }

    public static T Map<T>(PSObject psObject)
    {
        EnsureMapper(psObject);

        // ReSharper disable once RedundantCast
        return _mapper.Map<T>((object) psObject);
    }
}