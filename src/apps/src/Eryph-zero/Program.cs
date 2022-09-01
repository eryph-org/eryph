using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Sockets;
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

                    builder
                    .ConfigureInternalHost(hb =>
                    {
                        hb.UseWindowsService(cfg=>cfg.ServiceName = "eryph-zero");
                    })
                    .UseAspNetCore((module, webHostBuilder) =>
                    {
                        webHostBuilder.UseHttpSys(options => { options.UrlPrefixes.Add(module.Path); });
                    })
                    .UseSimpleInjector(container)
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
                        { "basePath", "https://localhost:0" }
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


}
