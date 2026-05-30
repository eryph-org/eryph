using System.Collections.Generic;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
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
                    }))
                .AddNetworkModule()
                .AddControllerModule()
                .UseSerilog()
                .RunConsoleAsync().ConfigureAwait(false);
        }
    }
}