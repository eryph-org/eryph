using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Sys;
using Eryph.VmManagement.Sys;
using Eryph.VmManagement.Wmi;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;
using static Eryph.VmManagement.Wmi.WmiUtils;

namespace Eryph.VmManagement.Inventory;

public static class HardwareIdQueries<RT> where RT : struct, HasRegistry<RT>, HasWmi<RT>
{
    public static Eff<RT, Guid> readSmBiosUuid() =>
        from queryResult in Wmi<RT>.executeQuery(
            @"\root\CIMv2",
            Seq1("UUID"),
            "Win32_ComputerSystemProduct",
            None)
        from product in queryResult.HeadOrNone()
            .ToEff(Error.New("Failed to query Win32_ComputerSystemProduct."))
        from guid in getSmBiosUuid(product)
        select guid;

    private static Eff<RT, Guid> getSmBiosUuid(WmiObject wmiObject) =>
        from uuidValue in getRequiredValue<string>(wmiObject, "UUID")
            .MapFail(e => Error.New("WMI did not return the SMBIOS UUID."))
        from guid in parseGuid(uuidValue)
            .ToEff("The found SMBIOS UUID is not a valid GUID.")
        // According to SMBIOS specification, both all 0s and all 1s (0xFF)
        // indicate that the UUID is not set.
        from _ in guard(guid != Guid.Empty && guid != Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"),
            Error.New("The SMBIOS UUID is not set."))
        select guid;

    public static Eff<RT, Guid> readCryptographyGuid() =>
        from value in Registry<RT>.getRegistryValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid")
        from validValue in value.ToEff(Error.New("Could not read the Machine GUID from the registry."))
        let guid = from s in Optional(validValue as string)
            from g in parseGuid(s)
            select g
        from validGuid in guid.ToEff(Error.New("The Machine GUID is not a valid GUID."))
        select validGuid;

    public static Eff<RT, Guid> ensureFallbackGuid() =>
        from existingValue in Registry<RT>.getRegistryValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\dbosoft\Eryph", "HardwareId")
        let existingGuid = from v in existingValue
                           from s in Optional(v as string)
                           from g in parseGuid(s)
                           select g
        from guid in existingGuid.Match(
            Some: SuccessEff<RT, Guid>,
            None: from newGuid in SuccessEff<RT, Guid>(Guid.NewGuid())
                  from _ in Registry<RT>.writeRegistryValue(
                      @"HKEY_LOCAL_MACHINE\SOFTWARE\dbosoft\Eryph", "HardwareId",
                      newGuid.ToString())
                  select newGuid)
        select guid;
}
