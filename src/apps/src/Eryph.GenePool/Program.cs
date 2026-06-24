using System;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.GenePool;

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

            var builder = ModulesHost.CreateDefaultBuilder(args);
            // Windows services start with the system32 directory as their working directory;
            // pin the content root to the app base directory so config/assets resolve (mirrors
            // eryph-zero). Hosuto otherwise defaults to Environment.CurrentDirectory.
            builder.UseContentRoot(AppContext.BaseDirectory);

            // Dispose the host after RunAsync returns so hosted services and other resources are
            // stopped/disposed before the process exits (and before Serilog flushes).
            using var host = builder
                // Run as a Windows service on each Hyper-V host. UseWindowsService installs the
                // service lifetime only when actually started as a service; interactively it is a
                // no-op and the default console lifetime applies. (RunConsoleAsync would force the
                // console lifetime and break service mode, so build + RunAsync like eryph-zero.)
                .ConfigureInternalHost(hb => { hb.UseWindowsService(cfg => cfg.ServiceName = "eryph-genepool"); })
                .UseSimpleInjector(container)
                .AddGenePoolModule()
                .UseSerilog()
                .Build();

            await host.RunAsync().ConfigureAwait(false);
        }
        finally
        {
            // Flush any log events buffered by Serilog before the process exits.
            await Log.CloseAndFlushAsync();
        }
    }
}
