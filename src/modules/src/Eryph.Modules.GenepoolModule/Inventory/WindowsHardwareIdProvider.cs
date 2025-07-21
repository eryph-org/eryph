using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Sys;
using Eryph.Modules.Genepool;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement.Inventory;
using Eryph.VmManagement.Sys;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Genepool.Inventory;

public class WindowsHardwareIdProvider : IHardwareIdProvider
{
    public WindowsHardwareIdProvider(
        Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
    {
        var result = WindowsHardwareIdProvider<WindowsGenepoolRuntime>
            .EnsureHardwareId()
            .Run(WindowsGenepoolRuntime.New(loggerFactory));
        
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
        @catch(ex => Logger<RT>.logWarning<WindowsHardwareIdProvider>(ex, message)
            .Bind(_ => FailEff<Guid>(ex)));
}
