﻿using System;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Modules.ComputeApi;
using Microsoft.Extensions.Hosting;
using SimpleInjector;

namespace Eryph.ApiEndpoint
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var container = new Container();
            container.Bootstrap();

            await ModulesHost.CreateDefaultBuilder(args)
                .UseSimpleInjector(container)
                .UseAspNetCore((module, webHostBuilder) => { })
                .HostModule<ComputeApiModule>()
                .RunConsoleAsync();
        }
    }
}