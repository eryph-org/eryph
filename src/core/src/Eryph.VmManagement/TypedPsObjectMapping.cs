using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using AutoMapper;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Data.Planned;
using Microsoft.Extensions.Logging;

namespace Eryph.VmManagement;

public class TypedPsObjectMapping : ITypedPsObjectMapping
{
    private IMapper _mapper;
    private readonly ILogger _logger;
    private object _syncRoot = new();

    public TypedPsObjectMapping(ILogger logger)
    {
        _logger = logger;
    }

    private void EnsureMapper()
    {
        lock (_syncRoot)
        {

            if (_mapper != null)
                return;

            static Assembly[] GetHyperVAssemblies()
            {
                var assembliesFound = (from hvAssembly in AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => a.FullName != null && a.FullName.Contains("HyperV"))
                    let tt = hvAssembly.GetType("Microsoft.HyperV.PowerShell.VMFirmware", false)
                    where tt != null
                    select hvAssembly).ToArray();

                if (assembliesFound.Length == 0)
                {
                    throw new InvalidOperationException("could not find Hyper-V powershell objects");
                }

                return assembliesFound;

            }

            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateProfile("Powershell", c =>
                {
                    c.CreateMap<CommandInfo, PowershellCommand>();
                });

                cfg.CreateProfile("HyperV", c =>
                {
                    var hyperVAssemblies = GetHyperVAssemblies();

                    _logger.LogTrace("HyperV assemblies found: {names}",
                        string.Join(',', hyperVAssemblies.Select(x => x.GetName())));

                    var assemblyNames = hyperVAssemblies.Select(x => x.GetName().Name).ToArray();

                    // if both Microsoft.HyperV.PowerShell.Objects and Microsoft.HyperV.PowerShell
                    // have been loaded prefer Microsoft.HyperV.PowerShell.Objects
                    if (assemblyNames.Length > 1 && assemblyNames.Contains("Microsoft.HyperV.PowerShell.Objects") &&
                        assemblyNames.Contains("Microsoft.HyperV.PowerShell"))
                    {
                        _logger.LogTrace(
                            "Assembly Microsoft.HyperV.PowerShell.Objects found - ignoring Microsoft.HyperV.PowerShell assembly.");
                        hyperVAssemblies = hyperVAssemblies
                            .Where(x => x.GetName().Name != "Microsoft.HyperV.PowerShell")
                            .ToArray();
                    }

                    //map types for all assemblies
                    //currently microsoft adds new features to Microsoft.HyperV.PowerShell.Objects
                    //but to be safe map all assemblies (unmatched properties will be Ignored, but logged)
                    foreach (var hyperVAssembly in hyperVAssemblies)
                    {
                        c.AddHyperVMapping<VMCompatibilityReportInfo>(hyperVAssembly, "VMCompatibilityReport");
                        c.AddHyperVMapping<VirtualMachineInfo>(hyperVAssembly, "VirtualMachine");
                        c.AddHyperVMapping<PlannedVirtualMachineInfo>(hyperVAssembly, "VirtualMachine");
                        c.AddHyperVMapping<PlannedHardDiskDriveInfo>(hyperVAssembly, "HardDiskDrive",
                            map => map.ForMember("Size", m => m.Ignore()));
                        c.AddHyperVMapping<VirtualMachineDeviceInfo>(hyperVAssembly, "HardDiskDrive");
                        c.AddHyperVMapping<VirtualMachineDeviceInfo>(hyperVAssembly, "DvdDrive");
                        c.AddHyperVMapping<VirtualMachineDeviceInfo>(hyperVAssembly, nameof(VMNetworkAdapter));

                        c.AddHyperVMapping<VMNetworkAdapter>(hyperVAssembly, nameof(VMNetworkAdapter));
                        c.AddHyperVMapping<PlannedVMNetworkAdapter>(hyperVAssembly, nameof(VMNetworkAdapter));

                        c.AddHyperVMapping<VMNetworkAdapterVlanSetting>(hyperVAssembly,
                            nameof(VMNetworkAdapterVlanSetting));
                        c.AddHyperVMapping<VirtualMachineCpuData>(hyperVAssembly, "VMProcessor");
                        c.AddHyperVMapping<VirtualMachineMemoryData>(hyperVAssembly, "VMMemory");
                        c.AddHyperVMapping<VMFirmwareInfo>(hyperVAssembly, "VMFirmware");

                        c.AddHyperVMapping<VMSwitchExtension>(hyperVAssembly, "VMSwitchExtension");
                        
                    }

                    c.IgnoreUnmapped(_logger);
                });
            });

            try
            {
                config.AssertConfigurationIsValid();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid HyperV mappings found.");
            }

            _mapper = new Mapper(config);
        }
    }


    public T Map<T>(PSObject psObject)
    {
        EnsureMapper();

        try
        {
            // cast is required to map correctly
            // ReSharper disable once RedundantCast
            return _mapper.Map<T>((object) psObject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to map powershell object {@psObject}", psObject);
            throw;
        }
    }
}