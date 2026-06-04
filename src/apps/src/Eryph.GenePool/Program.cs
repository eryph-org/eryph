using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.GenePool
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // Use Serilog (like eryph-zero and the other standalone runtimes) instead of the
            // host's default logging, which hits a Microsoft.Extensions.Logging version seam on
            // the .NET 10 runtime.
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
                    .AddGenePoolModule()
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
