using System;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Haipa.Modules.Api;
using Haipa.StateDb.MySql;
using Microsoft.AspNetCore;
using SimpleInjector;

namespace Haipa.ApiEndpoint
{
    public class Program
    {

        public static async Task Main(string[] args)
        {
            await MySqlConnectionCheck.WaitForMySql(new TimeSpan(0, 1, 0)).ConfigureAwait(false);

            var container = new Container();
            container.Bootstrap();

            await ModulesHost.CreateDefaultBuilder(args)
                .UseSimpleInjector(container)
                .UseAspNetCore(WebHost.CreateDefaultBuilder, (module, webHostBuilder) =>
                {

                })
                .RunModule<ApiModule>();

        }

    }
}
