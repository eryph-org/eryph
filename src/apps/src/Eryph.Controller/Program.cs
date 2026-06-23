using System.Collections.Generic;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.AppCore;
using Eryph.Modules.Controller;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Controller
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // Use Serilog (like eryph-zero) instead of the host's default logging, which
            // hits a Microsoft.Extensions.Logging version seam on the .NET 10 runtime.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                var container = new Container();
                // Must be set before any registration (and before Hosuto's UseSimpleInjector
                // tries to set it), otherwise SimpleInjector throws.
                container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
                // Hosuto verifies the container at the proper point in the module lifecycle.
                // Auto-verifying on first resolve fires mid-module-config (before cross-wired
                // logging is ready) and fails spuriously.
                container.Options.EnableAutoVerification = false;
                container.Bootstrap();

                await ModulesHost.CreateDefaultBuilder(args)
                    .UseSimpleInjector(container)
                    .ConfigureAppConfiguration((_, config) =>
                        config.AddInMemoryCollection(new Dictionary<string, string>
                        {
                            // Transport is RabbitMQ (registered configurer); Rebus stores stay
                            // in-memory for this milestone.
                            { "store:type", "inmemory" },
                            { "databus:type", "inmemory" },

                            // Seed the (owned) state DB from the on-disk config under this
                            // component's config root, mirroring eryph-zero's change-tracking
                            // setup. Without seedDatabase the network-providers seeder is
                            // skipped, so the default project's network realization fails to
                            // find its provider subnet.
                            { "changeTracking:trackChanges", bool.TrueString },
                            { "changeTracking:seedDatabase", bool.TrueString },
                            { "changeTracking:networksConfigPath", AppConfigPaths.GetNetworksConfigPath() },
                            { "changeTracking:projectsConfigPath", AppConfigPaths.GetProjectsConfigPath() },
                            { "changeTracking:projectNetworksConfigPath", AppConfigPaths.GetProjectNetworksConfigPath() },
                            { "changeTracking:projectNetworkPortsConfigPath", AppConfigPaths.GetProjectNetworkPortsConfigPath() },
                            { "changeTracking:virtualMachinesConfigPath", AppConfigPaths.GetMetadataConfigPath() },
                            { "changeTracking:catletSpecificationsConfigPath", AppConfigPaths.GetCatletSpecificationsConfigPath() },
                            { "changeTracking:catletSpecificationVersionsConfigPath", AppConfigPaths.GetCatletSpecificationVersionsConfigPath() },

                            // Operator-set deployment endpoints distributed via the Endpoints
                            // config domain (the override layer). 'identity' is intentionally NOT
                            // overridden here so it is sourced from what the identity component
                            // advertises on registration; an operator override would win if set
                            // (required behind a load balancer for the canonical issuer URL).
                            { "endpoints:base", "https://localhost:8443/" },
                            { "endpoints:compute", "https://localhost:8443/compute" },
                        }))
                    .AddControllerModule()
                    .UseSerilog()
                    .RunConsoleAsync().ConfigureAwait(false);
            }
            finally
            {
                // Flush any log events buffered by Serilog before the process exits.
                Log.CloseAndFlush();
            }
        }
    }
}