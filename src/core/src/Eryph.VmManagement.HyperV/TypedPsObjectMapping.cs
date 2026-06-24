using System;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Threading;
using AutoMapper;
using Eryph.Core;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Data.Planned;
using Eryph.VmManagement.Data.unused;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eryph.VmManagement;

public class TypedPsObjectMapping(ILogger logger) : ITypedPsObjectMapping
{
    private readonly Lock _syncRoot = new();
    private IMapper? _mapper;


    public T? Map<T>(PSObject psObject)
    {
        EnsureMapper();

        try
        {
            // special case for bool as it is not mapped correctly
            if (typeof(T) == typeof(bool))
                return psObject.BaseObject is bool b ? (T)(object)b : default;

            // special case for byte array
            if (typeof(T) == typeof(byte[]))
                return psObject.BaseObject is byte[] b ? (T)(object)b : default;

            // cast is required to map correctly
            // ReSharper disable once RedundantCast
            var mapper = _mapper ?? throw new InvalidOperationException("Mapper not initialized");
            return mapper.Map<T>((object)psObject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to map powershell object {@psObject}", psObject);
            throw;
        }
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

                return assembliesFound.Length == 0 ? throw new InvalidOperationException("could not find Hyper-V powershell objects") : assembliesFound;
            }

            var config = new MapperConfiguration(cfg =>
            {
                cfg.LicenseKey = AutoMapperLicense.Key;
                cfg.CreateProfile("Powershell", c => { c.CreateMap<CommandInfo, PowershellCommand>(); });

                cfg.CreateProfile("HyperV", c =>
                {
                    var hyperVAssemblies = GetHyperVAssemblies();

                    logger.LogTrace("HyperV assemblies found: {names}",
                        string.Join(',', hyperVAssemblies.Select(x => x.GetName())));

                    var assemblyNames = hyperVAssemblies.Select(x => x.GetName().Name).ToArray();

                    // if both Microsoft.HyperV.PowerShell.Objects and Microsoft.HyperV.PowerShell
                    // have been loaded prefer Microsoft.HyperV.PowerShell.Objects
                    if (assemblyNames.Length > 1 && assemblyNames.Contains("Microsoft.HyperV.PowerShell.Objects") &&
                        assemblyNames.Contains("Microsoft.HyperV.PowerShell"))
                    {
                        logger.LogTrace(
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
                        c.AddHyperVMapping<VhdInfo>(hyperVAssembly, "VirtualHardDisk");
                        c.AddHyperVMapping<VMSystemSwitchExtension>(hyperVAssembly, "VMSystemSwitchExtension");
                    }

                    c.IgnoreUnmapped(logger);
                });

                cfg.CreateProfile("Dism", c =>
                {
                    var assembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.FullName?.Contains("Microsoft.Dism.PowerShell") ?? false);
                    var assemblyType = assembly?.GetType("Microsoft.Dism.Commands.BasicDriverObject", false);
                    if (assemblyType is null)
                        return;

                    c.CreateMap(assemblyType, typeof(DismDriverInfo));
                    c.IgnoreUnmapped(logger);
                });

                cfg.CreateProfile("NetTCPIP", c =>
                {
                    c.AddCimInstanceMapping<CimNetworkNeighbor>();
                    c.IgnoreUnmapped(logger);
                });

                cfg.CreateProfile("HgsClient", c =>
                {
                    c.AddCimInstanceMapping<CimHgsGuardian>();
                    c.AddCimInstanceMapping<CimHgsKeyProtector>();
                    c.IgnoreUnmapped(logger);
                });
            }, NullLoggerFactory.Instance);

            try
            {
                config.AssertConfigurationIsValid();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Invalid HyperV mappings found.");
            }

            _mapper = new Mapper(config);
        }
    }
}
