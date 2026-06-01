using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.ApiEndpoint
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            container.Options.EnableAutoVerification = false;
            container.Bootstrap();

            await ModulesHost.CreateDefaultBuilder(args)
                .UseSimpleInjector(container)
                .UseAspNetCoreWithDefaults((module, webHostBuilder) => ComponentServerTls.Configure(webHostBuilder))
                .AddComputeApiModule()
                .RunConsoleAsync();
        }
    }
}
