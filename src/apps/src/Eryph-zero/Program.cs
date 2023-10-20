using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.OVN;
using Eryph.App;
using Eryph.ModuleCore;
using Eryph.Modules.ComputeApi;
using Eryph.Modules.Network;
using Eryph.Modules.VmHostAgent;
using Eryph.Runtime.Zero.Configuration;
using Eryph.Runtime.Zero.Configuration.Clients;
using Eryph.Runtime.Zero.HttpSys;
using Eryph.Security.Cryptography;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Serilog;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using static Eryph.Modules.VmHostAgent.Networks.NetworkProviderManager<Eryph.Runtime.Zero.ConsoleRuntime>;
using static Eryph.Modules.VmHostAgent.Networks.ProviderNetworkUpdate<Eryph.Runtime.Zero.ConsoleRuntime>;
using static Eryph.Modules.VmHostAgent.Networks.ProviderNetworkUpdateInConsole<Eryph.Runtime.Zero.ConsoleRuntime>;
using static LanguageExt.Sys.Console<Eryph.Runtime.Zero.ConsoleRuntime>;
using Eryph.Runtime.Zero.Configuration.Networks;
using LanguageExt.Common;
using Serilog.Templates;
using Serilog.Templates.Themes;
using Serilog.Events;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog.Extensions.Logging;
using Serilog.Sinks.SystemConsole.Themes;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Microsoft.IdentityModel.Logging;

namespace Eryph.Runtime.Zero;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        IdentityModelEventSource.ShowPII = true; 

        var rootCommand = new RootCommand();
        var debugWaitOption = new System.CommandLine.Option<bool>(name: "--debuggerWait",
            () => false, "Stops and waits for a debugger to be attached");

        rootCommand.AddGlobalOption(debugWaitOption);

        var inFileOption = new System.CommandLine.Option<FileInfo?>(
            name: "--inFile",
            description: "Use input file instead of reading from stdin.");

        var outFileOption = new System.CommandLine.Option<FileInfo?>(
            name: "--outFile",
            description: "Use output file instead of writing to stdout.");

        var nonInteractiveOption = new System.CommandLine.Option<bool>(
            name: "--non-interactive",
            description: "No operator involved - commands will not query for confirmation.");

        var warmupOption = new System.CommandLine.Option<bool>(
            name: "--warmup",
            description: "Run in warmup mode (internal use)");


        var runCommand = new Command("run");
        runCommand.AddOption(warmupOption);
        runCommand.SetHandler(_ => Run(args));
        rootCommand.AddCommand(runCommand);

        var installCommand = new Command("install");
        var deleteOutFile = new System.CommandLine.Option<bool>(
            name: "--deleteOutFile",
            description: "Delete output file on exit - useful if file is watched for changes.");
        installCommand.AddOption(outFileOption);
        installCommand.AddOption(deleteOutFile);
        installCommand.SetHandler(SelfInstall, outFileOption, deleteOutFile);
        rootCommand.AddCommand(installCommand);

        var networksCommand = new Command("networks");
        rootCommand.AddCommand(networksCommand);

        var getNetworksCommand = new Command("get");
        getNetworksCommand.AddOption(outFileOption);
        getNetworksCommand.SetHandler(GetNetworks, outFileOption);
        networksCommand.AddCommand(getNetworksCommand);

        var importNetworksCommand = new Command("import");

        var noCurrentConfigCheckOption = new System.CommandLine.Option<bool>(
            name: "--no-current-config-check",
            description: "Do not check if host state is valid for current config. ");

        importNetworksCommand.AddOption(inFileOption);
        importNetworksCommand.AddOption(nonInteractiveOption);
        importNetworksCommand.AddOption(noCurrentConfigCheckOption);
        importNetworksCommand.SetHandler(ImportNetworkConfig, inFileOption,
            nonInteractiveOption, noCurrentConfigCheckOption);
        networksCommand.AddCommand(importNetworksCommand);

        var commandLineBuilder = new CommandLineBuilder(rootCommand);

        commandLineBuilder.AddMiddleware(async (context, next) =>
        {
            var debugHaltOn = context.ParseResult.GetValueForOption(debugWaitOption);

            if (debugHaltOn)
            {
                Console.WriteLine("Waiting for debugger to be attached");
                while (!Debugger.IsAttached)
                {
                    await Task.Delay(500, context.GetCancellationToken());
                }
                Console.WriteLine("Debugger attached");

            }

            await next(context);
        });

        commandLineBuilder.UseDefaults();
        var parser = commandLineBuilder.Build();
        return await parser.InvokeAsync(args);

    }

    private static Task<int> Run(string[] args)
    {
        return AdminGuard.CommandIsElevated(async () =>
        {

            var returnCode = 0;
            var warmupMode = args.Contains("--warmup");

            var logFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "eryph", "zero", "logs", "debug.txt");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.FromLogContext()
                .WriteTo.Console(new ExpressionTemplate("[{@t:yyyy-MM-dd HH:mm:ss.fff} {@l:u3}] {#if ovsLogLevel is not null}[OVS:{controlFile}:{ovsSender}:{ovsLogLevel}] {#end}{@m}\n{@x}", theme: TemplateTheme.Literate),
                    restrictedToMinimumLevel: LogEventLevel.Debug)
                .WriteTo.File(
                    new ExpressionTemplate("[{@t:yyyy-MM-dd HH:mm:ss.fff zzz} {@l:u3}] [{SourceContext}] {#if ovsLogLevel is not null}[OVS:{controlFile}:{ovsSender}:{ovsLogLevel}] {#end}{@m}\n{@x}"),
                    logFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 10,
                    retainedFileTimeLimit: TimeSpan.FromDays(30))
                
                .CreateLogger();

            try
            {
                var fileVersion = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location);
                Log.Logger.Information("Starting eryph-zero {version}", fileVersion.ProductVersion);

                if (warmupMode)
                    Log.Logger.Information("Running in warmup mode. Process will be stopped after start is completed");
                
                var startupConfig = StartupConfiguration(args,
                    sp =>
                    {
                        var configuration = sp.GetRequiredService<IConfiguration>();
                        return new
                        {
                            BasePath = configuration["basePath"],
                            SSLEndpointManager = sp.GetRequiredService<ISSLEndpointManager>(),
                            CryptoIO = sp.GetRequiredService<ICryptoIOServices>(),
                            CertificateGenerator = sp.GetRequiredService<ICertificateGenerator>(),
                            OVSPackageDir = configuration["ovsPackagePath"]
                        };
                    });


                await using var processLock = new ProcessFileLock(Path.Combine(ZeroConfig.GetConfigPath(), ".lock"));

                var basePathUrl = ConfigureUrl(startupConfig.BasePath);

                var endpoints = new Dictionary<string, string>
                    {
                        { "identity", $"{basePathUrl}identity" },
                        { "compute", $"{basePathUrl}compute" },
                        { "common", $"{basePathUrl}common" },
                        { "network", $"{basePathUrl}network" },

                    };

                ZeroConfig.EnsureConfiguration();

                await SystemClientGenerator.EnsureSystemClient(startupConfig.CertificateGenerator, startupConfig.CryptoIO,
                    new Uri(endpoints["identity"]));

                using var _ = await startupConfig.SSLEndpointManager
                    .EnableSslEndpoint(new SSLOptions(
                        "eryph-zero CA",
                        Network.FQDN,
                        DateTime.UtcNow.AddDays(-1),
                        365 * 5,
                        ZeroConfig.GetPrivateConfigPath(),
                        "eryphCA",
                        Guid.Parse("9412ee86-c21b-4eb8-bd89-f650fbf44931"),
                        basePathUrl));



                processLock.SetMetadata(new Dictionary<string, object>
                    {
                        {
                            "endpoints", endpoints
                        }
                    });


                // do not check in service mode - during startup some features may be unavailable
                if (!WindowsServiceHelpers.IsWindowsService())
                {
                    try
                    {
                        HostSettingsBuilder.GetHostSettings();
                    }
                    catch (ManagementException ex)
                    {
                        if (ex.ErrorCode == ManagementStatus.InvalidNamespace)
                        {
                            await Console.Error.WriteAsync(
                                "Hyper-V ist not installed. Install Hyper-V feature and then try again.");
                            return -10;
                        }
                    }
                }

                var container = new Container();
                container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
                container.RegisterInstance<ILoggerFactory>(new SerilogLoggerFactory(Log.Logger));
                container.RegisterConditional(
                    typeof(ILogger),
                    c => typeof(Logger<>).MakeGenericType(c.Consumer!.ImplementationType),
                    Lifestyle.Singleton,
                    _ => true);

                container.Bootstrap(startupConfig.OVSPackageDir);
                container.RegisterInstance<IEndpointResolver>(new EndpointResolver(endpoints));

                var builder = ModulesHost.CreateDefaultBuilder(args) as ModulesHostBuilder;

                var host =
                    builder!
                        .ConfigureInternalHost(hb =>
                        {
                            hb.UseWindowsService(cfg => cfg.ServiceName = "eryph-zero");
                        })

                        .UseAspNetCore((module, webHostBuilder) =>
                        {
                            webHostBuilder.UseHttpSys(options => { options.UrlPrefixes.Add(module.Path); });
                        })
                        .UseSimpleInjector(container)
                        .ConfigureAppConfiguration((_, config) =>
                        {
                            config.AddInMemoryCollection(new Dictionary<string, string>
                            {
                                { "privateConfigPath", ZeroConfig.GetPrivateConfigPath() },
                                {"bus:type",  "inmemory"},
                                {"databus:type", "inmemory"},
                                { "store:type", "inmemory"}
                            });
                        })
                        .HostModule<VmHostAgentModule>()
                        .HostModule<NetworkModule>()
                        .AddControllerModule(container)
                        .HostModule<ComputeApiModule>()
                        .AddIdentityModule(container)
                        .ConfigureServices(c => c.AddSingleton(_ => container.GetInstance<IEndpointResolver>()))
                        .ConfigureServices(LoggerProviderOptions.RegisterProviderOptions<
                            EventLogSettings, EventLogLoggerProvider>)
                        .ConfigureHostOptions(cfg => cfg.ShutdownTimeout = new TimeSpan(0, 0, 15))
                        .UseSerilog(dispose: true)
                    .Build();

                //starting here all errors should be considered as recoverable
                returnCode = -1;

                if (warmupMode)
                {
                    try
                    {
                        await host.StartAsync();
                        await Task.Delay(1000);
                        Log.Logger.Information("Warmup completed. Stopping.");
                        var cancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));    
                        await host.StopAsync(cancelSource.Token);
                        returnCode = 0;
                    }
                    finally
                    {
                        if (host is IAsyncDisposable asyncDisposable)
                        {
                            await asyncDisposable.DisposeAsync();
                        }
                        else
                        {
                            host.Dispose();
                        }
                    }
                }
                else
                {
                    await host.RunAsync();
                }

                return returnCode;
            }
            catch (Exception ex)
            {
                Log.Logger.Fatal(ex, "eryph-zero failure");
                await Console.Error.WriteAsync(ex.Message);

                return returnCode;
            }
        });

    }

    private static Uri ConfigureUrl(string basePath)
    {
        var uriBuilder = new UriBuilder(basePath);
        if (uriBuilder.Port == 0)
            uriBuilder.Port = GetAvailablePort();

        return uriBuilder.Uri;
    }

    private static T StartupConfiguration<T>(string[] args, Func<IServiceProvider, T> selectResult)
    {
        using var configHost = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                var env = hostingContext.HostingEnvironment;

                config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "basePath", "https://localhost:0" },
                    })
                    // ReSharper disable once StringLiteralTypo
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    // ReSharper disable once StringLiteralTypo
                    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: false);

                config.AddEnvironmentVariables();

                if (args is { Length: > 0 })
                {
                    config.AddCommandLine(args);
                }
            })
            .ConfigureServices(srv =>
            {
                srv.AddSingleton<ISSLEndpointManager, SSLEndpointManager>();
                srv.AddSingleton<ICertificateStoreService, WindowsCertificateStoreService>();
                srv.AddSingleton<IRSAProvider, RSAProvider>();
                srv.AddSingleton<ISSLEndpointRegistry, WinHttpSSLEndpointRegistry>();
                srv.AddSingleton<ICryptoIOServices, WindowsCryptoIOServices>();
                srv.AddSingleton<ICertificateGenerator, CertificateGenerator>();
            })
            .Build();

        var res = selectResult(configHost.Services);

        return res;
    }

    private static readonly IPEndPoint DefaultLoopbackEndpoint = new(IPAddress.Loopback, port: 0);

    private static int GetAvailablePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(DefaultLoopbackEndpoint);
        return (((IPEndPoint)socket.LocalEndPoint)!).Port;
    }

    private static async Task<int> SelfInstall(FileSystemInfo? outFile, bool deleteOutFile)
    {
        TextWriter? outWriter = null;
        var standardOut = Console.Out;
        var standardError = Console.Error;

        if (outFile != null)
        {
            outWriter = File.CreateText(outFile.FullName);
            Console.SetOut(outWriter);
            Console.SetError(outWriter);

        }

        try
        {

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.Console(theme: ConsoleTheme.None)
                .CreateLogger();

            return await AdminGuard.CommandIsElevated(async () =>
            {
                var targetDir =
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        "eryph", "zero");
                var zeroExe = Path.Combine(targetDir, "bin", "eryph-zero.exe");

                var backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "eryph", "zero.old");

                var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "eryph", "zero", "private");

                var backupDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "eryph", "zero", "private.old");
                var loggerFactory = new SerilogLoggerFactory(Log.Logger);
                var sysEnv = new SystemEnvironment(loggerFactory);
                var serviceManager = sysEnv.GetServiceManager("eryph-zero");

                var backupCreated = false;
                var dataBackupCreated = false;

                var enableRollback = await serviceManager.ServiceExists()
                    .IfLeft(false);

                try
                {
                    var baseDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                    var parentDir = baseDir.Parent?.FullName ?? throw new IOException($"Invalid path {baseDir}");

                    if (Directory.Exists(backupDir))
                        Directory.Delete(backupDir, true);

                    EitherAsync<Error, Unit> CopyService()
                    {
                        return Prelude.TryAsync(async () =>
                        {
                            Log.Logger.Information("Copy new files...");
                            if (Directory.Exists(targetDir))
                            {
                                var cancelSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                                Exception? lastException = null;
                                while (!cancelSource.IsCancellationRequested)
                                {
                                    try
                                    {
                                        Directory.Move(targetDir, backupDir);
                                        backupCreated = true;
                                        lastException = null;
                                        break;
                                    }
                                    catch (IOException ex)
                                    {
                                        lastException = ex;
                                        if(Directory.Exists(backupDir))
                                            Directory.Delete(backupDir, true);
                                        await Task.Delay(1000, CancellationToken.None);
                                    }
                                }
                                if(lastException != null)
                                    throw lastException;

                            }

                            CopyDirectory(parentDir, targetDir);

                            return Unit.Default;
                        }).ToEither();


                    }

                    Log.Information("Installing eryph-zero service");

                    var installOrUpdate =
                        from serviceExists in serviceManager.ServiceExists()
                        let cancelSource1 = new CancellationTokenSource(TimeSpan.FromMinutes(1))
                        from uStopped in serviceExists
                            ?
                            LogProgress("Stopping running service...").Bind(_ =>
                                serviceManager.EnsureServiceStopped(cancelSource1.Token))
                            : Unit.Default
                        from uDBackup in Prelude.Try(() =>
                        {
                            if (!Directory.Exists(dataDir)) return Unit.Default;

                            Log.Logger.Information("Creating data backup...");
                            CopyDirectory(dataDir, backupDataDir);
                            dataBackupCreated = true;
                            return Unit.Default;
                        }).ToEitherAsync()
                        from _ in Prelude.Try(() => OVSPackage.UnpackAndProvide(true)).ToEitherAsync()
                        from uCopy in CopyService()
                        from uWarmup in LogProgress("Migrate and warmup... (this could take a while)").Bind(_ => RunWarmup(zeroExe))
                        let cancelSource2 = new CancellationTokenSource(TimeSpan.FromMinutes(5))
                        from uInstalled in serviceExists
                            ? LogProgress("Updating service...").Bind ( _ => serviceManager.UpdateService($"{zeroExe} run", cancelSource2.Token))
                            : LogProgress("Installing service...").Bind( _ => serviceManager.CreateService("eryph-zero", $"{zeroExe} run",
                                cancelSource2.Token))
                        let cancelSource3 = new CancellationTokenSource(TimeSpan.FromMinutes(5))

                        from pStart in LogProgress("Starting service...")
                        from uStarted in serviceManager.EnsureServiceStarted(cancelSource3.Token)
                        select Unit.Default;

                    _ = await installOrUpdate.IfLeft(l => l.Throw());

                    if (Directory.Exists(backupDir))
                        Directory.Delete(backupDir, true);

                    if (Directory.Exists(backupDataDir))
                        Directory.Delete(backupDataDir, true);

                    //update path variable
                    const string pathVariable = "PATH";
                    var pathVariableValue = Environment.GetEnvironmentVariable(pathVariable, EnvironmentVariableTarget.Machine);
                    var runDir = Path.Combine(targetDir, "bin");

                    if (pathVariableValue == null || 
                        !pathVariableValue.Contains(runDir, StringComparison.InvariantCultureIgnoreCase))
                    {
                        Environment.SetEnvironmentVariable(pathVariable,
                            $"{pathVariableValue};{runDir}", EnvironmentVariableTarget.Machine);
                    }


                    Log.Logger.Information("Installation completed");

                    return 0;

                }
                catch (Exception ex)
                {
                    Log.Error("Installation failed. Error: {message}", ex.Message );
                    Log.Debug(ex, "Error Details");

                    //undo operation

                    if (!enableRollback) return -1;

                    Log.Information("Trying to rollback to previous installation.");

                    try
                    {
                        var rollback =
                            from serviceExists in serviceManager.ServiceExists()
                            let cancelSourceStop = new CancellationTokenSource(TimeSpan.FromMinutes(1))
                            from uStopped in serviceExists
                                ? serviceManager.EnsureServiceStopped(cancelSourceStop.Token)
                                : Unit.Default
                            from uCopy in Prelude.TryAsync (async () =>
                            {
                                if (backupCreated)
                                {
                                    Log.Information("Restoring backup files");
                                    var cancelSourceCopy = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                                    await SaveDirectoryMove(backupDir, targetDir, cancelSourceCopy.Token);
                                }

                                if(dataBackupCreated)
                                {
                                    Log.Information("Restoring backup data files");
                                    var cancelSourceCopy = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                                    await SaveDirectoryMove(backupDataDir, dataDir, cancelSourceCopy.Token);
                                }

                                return Unit.Default;
                            }).ToEither()

                            from serviceExists2 in serviceManager.ServiceExists()
                            let cancelSourceStart = new CancellationTokenSource(TimeSpan.FromMinutes(5))

                            from uStarted in serviceExists2
                                ? LogProgress("Starting service...").Bind(_ => serviceManager.EnsureServiceStarted(cancelSourceStart.Token))
                                : Unit.Default
                            select Unit.Default;

                        _ = await rollback.IfLeft(l =>
                        {
                            Log.Error("Rollback failed. Error: {message}", l.Message);
                            Log.Debug("Error Details: {@error}", l);

                        });

                    }
                    catch (Exception rollBackEx)
                    {
                        Log.Error("Rollback failed. Error: {message}", rollBackEx.Message);
                        Log.Debug(rollBackEx, "Error Details");

                    }

                    return -1;
                }




            });
        }
        finally
        {
            if (outWriter != null)
            {
                Console.SetOut(standardOut);
                Console.SetError(standardError);
                outWriter.Close();

                if (deleteOutFile)
                {
                    Task.Delay(2000).GetAwaiter().GetResult();
                    File.Delete(outFile.FullName);

                }
            }
        }


        EitherAsync<Error, Unit> LogProgress(string message)
        {
            Log.Logger.Information(message);
            return Unit.Default;
        }

        EitherAsync<Error, Unit> RunWarmup(string eryphBinPath)
        {
            var cancelTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            return Prelude.TryAsync(async () =>
                        {
                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = eryphBinPath,
                        Arguments = "run --warmup",
                        UseShellExecute = false,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Environment.SystemDirectory
                    },
                    EnableRaisingEvents = true
                };
                process.Start();
                await process.WaitForExitAsync(cancelTokenSource.Token);


                return Unit.Default;
            }).ToEither();
        }
    }

    private static async Task SaveDirectoryMove(string source, string target, CancellationToken cancellationToken)
    {
        if (Directory.Exists(target))
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Directory.Delete(target, true);
                    break;
                }
                catch (Exception)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        Directory.Move(source, target);
    }

    private static async Task<int> GetNetworks(FileSystemInfo? outFile)
    {
        var manager = new NetworkProviderManager();

        return await manager.GetCurrentConfigurationYaml()
                .MatchAsync(
                    async config =>
                    {
                        if (outFile != null)
                        {
                            await File.WriteAllTextAsync(outFile.FullName, config);
                        }
                        else
                        {
                            Console.WriteLine(config);
                        }

                        return 0;
                    }, l =>
                    {
                        Console.WriteLine(l.Message);
                        return -1;
                    }
                    );
    }

    private static async Task<int> ImportNetworkConfig(FileSystemInfo? inFile, bool nonInteractive,
        bool noCurrentConfigCheck)
    {
        var configString = "";
        if (inFile != null)
        {
            configString = await File.ReadAllTextAsync(inFile.FullName);
        }
        else
        {
            try
            {
                if (!Console.KeyAvailable)
                {
                    await Console.Error.WriteLineAsync(
                        "Error: Supply the new network config to stdin or use --inFile option to read from file");
                    return -1;
                }
            }
            catch (InvalidOperationException)
            {
                // ignored, expected when console is redirected
            }

            await using var reader = Console.OpenStandardInput();
            using var textReader = new StreamReader(reader);
            configString = await textReader.ReadToEndAsync();

        }
        
        using var psEngine = new PowershellEngine(new NullLoggerFactory().CreateLogger(""));
        var ovsPackagePath = OVSPackage.UnpackAndProvide(true);
        var sysEnv = new EryphOVSEnvironment(new EryphOvsPathProvider(ovsPackagePath), new NullLoggerFactory());

        var res = (await (
            from newConfig in importConfig(configString)
            from currentConfig in getCurrentConfiguration()
            from hostState in getHostStateWithProgress()
            from syncResult in noCurrentConfigCheck
                ? Prelude.SuccessAff((false, hostState))
                : from currentConfigChanges in generateChanges(hostState, currentConfig)
                  from r in syncCurrentConfigBeforeNewConfig(hostState, currentConfigChanges, nonInteractive)
                  select r
            from newConfigChanges in generateChanges(syncResult.HostState, newConfig)
            from validateImpact in validateNetworkImpact(newConfig)
            from _ in applyChangesInConsole(currentConfig, newConfigChanges,
                nonInteractive, syncResult.IsValid)

            from save in saveConfigurationYaml(configString)
            from sync in syncNetworks()
            from m in writeLine("New Network configuration was imported.")
            select Unit.Default)

            .Run(new ConsoleRuntime(
                    new NullLoggerFactory(),
                    psEngine,sysEnv, new CancellationTokenSource())))
            .Match(
                r => 0, l =>
            {
                Console.Error.WriteLine("Error: " + l.Message);
                return -1;
            });

        return res;
    }


    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // Cache directories before we start copying
        var dirs = dir.GetDirectories();

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (var subDir in dirs)
        {
            var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }
}
