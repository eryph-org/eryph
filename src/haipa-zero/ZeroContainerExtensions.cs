using Haipa.Modules.Api;
using Haipa.Modules.Controller;
using Haipa.Modules.Hosting;
using Haipa.Modules.Identity;
using Haipa.Modules.SSL;
using Haipa.Modules.VmHostAgent;
using Haipa.Rebus;
using Haipa.StateDb;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;
using SimpleInjector;
using System;
using System.IO;

namespace Haipa.Runtime.Zero
{
    internal static class ZeroContainerExtensions
    {
        public static void Bootstrap(this Container container, string[] args)
        {
            container.HostModule<ApiModule>();
            container.HostModule<IdentityModule>();
            container.HostModule<VmHostAgentModule>();
            container.HostModule<ControllerModule>();

            container.CreateSsl(certOptions =>
            {
                certOptions.Issuer = Network.FQDN;
                certOptions.FriendlyName= "Haipa Zero Management Certificate";
                certOptions.ValidStartDate = DateTime.UtcNow;
                certOptions.ValidEndDate = certOptions.ValidStartDate.AddYears(5);
                certOptions.Password = "password";
                certOptions.ExportDirectory = Directory.GetCurrentDirectory();
                certOptions.URL = "https://localhost:62189/";
                certOptions.AppID = "9412ee86-c21b-4eb8-bd89-f650fbf44931";
            });
            
            container
                .HostAspNetCore((path) =>
                {
                    return WebHost.CreateDefaultBuilder(args)
                        .UseHttpSys(options =>
                        {
                            options.UrlPrefixes.Add($"https://localhost:62189/{path}");
                        })
                        .UseUrls($"https://localhost:62189/{path}")
                        .UseEnvironment("Development")
                        .ConfigureLogging(lc => lc.SetMinimumLevel(LogLevel.Warning));
                });

            container
                .UseInMemoryBus()
                .UseInMemoryDb();
        }
        public static Container UseInMemoryBus(this Container container)
        {
            container.RegisterInstance(new InMemNetwork(true));
            container.RegisterInstance(new InMemorySubscriberStore());
            container.Register<IRebusTransportConfigurer, InMemoryTransportConfigurer>();
            container.Register<IRebusSagasConfigurer, InMemorySagasConfigurer>();
            container.Register<IRebusSubscriptionConfigurer, InMemorySubscriptionConfigurer>();
            container.Register<IRebusTimeoutConfigurer, InMemoryTimeoutConfigurer>(); return container;
        }
        public static Container UseInMemoryDb(this Container container)
        {
            container.RegisterInstance(new InMemoryDatabaseRoot());
            container.Register<IDbContextConfigurer<StateStoreContext>, InMemoryStateStoreContextConfigurer>();

            return container;
        }
        public static Container CreateSsl(this Container container, Action<CertificateOptions> options)
        {
            var certOptions = new CertificateOptions();
            options(certOptions);
            if(!CertHelper.IsInMyStore(certOptions.Issuer))
            {
                certOptions.Thumbprint = CreateCertificate.Create(certOptions).Thumbprint;
                Command.RegisterSSLToUrl(certOptions);
            }
            return container;
        }
    }
}
