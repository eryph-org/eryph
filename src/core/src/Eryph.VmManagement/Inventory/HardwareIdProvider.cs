using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
    Guid HardwareId { get; }

    string HashedHardwareId { get; }
}

public class HardwareIdProvider : IHardwareIdProvider
{
    private readonly Guid _hardwareId;
    private readonly string _hashedHardwareId;

    public HardwareIdProvider()
    {
        _hardwareId = Guid.NewGuid();
        _hashedHardwareId = HardwareIdHasher.HashHardwareId(_hardwareId);
    }

    public Guid HardwareId => _hardwareId;

    public string HashedHardwareId => _hashedHardwareId;
}

public static class HardwareIdProvider<RT> where RT : struct,
    HasCancel<RT>,
    HasRegistry<RT>
{
    public static Aff<RT, Option<Guid>> ReadHardwareId() =>
        from _ in SuccessAff(unit)
        select Option<Guid>.None;

    public static Aff<RT, Guid> EnsureHardwareId() =>
        // TODO Add catch for logging
        from guid in ReadSmBiosUuid()
                     | ReadCryptographyGuid()
                     | ReadFallbackGuid()
        select guid;

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
        from value in Registry<RT>.getRegistryValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid").ToAff()
        from validValue in value.ToAff(Error.New("Could not read the Machine GUID from the registry."))
        let guid = from s in Optional(validValue as string)
                   from g in parseGuid(s)
                   select g
        from validGuid in guid.ToAff(Error.New("The Machine GUID is not a valid GUID."))
        select validGuid;

    public static Aff<RT, Guid> ReadFallbackGuid() =>
        from value in Registry<RT>.getRegistryValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\dbosoft\Eryph", "HardwareId").ToAff()
        from validValue in value.ToAff(Error.New("Could not read the Eryph Hardware ID from the registry."))
        let guid = from s in Optional(validValue as string)
                   from g in parseGuid(s)
                   select g
        from validGuid in guid.ToAff(Error.New("The Eryph Hardware ID is not a valid GUID."))
        select validGuid;

    public static Aff<RT, Guid> EnsureFallbackGuid() =>
        from _ in SuccessAff(unit)
        select Guid.Empty;
}

public static class HardwareIdHasher
{
    public static string HashHardwareId(Guid hardwareId)
    {
        var hashBytes = SHA256.HashData(hardwareId.ToByteArray());
        return Convert.ToHexString(hashBytes[..16]);
    }
}
