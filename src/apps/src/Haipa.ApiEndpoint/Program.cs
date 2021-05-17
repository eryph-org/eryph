using System;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Haipa.Modules.CommonApi;
using Haipa.Modules.ComputeApi;
using Haipa.StateDb.MySql;
using Microsoft.Extensions.Hosting;
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
                .UseAspNetCore((module, webHostBuilder) => { })
                .HostModule<ComputeApiModule>()
                .HostModule<CommonApiModule>()
                .RunConsoleAsync();
        }
    }
}