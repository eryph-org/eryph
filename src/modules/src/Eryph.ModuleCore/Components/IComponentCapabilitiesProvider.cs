using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Contributes the capabilities a component advertises to the controller at
/// registration (e.g. a host agent's datastore and environment names). A module
/// implements this for the local settings it wants the controller to know about;
/// the controller persists them for management transparency and placement.
/// </summary>
public interface IComponentCapabilitiesProvider
{
    Task<IReadOnlyDictionary<string, string>> GetCapabilitiesAsync(CancellationToken cancellationToken);
}
