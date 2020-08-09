using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Haipa.Modules.Api;
using Haipa.Modules.Controller;
using Haipa.Modules.VmHostAgent;
using Haipa.Security.Cryptography;
using Microsoft.AspNetCore;
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
        private static Task Main(string[] args)
        {
 
            ConfigStore.Config.EnsureConfigPaths();
            ConfigStore.Clients.ClientGenerator.EnsureSystemClient();

            File.WriteAllText(Path.Combine(ConfigStore.Config.GetConfigPath(), "zero_info"),
                $"{{ \"process_id\": \"{Process.GetCurrentProcess().Id}\", \"url\" : \"http://localhost:62189\" }}");

            Certificate.CreateSSL(new CertificateOptions
            {
                Issuer = Network.FQDN,
                FriendlyName = "Haipa Zero Management Certificate",
                Suffix = "CA",
                ValidStartDate = DateTime.UtcNow,
                ValidEndDate = DateTime.UtcNow.AddYears(5),
                Password = "password",
                ExportDirectory = Directory.GetCurrentDirectory(),
                URL = "https://localhost:62189/",
                AppID = "9412ee86-c21b-4eb8-bd89-f650fbf44931",
                CACertName = "HaipaCA.pfx"
            });

            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            container.Bootstrap();

            var host = ModulesHost.CreateDefaultBuilder(args)
                .UseAspNetCore(WebHost.CreateDefaultBuilder, (module, webHostBuilder) =>
                {
                    webHostBuilder.UseHttpSys(options =>
                        {
                            options.UrlPrefixes.Add($"https://localhost:62189/{module.Path}");
                        })
                        .UseUrls($"https://localhost:62189/{module.Path}");
                })
                .UseSimpleInjector(container)
                .HostModule<ApiModule>()
                .AddIdentityModule(container)
                .HostModule<VmHostAgentModule>()
                .HostModule<ControllerModule>()


                .UseEnvironment("Development")
                .ConfigureLogging(lc => lc.SetMinimumLevel(LogLevel.Warning))
                .Build();

            return host.RunAsync();
        }
    }
 }
