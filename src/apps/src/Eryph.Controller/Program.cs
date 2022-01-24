using System;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Modules.Controller;
using Eryph.StateDb;
using Eryph.StateDb.MySql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;

namespace Eryph.Controller
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            await MySqlConnectionCheck.WaitForMySql(new TimeSpan(0, 1, 0)).ConfigureAwait(false);

            var container = new Container();
            container.Bootstrap();

            await ModulesHost.CreateDefaultBuilder(args)
                .UseSimpleInjector(container)
                .HostModule<ControllerModule>(options => options.Configure(sp =>
                {
                    using (var scope = sp.CreateScope())
                    {
                        scope.ServiceProvider.GetService<StateStoreContext>().Database.Migrate();
                    }
                }))
                .RunConsoleAsync().ConfigureAwait(false);
        }
    }
}