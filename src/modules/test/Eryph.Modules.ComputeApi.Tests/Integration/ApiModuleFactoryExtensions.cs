using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Hosuto.Modules.Testing;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore;
using Eryph.Rebus;
using Eryph.StateDb;
using FluentAssertions.Common;
using Microsoft.AspNetCore.Authorization;
using LanguageExt;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Rebus.Persistence.InMem;
using Rebus.Sagas;
using Rebus.Serialization;
using Rebus.Subscriptions;
using Rebus.Timeouts;
using Rebus.Transport.InMem;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using WebMotions.Fake.Authentication.JwtBearer;

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


            }).WithWebHostBuilder(webBuilder =>
            {
                webBuilder
                .ConfigureTestServices(services =>
                {
                        services.AddAuthentication(FakeJwtBearerDefaults.AuthenticationScheme).AddFakeJwtBearer();
                        services.AddAuthorization(opts => ComputeApiModule.ConfigureScopes(opts, "fake"));
                    });
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

        public static List<T> GetPendingRebusMessages<T>(
            this WebModuleFactory<ComputeApiModule> factory)
        {
            var container = factory.Services.GetRequiredService<Container>();
            var inMemNetwork = container.GetInstance<InMemNetwork>();
            var transportMessages = inMemNetwork.GetMessages(QueueNames.Controllers);

            var workflowOptions = container.GetInstance<WorkflowOptions>();

            return transportMessages
                .Map(m => m.ToTransportMessage())
                // Unfortunately, we cannot get the ISerializer of Rebus.
                // Luckily, deserializing the JSON with default settings just works.
                .Map(m => JsonSerializer.Deserialize(m.Body, Type.GetType(m.Headers["rbs2-msg-type"])!))
                .OfType<CreateOperationCommand>()
                .Map(c => JsonSerializer.Deserialize(
                    c.TaskMessage!.CommandData!,
                    Type.GetType(c.TaskMessage.CommandType!)!,
                    workflowOptions.JsonSerializerOptions))
                .OfType<T>()
                .ToList();
        }
    }
}