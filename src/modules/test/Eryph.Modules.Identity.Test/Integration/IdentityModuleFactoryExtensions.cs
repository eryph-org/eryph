using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.IdentityDb;
using Eryph.ModuleCore;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimpleInjector;
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
}
