using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.Modules.GenePool.Genetics;

internal class GenePoolFactory(Container container) : IGenePoolFactory
{
    private readonly Dictionary<string, InstanceProducer<IGenePool>> _producers =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> RemotePools => _producers.Keys.ToList();

    IGenePool IGenePoolFactory.CreateNew(string name)
    {
        var result = _producers[name].GetInstance();
        return result;
    }

    public void Register<TImplementation>(GenePoolSettings settings)
        where TImplementation : class, IGenePool
    {
        var producer = Lifestyle.Transient
            .CreateProducer<IGenePool>(
                () => ActivatorUtilities.CreateInstance<TImplementation>(container, settings),
                container);

        _producers.Add(settings.Name, producer);
    }
}
