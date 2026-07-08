using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Builds the serialized payload for a configuration domain from the controller's
/// authoritative state. Used to (re)materialize a <c>ConfigRecord</c>.
/// </summary>
internal interface IConfigSource
{
    ConfigDomain Domain { get; }

    /// <summary>
    /// Builds the payload for the domain at the given scope. Authorable domains read the operator's
    /// value at that scope; system-derived domains are global and are only materialized at the
    /// default scope.
    /// </summary>
    Task<string> BuildPayloadAsync(string scope, CancellationToken cancellationToken);
}
