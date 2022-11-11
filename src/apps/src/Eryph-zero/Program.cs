using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.App;
using Eryph.ModuleCore;
using Eryph.Modules.CommonApi;
using Eryph.Modules.ComputeApi;
using Eryph.Modules.VmHostAgent;
using Eryph.Runtime.Zero.Configuration;
using Eryph.Runtime.Zero.Configuration.Clients;
using Eryph.Runtime.Zero.HttpSys;
using Eryph.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Serilog;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Runtime.Zero;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand();

        var runCommand = new Command("run");
        runCommand.SetHandler(_ => Run(args));
        rootCommand.AddCommand(runCommand);

        var installCommand = new Command("install");
        installCommand.SetHandler(_ => SelfInstall());
        rootCommand.AddCommand(installCommand);

        var networksCommand = new Command("networks");
        rootCommand.AddCommand(networksCommand);

        var getNetworksCommand = new Command("get");
        getNetworksCommand.SetHandler(_ => GetNetworks());
        networksCommand.AddCommand(getNetworksCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static Task<int> Run(string[] args)
    {
        return AdminGuard.CommandIsElevated(async () => {

            var returnCode = 0;

            var logFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "eryph", "zero", "logs", "debug.txt");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 10,
                    retainedFileTimeLimit: TimeSpan.FromDays(30))
                .CreateLogger();

            try
            {
                var fileVersion = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location);
                Log.Logger.Information("Starting eryph-zero {version}", fileVersion.ProductVersion);

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

                        };
                    });


                await using var processLock = new ProcessFileLock(Path.Combine(ZeroConfig.GetConfigPath(), ".lock"));

                var basePathUrl = ConfigureUrl(startupConfig.BasePath);

                var endpoints = new Dictionary<string, string>
                {
                    { "identity", $"{basePathUrl}identity" },
                    { "compute", $"{basePathUrl}compute" },
                    { "common", $"{basePathUrl}common" },
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

                var container = new Container();
                container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
                container.Bootstrap();
                container.RegisterInstance<IEndpointResolver>(new EndpointResolver(endpoints));

                var builder = ModulesHost.CreateDefaultBuilder(args) as ModulesHostBuilder;

                var host =
                    builder!

                        .ConfigureInternalHost(hb => { hb.UseWindowsService(cfg => cfg.ServiceName = "eryph-zero"); })
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
                            });
                        })
                        .HostModule<CommonApiModule>()
                        .HostModule<ComputeApiModule>()
                        .AddIdentityModule(container)
                        .HostModule<VmHostAgentModule>()
                        .AddControllerModule(container)
                        .ConfigureServices(c => c.AddSingleton(_ => container.GetInstance<IEndpointResolver>()))
                        .ConfigureServices(LoggerProviderOptions.RegisterProviderOptions<
                            EventLogSettings, EventLogLoggerProvider>)
                        .ConfigureLogging((context, logging) =>
                        {
                            logging.AddSerilog();
                            // See: https://github.com/dotnet/runtime/issues/47303
                            logging.AddConfiguration(
                                context.Configuration.GetSection("Logging"));
                        })
                        .Build();

                //starting here all errors should be considered as recoverable
                returnCode = -1;


                await host.RunAsync();

                return returnCode;
            }
            catch (Exception ex)
            {
                Log.Logger.Fatal(ex, "eryph-zero failure");

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
        var configHost = new HostBuilder()
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

    private static Task<int> SelfInstall()
    {
        return AdminGuard.CommandIsElevated(async () =>
        {
            var targetDir =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "eryph", "zero");
            var zeroExe = Path.Combine(targetDir, "bin", "eryph-zero.exe");

            var backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "eryph", "zero.old");

            var serviceRemoved = false;
            var serviceStopped = false;
            var backupCreated = false;
            try
            {
                var baseDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                var parentDir = baseDir.Parent?.FullName ?? throw new IOException($"Invalid path {baseDir}");

                if (Directory.Exists(backupDir))
                    Directory.Delete(backupDir, true);

                if (IsServiceRunning("eryph-zero"))
                {
                    StopService("eryph-zero");
                    serviceStopped = true;
                }

                if (IsServiceInstalled("eryph-zero"))
                {
                    await UnInstallService("eryph-zero");
                    serviceRemoved = true;
                }

                if (Directory.Exists(targetDir))
                {
                    Directory.Move(targetDir, backupDir);
                    backupCreated = true;
                }

                CopyDirectory(parentDir, targetDir);

#if DEBUG
                var dirName = Directory.GetDirectories(targetDir).FirstOrDefault();
                if (dirName != null && dirName != "bin")
                {
                    Directory.Move(dirName, Path.Combine(targetDir, "bin"));
                }
#endif


                if (!IsServiceInstalled("eryph-zero"))
                    await InstallService("eryph-zero", zeroExe, "run");

                StartService("eryph-zero");

                if (Directory.Exists(backupDir))
                    Directory.Delete(backupDir, true);

                return 0;

            }
            catch (Exception ex)
            {
                await Console.Error.WriteAsync(ex.Message);

                //undo operation
                if (backupCreated) Directory.Move(backupDir, targetDir);
                if (serviceRemoved) await InstallService("eryph-zero", zeroExe, "run");
                if (serviceStopped) StartService("eryph-zero");

                return -1;
            }


            ServiceController GetServiceController(string serviceName)
            {
                return new ServiceController(serviceName);
            }

            bool IsServiceInstalled(string serviceName)
            {
                try
                {
                    using var controller = GetServiceController(serviceName);
                    // ReSharper disable once UnusedVariable
                    var dummy = controller.Status;
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            void StopService(string serviceName)
            {
                using var controller = GetServiceController(serviceName);
                controller.Stop();
                controller.WaitForStatus(ServiceControllerStatus.Stopped);
            }

            bool IsServiceRunning(string serviceName)
            {
                if (!IsServiceInstalled(serviceName))
                    return false;

                using var controller = GetServiceController(serviceName);
                return controller.Status == ServiceControllerStatus.Running;
            }

            void StartService(string serviceName)
            {
                using var controller = GetServiceController(serviceName);
                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running);
            }

            async Task UnInstallService(string serviceName)
            {
                var cmd = $@"delete {serviceName}";
                var process = Process.Start(new ProcessStartInfo("sc", cmd)
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                });

                if (process == null)
                    return;

                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    var output = await process.StandardError.ReadToEndAsync();
                    throw new IOException($"Failed to remove service {serviceName}. Message: {output}");
                }
            }

            async Task InstallService(string serviceName, string path, string arguments)
            {
                var cmd = $@"create {serviceName} BinPath=""\""{path}\"" {arguments}"" Start=Auto";
                var process = Process.Start(new ProcessStartInfo("sc", cmd)
                {
                    RedirectStandardError = true
                });

                if (process == null)
                    return;

                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    var output = await process.StandardError.ReadToEndAsync();
                    throw new IOException($"Failed to install service {serviceName}. Message: {output}");

                }
            }

            static void CopyDirectory(string sourceDir, string destinationDir)
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
        });
    }

    private static Task<int> GetNetworks()
    {

        return 0;
    }
}
