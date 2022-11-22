using System.Text;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using LanguageExt;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys;
using LanguageExt.Sys.Test;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Traits = LanguageExt.Sys.Traits;


using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Test
{
    public readonly struct TestRuntime :
           HasCancel<TestRuntime>, 
           Traits.HasConsole<TestRuntime>, 
           Traits.HasFile<TestRuntime>, 
           Traits.HasEncoding<TestRuntime>, 
           Traits.HasTextRead<TestRuntime>, 
           Traits.HasTime<TestRuntime>, 
           Traits.HasEnvironment<TestRuntime>, 
           Traits.HasDirectory<TestRuntime>,
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
            env ?? throw new InvalidOperationException("Runtime Env not set.  Perhaps because of using default(Runtime) or new Runtime() rather than Runtime.New()");

        /// <summary>
        /// Constructor function
        /// </summary>
        /// <param name="timeSpec">Defines how time works in the runtime</param>
        public static TestRuntime New(TestTimeSpec? timeSpec = default) =>
            new TestRuntime(new RuntimeEnv<TestRuntime>(new CancellationTokenSource(),
                                       System.Text.Encoding.Default,
                                       new MemoryConsole(),
                                       new MemoryFS(),
                                       timeSpec,
                                       MemorySystemEnvironment.InitFromSystem()));

        /// <summary>
        /// Constructor function
        /// </summary>
        /// <param name="source">Cancellation token source</param>
        /// <param name="timeSpec">Defines how time works in the runtime</param>
        public static TestRuntime New(CancellationTokenSource source, TestTimeSpec? timeSpec = default) =>
            new TestRuntime(new RuntimeEnv<TestRuntime>(source,
                                       System.Text.Encoding.Default,
                                       new MemoryConsole(),
                                       new MemoryFS(),
                                       timeSpec,
                                       MemorySystemEnvironment.InitFromSystem()));

        /// <summary>
        /// Constructor function
        /// </summary>
        /// <param name="encoding">Text encoding</param>
        /// <param name="timeSpec">Defines how time works in the runtime</param>
        public static TestRuntime New(Encoding encoding, TestTimeSpec? timeSpec = default) =>
            new TestRuntime(new RuntimeEnv<TestRuntime>(new CancellationTokenSource(),
                                       encoding,
                                       new MemoryConsole(),
                                       new MemoryFS(),
                                       timeSpec,
                                       MemorySystemEnvironment.InitFromSystem()));

        /// <summary>
        /// Constructor function
        /// </summary>
        /// <param name="encoding">Text encoding</param>
        /// <param name="source">Cancellation token source</param>
        /// <param name="timeSpec">Defines how time works in the runtime</param>
        public static TestRuntime New(Encoding encoding, CancellationTokenSource source, TestTimeSpec? timeSpec = default) =>
            new TestRuntime(new RuntimeEnv<TestRuntime>(source,
                                       encoding,
                                       new MemoryConsole(),
                                       new MemoryFS(),
                                       timeSpec,
                                       MemorySystemEnvironment.InitFromSystem()));

        /// <summary>
        /// Create a new Runtime with a fresh cancellation token
        /// </summary>
        /// <remarks>Used by localCancel to create new cancellation context for its sub-environment</remarks>
        /// <returns>New runtime</returns>
        public TestRuntime LocalCancel =>
            new TestRuntime(Env.LocalCancel);

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
            Eff<TestRuntime, Traits.ConsoleIO>(rt => new ConsoleIO(rt.Env.Console));

        /// <summary>
        /// Access the file environment
        /// </summary>
        /// <returns>File environment</returns>
        public Eff<TestRuntime, Traits.FileIO> FileEff =>
            from n in Time<TestRuntime>.now
            from r in Eff<TestRuntime, Traits.FileIO>(
                rt => new FileIO(rt.Env.FileSystem, n))
            select r;

        /// <summary>
        /// Access the directory environment
        /// </summary>
        /// <returns>Directory environment</returns>
        public Eff<TestRuntime, Traits.DirectoryIO> DirectoryEff =>
            from n in Time<TestRuntime>.now
            from r in Eff<TestRuntime, Traits.DirectoryIO>(rt => new DirectoryIO(rt.Env.FileSystem, n))
            select r;

        /// <summary>
        /// Access the TextReader environment
        /// </summary>
        /// <returns>TextReader environment</returns>
        public Eff<TestRuntime, Traits.TextReadIO> TextReadEff =>
            SuccessEff(TextReadIO.Default);

        /// <summary>
        /// Access the time environment
        /// </summary>
        /// <returns>Time environment</returns>
        public Eff<TestRuntime, Traits.TimeIO> TimeEff =>
            Eff<TestRuntime, Traits.TimeIO>(rt => new TimeIO(rt.Env.TimeSpec));

        /// <summary>
        /// Access the operating-system environment
        /// </summary>
        /// <returns>Operating-system environment environment</returns>
        public Eff<TestRuntime, Traits.EnvironmentIO> EnvironmentEff =>
            Eff<TestRuntime, Traits.EnvironmentIO>(rt => new EnvironmentIO(rt.Env.SysEnv));

        public Eff<TestRuntime, IOVSControl> OVS =>
            Eff<TestRuntime, IOVSControl>(rt => rt.Env.OVS);

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

    public class RuntimeEnv<RT> where RT : struct, HasCancel<RT>
    {
        public readonly CancellationTokenSource Source;
        public readonly CancellationToken Token;
        public readonly Encoding Encoding;
        public readonly MemoryConsole Console;
        public readonly MemoryFS FileSystem;
        public readonly TestTimeSpec TimeSpec;
        public readonly MemorySystemEnvironment SysEnv;

        public IOVSControl OVS { get; set; }
        public ISyncClient SyncClient { get; set; }

        public IHostNetworkCommands<RT> HostNetworkCommands { get; set; }
        public INetworkProviderManager NetworkProviderManager { get; set; }
        public ILoggerFactory LoggerFactory { get; set; } = new NullLoggerFactory();


        public RuntimeEnv(
            CancellationTokenSource source,
            CancellationToken token,
            Encoding encoding,
            MemoryConsole console,
            MemoryFS fileSystem,
            TestTimeSpec? timeSpec,
            MemorySystemEnvironment sysEnv)
        {
            Source = source;
            Token = token;
            Encoding = encoding;
            Console = console;
            FileSystem = fileSystem;
            TimeSpec = timeSpec ?? TestTimeSpec.RunningFromNow();
            SysEnv = sysEnv;
        }

        public RuntimeEnv(
            CancellationTokenSource source,
            Encoding encoding,
            MemoryConsole console,
            MemoryFS fileSystem,
            TestTimeSpec? timeSpec,
            MemorySystemEnvironment sysEnv) :
            this(source, source.Token, encoding, console, fileSystem, timeSpec, sysEnv)
        {
        }

        public RuntimeEnv<TestRuntime> LocalCancel =>
            new(new CancellationTokenSource(), Encoding, Console, FileSystem, TimeSpec, SysEnv);
    }
}