using System.Text;
using Eryph.Core;
using Eryph.Modules.VmHostAgent;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys;
using LanguageExt.Sys.Traits;
using Microsoft.Extensions.Logging;
using Traits = LanguageExt.Sys.Traits;

using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent.HyperV.Test;

public readonly struct TestRuntime :
    HasCancel<TestRuntime>,
    HasConsole<TestRuntime>,
    HasFile<TestRuntime>,
    HasTime<TestRuntime>,
    HasDirectory<TestRuntime>,
    HasOVSControl<TestRuntime>,
    HasAgentSyncClient<TestRuntime>,
    HasHostNetworkCommands<TestRuntime>,
    HasNetworkProviderManager<TestRuntime>,
    HasLogger<TestRuntime>
{
    public readonly RuntimeEnv<TestRuntime> env;

    /// <summary>
    /// Constructor
    /// </summary>
    TestRuntime(RuntimeEnv<TestRuntime> env) =>
        this.env = env;

    /// <summary>
    /// Configuration environment accessor
    /// </summary>
    public RuntimeEnv<TestRuntime> Env =>
        env ?? throw new InvalidOperationException(
            "Runtime Env not set. Perhaps because of using default(Runtime) or new Runtime() rather than Runtime.New()");

    public static TestRuntime New(
        IOVSControl ovsControl,
        ISyncClient syncClient,
        IHostNetworkCommands<TestRuntime> hostNetworkCommands,
        INetworkProviderManager networkProviderManager) =>
        new TestRuntime(new RuntimeEnv<TestRuntime>(
            new CancellationTokenSource(),
            ovsControl,
            syncClient,
            hostNetworkCommands,
            networkProviderManager));

    /// <summary>
    /// Create a new Runtime with a fresh cancellation token
    /// </summary>
    /// <remarks>Used by localCancel to create new cancellation context for its sub-environment</remarks>
    /// <returns>New runtime</returns>
    public TestRuntime LocalCancel =>
        new TestRuntime(new RuntimeEnv<TestRuntime>(
            new CancellationTokenSource(),
            Env.Encoding,
            Env.Console,
            Env.FileSystem,
            Env.TimeSpec,
            Env.SysEnv,
            Env.OVSControl,
            Env.SyncClient,
            Env.HostNetworkCommands,
            Env.NetworkProviderManager));

    /// <summary>
    /// Direct access to cancellation token
    /// </summary>
    public CancellationToken CancellationToken =>
        Env.Source.Token;

    /// <summary>
    /// Directly access the cancellation token source
    /// </summary>
    /// <returns>CancellationTokenSource</returns>
    public CancellationTokenSource CancellationTokenSource => 
        Env.Source;

    /// <summary>
    /// Get encoding
    /// </summary>
    /// <returns></returns>
    public Encoding Encoding =>
        Env.Encoding;

    /// <summary>
    /// Access the console environment
    /// </summary>
    /// <returns>Console environment</returns>
    public Eff<TestRuntime, Traits.ConsoleIO> ConsoleEff =>
        Eff<TestRuntime, Traits.ConsoleIO>(
            rt => new LanguageExt.Sys.Test.ConsoleIO(rt.Env.Console));

    /// <summary>
    /// Access the file environment
    /// </summary>
    /// <returns>File environment</returns>
    public Eff<TestRuntime, Traits.FileIO> FileEff =>
        from n in Time<TestRuntime>.now
        from r in Eff<TestRuntime, Traits.FileIO>(
            rt => new LanguageExt.Sys.Test.FileIO(rt.Env.FileSystem, n))
        select r;

    /// <summary>
    /// Access the directory environment
    /// </summary>
    /// <returns>Directory environment</returns>
    public Eff<TestRuntime, Traits.DirectoryIO> DirectoryEff =>
        from n in Time<TestRuntime>.now
        from r in Eff<TestRuntime, Traits.DirectoryIO>(
            rt => new LanguageExt.Sys.Test.DirectoryIO(rt.Env.FileSystem, n))
        select r;

    /// <summary>
    /// Access the time environment
    /// </summary>
    /// <returns>Time environment</returns>
    public Eff<TestRuntime, Traits.TimeIO> TimeEff =>
        Eff<TestRuntime, Traits.TimeIO>(
            rt => new LanguageExt.Sys.Test.TimeIO(rt.Env.TimeSpec));

    /// <summary>
    /// Access the operating-system environment
    /// </summary>
    /// <returns>Operating-system environment environment</returns>
    public Eff<TestRuntime, Traits.EnvironmentIO> EnvironmentEff =>
        Eff<TestRuntime, Traits.EnvironmentIO>(
            rt => new LanguageExt.Sys.Test.EnvironmentIO(rt.Env.SysEnv));

    public Eff<TestRuntime, IOVSControl> OVS =>
        Eff<TestRuntime, IOVSControl>(rt => rt.Env.OVSControl);

    public Eff<TestRuntime, ISyncClient> AgentSync =>
        Eff<TestRuntime, ISyncClient>(rt => rt.Env.SyncClient);

    public Eff<TestRuntime, IHostNetworkCommands<TestRuntime>> HostNetworkCommands =>
        Eff<TestRuntime, IHostNetworkCommands<TestRuntime>>(
            rt => rt.Env.HostNetworkCommands);
    public Eff<TestRuntime, INetworkProviderManager> NetworkProviderManager =>
        Eff<TestRuntime, INetworkProviderManager>(
            rt => rt.Env.NetworkProviderManager);

    public Eff<TestRuntime, ILogger> Logger(string category) =>
        Eff<TestRuntime, ILogger>(rt => rt.Env.LoggerFactory.CreateLogger(category));

    public Eff<TestRuntime, ILogger<T>> Logger<T>() =>
        Eff<TestRuntime, ILogger<T>>(rt => rt.Env.LoggerFactory.CreateLogger<T>());

}