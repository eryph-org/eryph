using System;
using System.Threading.Tasks;
using Haipa.Modules.Controller;
using Haipa.Modules.Hosting;
using Haipa.StateDb;
using Haipa.StateDb.MySql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
            container.Bootstrap(args);

            await container.HostModules().RunModule<ControllerModule>((sp) =>
            {
                using(AsyncScopedLifestyle.BeginScope(sp as Container))
                    sp.GetService<StateStoreContext>().Database.Migrate();

                return Task.CompletedTask;

            }).ConfigureAwait(false);
        }
    }
}
