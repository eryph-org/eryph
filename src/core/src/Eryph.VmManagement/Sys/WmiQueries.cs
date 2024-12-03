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
    public static Eff<RT, Seq<(string Name, bool IsInstalled)>> getFeatures() =>
        from queryResult in Wmi<RT>.executeQuery(
            @"\Root\CIMv2",
            Seq("Name", "InstallState"),
            "Win32_OptionalFeature",
            None)
        let features = queryResult.Map(f =>
            from name in f.Find("Name")
                .Flatten()
                .Bind(v => Optional(v as string))
            from installState in f.Find("InstallState")
                .Flatten()
                .Bind(v => Optional(v as uint?))
            let isInstalled = installState == 1
            select (Name: name, IsInstalled: isInstalled))
            .ToList()
        select features.ToSeq().Somes();

    public static Eff<RT, (string DataRootPath, string VhdPath)> getHyperVDefaultPaths() =>
        from queryResult in Wmi<RT>.executeQuery(
            @"\Root\Virtualization\v2",
            Seq("DefaultExternalDataRoot", "DefaultVirtualHardDiskPath"),
            "Msvm_VirtualSystemManagementServiceSettingData",
            None)
        from settings in queryResult.HeadOrNone()
            .ToEff(Error.New("Failed to query for Hyper-V host settings."))
        from dataRootPath in settings.Find("DefaultExternalDataRoot")
            .Flatten()
            .Bind(v => Optional(v as string))
            .ToEff(Error.New("Failed to lookup the Hyper-V setting 'DefaultExternalDataRoot'."))
        from vhdPath in settings.Find("DefaultVirtualHardDiskPath")
            .Flatten()
            .Bind(v => Optional(v as string))
            .ToEff(Error.New("Failed to lookup the Hyper-V setting 'DefaultVirtualHardDiskPath'."))
        select (DataRootPath: dataRootPath, VhdPath: vhdPath);
}
