using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Eryph.Core.Sys;
using Eryph.VmManagement;
using Eryph.VmManagement.Sys;
using Eryph.VmManagement.Wmi;
using LanguageExt;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool.Inventory;

[SupportedOSPlatform("windows")]
public class WindowsHardwareIdProvider : IHardwareIdProvider
{
    public WindowsHardwareIdProvider(
        ILoggerFactory loggerFactory)
    {
        var result = WindowsHardwareIdProvider<WindowsGenePoolRuntime>
            .EnsureHardwareId()
            .Run(WindowsGenePoolRuntime.New(loggerFactory));

        // We cache the hardware ID as it should obviously not change
        // and the lookup requires WMI and registry queries.
        HardwareId = result.ThrowIfFail();
        HashedHardwareId = HashHardwareId(HardwareId);
    }

    public Guid HardwareId { get; }

    public string HashedHardwareId { get; }

    private static string HashHardwareId(Guid hardwareId)
    {
        var hashBytes = SHA256.HashData(hardwareId.ToByteArray());
        return Convert.ToHexString(hashBytes[..16]).ToLowerInvariant();
    }
}

[SupportedOSPlatform("windows")]
internal static class WindowsHardwareIdProvider<RT> where RT : struct,
    HasLogger<RT>,
    HasRegistry<RT>,
    HasWmi<RT>
{
    public static Eff<RT, Guid> EnsureHardwareId() =>
        from guid in HardwareIdQueries<RT>.readSmBiosUuid()
                     | logAndContinue("Could not read the SMBIOS UUID")
                     | HardwareIdQueries<RT>.readCryptographyGuid()
                     | logAndContinue("Could not read the Cryptography GUID")
                     | HardwareIdQueries<RT>.ensureFallbackGuid()
        select guid;

    private static EffCatch<RT, Guid> logAndContinue(string message) =>
        @catch(ex => VmManagement.Sys.Logger<RT>.logWarning<WindowsHardwareIdProvider>(ex, message)
            .Bind(_ => FailEff<Guid>(ex)));
}
