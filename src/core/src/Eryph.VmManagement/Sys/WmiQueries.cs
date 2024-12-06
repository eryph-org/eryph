using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;
using static Eryph.VmManagement.Wmi.WmiUtils;

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
}
