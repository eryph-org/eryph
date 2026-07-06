using System.Text.Json.Serialization;

namespace Eryph.Messages.Components;

/// <summary>
/// The kind of component that registers with the controller. Determines which
/// configuration domains the component is entitled to receive.
/// </summary>
/// <remarks>Serialized by name (not ordinal) — see <see cref="ConfigDomain"/>.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ComponentType
{
    VMHostAgent,
    GenePoolAgent,
    Network,
    ComputeApi,
    Identity,

    /// <summary>
    /// The management API (full/server deployments only). It registers so it can authenticate on the
    /// bus via mTLS and dispatch operations to the controller; it is not entitled to any configuration
    /// domain, since it authors configuration rather than receiving it.
    /// </summary>
    Management,

    /// <summary>
    /// The controller itself. It does not register in the component catalog (it is the authority),
    /// but it has a component identity so it can authenticate on the bus via mTLS.
    /// </summary>
    Controller,
}
