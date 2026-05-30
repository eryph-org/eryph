using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Core.VmAgent;
using Eryph.AppCore;
using Eryph.Modules.HostAgent.Configuration;
using LanguageExt;
using LanguageExt.Common;

using RT = LanguageExt.Sys.Live.Runtime;
using static LanguageExt.Prelude;

namespace Eryph.Agent
{
    /// <summary>
    /// Standalone-runtime implementation of <see cref="IApplicationInfoProvider"/>
    /// (the agent's counterpart to eryph-zero's provider).
    /// </summary>
    internal sealed class AgentApplicationInfoProvider : IApplicationInfoProvider
    {
        public AgentApplicationInfoProvider()
        {
            Name = "eryph-agent";
            var entryAssembly = Assembly.GetEntryAssembly()!;
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(entryAssembly.Location);
            ProductVersion = fileVersionInfo.ProductVersion ?? "unknown";
            // Truncated to 24 characters for compatibility with AutoRest.
            ApplicationId = $"agent-{ProductVersion}"[..24];
        }

        public string Name { get; }
        public string ProductVersion { get; }
        public string ApplicationId { get; set; }
    }

    /// <summary>
    /// Reads the VM host agent configuration (<c>agentsettings.yml</c>) from the standalone
    /// component config root. Mirrors eryph-zero's manager but uses <see cref="AppConfigPaths"/>.
    /// </summary>
    internal sealed class AgentVmHostAgentConfigurationManager : IVmHostAgentConfigurationManager
    {
        public EitherAsync<Error, VmHostAgentConfiguration> GetCurrentConfiguration(
            HostSettings hostSettings) =>
            VmHostAgentConfiguration<RT>.readConfig(
                    Path.Combine(AppConfigPaths.GetVmHostAgentConfigPath(), "agentsettings.yml"),
                    hostSettings)
                .Run(RT.New())
                .ToEitherAsync();
    }

    /// <summary>
    /// Split-runtime placeholder for <see cref="INetworkSyncService"/>. In eryph-zero the
    /// host-agent resolves the controller's in-process network sync service directly; in a
    /// split deployment those operations belong to the controller and must travel over the
    /// bus. Until that path exists, the agent's named-pipe sync commands
    /// (REBUILD_NETWORKS / VALIDATE_CHANGES) report that they are not available locally.
    /// This is never invoked during normal boot, registration, or config application.
    /// </summary>
    internal sealed class UnavailableNetworkSyncService : INetworkSyncService
    {
        private static readonly Error NotAvailable = Error.New(
            "Network synchronization is owned by the controller and is not available "
            + "directly in the standalone agent runtime.");

        public EitherAsync<Error, Unit> SyncNetworks(CancellationToken cancellationToken) =>
            LeftAsync<Error, Unit>(NotAvailable);

        public EitherAsync<Error, string[]> ValidateChanges(NetworkProvider[] networkProviders) =>
            LeftAsync<Error, string[]>(NotAvailable);
    }
}
