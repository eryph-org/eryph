using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Sys;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Inventory;

public interface IHardwareIdProvider
{
    string GetHardwareId();
}

public class HardwareIdProvider
{
}

public static class HardwareIdProvider<RT> where RT : struct,
    HasCancel<RT>,
    HasRegistry<RT>
{
    public static Aff<RT, Option<Guid>> ReadHardwareId() =>
        from _ in SuccessAff(unit)
        select Option<Guid>.None;

    public static Aff<RT, Guid> ReadSmBiosUuid() =>
        from wmiValue in Eff(() =>
        {
            // TODO use WMI runtime
            using var uuidSearcher = new ManagementObjectSearcher("SELECT UUId FROM Win32_ComputerSystemProduct");
            var result = uuidSearcher.Get().Cast<ManagementBaseObject>().HeadOrNone();
            return result.Bind(r => Optional(r["UUId"] as string));
        }).ToAff()
        from guid in wmiValue.Bind(parseGuid)
            .Filter(g => g != Guid.Empty && g != Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"))
            .ToAff(Error.New("The found SMBIOS UUID is not a valid GUID."))
        select guid;

    public static Aff<RT, Guid> ReadCryptographyGuid() =>
        // TODO HKLM
        from registryValue in Registry<RT>.getRegistryValue(
            @"SOFTWARE\Microsoft\Cryptography", "MachineGuid").ToAff()
        let guid = from v in registryValue
            from s in Optional(v as string)
            from g in parseGuid(s)
            select g
        from validGuid in guid.ToAff(Error.New("The Machine GUID is not a valid GUID."))
        select validGuid;

    public static Aff<RT, Guid> ReadFallbackGuid() =>
        // TODO HKLM
        from registryValue in Registry<RT>.getRegistryValue(
            @"SOFTWARE\dbosoft\Eryph", "HardwareId").ToAff()
        let guid = from v in registryValue
            from s in Optional(v as string)
            from g in parseGuid(s)
            select g
        from validGuid in guid.ToAff(Error.New("The Eryph Hardware ID is not a valid GUID."))
        from _ in SuccessAff(unit)
        select Guid.Empty;

    public static Aff<RT, Guid> EnsureFallbackGuid() =>
        from _ in SuccessAff(unit)
        select Guid.Empty;

    public static string HashGuid(Guid guid)
    {
        var hashBytes = SHA256.HashData(guid.ToByteArray());
        return Convert.ToHexString(hashBytes[..16]);
    }
}