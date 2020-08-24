using System;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Haipa.Modules.Api;
using Haipa.Modules.Identity;
using Microsoft.AspNetCore;
using SimpleInjector;

namespace Haipa.Identity
{
    public class Program
    {

        public static async Task Main(string[] args)
        {
            var container = new Container();
            container.Bootstrap();

            await ModulesHost.CreateDefaultBuilder(args)
                .UseSimpleInjector(container)
                .UseAspNetCore((module, webHostBuilder) =>
                {

                })
                .RunModule<IdentityModule>();
        }

    }
}
