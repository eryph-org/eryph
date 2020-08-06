using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Hosuto.Modules.Testing;
using Haipa.IdentityDb;
using SimpleInjector;

namespace Haipa.Modules.Identity.Test.Integration
{
    public class IdentityModuleFactory : WebModuleFactory<IdentityModule>
    {
        readonly Container _container = new Container();

        protected override IModulesHostBuilder CreateModuleHostBuilder()
        {
            var moduleHostBuilder = new ModulesHostBuilder();
            moduleHostBuilder.UseSimpleInjector(_container);

            _container.Register<IDbContextConfigurer<ConfigurationStoreContext>, InMemoryConfigurationStoreContextConfigurer>();

            return moduleHostBuilder;
        }

    }
}