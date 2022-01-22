using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Modules.Identity;
using SimpleInjector;

namespace Eryph.Identity
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var container = new Container();
            container.Bootstrap();

            await ModulesHost.CreateDefaultBuilder(args)
                .UseSimpleInjector(container)
                .UseAspNetCoreWithDefaults((module, webHostBuilder) => { })
                .RunModule<IdentityModule>();
        }
    }
}