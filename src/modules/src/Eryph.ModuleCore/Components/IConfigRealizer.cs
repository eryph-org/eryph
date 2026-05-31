using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Applies the configuration of a single <see cref="ConfigDomain"/> received from
/// the controller to local system state. Must be idempotent: re-applying the same
/// or an older-then-newer version has to converge. A module registers one realizer
/// per domain it consumes (auto-registered as part of an <c>IConfigRealizer</c>
/// collection).
/// </summary>
public interface IConfigRealizer
{
    ConfigDomain Domain { get; }

    Task ApplyAsync(long version, string payload, CancellationToken cancellationToken);
}
