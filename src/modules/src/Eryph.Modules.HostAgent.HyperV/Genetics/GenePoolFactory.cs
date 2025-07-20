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

    ILocalGenePool IGenePoolFactory.CreateLocal(string genePoolPath) =>
        ActivatorUtilities.CreateInstance<LocalGenePoolSource>(
            container, GenePoolConstants.Local.Name, genePoolPath);

    public void Register<TImplementation>(GenepoolSettings settings)
        where TImplementation : class, IGenePool
    {
        var producer = Lifestyle.Transient
            .CreateProducer<IGenePool>(
                () => ActivatorUtilities.CreateInstance<TImplementation>(container, settings),
                container);

        _producers.Add(settings.Name, producer);
    }
}
