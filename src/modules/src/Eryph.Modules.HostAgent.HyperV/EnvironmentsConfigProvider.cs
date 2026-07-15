using Eryph.Core;

namespace Eryph.Modules.HostAgent;

/// <summary>
/// Holds the most recently applied controller-distributed <see cref="EnvironmentsConfig"/> — the
/// environment vocabulary the agent is allowed to serve — so the provisioning handlers can enforce
/// it. Updated by <see cref="EnvironmentsConfigRealizer"/>.
/// </summary>
internal interface IEnvironmentsConfigProvider
{
    /// <summary>The last applied environment configuration; empty until the first apply.</summary>
    EnvironmentsConfig Current { get; }

    void Update(EnvironmentsConfig config);
}

internal sealed class EnvironmentsConfigProvider : IEnvironmentsConfigProvider
{
    private volatile EnvironmentsConfig _current = new();

    public EnvironmentsConfig Current => _current;

    public void Update(EnvironmentsConfig config) => _current = config;
}
