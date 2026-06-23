using System;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Network
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // Use Serilog (like eryph-zero and the standalone controller) instead of the host's
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
                // Windows services start with system32 as the working directory; pin the content root
                // so module/config resolution works the same as interactive runs.
                builder.UseContentRoot(AppContext.BaseDirectory);

                // Dispose the host after RunAsync returns so hosted services and other resources are
                // stopped/disposed before the process exits (and before Serilog flushes).
                using var host = builder
                    .ConfigureInternalHost(hb =>
                    {
#if WINDOWS
                        // No-op when not started as a Windows service (interactive/dev).
                        hb.UseWindowsService(cfg => cfg.ServiceName = "eryph-network");
#else
                        // The OVN network control plane is native on Linux (the cross-platform OVN system
                        // environment uses the OS-provided OVN under /usr). No-op when not run under
                        // systemd (interactive/dev).
                        hb.UseSystemd();
#endif
                    })
                    .UseSimpleInjector(container)
                    .AddNetworkModule()
                    .UseSerilog()
                    .Build();

                await host.RunAsync().ConfigureAwait(false);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
