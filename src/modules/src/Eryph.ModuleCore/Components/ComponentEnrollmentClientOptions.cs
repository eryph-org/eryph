using System;

namespace Eryph.ModuleCore.Components;

/// <summary>Options for the component enrollment client (operator-provisioned secret + retry backoff).</summary>
public sealed class ComponentEnrollmentClientOptions
{
    /// <summary>The enrollment credential presented to the identity service (operator-provisioned).</summary>
    public string EnrollmentSecret { get; init; } = "";

    /// <summary>Initial delay between enrollment retries; doubles up to <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(30);
}
