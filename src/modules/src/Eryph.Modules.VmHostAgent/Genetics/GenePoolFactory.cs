using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal class GenePoolFactory(Container container) : IGenePoolFactory
{
    private readonly Dictionary<string, InstanceProducer<IGenePool>> _producers =
        new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> RemotePools => _producers.Keys
        .Where(x=> x != GenePoolConstants.Local.Name);

    IGenePool IGenePoolFactory.CreateNew(string name)
    {
        var result = _producers[name].GetInstance();
        return result;
    }

    ILocalGenePool IGenePoolFactory.CreateLocal() =>
        (ILocalGenePool) _producers[GenePoolConstants.Local.Name].GetInstance();

    public void Register<TImplementation>(string name)
        where TImplementation : class, IGenePool
    {
        var producer = Lifestyle.Transient
            .CreateProducer<IGenePool>(
                () => ActivatorUtilities.CreateInstance<TImplementation>(container, name),
                container);

        _producers.Add(name, producer);
    }
}
