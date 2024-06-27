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
using Eryph.Rebus;
using Eryph.StateDb.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Rebus.Sagas;
using Rebus.Timeouts;
using Rebus.Transport.InMem;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using WebMotions.Fake.Authentication.JwtBearer;

namespace Eryph.Modules.ComputeApi.Tests.Integration;

public static class ApiModuleFactoryExtensions
{
    public static WebModuleFactory<ComputeApiModule> WithApiHost(
        this WebModuleFactory<ComputeApiModule> factory,
        Action<Container> configureContainer) =>
        factory.WithModuleHostBuilder(hostBuilder =>
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
            container.Register<IRebusTransportConfigurer, DefaultTransportSelector>();
            container.Register<IRebusConfigurer<ISagaStorage>, DefaultSagaStoreSelector>();
            container.Register<IRebusConfigurer<ITimeoutManager>, DefaultTimeoutsStoreSelector>();

            container.RegisterInstance(new InMemoryDatabaseRoot());
            configureContainer(container);

            hostBuilder.ConfigureFrameworkServices((_, services) =>
            {
                services.AddTransient<IAddSimpleInjectorFilter<ComputeApiModule>, ModuleFilters>();
            });
        }).WithWebHostBuilder(webBuilder =>
        {
            webBuilder
                .Configure(app => app.UseDeveloperExceptionPage())
                .ConfigureTestServices(services =>
                {
                    services.AddAuthentication(FakeJwtBearerDefaults.AuthenticationScheme).AddFakeJwtBearer();
                    services.AddAuthorization(opts => ComputeApiModule.ConfigureScopes(opts, "fake"));
                });
        });

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

    private class ModuleFilters : IAddSimpleInjectorFilter<ComputeApiModule>
    {
        public Action<IModulesHostBuilderContext<ComputeApiModule>, SimpleInjectorAddOptions> Invoke(
            Action<IModulesHostBuilderContext<ComputeApiModule>, SimpleInjectorAddOptions> next)
        {
            return (context, options) =>
            {
                options.RegisterSqliteStateStore();
                next(context, options);
            };
        }
    }
}
