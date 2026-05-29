using System.Collections.Concurrent;
using System.Collections.Generic;
using Eryph.Messages.Components;

namespace Eryph.ModuleCore.Components;

internal sealed class ComponentConfigState : IComponentConfigState
{
    private readonly ConcurrentDictionary<ConfigDomain, long> _applied = new();

    public void SetApplied(ConfigDomain domain, long version) =>
        _applied.AddOrUpdate(domain, version, (_, existing) => version > existing ? version : existing);

    public IReadOnlyDictionary<ConfigDomain, long> GetApplied() =>
        new Dictionary<ConfigDomain, long>(_applied);
}
