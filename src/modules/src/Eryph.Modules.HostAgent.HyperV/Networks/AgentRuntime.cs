using System;
using System.Threading;
using Dbosoft.OVN.Windows;
using Eryph.Core;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Effects.Traits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Networks;

public readonly struct AgentRuntime :
    HasCancel<AgentRuntime>,
    HasPowershell<AgentRuntime>,
    HasOVSControl<AgentRuntime>,
    HasAgentSyncClient<AgentRuntime>,
    HasHostNetworkCommands<AgentRuntime>,
    HasHyperVOvsPortManager<AgentRuntime>,
    HasNetworkProviderManager<AgentRuntime>,
    HasLogger<AgentRuntime>,
    HasWmi<AgentRuntime>
{
    readonly AgentRuntimeEnv _env;

    /// <summary>
    /// Constructor
    /// </summary>
    AgentRuntime(AgentRuntimeEnv env) =>
        this._env = env;

    /// <summary>
    /// Configuration environment accessor
    /// </summary>
    public AgentRuntimeEnv Env =>
        _env ?? throw new InvalidOperationException("Runtime Env not set.  Perhaps because of using default(Runtime) or new Runtime() rather than Runtime.New()");

    /// <summary>
    /// Constructor function
    /// </summary>
    public static AgentRuntime New(IServiceProvider sp) =>
        new AgentRuntime(new AgentRuntimeEnv(new CancellationTokenSource(), sp));

    /// <summary>
    /// Constructor function
    /// </summary>
    /// <param name="source">Cancellation token source</param>
    /// <param name="sp">Service provider</param>
    public static AgentRuntime New(CancellationTokenSource source, IServiceProvider sp) =>
        new AgentRuntime(new AgentRuntimeEnv(source, sp));


    /// <summary>
    /// Create a new Runtime with a fresh cancellation token
    /// </summary>
    /// <remarks>Used by localCancel to create new cancellation context for its sub-environment</remarks>
    /// <returns>New runtime</returns>
    public AgentRuntime LocalCancel =>
        new AgentRuntime(new AgentRuntimeEnv(new CancellationTokenSource(), Env.ServiceProvider));

    /// <summary>
    /// Direct access to cancellation token
    /// </summary>
    public CancellationToken CancellationToken =>
        Env.Token;

    /// <summary>
    /// Directly access the cancellation token source
    /// </summary>
    /// <returns>CancellationTokenSource</returns>
    public CancellationTokenSource CancellationTokenSource =>
        Env.Source;

    private static Eff<AgentRuntime, T> FromServiceProvider<T>() =>
        Eff<AgentRuntime, T>(rt => rt.Env.ServiceProvider.GetRequiredService<T>());

    public Eff<AgentRuntime, IPowershellEngine> Powershell => 
        FromServiceProvider<IPowershellEngine>();

    public Eff<AgentRuntime, IOVSControl> OVS => 
        FromServiceProvider<IOVSControl>();

    public Eff<AgentRuntime, IHostNetworkCommands<AgentRuntime>> HostNetworkCommands =>
        FromServiceProvider<IHostNetworkCommands<AgentRuntime>>();

    public Eff<AgentRuntime, ISyncClient> AgentSync =>
        FromServiceProvider<ISyncClient>();

    public Eff<AgentRuntime, INetworkProviderManager> NetworkProviderManager =>
        FromServiceProvider<INetworkProviderManager>();

    public Eff<AgentRuntime, ILogger> Logger(string category) => 
        FromServiceProvider<ILoggerFactory>().Map(lf => lf.CreateLogger(category));

    public Eff<AgentRuntime, ILogger<T>> Logger<T>() =>
        FromServiceProvider<ILoggerFactory>().Map(lf => lf.CreateLogger<T>());

    public Eff<AgentRuntime, WmiIO> WmiEff => SuccessEff(LiveWmiIO.Default);

    public Eff<AgentRuntime, IHyperVOvsPortManager> HyperVOvsPortManager =>
        FromServiceProvider<IHyperVOvsPortManager>();
}
