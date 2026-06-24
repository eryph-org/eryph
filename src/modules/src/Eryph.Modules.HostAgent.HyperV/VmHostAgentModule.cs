using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Dbosoft.OVN.Windows;
using Dbosoft.Rebus;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.GuestServices.HvDataExchange.Host;
using Eryph.Messages.Components;
using Eryph.ModuleCore;
using Eryph.ModuleCore.Components;
using Eryph.ModuleCore.Networks;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.AspNetCore.Channels;
using Eryph.Modules.HostAgent.Channels;
using Eryph.Modules.HostAgent.Inventory;
using Eryph.Modules.HostAgent.Networks;
using Eryph.Modules.HostAgent.Networks.OVS;
using Eryph.Modules.HostAgent.Tracing;
using Eryph.Rebus;
using Eryph.VmManagement;
using Eryph.VmManagement.Tracing;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Subscriptions;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using IFileSystem = System.IO.Abstractions.IFileSystem;

namespace Eryph.Modules.HostAgent;

[UsedImplicitly]
public class VmHostAgentModule : WebModule
{
    private readonly ChannelListenerOptions _channelListenerOptions = new();
    private readonly InventoryConfig _inventoryConfig = new();
    private readonly TracingConfig _tracingConfig = new();
    private Container? _container;

    public VmHostAgentModule(IConfiguration configuration)
    {
        configuration.GetSection("Tracing")
            .Bind(_tracingConfig);

        configuration.GetSection("Inventory")
            .Bind(_inventoryConfig);

        configuration.GetSection(ChannelListenerOptions.SectionName)
            .Bind(_channelListenerOptions);
        // The channel listener loads its server certificate and CA trust bundle from the same
        // component certificate directory the bus transport uses (componentMtls:certificateDirectory),
        // so the listener and the bus stay on one source of truth.
        _channelListenerOptions.CertificateDirectory =
            configuration.GetSection("componentMtls")["certificateDirectory"];
    }

    public string Name => "Eryph.VmHostAgent";

    [UsedImplicitly]
    public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
    {
        services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(15));

        services.AddTransient<WmiVmUptimeCheckJob>();
        services.AddQuartz(q =>
        {
            q.SchedulerName = $"{Name}.Scheduler";
            q.ScheduleJob<WmiVmUptimeCheckJob>(
                trigger => trigger.WithIdentity("WmiVmUptimeCheckJobTrigger")
                    .ForJob(WmiVmUptimeCheckJob.Key)
                    .StartNow()
                    .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(1)).RepeatForever()),
                job => job.WithIdentity(WmiVmUptimeCheckJob.Key)
                    .DisallowConcurrentExecution());
        });
        services.AddQuartzHostedService();
    }

    // Maps the network channel listener (GET /v1/channels/{token}). Enabled only where the agent is
    // reached over the network (the split runtime, with mTLS configured by Eryph.Agent/AgentChannelTls);
    // in eryph-zero the listener is off and the compute API reaches the channel service in-process.
    [UsedImplicitly]
    public void Configure(IApplicationBuilder app)
    {
        if (!_channelListenerOptions.Enabled)
            return;

        app.UseWebSockets();
        app.UseRouting();
        app.UseEndpoints(endpoints =>
            endpoints.MapGet("/v1/channels/{token}", HandleChannelAsync));
    }

    // GET /v1/channels/{token}: upgrade to a WebSocket and bridge it to the guest hvsocket via
    // IChannelService. The transport-level mTLS (host Kestrel) has already proven the caller is the
    // compute API; the one-time token authorizes this specific channel.
    private async Task HandleChannelAsync(HttpContext context, string token)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var channelService = _container!.GetInstance<IChannelService>();
        var logger = _container.GetInstance<ILogger<VmHostAgentModule>>();

        // Validate + consume the one-time token and open the guest hvsocket BEFORE upgrading. A null
        // stream means an unknown/expired/used token → 404 without an upgrade, not leaking whether the
        // token ever existed.
        Stream guestStream;
        try
        {
            guestStream = await channelService.OpenChannelAsync(token, context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The token was accepted but dialing the guest hvsocket failed (catlet stopped, vsock not
            // ready). Fail with a controlled status instead of letting it bubble out of the pipeline.
            logger.LogWarning(ex, "Failed to open the EGS guest channel.");
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        if (guestStream is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        WebSocket webSocket;
        try
        {
            webSocket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await guestStream.DisposeAsync().ConfigureAwait(false);
            logger.LogDebug(ex, "Failed to accept the EGS channel WebSocket.");
            return;
        }

        try
        {
            await WebSocketBridge.PumpAsync(webSocket, guestStream, context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The EGS channel bridge failed.");
        }
        finally
        {
            webSocket.Dispose();
            await guestStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    [UsedImplicitly]
    public void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.AddHostedService<SyncService>();
        options.AddHostedService<OVNChassisService>();
        options.AddStartupHandler<StartBusModuleHandler>();
        // Remove this hosted service to avoid triggering the inventory
        // based on WMI events.
        options.AddHostedService<VmChangeWatcherService>();
        options.AddHostedService<VmRemovalWatcherService>();
        options.AddHostedService<VmStateChangeWatcherService>();
        options.AddHostedService<DiskStoresChangeWatcherService>();

        // Opt in to controller-driven configuration distribution. The agent
        // registers on its own inbound queue and subscribes to the placement
        // vocabulary and the network-provider configuration via its realizers.
        options.AddComponentRegistration(
            ComponentType.VMHostAgent,
            // The agent's inbound queue suffix must equal the agent name the controller uses
            // to route operations to it ({QueueNames.VMHostAgent}.{AgentName}). That name is
            // Environment.MachineName everywhere in the controller — placement, the compute
            // sagas, and the VM/disk inventory it stores — so the queue stays on the machine
            // name. Unifying the agent identity on the FQDN belongs to the deferred multi-host
            // slice, where routing switches to the registered InboundQueue from the component
            // catalog instead of a name constructed from the machine name.
            $"{QueueNames.VMHostAgent}.{Environment.MachineName}",
            // EGS remote-channel listener (when enabled): advertise the agent's channel base URL
            // under a per-host endpoint name so the compute API can resolve the listener for the
            // specific host that runs a given catlet. The endpoints config domain is a flat
            // name -> URL map across all components, so the name must be host-qualified (the
            // short machine name matches the agent identity the controller routes operations to;
            // see the inbound-queue comment above). Empty when the listener is disabled (eryph-zero
            // and dev), so nothing is advertised then.
            BuildAdvertisedEndpoints(),
            typeof(PlacementConfigRealizer),
            typeof(NetworkProvidersConfigRealizer),
            typeof(EndpointsConfigRealizer));

        options.AddLogging();
    }

    // Builds the EGS channel listener's advertised endpoints. Empty unless the listener is enabled
    // (so non-mTLS dev and eryph-zero advertise nothing). The endpoint name is host-qualified on the
    // agent's machine name — the same identity the controller routes VM operations to.
    private Dictionary<string, string> BuildAdvertisedEndpoints()
    {
        if (!_channelListenerOptions.Enabled)
            return new Dictionary<string, string>();

        var provider = new ChannelEndpointProvider(_channelListenerOptions);
        return new Dictionary<string, string>
        {
            [$"egs-channel:{Environment.MachineName}"] = provider.BaseUrl,
        };
    }

    [UsedImplicitly]
    public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
    {
        // Captured so the channel WebSocket endpoint (mapped in Configure, run by the ASP.NET
        // pipeline) can resolve IChannelService from the module's SimpleInjector container at
        // request time. By the first request ConfigureContainer has always run.
        _container = container;

        container.RegisterInstance(_inventoryConfig);

        // EGS remote-channel data plane.
        container.RegisterInstance(_channelListenerOptions);
        container.RegisterSingleton<IHostDataExchange, HostDataExchange>();
        container.RegisterSingleton<IChannelEndpointProvider, ChannelEndpointProvider>();
        container.RegisterSingleton<IChannelService, ChannelService>();
        // Reads guest services + provisioning status from the guest KVP pool.
        container.RegisterSingleton<IGuestStatusReader, GuestStatusReader>();
        // Single write path for guest-services settings (shell, authorized keys, ...).
        container.RegisterSingleton<IGuestDataWriter, GuestDataWriter>();

        container.Register<ISyncClient, SyncClient>();
        container.Register<IHostNetworkCommands<AgentRuntime>, HostNetworkCommands<AgentRuntime>>();
        container.Register<IOVSControl, OVSControl>();
        container.RegisterInstance(serviceProvider.GetRequiredService<INetworkSyncService>());

        container.RegisterSingleton<OVNChassisNode>();
        container.RegisterSingleton<OVSDbNode>();
        container.RegisterSingleton<OVSSwitchNode>();
        container.RegisterSingleton<IOVSService<OVNChassisNode>, OVSNodeService<OVNChassisNode>>();
        container.RegisterSingleton<IOVSService<OVSDbNode>, OVSNodeService<OVSDbNode>>();
        container.RegisterSingleton<IOVSService<OVSSwitchNode>, OVSNodeService<OVSSwitchNode>>();

        // Holds the controller-distributed placement vocabulary applied by
        // PlacementConfigRealizer and enforced by the provisioning handlers.
        container.RegisterSingleton<IPlacementConfigProvider, PlacementConfigProvider>();

        // Holds the controller-distributed deployment endpoints applied by
        // EndpointsConfigRealizer (the identity issuer etc.). Registered as the
        // concrete type so it does not collide with a host-provided IEndpointResolver.
        container.RegisterSingleton<DistributedEndpointResolver>();

        container.RegisterSingleton<IFileSystem, FileSystem>();
        container.RegisterSingleton<IFileSystemService, FileSystemService>();
        container.RegisterInstance(serviceProvider.GetRequiredService<IAgentControlService>());

        if (_tracingConfig.Enabled)
        {
            container.RegisterSingleton<ITracer, Tracer>();
            container.RegisterSingleton<ITraceWriter, DiagnosticTraceWriter>();
            container.RegisterDecorator(typeof(IHandleMessages<>), typeof(TraceDecorator<>));
        }

        container.RegisterSingleton<IPowershellEngineLock>(() => new PowershellEngineLock(false));
        container.Register<IPowershellEngine, PowershellEngine>(Lifestyle.Scoped);

        container.RegisterInstance(serviceProvider.GetRequiredService<IVmHostAgentConfigurationManager>());
        container.RegisterInstance(serviceProvider.GetRequiredService<IApplicationInfoProvider>());
        container.RegisterInstance(serviceProvider.GetRequiredService<IHostSettingsProvider>());
        container.RegisterInstance(serviceProvider.GetRequiredService<INetworkProviderManager>());
        container.RegisterSingleton<IHostInfoProvider, HostInfoProvider>();
        container.RegisterSingleton<IHostArchitectureProvider, HostArchitectureProvider>();

        container.Register<IHyperVOvsPortManager>(() => new HyperVOvsPortManager(), Lifestyle.Scoped);

        container.RegisterInstance(serviceProvider.GetRequiredService<WorkflowOptions>());
        container.Collection.Register(typeof(IHandleMessages<>), typeof(VmHostAgentModule).Assembly);
        container.Collection.Append(typeof(IHandleMessages<>), typeof(FailedOperationTaskHandler<>),
            Lifestyle.Scoped);
        container.AddRebusOperationsHandlers();

        container.ConfigureRebus(configurer => configurer
            .Serialization(s => s.UseEryphSettings())
            // Use the registered component inbound queue as the single source of truth for
            // the bus endpoint name (it must match what AddComponentRegistration announced).
            // Resolved inside the transport lambda (which runs at bus start) so it does not
            // trigger premature container verification during ConfigureContainer.
            .Transport(t =>
                container.GetInstance<IRebusTransportConfigurer>()
                    .Configure(t, container.GetInstance<ComponentIdentity>().InboundQueue))
            .Options(x =>
            {
                x.RetryStrategy(secondLevelRetriesEnabled: true, errorDetailsHeaderMaxLength: 5);
                x.SetNumberOfWorkers(5);
                x.EnableSynchronousRequestReply();
            })
            .Subscriptions(s => container.GetService<IRebusConfigurer<ISubscriptionStorage>>()?.Configure(s))
            .Logging(x => x.MicrosoftExtensionsLogging(container.GetInstance<ILoggerFactory>()))
            .Start());
    }
}
