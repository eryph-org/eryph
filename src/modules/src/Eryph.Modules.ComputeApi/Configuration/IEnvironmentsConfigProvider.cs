using Eryph.Core;

namespace Eryph.Modules.ComputeApi.Configuration;

/// <summary>
/// Holds the most recently applied controller-distributed <see cref="EnvironmentsConfig"/> — the sites
/// and environments of the deployment — so the configuration-option endpoints can present them to
/// clients. Updated by <see cref="EnvironmentsConfigRealizer"/>; the compute API only reads it.
/// </summary>
public interface IEnvironmentsConfigProvider
{
    /// <summary>The last applied environment configuration; an empty catalog until the first apply.</summary>
    EnvironmentsConfig Current { get; }

    void Update(EnvironmentsConfig config);
}

internal sealed class EnvironmentsConfigProvider : IEnvironmentsConfigProvider
{
    private volatile EnvironmentsConfig _current = new();

    public EnvironmentsConfig Current => _current;

    public void Update(EnvironmentsConfig config) => _current = config;
}
