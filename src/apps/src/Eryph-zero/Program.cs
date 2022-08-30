using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
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

        var startupConfig = await StartupConfiguration(args,
            sp => new
            {
                BasePath = sp.GetRequiredService<IConfiguration>()["basePath"]
            });


        var endpoints = new Dictionary<string, string>
        {
            { "identity", $"{startupConfig.BasePath}/identity" },
            { "compute", $"{startupConfig.BasePath}/compute" },
            { "common", $"{startupConfig.BasePath}/common" },
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

    private static async Task<T> StartupConfiguration<T>(string[] args, Func<IServiceProvider, T> selectResult)
    {
        var configHost = new HostBuilder()
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                var env = hostingContext.HostingEnvironment;

                config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "basePath", "https://localhost:8000" },
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

        var startupConfig = configHost.Services.GetRequiredService<IConfiguration>();
        var sslEndpointManager = configHost.Services.GetRequiredService<ISSLEndpointManager>();
        var certificateGenerator = configHost.Services.GetRequiredService<ICertificateGenerator>();

        var basePath = startupConfig["basePath"];

        var res = selectResult(configHost.Services);

        configHost.Dispose();

        ZeroConfig.EnsureConfiguration();
        SystemClientGenerator.EnsureSystemClient(certificateGenerator);

        await sslEndpointManager.EnableSslEndpoint(new SSLOptions(
            "eryph-zero CA",
            Network.FQDN,
            DateTime.UtcNow.AddDays(-1),
            365 * 5,
            ZeroConfig.GetPrivateConfigPath(),
            "eryphCA",
            Guid.Parse("9412ee86-c21b-4eb8-bd89-f650fbf44931"),
            new Uri(basePath)));

        return res;
    }
}