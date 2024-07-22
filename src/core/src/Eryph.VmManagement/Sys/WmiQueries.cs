using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Sys;

public static class WmiQueries<RT> where RT: struct, HasWmi<RT>
{
    public static Eff<RT, Seq<(string Name, bool IsInstalled )>> getFeatures() =>
        from scope in Wmi<RT>.createScope(@"\Root\CIMv2")
        from queryResult in Wmi<RT>.executeQuery(
            scope,
            "SELECT Name, InstallState FROM Win32_OptionalFeature")
        let features = queryResult.Map(f =>
            from name in Try(() => f.GetPropertyValue("Name"))
                .ToOption()
                .Map(v => v as string)
            from installState in Try(() => f.GetPropertyValue("InstallState"))
                .ToOption()
                .Map(v => v as uint?)
            let isInstalled = installState == 1
            select (Name: name, IsInstalled: isInstalled))
            .ToList()
        select features.ToSeq().Somes();

    public static Eff<RT, (string DataRootPath, string VhdPath)> getHyperVDefaultPaths() =>
        from scope in Wmi<RT>.createScope(@"\Root\Virtualization\v2")
        from queryResult in Wmi<RT>.executeQuery(
            scope,
            "SELECT DefaultExternalDataRoot, DefaultVirtualHardDiskPath FROM Msvm_VirtualSystemManagementServiceSettingData")
        from settings in queryResult.HeadOrNone()
            .ToEff(Error.New("failed to query for hyper-v host settings"))
        from dataRootPath in Try(() => settings.GetPropertyValue("DefaultExternalDataRoot"))
            .ToOption()
            .Map(v => v as string)
            .ToEff(Error.New("Failed to lookup the Hyper-V setting DefaultExternalDataRoot"))
        from vhdPath in Try(() => settings.GetPropertyValue("DefaultVirtualHardDiskPath"))
            .ToOption()
            .Map(v => v as string)
            .ToEff(Error.New("Failed to lookup the Hyper-V setting DefaultVirtualHardDiskPath"))
        select (DataRootPath: dataRootPath, VhdPath: vhdPath);
}
