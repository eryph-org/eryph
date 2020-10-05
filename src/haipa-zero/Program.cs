using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Haipa.App;
using Haipa.Modules.CommonApi;
using Haipa.Modules.ComputeApi;
using Haipa.Modules.Controller;
using Haipa.Modules.VmHostAgent;
using Haipa.Runtime.Zero.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;


namespace Haipa.Runtime.Zero
﻿{
    /// <summary>
    /// Defines the <see cref="Program" />
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// The Main
        /// </summary>
        /// <param name="args">The args<see cref="string[]"/></param>
        private static async Task Main(string[] args)
        {
 
            await using var processLock = new ProcessFileLock(Path.Combine(ZeroConfig.GetConfigPath(), ".lock"));

            ZeroConfig.EnsureConfiguration();
            const string basePath = "https://localhost:62189/";

            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            container.Bootstrap();

            var host = ModulesHost.CreateDefaultBuilder(args)
                .UseAspNetCore((module, webHostBuilder) =>
                {
                    webHostBuilder.UseHttpSys(options =>
                        {
                            options.UrlPrefixes.Add($"{basePath}{module.Path}");
                        })
                        .UseUrls($"{basePath}{module.Path}");
                })
                .UseSimpleInjector(container)
                .HostModule<CommonApiModule>()
                .HostModule<ComputeApiModule>()
                .AddIdentityModule(container)
                .HostModule<VmHostAgentModule>()
                .HostModule<ControllerModule>()


                .UseEnvironment("Development")
                .ConfigureLogging(lc => lc.SetMinimumLevel(LogLevel.Trace))
                .Build();

            processLock.SetMetadata(new Dictionary<string, object>
            {
                { "endpoints", new Dictionary<string, string>
                {
                    {"identity", $"{basePath}identity"},
                    {"compute", $"{basePath}api"}
                }}
            });
            
            await host.RunAsync();

        }

    }
 }
