using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Eryph.Rebus;
using Eryph.StateDb;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Rebus.Persistence.InMem;
using Rebus.Sagas;
using Rebus.Subscriptions;
using Rebus.Timeouts;
using Rebus.Transport.InMem;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.ComputeApi.Tests.Integration
{
    public static class ApiModuleFactoryExtensions
    {
        public static WebModuleFactory<ComputeApiModule> WithApiHost(this WebModuleFactory<ComputeApiModule> factory)
        {

            return factory.WithModuleHostBuilder(hostBuilder =>
            {
                Container container = new();

                container.Options.AllowOverridingRegistrations = true;
                hostBuilder.UseSimpleInjector(container);

                container.RegisterInstance<ILoggerFactory>(new NullLoggerFactory());
                container.RegisterConditional(
                    typeof(ILogger),
                    c => typeof(Logger<>).MakeGenericType(c.Consumer!.ImplementationType),
                    Lifestyle.Singleton,
                    _ => true);

                hostBuilder.UseEnvironment(Environments.Development);

                var endpoints = new Dictionary<string, string>
                {
                    { "identity", "http://localhost/identity" },
                    { "compute", "http://localhost/compute" },
                    { "common", "http://localhost/common" },
                    { "network", "http://localhost/network" },

                };

                hostBuilder.ConfigureAppConfiguration(cfg =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "bus:type", "inmemory" },
                        { "databus:type", "inmemory" },
                        { "store:type", "inmemory" }
                    });
                });


                container.RegisterInstance(new WorkflowOptions
                {
                    DispatchMode = WorkflowEventDispatchMode.Publish,
                    EventDestination = QueueNames.Controllers,
                    OperationsDestination = QueueNames.Controllers,
                });

                container.RegisterInstance<IEndpointResolver>(new EndpointResolver(endpoints));

                container.RegisterInstance(new InMemNetwork());
                container.RegisterInstance(new InMemorySubscriberStore());
                container.Register<IRebusTransportConfigurer, DefaultTransportSelector>();
                container.Register<IRebusConfigurer<ISagaStorage>, DefaultSagaStoreSelector>();
                container.Register<IRebusConfigurer<ITimeoutManager>, DefaultTimeoutsStoreSelector>();
                container.Register<IRebusConfigurer<ISubscriptionStorage>, DefaultSubscriptionStoreSelector>();

                container.RegisterInstance(new InMemoryDatabaseRoot());
                container.Register<IDbContextConfigurer<StateStoreContext>, InMemoryStateStoreContextConfigurer>();


            });

        }

        public static WebModuleFactory<ComputeApiModule> SetupStateStore(this WebModuleFactory<ComputeApiModule> factory, Action<StateStoreContext> configure)
        {

            return factory.WithModuleConfiguration(options =>
            {
                options.Configure(cfg =>
                {
                    var container = cfg.Services.GetRequiredService<Container>();
                    using var scope = AsyncScopedLifestyle.BeginScope(container);

                    var stateStore = scope.GetInstance<StateStoreContext>();
                    configure(stateStore);
                    stateStore.SaveChanges();
                });
            });
        }

        public static WebModuleFactory<ComputeApiModule> WithTestToken(this WebModuleFactory<ComputeApiModule> factory)
        {
            return factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters()
                        {
                            SignatureValidator = (token, parameters) => new JwtSecurityToken(token)
                        };
                        options.Audience = "https://component-test";
                        options.Authority = "https://sts.windows.net/{tenantid}/";
                        options.BackchannelHttpHandler = new MockBackchannel();
                        options.MetadataAddress =
                            "https://inmemory.microsoft.com/common/.well-known/openid-configuration";
                    });
                });
            });
        }
    }
}