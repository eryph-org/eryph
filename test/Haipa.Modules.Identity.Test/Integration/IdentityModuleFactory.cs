using Haipa.IdentityDb;
using Haipa.TestUtils;
using IdentityServer4.EntityFramework.DbContexts;
using SimpleInjector;

namespace Haipa.Modules.Identity.Test.Integration
{
    public class IdentityModuleFactory : WebModuleFactory<IdentityModule>
    {
        protected override void ConfigureModuleContainer(Container container)
        {
            base.ConfigureModuleContainer(container);
            container.Register<IDbContextConfigurer<ConfigurationDbContext>, InMemoryConfigurationStoreContextConfigurer>();           
        }        
    }
}