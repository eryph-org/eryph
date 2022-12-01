using System.Collections.Generic;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Hosuto.Modules.Testing;
using Dbosoft.IdentityServer.EfCore.Storage.DbContexts;
using Dbosoft.IdentityServer.Storage.Stores;
using Dbosoft.IdentityServer.Stores;
using Eryph.IdentityDb;
using Eryph.ModuleCore;
using Microsoft.EntityFrameworkCore.Storage;
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


            //_container.RegisterInstance(new InMemoryDatabaseRoot());
            //_container
            //    .Register<IDbContextConfigurer<ConfigurationDbContext>, InMemoryConfigurationStoreContextConfigurer>();

            return moduleHostBuilder;
        }

    }
}