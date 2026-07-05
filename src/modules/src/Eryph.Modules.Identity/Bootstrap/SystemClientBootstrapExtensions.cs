using Eryph.ModuleCore.Startup;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Identity.Bootstrap;

public static class SystemClientBootstrapExtensions
{
    extension(SimpleInjectorAddOptions options)
    {
        /// <summary>
        /// Registers the <see cref="SystemClientBootstrap"/> startup handler so the module ensures the
        /// <c>system-client</c> on startup. The host must also register an
        /// <see cref="ISystemClientKeyStore"/> on the module container; the bootstrap resolves it to read
        /// and persist the private key. A packaging that does not call this (e.g. the in-memory test host)
        /// simply has no system-client bootstrap.
        /// </summary>
        public void AddSystemClientBootstrap() => options.AddStartupHandler<SystemClientBootstrap>();
    }
}
