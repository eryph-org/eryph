using System;
using System.Threading.Tasks;
using Haipa.Modules.Api;
using Haipa.Modules.Hosting;
using Haipa.StateDb.MySql;
using SimpleInjector;

namespace Haipa.ApiEndpoint
{
    public class Program
    {

        public static async Task Main(string[] args)
        {
            await MySqlConnectionCheck.WaitForMySql(new TimeSpan(0, 1, 0)).ConfigureAwait(false);

            var container = new Container();
            container.Bootstrap(args);

            await container.HostModules().RunModule<ApiModule>((sp) => Task.CompletedTask).ConfigureAwait(false);

        }

    }
}
