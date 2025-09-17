using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Dbosoft.OVN.Windows;
using Eryph.AnsiConsole.Sys;
using Eryph.App;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.ModuleCore;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.Controller;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.Seeding;
using Eryph.Modules.GenePool.Genetics;
using Eryph.Modules.HostAgent;
using Eryph.Modules.HostAgent.Configuration;
using Eryph.Modules.HostAgent.Networks.OVS;
using Eryph.Runtime.Zero.Configuration;
using Eryph.Runtime.Zero.Configuration.AgentSettings;
using Eryph.Runtime.Zero.Configuration.Networks;
using Eryph.Runtime.Zero.Startup;
using Eryph.StateDb.Sqlite;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys.IO;
using LanguageExt.Sys.Traits;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Logging;
using Microsoft.Win32;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Sinks.SystemConsole.Themes;
using Serilog.Templates;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using Spectre.Console;

using static Eryph.Modules.HostAgent.Networks.NetworkProviderManager<Eryph.Runtime.Zero.ConsoleRuntime>;
using static Eryph.Modules.HostAgent.Networks.ProviderNetworkUpdate<Eryph.Runtime.Zero.ConsoleRuntime>;
using static Eryph.Modules.HostAgent.Networks.ProviderNetworkUpdateInConsole<Eryph.Runtime.Zero.ConsoleRuntime>;
using static Eryph.Modules.HostAgent.Networks.OvsDriverProvider<Eryph.Runtime.Zero.ConsoleRuntime>;
using static LanguageExt.Sys.Console<Eryph.Runtime.Zero.ConsoleRuntime>;

using static LanguageExt.Prelude;
using static Eryph.AnsiConsole.Prelude;

namespace Eryph.Runtime.Zero;

internal static class Program
{
    private static GenePoolSettings _genepoolSettings = GenePoolConstants.ProductionGenePool;

    private static async Task<int> Main(string[] args)
    {
        IdentityModelEventSource.ShowPII = true;

        // we use environment variables here as it is only used for testing and packer is using same variables
        var stagingAuthority = Environment.GetEnvironmentVariable("ERYPH_GENEPOOL_AUTHORITY") == "staging";

        if (stagingAuthority)
        {
            _genepoolSettings = GenePoolConstants.StagingGenePool;
            var overwriteGenePoolApi = Environment.GetEnvironmentVariable("ERYPH_GENEPOOL_API");
            if(!string.IsNullOrWhiteSpace(overwriteGenePoolApi))
                _genepoolSettings = _genepoolSettings with { ApiEndpoint = new Uri(overwriteGenePoolApi) };

        }


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
        var deleteCatlets = new System.CommandLine.Option<bool>(
            name: "--delete-catlets",
            description: "Delete all catlets and disks");
        uninstallCommand.AddOption(deleteCatlets);
        uninstallCommand.SetHandler(SelfUnInstall, outFileOption, deleteOutFile, deleteAppData, deleteCatlets);
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

        var genePoolCommand = new Command("genepool");
        rootCommand.AddCommand(genePoolCommand);

        var genePoolInfoCommand = new Command("info");
        genePoolCommand.AddCommand(genePoolInfoCommand);
        genePoolInfoCommand.SetHandler(() => GetGenePoolInfo(_genepoolSettings));

        var loginCommand = new Command("login");
        genePoolCommand.AddCommand(loginCommand);
        loginCommand.SetHandler(() => Login(_genepoolSettings));

        var logoutCommand = new Command("logout");
        genePoolCommand.AddCommand(logoutCommand);
        logoutCommand.SetHandler(() => Logout(_genepoolSettings));


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
            string? basePath;
            Serilog.Core.Logger logger;

            try
            {
                var startupConfig = ReadConfiguration(args);
                basePath = startupConfig["basePath"];
                logger = ZeroLogging.CreateLogger(startupConfig);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync(ex.ToString());
                return -1;
            }

            var warmupMode = args.Contains("--warmup");

            try
            {
                logger.Information("Starting eryph-zero {Version}", new ZeroApplicationInfoProvider().ProductVersion);

                if (warmupMode)
                    logger.Information("Running in warmup mode. Process will be stopped after start is completed.");

                await using var processLock = new ProcessFileLock(Path.Combine(ZeroConfig.GetConfigPath(), ".lock"));

                // We need to ensure this early that the configuration directories exist
                // as the identity module tries to write files during bootstrapping.
                ZeroConfig.EnsureConfiguration();

                var container = new Container();
                container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
                
                if (warmupMode)
                {
                    container.UseSqlLite();
                    container.RegisterInstance<IEryphOvsPathProvider>(new EryphOvsPathProvider());

                    container.RegisterSingleton<System.IO.Abstractions.IFileSystem, FileSystem>();
                    container.Register<IHostSettingsProvider, HostSettingsProvider>();
                    container.Register<INetworkProviderManager, NetworkProviderManager>();
                    container.Register<IVmHostAgentConfigurationManager, VmHostAgentConfigurationManager>();

                    container.AddStateDbDataServices();

                    // Warmup mode only performs minimal validation
                    var warmupHost = Host.CreateDefaultBuilder(args)
                        .ConfigureEryphAppConfiguration(args)
                        .ConfigureAppConfiguration((_, config) =>
                        {
                            config.AddInMemoryCollection(new Dictionary<string, string>
                            {
                                ["warmupMode"] = bool.TrueString,
                            });
                        })
                        .ConfigureChangeTracking()
                        .ConfigureServices((context, services )=>
                        {
                            var changeTrackingConfig = new ChangeTrackingConfig();
                            context.Configuration.GetSection("ChangeTracking").Bind(changeTrackingConfig);
                            services.AddSimpleInjector(container, options =>
                            {
                                options.AddLogging();
                                
                                options.RegisterSqliteStateStore();

                                options.AddStartupHandler<EnsureHyperVAndOvsStartupHandler>();
                                options.AddStartupHandler<EnsureConfigurationStartupHandler>();
                                options.AddStartupHandler<DatabaseResetHandler>();

                                container.RegisterInstance(changeTrackingConfig);
                                options.AddSeeding(changeTrackingConfig);
                                if (changeTrackingConfig.TrackChanges)
                                    options.AddChangeTracking();
                            });
                        })
                        // The logger must not be disposed here as it is used for error reporting
                        // after the host has stopped.
                        .UseSerilog(logger: logger, dispose: false)
                        .Build()
                        .UseSimpleInjector(container);
                    try
                    {
                        await warmupHost.StartAsync();
                        await Task.Delay(1000);
                        logger.Information("Warmup completed. Stopping.");
                        using var cancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                        await warmupHost.StopAsync(cancelSource.Token);
                        return 0;
                    }
                    finally
                    {
                        await ((IAsyncDisposable)warmupHost).DisposeAsync();
                    }
                }

                var basePathUrl = ConfigureUrl(basePath);

                var endpoints = new Dictionary<string, string>
                {
                    { "base", $"{basePathUrl}" },
                    { "identity", $"{basePathUrl}identity" },
                    { "compute", $"{basePathUrl}compute" },
                };

                processLock.SetMetadata(new Dictionary<string, object>
                {
                    {
                        "endpoints", endpoints
                    }
                });

                container.Bootstrap();
                container.RegisterInstance<IEndpointResolver>(new EndpointResolver(endpoints));

                var builder = ModulesHost.CreateDefaultBuilder(args);
                // Explicitly set the content root. Hosuto defaults to Environment.CurrentDirectory.
                // This leads to incorrect behavior when running as a Windows service. Windows services
                // are started with the system32 directory as the working directory.
                builder.UseContentRoot(AppContext.BaseDirectory);

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
                    .ConfigureEryphAppConfiguration(args)
                    .ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string>
                        {
                            { "bus:type", "inmemory" },
                            { "databus:type", "inmemory" },
                            { "store:type", "inmemory" },
                        });
                    })
                    .ConfigureChangeTracking()
                    .HostModule<ZeroStartupModule>()
                    .AddVmHostAgentModule()
                    .AddGenePoolModule()
                    .AddNetworkModule()
                    .AddControllerModule(container)
                    .AddComputeApiModule()
                    .AddIdentityModule(container)
                    .ConfigureServices(c => c.AddSingleton(_ => container.GetInstance<IEndpointResolver>()))
                    .ConfigureServices(c => c.AddSingleton(_genepoolSettings))
                    .ConfigureHostOptions(cfg => cfg.ShutdownTimeout = new TimeSpan(0, 0, 15))
                    // The logger must not be disposed here as it is injected into multiple modules.
                    // Serilog requires a single logger instance for synchronization.
                    .UseSerilog(logger: logger, dispose: false)
                    .Build();

                await host.RunAsync();
                return 0;
                
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "eryph-zero failure");
                return ex is ErrorException { Code: < 0 } eex ? eex.Code : -1;
            }
            finally
            {
                await logger.DisposeAsync();
            }
        });

    }

    private static Uri ConfigureUrl(string? basePath)
    {
        var uriBuilder = new UriBuilder(basePath ?? "https://localhost:0");
        if (uriBuilder.Port == 0)
            uriBuilder.Port = GetAvailablePort();

        return uriBuilder.Uri;
    }

    private static IConfiguration ReadConfiguration(string[] args)
    {
        using var configHost = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureEryphAppConfiguration(args)
            .Build();

        return configHost.Services.GetRequiredService<IConfiguration>();
    }

    private static T ConfigureEryphAppConfiguration<T>(this T hostBuilder, string[] args)
        where T : IHostBuilder
    {
        hostBuilder.ConfigureAppConfiguration((hostingContext, config) =>
        {
            var env = hostingContext.HostingEnvironment;
            config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: false)
                .AddJsonFile(Path.Combine(ZeroConfig.GetConfigPath(), "appsettings.json"), optional: true, reloadOnChange: false);

            config.AddEnvironmentVariables();
            config.AddEnvironmentVariables("ERYPH_");

            if (args is { Length: > 0 })
            {
                config.AddCommandLine(args);
            }
        });

        return hostBuilder;
    }

    private static T ConfigureChangeTracking<T>(this T hostBuilder) where T : IHostBuilder
    {
        hostBuilder.ConfigureAppConfiguration((_, config) =>
        {
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
        });

        return hostBuilder;
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
                var sysEnv = new WindowsSystemEnvironment(loggerFactory);
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
                                    var cancelSourceCopy = new CancellationTokenSource(TimeSpan.FromMinutes(1));
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


        EitherAsync<Error, Unit> RunWarmup(string eryphBinPath) => TryAsync(async () =>
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

            try
            {
                using var cancelTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                await process.WaitForExitAsync(cancelTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                // Kill() is asynchronous. Hence, we wait again. No cancellation token
                // is provided as the process blocks the rollback. If we get stuck here,
                // the user needs to intervene anyway.
                await process.WaitForExitAsync();
                throw Error.New("Warmup did not complete within the allotted time.");
            }
            
            if (process.ExitCode != 0)
                throw Error.New($"Warmup failed with exit code {process.ExitCode}.");
            
            return Unit.Default;
        }).ToEither();
        
    }

    private static async Task<int> SelfUnInstall(FileSystemInfo? outFile, bool deleteOutFile, bool deleteAppData, bool deleteCatlets)
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
            var consoleTemplate = new ExpressionTemplate(
                "[{@t:yyyy-MM-dd HH:mm:ss.fff} {@l:u3}] {@m}\n{#if @x is not null}{Inspect(@x).Message}\n{#end}");
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Logger(c =>
                    c.MinimumLevel.Is(LogEventLevel.Information)
                    .WriteTo.Console(consoleTemplate))
                .CreateLogger();

            return await AdminGuard.CommandIsElevated(async () =>
            {
                var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "eryph");

                var ovsDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "openvswitch");

                var loggerFactory = new SerilogLoggerFactory(Log.Logger);
                var sysEnv = new WindowsSystemEnvironment(loggerFactory);
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
                                fin.IfFail(l => Log.Logger.Debug(l, "Failed to send stop chassis commands."));
                                return Unit.Default; // ignore error from stop command - we can also take control of existing processes
                            }).ToAsync()
                            : Unit.Default

                        from uStopped in serviceExists
                            ? LogProgress("Stopping running service...")
                                .Bind(_ => serviceManager.EnsureServiceStopped(cancelSource1.Token))
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
                        var ovsEnv = new EryphOvsEnvironment(new EryphOvsPathProvider(ovsPath), loggerFactory);
                        var ovsControl = new OVSControl(ovsEnv);
                        await using var ovsDbNode = new OVSDbNode(ovsEnv, new LocalOVSWithOVNSettings(), loggerFactory);
                        await using var ovsVSwitchNode = new OVSSwitchNode(ovsEnv, new LocalOVSWithOVNSettings(), loggerFactory);
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
                            from uRemove in bridges
                                .Map(b => ovsControl.RemoveBridge(b.Name, controlCancel.Token)
                                    .MapLeft(l => Error.New($"Failed to remove OVS bridge {b.Name}.", l)))
                                .SequenceSerial().Map(_ => Unit.Default)
                            let stopCancel = new CancellationTokenSource(TimeSpan.FromMinutes(5))

                            from uStopLog in LogProgress("Stopping temporary chassis services...")
                            from switchStop in ovsVSwitchNode.Stop(true, stopCancel.Token)
                            from dbStop in ovsDbNode.Stop(true, stopCancel.Token)
                            
                            select Unit.Default;
                        
                        _ = await ovsCleanup.IfLeft(l =>
                        {
                            Log.Logger.Warning(l, "The OVS cleanup failed. If necessary, delete the OVS network adapters manually.");
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
                            Log.Logger.Warning(ex, "The OVS data files cleanup failed. If necessary, delete the OVS data files manually from '{OvsPath}'.", ovsDataDir);
                        }
                    }

                    await DriverCommands.Run(
                        UninstallCommands.RemoveNetworking(),
                        loggerFactory);

                    UninstallCommands.RemoveCertificatesAndKeys().Run()
                        .IfFail(e => Log.Logger.Warning(e,
                            "The cleanup of the certificates failed. Please remove any leftover certificates from the Windows certificate store."));

                    // We need the configuration stored in the data directory to perform
                    // the cleanup of the catlets and disks.
                    if (Directory.Exists(dataDir) && deleteCatlets)
                    {
                        await DriverCommands.Run(
                            UninstallCommands.RemoveCatletsAndDisk(),
                            loggerFactory);
                    }

                    if (Directory.Exists(dataDir) && deleteAppData)
                    {
                        try
                        {
                            Log.Logger.Information("Removing data files...");
                            Directory.Delete(dataDir, true);
                        }
                        catch (Exception ex)
                        {
                            Log.Logger.Warning(ex, "The data files cleanup failed. If necessary, delete data files manually from '{DataDir}'.", dataDir);
                        }
                    }

                    UnregisterUninstaller();

                    Log.Logger.Information("Uninstallation completed.");

                    return 0;

                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, "Uninstallation failed.");

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
                    await Task.Delay(2000);
                    File.Delete(outFile.FullName);
                }
            }
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
            from _1 in AnsiConsole<SimpleConsoleRuntime>.writeLine("Updating agent settings...")
            from _2 in VmHostAgentConfigurationUpdate<SimpleConsoleRuntime>.updateConfig(
                configString,
                Path.Combine(ZeroConfig.GetVmHostAgentConfigPath(), "agentsettings.yml"),
                hostSettings)
            // Check that the sync service is available (and hence the VM host agent is running).
            // When the VM host agent is not running, we do not need to sync the configuration.
            from canConnect in use(
                Eff(() => new CancellationTokenSource(TimeSpan.FromSeconds(2))),
                cts => default(SimpleConsoleRuntime).SyncClientEff
                    .Bind(sc => sc.CheckRunning(cts.Token))
                    .IfFail(_ => false))
            from _3 in canConnect
                ? from _1 in AnsiConsole<SimpleConsoleRuntime>.writeLine(
                    "eryph is running. Syncing agent settings...")
                  from _2 in default(SimpleConsoleRuntime).SyncClientEff.Bind(
                    sc => sc.SendSyncCommand("SYNC_AGENT_SETTINGS", CancellationToken.None))
                  select unit
                : SuccessAff(unit)
            select unit,
            SimpleConsoleRuntime.New());

    private static Task<int> Login(GenePoolSettings genepoolSettings) =>
        RunAsAdmin(
            from _ in unitEff
            let genePoolApiStore = new ZeroGenePoolApiKeyStore()
            from __ in GenePoolCli<SimpleConsoleRuntime>.login(genePoolApiStore, genepoolSettings)
            select unit,
            SimpleConsoleRuntime.New());

    private static Task<int> GetGenePoolInfo(GenePoolSettings genepoolSettings) =>
        RunAsAdmin(
            from _ in unitEff
            let genePoolApiStore = new ZeroGenePoolApiKeyStore()
            from __ in GenePoolCli<SimpleConsoleRuntime>.getApiKeyStatus(genePoolApiStore, genepoolSettings)
            select unit,
            SimpleConsoleRuntime.New());

    private static Task<int> Logout(GenePoolSettings genepoolSettings) =>
        RunAsAdmin(
            from _ in unitEff
            let genePoolApiStore = new ZeroGenePoolApiKeyStore()
            from __ in GenePoolCli<SimpleConsoleRuntime>.logout(genePoolApiStore, genepoolSettings)
            select unit,
            SimpleConsoleRuntime.New());

    private static async Task<int> GetDriverStatus()
    {
        using var loggerFactory = new NullLoggerFactory();
        using var psEngineLock = new PowershellEngineLock();
        using var psEngine = new PowershellEngine(
            loggerFactory.CreateLogger<PowershellEngine>(),
             psEngineLock);

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
        using var psEngineLock = new PowershellEngineLock();
        using var psEngine = new PowershellEngine(
            nullLoggerFactory.CreateLogger(""),
            psEngineLock);
        var ovsRunDir = OVSPackage.UnpackAndProvide(nullLoggerFactory.CreateLogger<OVSPackage>());
        var sysEnv = new EryphOvsEnvironment(new EryphOvsPathProvider(ovsRunDir), nullLoggerFactory);

        return await RunAsAdmin(
            from configString in ReadInput(inFile)
            from _1 in ensureDriver(ovsRunDir, true, false)
            from _2 in isAgentRunning()
            from newConfig in importConfig(configString)
            from currentConfig in getCurrentConfiguration()
            from defaults in getDefaults()
            from hostState in getHostStateWithProgress()
            from syncResult in noCurrentConfigCheck switch
            {
                true => SuccessAff((false, hostState)),
                false =>
                    from currentConfigChanges in generateChanges(hostState, currentConfig, true)
                    from r in syncCurrentConfigBeforeNewConfig(hostState, currentConfigChanges, nonInteractive)
                    from s in r.RefreshState
                        ? getHostStateWithProgress()
                        : SuccessAff(hostState)
                    select (r.IsValid, HostState: s)
            }
            from newConfigChanges in generateChanges(syncResult.HostState, newConfig, false)
            from _3 in validateNetworkImpact(newConfig, currentConfig, defaults)
            from _4 in applyChangesInConsole(currentConfig, newConfigChanges,
                getHostStateWithProgress, nonInteractive, syncResult.IsValid)
            from _5 in saveConfigurationYaml(configString)
            from _6 in syncNetworks()
            from _7 in writeLine("New Network configuration was imported.")
            from _8 in checkHostInterfacesWithProgress()
            select unit,
            new ConsoleRuntime(new ConsoleRuntimeEnv(
                nullLoggerFactory, psEngine, sysEnv, new CancellationTokenSource())));
    }

    private static async Task<int> SyncNetworkConfig(bool nonInteractive)
    {
        using var nullLoggerFactory = new NullLoggerFactory();
        using var psEngineLock = new PowershellEngineLock();
        using var psEngine = new PowershellEngine(
            nullLoggerFactory.CreateLogger(""),
            psEngineLock);
        var ovsRunDir = OVSPackage.UnpackAndProvide(nullLoggerFactory.CreateLogger<OVSPackage>());
        var sysEnv = new EryphOvsEnvironment(new EryphOvsPathProvider(ovsRunDir), nullLoggerFactory);

        return await RunAsAdmin(
            from _1 in writeLine("Going to sync network state with the current configuration...")
            from _2 in ensureDriver(ovsRunDir, true, false)
            from _3 in isAgentRunning()
            from currentConfig in getCurrentConfiguration()
            from hostState in getHostStateWithProgress()
            from pendingChanges in generateChanges(hostState, currentConfig, true)
            from _4 in applyChangesInConsole(currentConfig, pendingChanges,
                getHostStateWithProgress, nonInteractive, false)
            from _5 in syncNetworks()
            from _6 in checkHostInterfacesWithProgress()
            select unit,
            new ConsoleRuntime(new ConsoleRuntimeEnv(
                nullLoggerFactory, psEngine, sysEnv, new CancellationTokenSource())));
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

    private const string RegistryKeyName = "eryph-zero";

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

        eryphKey.SetValue("DisplayName", "eryph-zero");
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
