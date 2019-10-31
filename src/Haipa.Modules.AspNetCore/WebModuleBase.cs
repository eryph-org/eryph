using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.AspNetCore;
using SimpleInjector.Integration.ServiceCollection;

namespace Haipa.Modules
{
    public abstract class WebModuleBase : ModuleBase
    {
        public abstract string Path { get; }

        public sealed override void AddSimpleInjector(SimpleInjectorAddOptions options)
        {
            AddAspNetCore(options.AddAspNetCore());

        }

        protected virtual void AddAspNetCore(SimpleInjectorAspNetCoreBuilder builder)
        {
            builder
                .AddControllerActivation()
                .AddViewComponentActivation();
        }


        public abstract void Configure(IApplicationBuilder app);

        public override void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {
            
        }
    }
}