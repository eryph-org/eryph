using Dbosoft.OVN;
using SimpleInjector;

namespace Eryph.Controller
{
    internal static class OvnHosting
    {
        /// <summary>
        /// Registers the OVN system environment for the controller's network handlers.
        /// Uses the cross-platform <see cref="SystemEnvironment"/> (Dbosoft.OVN.Core) — the
        /// Windows-specific environment belongs to eryph-zero / the host-agent chassis, not
        /// the cross-platform controller runtime.
        /// </summary>
        public static void UseOvn(this Container container)
        {
            var ovnSettings = new LocalOVSWithOVNSettings();
            container.RegisterInstance<IOVNSettings>(ovnSettings);
            container.RegisterInstance<IOvsSettings>(ovnSettings);
            container.RegisterSingleton<ISystemEnvironment, SystemEnvironment>();
        }
    }
}
