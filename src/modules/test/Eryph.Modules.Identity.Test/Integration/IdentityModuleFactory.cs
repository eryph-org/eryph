using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.IdentityDb;
using IdentityServer4.EntityFramework.DbContexts;
using SimpleInjector;

namespace Eryph.Modules.Identity.Test.Integration
{
    public class IdentityModuleFactory : WebModuleFactory<IdentityModule>
    {
        private readonly Container _container = new Container();

        protected override IModulesHostBuilder CreateModuleHostBuilder()
        {
            var moduleHostBuilder = new ModulesHostBuilder();
            _container.Options.AllowOverridingRegistrations = true;
            moduleHostBuilder.UseSimpleInjector(_container);

            _container
                .Register<IDbContextConfigurer<ConfigurationDbContext>, InMemoryConfigurationStoreContextConfigurer>();

            return moduleHostBuilder;
        }
    }
}