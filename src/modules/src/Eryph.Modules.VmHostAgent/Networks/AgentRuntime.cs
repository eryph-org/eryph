using System.Threading;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Effects.Traits;
using Microsoft.Extensions.Logging;
using System;
using Eryph.Core;
using Microsoft.Extensions.DependencyInjection;


namespace Eryph.Modules.VmHostAgent.Networks;

public readonly struct AgentRuntime :
    HasCancel<AgentRuntime>,
    HasPowershell<AgentRuntime>,
    HasOVSControl<AgentRuntime>,
    HasAgentSyncClient<AgentRuntime>,
    HasHostNetworkCommands<AgentRuntime>,
    HasNetworkProviderManager<AgentRuntime>,
    HasLogger<AgentRuntime>

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

    private Eff<AgentRuntime, T> FromServiceProvider<T>()
        => Prelude.Eff<AgentRuntime, T>(rt => 
            rt.Env.ServiceProvider.GetRequiredService<T>());

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

}



public class AgentRuntimeEnv
    {
        public readonly CancellationTokenSource Source;
        public readonly CancellationToken Token;
        public readonly IServiceProvider ServiceProvider;

        public AgentRuntimeEnv(CancellationTokenSource source, CancellationToken token, 
            IServiceProvider serviceProvider)
        {
            Source = source;
            Token = token;
            ServiceProvider = serviceProvider;
        }

        public AgentRuntimeEnv(CancellationTokenSource source, IServiceProvider serviceProvider) 
            : this(source, source.Token, serviceProvider)
        {
        }
    }
