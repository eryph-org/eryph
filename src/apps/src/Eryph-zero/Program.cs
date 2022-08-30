using System;
using System.Collections.Generic;
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
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Runtime.Zero;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        await using var processLock = new ProcessFileLock(Path.Combine(ZeroConfig.GetConfigPath(), ".lock"));

        var startupConfig = StartupConfiguration(args,
            sp => new
            {
                BasePath = sp.GetRequiredService<IConfiguration>()["basePath"],
                SSLEndpointManager = sp.GetRequiredService<ISSLEndpointManager>()
            });

        var basePathUrl = ConfigureUrl(startupConfig.BasePath);
         

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

        var endpoints = new Dictionary<string, string>
        {
            { "identity", $"{basePathUrl}identity" },
            { "compute", $"{basePathUrl}compute" },
            { "common", $"{basePathUrl}common" },
        };

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

        var host = ModulesHost.CreateDefaultBuilder(args)
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
            .Build();

        processLock.SetMetadata(new Dictionary<string, object>
        {
            {
                "endpoints", endpoints
            }
        });

        await host.RunAsync();
        return 0;
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

        var certificateGenerator = configHost.Services.GetRequiredService<ICertificateGenerator>();
        var res = selectResult(configHost.Services);

 
        ZeroConfig.EnsureConfiguration();
        SystemClientGenerator.EnsureSystemClient(certificateGenerator);
        
        configHost.Dispose();

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