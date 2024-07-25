using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Sys;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement.Inventory;
using LanguageExt;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Inventory;

internal class HardwareIdProvider : IHardwareIdProvider
{
    private readonly Guid _hardwareId;
    private readonly string _hashedHardwareId;

    public HardwareIdProvider(ILoggerFactory loggerFactory)
    {
        var result = HardwareIdProvider<SimpleAgentRuntime>
            .EnsureHardwareId()
            .Run(SimpleAgentRuntime.New(loggerFactory));
        
        // We cache the hardware ID as it should obviously not change
        // and the lookup requires WMI and registry queries.
        _hardwareId = result.ThrowIfFail();
        _hashedHardwareId = HardwareIdHasher.HashHardwareId(_hardwareId);
    }

    public Guid HardwareId => _hardwareId;

    public string HashedHardwareId => _hashedHardwareId;
}

internal static class HardwareIdProvider<RT> where RT : struct, HasLogger<RT>, HasRegistry<RT>
{
    public static Eff<RT, Option<Guid>> ReadHardwareId() =>
        from _ in SuccessEff(unit)
        select Option<Guid>.None;

    public static Eff<RT, Guid> EnsureHardwareId() =>
        // TODO Add catch for logging
        from guid in HardwareIdQueries<RT>.ReadSmBiosUuid()
                     | HardwareIdQueries<RT>.ReadCryptographyGuid()
                     | HardwareIdQueries<RT>.ReadFallbackGuid()
        select guid;
}