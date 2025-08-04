using System.Runtime.Versioning;
using Eryph.Core.Sys;
using Eryph.VmManagement.Sys;
using LanguageExt;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool;

[SupportedOSPlatform("windows")]
internal readonly struct WindowsGenePoolRuntime :
    HasLogger<WindowsGenePoolRuntime>,
    HasRegistry<WindowsGenePoolRuntime>,
    HasWmi<WindowsGenePoolRuntime>
{
    private readonly GenePoolRuntimeEnv _env;

    private WindowsGenePoolRuntime(GenePoolRuntimeEnv env)
    {
        _env = env;
    }

    public static WindowsGenePoolRuntime New(ILoggerFactory loggerFactory) =>
        new(new GenePoolRuntimeEnv(loggerFactory));

    public Eff<WindowsGenePoolRuntime, ILogger> Logger(string category) => Eff<WindowsGenePoolRuntime, ILogger>(rt => rt._env.LoggerFactory.CreateLogger(category));

    public Eff<WindowsGenePoolRuntime, ILogger<T>> Logger<T>() => Eff<WindowsGenePoolRuntime, ILogger<T>>(rt => LoggerFactoryExtensions.CreateLogger<T>(rt._env.LoggerFactory));

    public Eff<WindowsGenePoolRuntime, RegistryIO> RegistryEff => SuccessEff(LiveRegistryIO.Default);

    public Eff<WindowsGenePoolRuntime, WmiIO> WmiEff => SuccessEff(LiveWmiIO.Default);
}