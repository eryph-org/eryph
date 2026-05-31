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

    Task<string> BuildPayloadAsync(CancellationToken cancellationToken);
}
