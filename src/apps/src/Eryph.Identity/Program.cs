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
        public static async Task<int> Main(string[] args)
        {
            // Operator command: produce a component enrollment file from the CA on this host, then
            // exit (it does not start the identity host). Return the exit code instead of calling
            // Environment.Exit, which would terminate the process immediately and bypass flush/dispose.
            if (args.Length > 0 && string.Equals(args[0], EnrollmentCommand.Verb, StringComparison.OrdinalIgnoreCase))
            {
                return await EnrollmentCommand.RunAsync(args);
            }

            // Setup command: create the identity-database schema in an empty database, then exit. The
            // cluster schema is setup (SQL scripts in production), not a startup migration.
            if (args.Length > 0 && string.Equals(args[0], CreateDbCommand.Verb, StringComparison.OrdinalIgnoreCase))
            {
                return await CreateDbCommand.RunAsync(args);
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
                .UseAspNetCoreWithDefaults((module, webHostBuilder) => IdentityServerTls.Configure(webHostBuilder))
                .RunModule<IdentityModule>();
            return 0;
        }
    }
}
