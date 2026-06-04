using System;
using Dbosoft.Rebus.Operations;
using Eryph.AppCore;
using Eryph.Core;
using Eryph.Modules.GenePool.Genetics;
using Eryph.Rebus;
using Eryph.VmManagement;
using SimpleInjector;

namespace Eryph.GenePool
{
    /// <summary>
    /// Root-container registrations that <see cref="Eryph.Modules.GenePool.GenePoolModule"/>
    /// resolves through the cross-wired host service provider. The bus transport (mTLS or the
    /// plaintext dev path) is registered on the module container via the host filter in
    /// <see cref="HostGenePoolModuleExtensions"/>.
    /// </summary>
    internal static class GenePoolContainerExtensions
    {
        public static void Bootstrap(this Container container)
        {
            container.RegisterInstance(SelectGenePoolSettings());
            container.RegisterSingleton<IGenePoolApiKeyStore, GenePoolApiKeyStore>();
            container.Register<IGenePoolPathProvider, HyperVGenePoolPathProvider>();
            container.RegisterSingleton<IApplicationInfoProvider, GenePoolApplicationInfoProvider>();
            container.Register<INetworkProviderManager, NetworkProviderManager>();

            container.RegisterInstance(new WorkflowOptions
            {
                DispatchMode = WorkflowEventDispatchMode.Publish,
                EventDestination = QueueNames.Controllers,
                OperationsDestination = QueueNames.Controllers,
                DeferCompletion = TimeSpan.FromMinutes(1),
                JsonSerializerOptions = EryphJsonSerializerOptions.Options,
            });
        }

        // Defaults to the production gene pool; ERYPH_GENEPOOL_API overrides the API endpoint
        // (the same override eryph-zero honours), so a dev/staging pool can be targeted without
        // a code change.
        private static GenePoolSettings SelectGenePoolSettings()
        {
            var settings = GenePoolConstants.ProductionGenePool;
            var apiOverride = Environment.GetEnvironmentVariable("ERYPH_GENEPOOL_API");
            return string.IsNullOrWhiteSpace(apiOverride)
                ? settings
                : settings with { ApiEndpoint = new Uri(apiOverride) };
        }
    }
}
