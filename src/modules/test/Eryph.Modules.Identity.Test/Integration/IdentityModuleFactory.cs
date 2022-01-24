using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Hosuto.Modules.Testing;
using Dbosoft.IdentityServer.EfCore.Storage.DbContexts;
using Dbosoft.IdentityServer.Storage.Stores;
using Dbosoft.IdentityServer.Stores;
using Eryph.IdentityDb;
using Eryph.ModuleCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.Modules.Identity.Test.Integration
{
    public class IdentityModuleFactory : WebModuleFactory<IdentityModule>
    {
        private readonly Container _container = new();
        
        protected override IModulesHostBuilder CreateModuleHostBuilder()
        {
            var moduleHostBuilder = new ModulesHostBuilder();
            _container.Options.AllowOverridingRegistrations = true;
            moduleHostBuilder.UseSimpleInjector(_container);


            var endpoints = new Dictionary<string, string>
            {
                {"identity", "http://localhost/identity"},
                {"compute", "http://localhost/compute"},
                {"common", "http://localhost/common"},
            };

            _container.RegisterInstance<IEndpointResolver>(new EndpointResolver(endpoints));


            _container.RegisterInstance(new InMemoryDatabaseRoot());
            _container
                .Register<IDbContextConfigurer<ConfigurationDbContext>, InMemoryConfigurationStoreContextConfigurer>();

            return moduleHostBuilder;
        }

    }

    public class IdentityModuleNoAuthFactory : IdentityModuleFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IAuthorizationHandler, AllowAnonymous>();
            });
        }


        private class AllowAnonymous : IAuthorizationHandler
        {
            public Task HandleAsync(AuthorizationHandlerContext context)
            {
                foreach (var requirement in context.PendingRequirements.ToList())
                    context.Succeed(requirement); //Simply pass all requirements

                return Task.CompletedTask;
            }
        }
    }
}