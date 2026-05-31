using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Hosuto.Modules.Testing;
using Dbosoft.Rebus.Configuration;
using Eryph.IdentityDb;
using Eryph.ModuleCore;
using Eryph.Modules.Identity.Services;
using Eryph.Rebus;
using Eryph.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rebus.Transport.InMem;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using WebMotions.Fake.Authentication.JwtBearer;
using Xunit.Abstractions;

namespace Eryph.Modules.Identity.Test.Integration;

public static class IdentityModuleFactoryExtensions
{
    public static WebModuleFactory<IdentityModule> WithIdentityHost(
        this WebModuleFactory<IdentityModule> factory,
        TokenCertificateFixture tokenCertificates) =>
        factory.WithModuleHostBuilder(hostBuilder =>
        {
            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            hostBuilder.UseSimpleInjector(container);

            var endpoints = new Dictionary<string, string>
            {
                {"identity", "https://localhost/identity/"},
                {"compute", "https://localhost/compute/"},
            };

            container.RegisterInstance<IEndpointResolver>(new EndpointResolver(endpoints));

            container.RegisterSingleton<ICertificateKeyService, TestCertificateKeyService>();
            container.RegisterSingleton<ICertificateGenerator, CertificateGenerator>();
            container.RegisterInstance<ITokenCertificateManager>(
                new TestTokenCertificateManager(tokenCertificates));

            container.RegisterInstance(new InMemoryDatabaseRoot());
            container.Register<IDbContextConfigurer<IdentityDbContext>, InMemoryIdentityDbContextConfigurer>();

            // The identity module now runs a bus + registers as a component, so the test host
            // must supply an in-memory transport the module's ConfigureRebus can resolve
            // (mirrors the compute-API test harness).
            hostBuilder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "bus:type", "inmemory" },
                    { "databus:type", "inmemory" },
                    { "store:type", "inmemory" },
                }));
            container.RegisterInstance(new InMemNetwork());
            hostBuilder.ConfigureFrameworkServices((_, services) =>
                services.AddTransient<IConfigureContainerFilter<IdentityModule>, InMemoryBusFilter>());
        }).WithWebHostBuilder(webBuilder =>
        {
            webBuilder.ConfigureTestServices(services =>
            {
                services.AddOpenIddict(openIdDict =>
                {
                    openIdDict.AddServer(options =>
                    {
                        options.UseAspNetCore().DisableTransportSecurityRequirement();
                    });
                });

                services.AddAuthentication(FakeJwtBearerDefaults.AuthenticationScheme).AddFakeJwtBearer();
                services.AddAuthorization(opts => IdentityModule.ConfigureIdentityScopes(opts, "fake"));
            });
        });

    public static WebModuleFactory<IdentityModule> WithXunitLogging(
        this WebModuleFactory<IdentityModule> factory,
        ITestOutputHelper testOutputHelper)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddLogging(loggingBuilder => loggingBuilder.AddXUnit(testOutputHelper));
            });
        });
    }

    /// <summary>
    /// Registers the in-memory bus transport on the identity module container so the module's
    /// own ConfigureRebus (added when registration moved into the module) can start.
    /// </summary>
    private sealed class InMemoryBusFilter : IConfigureContainerFilter<IdentityModule>
    {
        public Action<IModuleContext<IdentityModule>, Container> Invoke(
            Action<IModuleContext<IdentityModule>, Container> next)
        {
            return (context, container) =>
            {
                // Register the transport BEFORE the module configures its bus (the module starts
                // Rebus inside ConfigureContainer), matching the production identity host filter.
                container.RegisterInstance(context.ModulesHostServices.GetRequiredService<InMemNetwork>());
                container.Register<IRebusTransportConfigurer, DefaultTransportSelector>();

                next(context, container);
            };
        }
    }
}
