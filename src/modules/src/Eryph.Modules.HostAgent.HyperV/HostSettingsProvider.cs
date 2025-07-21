using System;
using System.IO;
using System.Linq;
using Eryph.Core.VmAgent;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

public class HostSettingsProvider(ILoggerFactory loggerFactory)
    : IHostSettingsProvider
{
    public EitherAsync<Error, HostSettings> GetHostSettings() =>
        HostSettingsProvider<SimpleAgentRuntime>.getHostSettings()
            .Run(SimpleAgentRuntime.New(loggerFactory))
            .ToEitherAsync();
}

public static class HostSettingsProvider<RT> where RT : struct, HasWmi<RT>
{
    public static Eff<RT, HostSettings> getHostSettings() =>
        from queryResult in WmiQueries<RT>.getHyperVDefaultPaths()
        from dataPath in Try(() => Path.Combine(queryResult.DataRootPath, "Eryph"))
            .ToEff()
            .MapFail(e => Error.New("Could not construct the path for Eryph VMs.", e))
        from vhdPath in Try(() => Path.Combine(queryResult.VhdPath, "Eryph"))
            .ToEff()
            .MapFail(e => Error.New("Could not construct the path for Eryph VHDs.", e))
        select new HostSettings
        {
            DefaultDataPath = dataPath,
            DefaultVirtualHardDiskPath = vhdPath,
            DefaultNetwork = "nat"
        };
}
