using System;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Modules.Identity;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Identity
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Operator command: produce a component enrollment file from the CA on this host, then
            // exit (it does not start the identity host).
            if (args.Length > 0 && string.Equals(args[0], EnrollmentCommand.Verb, StringComparison.OrdinalIgnoreCase))
            {
                Environment.Exit(await EnrollmentCommand.RunAsync(args));
                return;
            }

            // Logging uses the ASP.NET Core host defaults (UseAspNetCoreWithDefaults). Serilog is
            // intentionally not referenced here: wiring it via .UseSerilog() returns IHostBuilder
            // and breaks the RunModule chain. It can be added (with its packages) once the host
            // moves to the RunConsoleAsync pattern.
            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            container.Options.EnableAutoVerification = false;
            container.Bootstrap();

            await ModulesHost.CreateDefaultBuilder(args)
                .UseSimpleInjector(container)
                .ConfigureIdentityComponent()
                .UseAspNetCoreWithDefaults((module, webHostBuilder) => { })
                .RunModule<IdentityModule>();
        }
    }
}
