using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.PowerShell.Commands;
using static LanguageExt.Prelude;
using static Eryph.VmManagement.Wmi.WmiUtils;
using Eryph.VmManagement.Wmi;

namespace Eryph.VmManagement.Sys;

public static class WmiQueries<RT> where RT: struct, HasWmi<RT>
{
    public static Eff<RT, Seq<(string Name, bool IsInstalled)>> getFeatures() =>
        from queryResult in Wmi<RT>.executeQuery(
            @"\Root\CIMv2",
            Seq("Name", "InstallState"),
            "Win32_OptionalFeature",
            None)
        from features in queryResult
            .Map(f => 
                from name in getRequiredValue<string>(f, "Name")
                from installState in getRequiredValue<uint>(f, "InstallState")
                let isInstalled = installState == 1
                select Some((Name: name, IsInstalled: isInstalled)))
            // Ignore results which could not parsed in case WMI returns invalid data.
            .Map(r => r.IfFail(_ => None))
            .Sequence()
        select features.Somes();

    public static Eff<RT, (string DataRootPath, string VhdPath)> getHyperVDefaultPaths() =>
        from queryResult in Wmi<RT>.executeQuery(
            @"\Root\Virtualization\v2",
            Seq("DefaultExternalDataRoot", "DefaultVirtualHardDiskPath"),
            "Msvm_VirtualSystemManagementServiceSettingData",
            None)
        from settings in queryResult.HeadOrNone()
            .ToEff(Error.New("Failed to query for Hyper-V host settings."))
        from dataRootPath in getRequiredValue<string>(settings, "DefaultExternalDataRoot")
            .MapFail(e => Error.New("Failed to lookup the Hyper-V setting 'DefaultExternalDataRoot'.", e))
        from vhdPath in getRequiredValue<string>(settings, "DefaultVirtualHardDiskPath")
            .MapFail(e => Error.New("Failed to lookup the Hyper-V setting 'DefaultVirtualHardDiskPath'.", e))
        select (DataRootPath: dataRootPath, VhdPath: vhdPath);

    /// <summary>
    /// Finds the ID of the worker process of the Hyper-V VM with
    /// the given <paramref name="vmId"/>. The result will be
    /// <see cref="None"/> when the VM is not running and hence
    /// has no worker process.
    /// </summary>
    public static Eff<RT, Option<uint>> getVmProcessId(Guid vmId) =>
        from queryResult in Wmi<RT>.executeQuery(
            @"\Root\Virtualization\v2",
            Seq1("ProcessID"),
            "Msvm_ComputerSystem",
            $"Name = '{vmId}'")
        from vm in queryResult.HeadOrNone()
            .ToEff(Error.New($"Could not find the VM '{vmId}'"))
        from processId in getValue<uint>(vm, "ProcessID")
        from _ in processId
            // Process IDs <=4 are reserved for low-level system processes.
            .Map(pid => guard(pid > 4, Error.New($"The process ID {pid} of the Hyper-V VM {vmId} is invalid."))
                .ToEff())
            .Sequence()
        select processId;
}
