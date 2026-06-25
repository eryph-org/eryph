using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Hosuto.Modules.Testing;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore.Channels;
using Eryph.Rebus;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rebus.Sagas;
using Rebus.Timeouts;
using Rebus.Transport.InMem;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using WebMotions.Fake.Authentication.JwtBearer;

namespace Eryph.Modules.ComputeApi.Tests.Integration;

public static class ApiModuleFactoryExtensions
{
    extension(WebModuleFactory<ComputeApiModule> factory)
    {
        public WebModuleFactory<ComputeApiModule> WithApiHost(Action<Container> configureContainer,
            Action<SimpleInjectorAddOptions> configureModuleContainer,
            IAgentChannelForwarder? channelForwarder = null)
        {
            // The compute API consumes IAgentChannelForwarder but does not register it — the host wires the
            // implementation (network dial in the split runtime, in-process in eryph-zero). The test host
            // does the same; a no-op forwarder is enough unless a test needs to observe the forward call.
            var forwarder = channelForwarder ?? new NullAgentChannelForwarder();

            return factory.WithModuleHostBuilder(hostBuilder =>
            {
                Container container = new();

                container.Options.AllowOverridingRegistrations = true;
                hostBuilder.UseSimpleInjector(container);

                hostBuilder.UseEnvironment(Environments.Development);

                var endpoints = new Dictionary<string, string>
                {
                    { "identity", "http://localhost/identity/" },
                    { "compute", "http://localhost/compute/" },
                };

                hostBuilder.ConfigureAppConfiguration(cfg =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "bus:type", "inmemory" },
                        { "databus:type", "inmemory" },
                        { "store:type", "inmemory" },
                    });
                });

                container.RegisterInstance(new WorkflowOptions
                {
                    DispatchMode = WorkflowEventDispatchMode.Publish,
                    EventDestination = QueueNames.Controllers,
                    OperationsDestination = QueueNames.Controllers,
                    JsonSerializerOptions = EryphJsonSerializerOptions.Options,
                });

                container.RegisterInstance<IEndpointResolver>(new EndpointResolver(endpoints));

                container.RegisterInstance(new InMemNetwork());

                container.RegisterInstance(new InMemoryDatabaseRoot());
                configureContainer(container);

                hostBuilder.ConfigureFrameworkServices((_, services) =>
                {
                    services.AddTransient<IAddSimpleInjectorFilter<ComputeApiModule>>(_ =>
                        new ModuleFilters(configureModuleContainer, forwarder));
                    services.AddTransient<IConfigureContainerFilter<ComputeApiModule>, ModuleFilters>(_ =>
                        new ModuleFilters(configureModuleContainer, forwarder));
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
        }

        public List<T> GetPendingRebusMessages<T>()
        {
            var container = factory.Services.GetRequiredService<Container>();
            var inMemNetwork = container.GetInstance<InMemNetwork>();
            var transportMessages = inMemNetwork.GetMessages(QueueNames.Controllers);

            var workflowOptions = container.GetInstance<WorkflowOptions>();

            return transportMessages
                .Map(m => m.ToTransportMessage())
                // Unfortunately, we cannot get the ISerializer of Rebus.
                // Luckily, we can just use the JSON serializer with the same
                // settings which we passed to Rebus.
                .Map(m =>
                {
                    var type = Type.GetType(m.Headers["rbs2-msg-type"])
                        ?? throw new InvalidOperationException(
                            $"The message type '{m.Headers["rbs2-msg-type"]}' could not be loaded.");
                    return JsonSerializer.Deserialize(m.Body, type, EryphJsonSerializerOptions.Options);
                })
                .OfType<CreateOperationCommand>()
                .Map(c =>
                {
                    var taskMessage = c.TaskMessage
                        ?? throw new InvalidOperationException("The command has no task message.");
                    var commandData = taskMessage.CommandData
                        ?? throw new InvalidOperationException("The task message has no command data.");
                    var commandTypeName = taskMessage.CommandType
                        ?? throw new InvalidOperationException("The task message has no command type.");
                    var commandType = Type.GetType(commandTypeName)
                        ?? throw new InvalidOperationException(
                            $"The command type '{commandTypeName}' could not be loaded.");
                    return JsonSerializer.Deserialize(commandData, commandType, workflowOptions.JsonSerializerOptions);
                })
                .OfType<T>()
                .ToList();
        }
    }

    private class ModuleFilters(
        Action<SimpleInjectorAddOptions> configureModuleContainer,
        IAgentChannelForwarder channelForwarder)
        : IAddSimpleInjectorFilter<ComputeApiModule>,
            IConfigureContainerFilter<ComputeApiModule>
    {
        public Action<IModulesHostBuilderContext<ComputeApiModule>, SimpleInjectorAddOptions> Invoke(
            Action<IModulesHostBuilderContext<ComputeApiModule>, SimpleInjectorAddOptions> next)
        {
            return (context, options) =>
            {
                configureModuleContainer(options);
                next(context, options);
            };
        }

        public Action<IModuleContext<ComputeApiModule>, Container> Invoke(
            Action<IModuleContext<ComputeApiModule>, Container> next)
        {
            return (context, container) =>
            {
                next(context, container);

                container.RegisterInstance(context.ModulesHostServices.GetRequiredService<InMemNetwork>());
                container.RegisterInstance(channelForwarder);
                container.Register<IRebusTransportConfigurer, DefaultTransportSelector>();
                container.Register<IRebusConfigurer<ISagaStorage>, DefaultSagaStoreSelector>();
                container.Register<IRebusConfigurer<ITimeoutManager>, DefaultTimeoutsStoreSelector>();
            };
        }
    }
}
