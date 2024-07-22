using System;
using System.IO;
using System.Linq;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

public interface IHostSettingsProvider
{
    EitherAsync<Error, HostSettings> GetHostSettings();
}

public class HostSettingsProvider : IHostSettingsProvider
{
    public EitherAsync<Error, HostSettings> GetHostSettings() =>
        from hostSettings in HostSettingsProvider<WmiRuntime>.getHostSettings()
            .Run(WmiRuntime.New())
            .ToEitherAsync()
        select hostSettings;
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
