using SimpleInjector;
using Topshelf.HostConfigurators;

namespace Haipa.Modules.Hosting
{
    public static class HostConfiguratorExtensions
    {
        #region Public Static Methods

        public static HostConfigurator UseSimpleInjector(this HostConfigurator configurator, Container container)
        {
            configurator.AddConfigurator(new SimpleInjectorHostBuilderConfigurator(container));
            return configurator;
        }

        #endregion
    }
}