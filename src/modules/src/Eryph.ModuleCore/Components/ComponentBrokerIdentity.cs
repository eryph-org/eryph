using System;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Maps a component identity to its broker (RabbitMQ) user name. The name is the component-id URN —
/// the exact value the broker derives from the client certificate's URI SAN
/// (<c>ssl_cert_login_from = subject_alternative_name</c>) when a component authenticates with SASL
/// EXTERNAL. Deriving the user name the same way on both sides means the authenticated AMQP user IS
/// the verified component identity, so a per-component user can be provisioned at enrollment and the
/// controller can trust the broker-stamped sender.
/// </summary>
public static class ComponentBrokerIdentity
{
    /// <summary>
    /// The URN prefix carried as the component certificate's URI SAN and used as the broker user name
    /// prefix. Must match what <c>ComponentCertificateAuthority</c> writes and what the broker is
    /// configured to read from the certificate.
    /// </summary>
    public const string ComponentUrnPrefix = "urn:eryph:component:";

    /// <summary>The broker user name for the given component id (the component-id URN).</summary>
    public static string UserName(Guid componentId) => ComponentUrnPrefix + componentId;
}
