using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.HostAgent;

/// <summary>
/// Pilot/demonstration realizer for the <see cref="ConfigDomain.Projects"/> domain.
/// The host agent does not consume project configuration in production; this exists
/// only to exercise the config-distribution mechanism end-to-end in eryph-zero
/// (register → snapshot → apply → acknowledge). It records receipt and nothing more.
/// </summary>
internal sealed class ProjectsConfigDemoRealizer(
    ILogger<ProjectsConfigDemoRealizer> logger)
    : IConfigRealizer
{
    public ConfigDomain Domain => ConfigDomain.Projects;

    public Task ApplyAsync(long version, string payload, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Applied {Domain} configuration version {Version} ({Length} bytes).",
            ConfigDomain.Projects, version, payload?.Length ?? 0);
        return Task.CompletedTask;
    }
}
