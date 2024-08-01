using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Eryph.AnsiConsole;
using Eryph.App;
using Eryph.AnsiConsole.Sys;
using Eryph.ModuleCore;
using Eryph.Modules.Network;
using Eryph.Modules.VmHostAgent;
using Eryph.Modules.VmHostAgent.Configuration;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.Runtime.Zero.Configuration;
using Eryph.Runtime.Zero.Configuration.AgentSettings;
using Eryph.Runtime.Zero.Configuration.Clients;
using Eryph.Runtime.Zero.HttpSys;
using Eryph.Security.Cryptography;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
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
using static Eryph.Modules.VmHostAgent.Networks.OvsDriverProvider<Eryph.Runtime.Zero.ConsoleRuntime>;
using static LanguageExt.Sys.Console<Eryph.Runtime.Zero.ConsoleRuntime>;
using Eryph.Runtime.Zero.Configuration.Networks;
using Eryph.VmManagement.Data.Core;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys.IO;
using LanguageExt.Sys.Traits;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog.Extensions.Logging;
using Serilog.Sinks.SystemConsole.Themes;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Microsoft.IdentityModel.Logging;
using Microsoft.Win32;
using Spectre.Console;

using static LanguageExt.Prelude;
using static Eryph.AnsiConsole.Prelude;

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

        var noCurrentConfigCheckOption = new System.CommandLine.Option<bool>(
            name: "--no-current-config-check",
            description: "Do not check if host state is valid for current config. ");


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


        var uninstallCommand = new Command("uninstall");
        uninstallCommand.AddOption(outFileOption);
        uninstallCommand.AddOption(deleteOutFile);
        var deleteAppData = new System.CommandLine.Option<bool>(
            name: "--delete-app-data",
            description: "Delete all local application data");
        uninstallCommand.AddOption(deleteAppData);
        uninstallCommand.SetHandler(SelfUnInstall, outFileOption, deleteOutFile, deleteAppData);
        rootCommand.AddCommand(uninstallCommand);


        var agentSettingsCommand = new Command("agentsettings");
        rootCommand.AddCommand(agentSettingsCommand);

        var getAgentSettingsCommand = new Command("get");
        getAgentSettingsCommand.AddOption(outFileOption);
        getAgentSettingsCommand.SetHandler(GetAgentSettings, outFileOption);
        agentSettingsCommand.AddCommand(getAgentSettingsCommand);

        var importAgentSettingsCommand = new Command("import");
        importAgentSettingsCommand.AddOption(inFileOption);
        importAgentSettingsCommand.AddOption(nonInteractiveOption);
        importAgentSettingsCommand.AddOption(noCurrentConfigCheckOption);
        importAgentSettingsCommand.SetHandler(ImportAgentSettings, inFileOption,
            nonInteractiveOption, noCurrentConfigCheckOption);
        agentSettingsCommand.AddCommand(importAgentSettingsCommand);

        var loginCommand = new Command("login");
        rootCommand.AddCommand(loginCommand);
        loginCommand.SetHandler(Login);

        var logoutCommand = new Command("logout");
        rootCommand.AddCommand(logoutCommand);
        logoutCommand.SetHandler(Logout);

        var genePoolCommand = new Command("genepool");
        rootCommand.AddCommand(genePoolCommand);

        var genePoolInfoCommand = new Command("info");
        genePoolCommand.AddCommand(genePoolInfoCommand);
        genePoolInfoCommand.SetHandler(GetGenePoolInfo);

        var networksCommand = new Command("networks");
        rootCommand.AddCommand(networksCommand);

        var getNetworksCommand = new Command("get");
        getNetworksCommand.AddOption(outFileOption);
        getNetworksCommand.SetHandler(GetNetworks, outFileOption);
        networksCommand.AddCommand(getNetworksCommand);

        var importNetworksCommand = new Command("import");
        importNetworksCommand.AddOption(inFileOption);
        importNetworksCommand.AddOption(nonInteractiveOption);
        importNetworksCommand.AddOption(noCurrentConfigCheckOption);
        importNetworksCommand.SetHandler(ImportNetworkConfig, inFileOption,
            nonInteractiveOption, noCurrentConfigCheckOption);
        networksCommand.AddCommand(importNetworksCommand);

        var syncNetworkConfigCommand = new Command("sync");
        syncNetworkConfigCommand.AddOption(nonInteractiveOption);
        syncNetworkConfigCommand.SetHandler(SyncNetworkConfig,
            nonInteractiveOption);
        networksCommand.AddCommand(syncNetworkConfigCommand);

        var driverCommand = new Command("driver");
        rootCommand.AddCommand(driverCommand);

        var getDriverStatusCommand = new Command("status");
        getDriverStatusCommand.SetHandler(GetDriverStatus);
        driverCommand.AddCommand(getDriverStatusCommand);

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

    private static async Task<int> Run(string[] args)
    {
        return await AdminGuard.CommandIsElevated(async () =>
        {
            ZeroStartupConfig startupConfig;
            Serilog.Core.Logger logger;

            try
            {
                startupConfig = StartupConfiguration(args, sp =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    return new ZeroStartupConfig
                    {
                        Configuration = configuration,
                        BasePath = configuration["basePath"],
                        SslEndpointManager = sp.GetRequiredService<ISSLEndpointManager>(),
                        CryptoIO = sp.GetRequiredService<ICryptoIOServices>(),
                        CertificateGenerator = sp.GetRequiredService<ICertificateGenerator>(),
                        OvsPackageDir = configuration["ovsPackagePath"]
                    };
                });
                logger = ZeroLogging.CreateLogger(startupConfig.Configuration);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync(ex.ToString());
                return -1;
            }

            var returnCode = 0;
            var warmupMode = args.Contains("--warmup");

            try
            {
                logger.Information("Starting eryph-zero {Version}", new ZeroApplicationInfoProvider().ProductVersion);

                if (warmupMode)
                    logger.Information("Running in warmup mode. Process will be stopped after start is completed");

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

                using var _ = await startupConfig.SslEndpointManager
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

                var isWindowsService = WindowsServiceHelpers.IsWindowsService();
                using var loggerFactory = new SerilogLoggerFactory(logger, dispose: false);

                var result = await DriverCommands.Run(
                    from _1 in SystemRequirementsChecker<DriverCommandsRuntime>.ensureHyperV(isWindowsService)
                        .MapFail(e => Error.New(
                            -10,
                            "Hyper-V is not available. Make sure it is installed and the management service (VMMS) is running.",
                            e))
                    from ovsLogger in default(DriverCommandsRuntime).Logger<OVSPackage>()
                    from runDir in Eff(() => OVSPackage.UnpackAndProvide(
                        ovsLogger,
                        startupConfig.OvsPackageDir))
                        .ToAff()
                        .MapFail(e => Error.New(-12, "Could not extract the included OVS/OVN distribution.", e))
                    from _2 in OvsDriverProvider<DriverCommandsRuntime>.ensureDriver(
                            runDir, !isWindowsService && !warmupMode, !isWindowsService && !warmupMode)
                        .MapFail(e => Error.New(-11, "The Hyper-V switch extension for OVS is not available.", e))
                    select runDir,
                    loggerFactory);

                if (result.Case is Error error)
                {
                    if (isWindowsService)
                    {
                        logger.Fatal(error, "The service startup failed.");
                    }
                    else
                    {
                        Spectre.Console.AnsiConsole.Write(new Rows(
                            new Markup("[red]The command failed with the following error(s):[/]"),
                            Renderable(error)));
                    }
                    return error.Code;
                }

                var ovsRunDir = result.ToOption().ValueUnsafe();

                var container = new Container();
                container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
                
                container.RegisterInstance<ILoggerFactory>(loggerFactory);
                container.RegisterConditional(
                    typeof(ILogger),
                    c => typeof(Microsoft.Extensions.Logging.Logger<>).MakeGenericType(c.Consumer!.ImplementationType),
                    Lifestyle.Singleton,
                    _ => true);

                container.Bootstrap(ovsRunDir);
                container.RegisterInstance<IEndpointResolver>(new EndpointResolver(endpoints));

                var builder = ModulesHost.CreateDefaultBuilder(args);

                var host = builder
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
                            { "bus:type", "inmemory" },
                            { "databus:type", "inmemory" },
                            { "store:type", "inmemory" },
                        });
                        config.AddInMemoryCollection(new Dictionary<string, string>
                        {
                            ["changeTracking:trackChanges"] = bool.TrueString,
                            ["changeTracking:seedDatabase"] = bool.TrueString,
                            ["changeTracking:networksConfigPath"] = ZeroConfig.GetNetworksConfigPath(),
                            ["changeTracking:projectsConfigPath"] = ZeroConfig.GetProjectsConfigPath(),
                            ["changeTracking:projectNetworksConfigPath"] = ZeroConfig.GetProjectNetworksConfigPath(),
                            ["changeTracking:projectNetworkPortsConfigPath"] = ZeroConfig.GetProjectNetworkPortsConfigPath(),
                            ["changeTracking:virtualMachinesConfigPath"] = ZeroConfig.GetMetadataConfigPath(),
                        });
                    })
                    .HostModule<VmHostAgentModule>()
                    .HostModule<NetworkModule>()
                    .AddControllerModule(container)
                    .AddComputeApiModule()
                    .AddIdentityModule(container)
                    .ConfigureServices(c => c.AddSingleton(_ => container.GetInstance<IEndpointResolver>()))
                    .ConfigureServices(LoggerProviderOptions.RegisterProviderOptions<
                        EventLogSettings, EventLogLoggerProvider>)
                    .ConfigureHostOptions(cfg => cfg.ShutdownTimeout = new TimeSpan(0, 0, 15))
                    // The logger must not be disposed here as it is injected into multiple modules.
                    // Serilog requires a single logger instance for synchronization.
                    .UseSerilog(logger: logger, dispose: false)
                    .Build();

                //starting here all errors should be considered as recoverable
                returnCode = -1;

                if (warmupMode)
                {
                    try
                    {
                        await host.StartAsync();
                        await Task.Delay(1000);
                        logger.Information("Warmup completed. Stopping.");
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
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "eryph-zero failure");
            }
            finally
            {
                await logger.DisposeAsync();
            }

            return returnCode;
        });

    }

    private static Uri ConfigureUrl(string? basePath)
    {
        var uriBuilder = new UriBuilder(basePath ?? "https://localhost:0");
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
                
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
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
                        return TryAsync(async () =>
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
                        from uDBackup in TryAsync(async () =>
                        {
                            if (!Directory.Exists(dataDir)) return Unit.Default;

                            Log.Logger.Information("Creating data backup...");
                            await Task.Delay(2000, CancellationToken.None);
                            CopyDirectory(dataDir, backupDataDir,
                                "state.db-shm", "state.db-wal");
                            dataBackupCreated = true;
                            return Unit.Default;
                        }).ToEither()
                        from ovsRootPath in Try(() => OVSPackage.UnpackAndProvide(loggerFactory.CreateLogger<OVSPackage>()))
                            .ToEitherAsync()
                        from _ in DriverCommands.EnsureDriver(ovsRootPath, true, true, loggerFactory).Map(r => r.ToEither()).ToAsync()
                        from uCopy in CopyService() 
                        from __ in Try(() => { RegisterUninstaller(targetDir); return unit; })
                            .ToEitherAsync()
                        from uWarmup in LogProgress("Migrate and warmup... (this could take a while)").Bind(_ => RunWarmup(zeroExe))
                        let cancelSource2 = new CancellationTokenSource(TimeSpan.FromMinutes(5))
                        from uInstalled in serviceExists
                            ? LogProgress("Updating service...")
                                .Bind(_ => serviceManager.UpdateService($"{zeroExe} run", cancelSource2.Token))
                            : LogProgress("Installing service...").Bind(_ => serviceManager.CreateService("eryph-zero",
                                $"{zeroExe} run",
                                // vmms is the Hyper-V Virtual Machine Management service
                                Seq1("vmms"),
                                cancelSource2.Token))
                        from ___ in LogProgress("Setting service recovery options...")
                            .Bind(_ => serviceManager.SetRecoveryOptions(
                                TimeSpan.FromMinutes(1),
                                TimeSpan.FromMinutes(10),
                                None,
                                None,
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
                        !pathVariableValue.Contains(runDir, StringComparison.OrdinalIgnoreCase))
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
                            from uCopy in TryAsync (async () =>
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


        EitherAsync<Error, Unit> RunWarmup(string eryphBinPath)
        {
            var cancelTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            return TryAsync(async () =>
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

    private static async Task<int> SelfUnInstall(FileSystemInfo? outFile, bool deleteOutFile, bool deleteAppData)
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
                var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "eryph");

                var ovsDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "openvswitch");


                var loggerFactory = new SerilogLoggerFactory(Log.Logger);
                var sysEnv = new SystemEnvironment(loggerFactory);
                var serviceManager = sysEnv.GetServiceManager("eryph-zero");
                var syncClient = new SyncClient();
                try
                {
                    Log.Information("Uninstalling eryph-zero service");

                    var unInstallService =
                        from serviceExists in serviceManager.ServiceExists()
                        let cancelSource1 = new CancellationTokenSource(TimeSpan.FromMinutes(1))
                        from uNetworkStopped in serviceExists
                            ? LogProgress("Stopping chassis services...").ToEither().MapAsync(async _ =>
                            {
                                var fin = await syncClient.SendSyncCommand("STOP_VSWITCH", cancelSource1.Token)
                                    .Bind(_ => syncClient.SendSyncCommand("STOP_OVSDB", cancelSource1.Token))
                                    .Run();
                                fin.IfFail(l =>
                                    Log.Logger.Debug("Failed to send stop chassis commands. Error: {error}", l));
                                return Unit.Default; // ignore error from stop command - we can also take control of existing processes
                            }).ToAsync()
                            : Unit.Default

                        from uStopped in serviceExists
                            ?
                            LogProgress("Stopping running service...").Bind(_ =>
                                serviceManager.EnsureServiceStopped(cancelSource1.Token))
                            : Unit.Default
                        let cancelSource2 = new CancellationTokenSource(TimeSpan.FromMinutes(5))
                        from uUninstalled in serviceExists
                            ? LogProgress("Removing service...").Bind(_ => serviceManager.RemoveService(cancelSource2.Token))
                            : Unit.Default
                        select Unit.Default;

                    _ = await unInstallService.IfLeft(l => l.Throw());

                    var ovsPath = OVSPackage.GetCurrentOVSPath();

                    if (ovsPath != null)
                    {
                        var ovsEnv = new EryphOVSEnvironment(new EryphOvsPathProvider(ovsPath), loggerFactory);
                        var ovsControl = new OVSControl(ovsEnv);
                        await using var ovsDbNode = new OVSDbNode(ovsEnv, loggerFactory);
                        await using var ovsVSwitchNode = new OVSSwitchNode(ovsEnv, loggerFactory);
                        var cancelSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                        // ReSharper disable AccessToDisposedClosure

                        var ovsCleanup =
                            from uStartLog in LogProgress("Starting temporary chassis services...")
                            from dbStart in ovsDbNode.Start(cancelSource.Token).MapAsync( async _=>
                            {
                                await ovsDbNode.WaitForStart(cancelSource.Token);
                                return Unit.Default;
                            })
                            from switchStart in ovsVSwitchNode.Start(cancelSource.Token).MapAsync(async _ =>
                            {
                                await ovsVSwitchNode.WaitForStart(cancelSource.Token);
                                return Unit.Default;
                            })
                            from delay in Delay(2000)
                            let controlCancel = new CancellationTokenSource(TimeSpan.FromMinutes(5))
                            from uCleanup in LogProgress("Removing OVS bridges....")
                            from bridges in ovsControl.GetBridges(controlCancel.Token)
                            from uRemove in bridges.Map(b => ovsControl.RemoveBridge(b.Name, controlCancel.Token))
                                .TraverseSerial(l => l).Map(_ => Unit.Default)
                            let stopCancel = new CancellationTokenSource(TimeSpan.FromMinutes(5))

                            from uStopLog in LogProgress("Stopping temporary chassis services...")
                            from switchStop in ovsVSwitchNode.Stop(true, stopCancel.Token)
                            from dbStop in ovsDbNode.Stop(true, stopCancel.Token)
                            
                            select Unit.Default;
                        
                        _ = await ovsCleanup.IfLeft(l =>
                        {
                            Log.Warning("OVS Cleanup failed with error '{error}'.\nIf necessary, delete OVS network adapters manually.", l);

                        });

                        // ReSharper restore AccessToDisposedClosure

                    }

                    if (Directory.Exists(ovsDataDir))
                    {
                        try
                        {
                            Directory.Delete(ovsDataDir, true);
                        }
                        catch (Exception ex)
                        {
                            Log.Logger.Debug(ex, "Failed to delete ovs data files from '{ovsPath}'", ovsDataDir);
                            Log.Warning("OVS data files cleanup failed with error '{error}'. \nIf necessary, delete OVS data files manually from {ovsPath}", ex.Message, ovsDataDir);
                        }
                    }

                    using var psEngine = new PowershellEngine(loggerFactory.CreateLogger<PowershellEngine>());
                    var removeSwitch = from extensions in psEngine.GetObjectsAsync<VMSwitchExtension>(PsCommandBuilder.Create()
                            .AddCommand("Get-VMSwitch")
                            .AddCommand("Get-VMSwitchExtension")
                            .AddParameter("Name", "dbosoft Open vSwitch Extension")).ToAsync().ToError()
                        from uRemove in extensions.Where(e => e.Value.Enabled)
                            .Map(e => LogProgress($"Removing vm switch '{e.Value.SwitchName}'...").Bind(_ => psEngine.RunAsync(PsCommandBuilder.Create()
                                                   .AddCommand("Remove-VMSwitch")
                                                   .AddParameter("Name", e.Value.SwitchName)
                                                   .AddParameter("Force", true)).ToAsync().ToError()))
                            .TraverseSerial(l => l).Map(_ => Unit.Default)
                        select Unit.Default;

                    _ = await removeSwitch.IfLeft(l =>
                    {
                        Log.Warning("VM Switch cleanup failed with error '{error}'.\nIf necessary, delete the eryph overlay switch manually.", l);

                    });

                    var removeDriver = DriverCommands.RemoveDriver(loggerFactory);
                    _ = (await removeDriver).IfFail(l =>
                    {
                        Log.Warning("Hyper-V switch extension cleanup failed with error '{error}'.\nIf necessary, remove the Hyper-V switch extension manually.", l);

                    });

                    if (Directory.Exists(dataDir) && deleteAppData)
                    {
                        Log.Logger.Information("Removing data files...");
                        Directory.Delete(dataDir, true);
                    }

                    UnregisterUninstaller();

                    Log.Logger.Information("Uninstallation completed");

                    return 0;

                }
                catch (Exception ex)
                {
                    Log.Error("Uninstallation failed. Error: {message}", ex.Message);
                    Log.Debug(ex, "Error Details");

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


        EitherAsync<Error, Unit> RunWarmup(string eryphBinPath)
        {
            var cancelTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            return TryAsync(async () =>
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

    private static EitherAsync<Error, Unit> LogProgress(string message)
    {
        Log.Logger.Information(message);
        return Unit.Default;
    }

    private static EitherAsync<Error, Unit> Delay(int timeout)
    {
        async Task<Either<Error, Unit>> DelayAsync()
        {
            await Task.Delay(timeout);
            return Unit.Default;
        }

        return DelayAsync().ToAsync();
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

    private static Task<int> GetAgentSettings(FileSystemInfo? outFile) =>
        RunAsAdmin(
            from hostSettings in HostSettingsProvider<SimpleConsoleRuntime>.getHostSettings()
            from yaml in VmHostAgentConfiguration<SimpleConsoleRuntime>.getConfigYaml(
                Path.Combine(ZeroConfig.GetVmHostAgentConfigPath(), "agentsettings.yml"),
                hostSettings)
            from _ in WriteOutput<SimpleConsoleRuntime>(outFile, yaml)
            select unit,
            SimpleConsoleRuntime.New());

    private static Task<int> ImportAgentSettings(
        FileSystemInfo? inFile,
        bool nonInteractive,
        bool noCurrentConfigCheck) =>
        RunAsAdmin(
            from configString in ReadInput(inFile)
            from hostSettings in HostSettingsProvider<SimpleConsoleRuntime>.getHostSettings()
            from _ in VmHostAgentConfigurationUpdate<SimpleConsoleRuntime>.updateConfig(
                configString,
                Path.Combine(ZeroConfig.GetVmHostAgentConfigPath(), "agentsettings.yml"),
                hostSettings)
            select unit,
            SimpleConsoleRuntime.New());

    private static Task<int> Login() =>
        RunAsAdmin(
            from _ in unitEff
            let genePoolApiStore = new ZeroGenePoolApiKeyStore()
            from __ in GenePoolCli<SimpleConsoleRuntime>.login(genePoolApiStore)
            select unit,
            SimpleConsoleRuntime.New());

    private static Task<int> GetGenePoolInfo() =>
        RunAsAdmin(
            from _ in unitEff
            let genePoolApiStore = new ZeroGenePoolApiKeyStore()
            from __ in GenePoolCli<SimpleConsoleRuntime>.getApiKeyStatus(genePoolApiStore)
            select unit,
            SimpleConsoleRuntime.New());

    private static Task<int> Logout() =>
        RunAsAdmin(
            from _ in unitEff
            let genePoolApiStore = new ZeroGenePoolApiKeyStore()
            from __ in GenePoolCli<SimpleConsoleRuntime>.logout(genePoolApiStore)
            select unit,
            SimpleConsoleRuntime.New());

    private static async Task<int> GetDriverStatus()
    {
        using var loggerFactory = new NullLoggerFactory();
        using var psEngine = new PowershellEngine(loggerFactory.CreateLogger<PowershellEngine>());

        return await RunAsAdmin(
            DriverCommands.GetDriverStatus(),
            new DriverCommandsRuntime(new(new CancellationTokenSource(), loggerFactory, psEngine)));
    }

    private static Task<int> GetNetworks(FileSystemInfo? outFile) =>
        Run(
            from yaml in new NetworkProviderManager().GetCurrentConfigurationYaml().ToAff(e => e)
            from _ in  WriteOutput<SimpleConsoleRuntime>(outFile, yaml)
            select unit,
            SimpleConsoleRuntime.New());

    private static async Task<int> ImportNetworkConfig(FileSystemInfo? inFile, bool nonInteractive,
        bool noCurrentConfigCheck)
    {
        using var nullLoggerFactory = new NullLoggerFactory();
        using var psEngine = new PowershellEngine(nullLoggerFactory.CreateLogger(""));
        var ovsRunDir = OVSPackage.UnpackAndProvide(nullLoggerFactory.CreateLogger<OVSPackage>());
        var sysEnv = new EryphOVSEnvironment(new EryphOvsPathProvider(ovsRunDir), nullLoggerFactory);

        return await RunAsAdmin(
            from configString in ReadInput(inFile)
            from _ in ensureDriver(ovsRunDir, true, false)
            from newConfig in importConfig(configString)
            from currentConfig in getCurrentConfiguration()
            from hostState in getHostStateWithProgress()
            from syncResult in noCurrentConfigCheck
                ? SuccessAff((false, hostState))
                : from currentConfigChanges in generateChanges(hostState, currentConfig)
                  from r in syncCurrentConfigBeforeNewConfig(hostState, currentConfigChanges, nonInteractive)
                  select r
            from newConfigChanges in generateChanges(syncResult.HostState, newConfig)
            from validateImpact in validateNetworkImpact(newConfig)
            from __ in applyChangesInConsole(currentConfig, newConfigChanges,
                nonInteractive, syncResult.IsValid)

            from save in saveConfigurationYaml(configString)
            from sync in syncNetworks()
            from m in writeLine("New Network configuration was imported.")
            select unit,
            new ConsoleRuntime(nullLoggerFactory, psEngine, sysEnv, new CancellationTokenSource()));
    }

    private static async Task<int> SyncNetworkConfig(bool nonInteractive)
    {
        using var nullLoggerFactory = new NullLoggerFactory();
        using var psEngine = new PowershellEngine(nullLoggerFactory.CreateLogger(""));
        var ovsRunDir = OVSPackage.UnpackAndProvide(nullLoggerFactory.CreateLogger<OVSPackage>());
        var sysEnv = new EryphOVSEnvironment(new EryphOvsPathProvider(ovsRunDir), nullLoggerFactory);

        return await RunAsAdmin(
            from _ in writeLine("Going to sync network state with the current configuration...")
            from __ in ensureDriver(ovsRunDir, true, false)
            from currentConfig in getCurrentConfiguration()
            from hostState in getHostStateWithProgress()
            from pendingChanges in generateChanges(hostState, currentConfig)
            from ___ in applyChangesInConsole(currentConfig, pendingChanges, nonInteractive, false)
            from ____ in syncNetworks()
            select unit,
            new ConsoleRuntime(nullLoggerFactory, psEngine, sysEnv, new CancellationTokenSource()));
    }

    private static Aff<string> ReadInput(FileSystemInfo? inFile) => AffMaybe(async () =>
    {
        // This is not using a runtime for simplicity. We need to read from stdin
        // which is not supported out of the box.

        if (inFile is not null)
            return await File.ReadAllTextAsync(inFile.FullName);

        if (!Console.IsInputRedirected)
            return FinFail<string>(Error.New(
                "Error: Supply the new config to stdin or use --inFile option to read from file"));

        await using var reader = Console.OpenStandardInput();
        using var textReader = new StreamReader(reader);
        return await textReader.ReadToEndAsync();
    });

    private static Aff<RT, Unit> WriteOutput<RT>(FileSystemInfo? outFile, string content)
        where RT : struct, HasAnsiConsole<RT>, HasFile<RT> =>
        from _ in Optional(outFile).Match(
            Some: fsi => File<RT>.writeAllText(fsi.FullName, content),
            None: () => AnsiConsole<RT>.writeLine(content))
        select unit;

    private static Task<int> Run<RT>(Aff<RT, Unit> action, RT runtime)
        where RT : struct, HasAnsiConsole<RT>, HasCancel<RT> =>
        action.Catch(e =>
                from _ in AnsiConsole<RT>.write(new Rows(
                    new Markup("[red]The command failed with the following error(s):[/]"),
                    Renderable(e)))
                from __ in FailAff<RT>(e)
                select unit)
            .Run(runtime)
            .AsTask()
            .Map(r => r.Match(
                Succ: _ => 0,
                Fail: error => error.Code != 0 ? error.Code : -1));

    private static Task<int> RunAsAdmin<RT>(Aff<RT, Unit> action, RT runtime)
        where RT : struct, HasAnsiConsole<RT>, HasCancel<RT> =>
        AdminGuard.CommandIsElevated(() => Run(action, runtime));

    private static void CopyDirectory(string sourceDir, string destinationDir, params string[] ignoredFiles)
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
            if(ignoredFiles.Contains(file.Name))
                continue;

            var targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (var subDir in dirs)
        {
            var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }

    private const string RegistryKeyName = "Eryph Zero";

    private static void RegisterUninstaller(string installDirectory)
    {
        var fileVersion = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location);
        var uninstallerPath = Path.Combine(installDirectory, "bin", "eryph-uninstaller.exe");

        var uninstallKey = Registry.LocalMachine.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
                writable: true)
            ?? throw new InvalidOperationException("Could not open the uninstall registry.");

        uninstallKey.DeleteSubKeyTree(RegistryKeyName, throwOnMissingSubKey: false);
        var eryphKey = uninstallKey.CreateSubKey(RegistryKeyName)
            ?? throw new InvalidOperationException("Could not create registry key for registering the uninstaller.");

        eryphKey.SetValue("DisplayName", "Eryph Zero");
        eryphKey.SetValue("DisplayIcon", @"C:\Windows\System32\msiexec.exe,0");
        eryphKey.SetValue("UninstallString", uninstallerPath);
        eryphKey.SetValue("DisplayVersion", fileVersion.ProductVersion ?? "");
        eryphKey.SetValue("Publisher", "dbosoft GmbH");
        eryphKey.SetValue("InstallLocation", installDirectory);
        eryphKey.SetValue("VersionMajor", fileVersion.ProductMajorPart);
        eryphKey.SetValue("VersionMinor", fileVersion.ProductMinorPart);
        eryphKey.SetValue("URLInfoAbout ", "https://www.eryph.io");
    }

    private static void UnregisterUninstaller()
    {
        var uninstallKey = Registry.LocalMachine.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
                writable: true)
            ?? throw new InvalidOperationException("Could not open the uninstall registry.");

        uninstallKey.DeleteSubKeyTree(RegistryKeyName);
    }
}
