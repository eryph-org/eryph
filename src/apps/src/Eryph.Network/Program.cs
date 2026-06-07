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

                await ModulesHost.CreateDefaultBuilder(args)
                    .UseSimpleInjector(container)
                    .AddNetworkModule()
                    .UseSerilog()
                    .RunConsoleAsync().ConfigureAwait(false);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
