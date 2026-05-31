using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Eryph.Messages.Components;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// The identity a module uses when registering with the controller.
/// <see cref="ComponentId"/> is stable across restarts (derived deterministically
/// from the component type and the host's fully-qualified domain name, not a random
/// per-run value); <see cref="InstanceId"/> identifies this particular run.
/// </summary>
/// <remarks>
/// Identity is domain-name based (FQDN) rather than the short machine name: the FQDN is
/// globally unique across a multi-host deployment and aligns with how certificate/mTLS
/// identities name hosts, which is the foundation for component authentication.
/// </remarks>
public sealed class ComponentIdentity
{
    public ComponentIdentity(
        ComponentType componentType,
        string inboundQueue,
        IReadOnlyDictionary<string, string>? advertisedEndpoints = null)
    {
        ComponentType = componentType;
        InboundQueue = inboundQueue;
        MachineName = GetFullyQualifiedDomainName();
        ComponentId = DeriveComponentId(componentType, MachineName);
        InstanceId = Guid.NewGuid();
        Version = GetComponentVersion();
        // Defensive copy so a caller cannot mutate what the component advertises after
        // construction; endpoint names are compared case-insensitively, as they are downstream.
        AdvertisedEndpoints = advertisedEndpoints is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(advertisedEndpoints, StringComparer.OrdinalIgnoreCase);
    }

    public Guid ComponentId { get; }

    public Guid InstanceId { get; }

    public ComponentType ComponentType { get; }

    public string InboundQueue { get; }

    public string MachineName { get; }

    /// <summary>The running build version of this component (entry assembly), for the catalog.</summary>
    public string Version { get; }

    /// <summary>
    /// Service endpoints this component hosts and advertises to the controller (logical
    /// name → URL). Empty for components that host nothing.
    /// </summary>
    public IReadOnlyDictionary<string, string> AdvertisedEndpoints { get; }

    /// <summary>
    /// A globally-unique, stable identifier for the local host: the lower-cased
    /// fully-qualified domain name (DNS is case-insensitive). This is the same host
    /// identity <see cref="ComponentId"/> is derived from; components also use it to suffix
    /// their inbound queue so queue names are unique across hosts on a shared broker — the
    /// short machine name is not (two hosts can share it in different DNS domains).
    /// </summary>
    public static string GetLocalHostId() => GetFullyQualifiedDomainName().ToLowerInvariant();

    private static string GetComponentVersion()
    {
        var entry = Assembly.GetEntryAssembly();
        if (entry is null)
            return "";
        var productVersion = FileVersionInfo.GetVersionInfo(entry.Location).ProductVersion;
        return productVersion ?? entry.GetName().Version?.ToString() ?? "";
    }

    /// <summary>
    /// Derives the stable component id from the component type and host FQDN. This is the same
    /// value a component computes for itself and that the enrollment service derives server-side,
    /// so the identity bound into an issued certificate cannot be independently spoofed by the
    /// requester. Deterministic and case-insensitive in the FQDN.
    /// </summary>
    public static Guid DeriveComponentId(ComponentType componentType, string fullyQualifiedDomainName)
    {
        var name = $"eryph-component:{componentType}:{fullyQualifiedDomainName.ToLowerInvariant()}";
        // SHA-256 (not SHA-1) so the derivation isn't flagged by security tooling; the id only
        // needs to be deterministic and collision-resistant, so the first 16 bytes form the Guid.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }

    /// <summary>
    /// The host's fully-qualified domain name (host + DNS domain). Falls back to the short
    /// host name when the machine is not domain-joined (e.g. a workgroup dev box), which keeps
    /// the identity stable in that environment.
    /// </summary>
    private static string GetFullyQualifiedDomainName()
    {
        var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
        var hostName = ipProperties.HostName;
        var domainName = ipProperties.DomainName;

        // Fall back to the machine name if the host name is unavailable, so the derived
        // ComponentId stays well-formed and stable rather than becoming ".domain".
        if (string.IsNullOrWhiteSpace(hostName))
            return Environment.MachineName;

        if (string.IsNullOrEmpty(domainName)
            || hostName.EndsWith("." + domainName, StringComparison.OrdinalIgnoreCase))
            return hostName;

        return $"{hostName}.{domainName}";
    }
}
