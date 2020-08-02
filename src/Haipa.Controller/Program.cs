using System;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Haipa.Modules.Controller;
using Haipa.StateDb;
using Haipa.StateDb.MySql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Haipa.Controller
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await MySqlConnectionCheck.WaitForMySql(new TimeSpan(0, 1, 0)).ConfigureAwait(false);

            var container = new Container();
            container.Bootstrap();

            await ModuleHost.CreateDefaultBuilder(args)
                .UseSimpleInjector(container)
                .HostModule<ControllerModule>(options => options.Configure((sp) =>
                    {
                        using (var scope = sp.CreateScope())
                            scope.ServiceProvider.GetService<StateStoreContext>().Database.Migrate();
                    }))

        .RunConsoleAsync().ConfigureAwait(false);
        }
    }
}
