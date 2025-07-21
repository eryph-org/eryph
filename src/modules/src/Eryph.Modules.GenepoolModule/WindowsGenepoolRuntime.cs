using System.Runtime.Versioning;
using Eryph.Core.Sys;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement.Sys;
using LanguageExt;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Genepool;

[SupportedOSPlatform("windows")]
internal readonly struct WindowsGenepoolRuntime :
    HasLogger<WindowsGenepoolRuntime>,
    HasRegistry<WindowsGenepoolRuntime>,
    HasWmi<WindowsGenepoolRuntime>
{
    private readonly GenepoolRuntimeEnv _env;

    private WindowsGenepoolRuntime(GenepoolRuntimeEnv env)
    {
        _env = env;
    }

    public static WindowsGenepoolRuntime New(ILoggerFactory loggerFactory) =>
        new(new GenepoolRuntimeEnv(loggerFactory));

    public Eff<WindowsGenepoolRuntime, ILogger> Logger(string category) => Eff<WindowsGenepoolRuntime, ILogger>(rt => rt._env.LoggerFactory.CreateLogger(category));

    public Eff<WindowsGenepoolRuntime, ILogger<T>> Logger<T>() => Eff<WindowsGenepoolRuntime, ILogger<T>>(rt => LoggerFactoryExtensions.CreateLogger<T>(rt._env.LoggerFactory));

    public Eff<WindowsGenepoolRuntime, RegistryIO> RegistryEff => SuccessEff(LiveRegistryIO.Default);

    public Eff<WindowsGenepoolRuntime, WmiIO> WmiEff => SuccessEff(LiveWmiIO.Default);
}