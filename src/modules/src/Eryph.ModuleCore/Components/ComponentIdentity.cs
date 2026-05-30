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
        ComponentId = CreateStableId(componentType, MachineName);
        InstanceId = Guid.NewGuid();
        Version = GetComponentVersion();
        AdvertisedEndpoints = advertisedEndpoints ?? new Dictionary<string, string>();
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

    private static string GetComponentVersion()
    {
        var entry = Assembly.GetEntryAssembly();
        if (entry is null)
            return "";
        var productVersion = FileVersionInfo.GetVersionInfo(entry.Location).ProductVersion;
        return productVersion ?? entry.GetName().Version?.ToString() ?? "";
    }

    private static Guid CreateStableId(ComponentType componentType, string fullyQualifiedDomainName)
    {
        var name = $"eryph-component:{componentType}:{fullyQualifiedDomainName.ToLowerInvariant()}";
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(name));
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
