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
            // Logging uses the ASP.NET Core host defaults (UseAspNetCoreWithDefaults). Serilog is
            // intentionally not wired here: .UseSerilog() returns IHostBuilder and breaks the
            // RunModule chain — it lands with the move to the RunConsoleAsync host pattern.
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
