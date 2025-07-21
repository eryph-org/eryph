using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Sys;
using Eryph.VmManagement.Sys;
using LanguageExt;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

internal readonly struct SimpleAgentRuntime :
    HasLogger<SimpleAgentRuntime>,
    HasRegistry<SimpleAgentRuntime>,
    HasWmi<SimpleAgentRuntime>
{
    private readonly SimpleAgentRuntimeEnv _env;

    private SimpleAgentRuntime(SimpleAgentRuntimeEnv env)
    {
        _env = env;
    }

    public static SimpleAgentRuntime New(ILoggerFactory loggerFactory) =>
        new(new SimpleAgentRuntimeEnv(loggerFactory));

    public Eff<SimpleAgentRuntime, ILogger> Logger(string category) =>
        Eff<SimpleAgentRuntime, ILogger>(rt => rt._env.LoggerFactory.CreateLogger(category));

    public Eff<SimpleAgentRuntime, ILogger<T>> Logger<T>() =>
        Eff<SimpleAgentRuntime, ILogger<T>>(rt => rt._env.LoggerFactory.CreateLogger<T>());

    public Eff<SimpleAgentRuntime, RegistryIO> RegistryEff => SuccessEff(LiveRegistryIO.Default);

    public Eff<SimpleAgentRuntime, WmiIO> WmiEff => SuccessEff(LiveWmiIO.Default);
}

internal class SimpleAgentRuntimeEnv(ILoggerFactory loggerFactory)
{
    public ILoggerFactory LoggerFactory { get; } = loggerFactory;
}
