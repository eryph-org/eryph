using System.Collections.Generic;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Supplies extra endpoints a component advertises to the controller at registration, resolved at
/// registration time rather than baked in at module setup. The hosting process registers an
/// implementation when it exposes endpoints whose presence depends on the deployment (e.g. the
/// standalone network host exposes the OVN databases over SSL and advertises them; the same module
/// in-process under eryph-zero registers no provider and advertises nothing). The module itself stays
/// mode-blind — the host wires the implementation, the module never reads a flag to decide.
/// </summary>
public interface IComponentEndpointProvider
{
    /// <summary>Logical endpoint name → URL/address, merged into the component's advertised set.</summary>
    IReadOnlyDictionary<string, string> GetAdvertisedEndpoints();
}
