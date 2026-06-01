using System;
using System.Collections.Generic;

namespace Eryph.ModuleCore.Components;

/// <summary>Options for the component enrollment client (one-time token + server DNS names + retry backoff).</summary>
public sealed class ComponentEnrollmentClientOptions
{
    /// <summary>The one-time enrollment token presented to the identity service (from the enrollment file).</summary>
    public string Token { get; init; } = "";

    /// <summary>The DNS name(s) the component serves on; requested as the server certificate's SAN.
    /// Empty means the component's FQDN is used.</summary>
    public IReadOnlyList<string> ServerDnsNames { get; init; } = [];

    /// <summary>Initial delay between enrollment retries; doubles up to <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(30);
}
