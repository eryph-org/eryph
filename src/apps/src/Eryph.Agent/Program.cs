using System;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Agent
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // Use Serilog (like eryph-zero / the standalone controller) instead of the host's
            // default logging, which hits a Microsoft.Extensions.Logging version seam on .NET 10.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                var container = new Container();
                container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
                container.Options.EnableAutoVerification = false;
                container.Bootstrap();

                var builder = ModulesHost.CreateDefaultBuilder(args);
                // Windows services start with the system32 directory as their working directory;
                // pin the content root to the app base directory so config/assets resolve (mirrors
                // eryph-zero). Hosuto otherwise defaults to Environment.CurrentDirectory.
                builder.UseContentRoot(AppContext.BaseDirectory);

                var host = builder
                    // Run as a Windows service on each Hyper-V host. UseWindowsService installs the
                    // service lifetime only when actually started as a service; interactively it is a
                    // no-op and the default console lifetime applies. (RunConsoleAsync would force the
                    // console lifetime and break service mode, so build + RunAsync like eryph-zero.)
                    .ConfigureInternalHost(hb =>
                    {
                        hb.UseWindowsService(cfg => cfg.ServiceName = "eryph-hostagent");
                    })
                    .UseSimpleInjector(container)
                    // The agent is a WebModule: host Kestrel so it can serve the EGS remote-channel
                    // listener (/v1/channels/{token}). AgentChannelTls binds the mTLS endpoint (server
                    // cert + required component client certificate); it is a no-op when the channel is
                    // disabled.
                    .UseAspNetCoreWithDefaults((_, webHostBuilder) => AgentChannelTls.Configure(webHostBuilder))
                    .AddVmHostAgentModule()
                    .UseSerilog()
                    .Build();

                await host.RunAsync().ConfigureAwait(false);
            }
            finally
            {
                // Flush any log events buffered by Serilog before the process exits.
                Log.CloseAndFlush();
            }
        }
    }
}
