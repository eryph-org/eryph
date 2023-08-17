using System;
using System.Collections.Generic;
using System.Linq;
using SimpleInjector;

namespace Eryph.Modules.VmHostAgent.Genetics;


internal class GenePoolFactory: IGenePoolFactory
{
    readonly Container _container;
    readonly Dictionary<string, InstanceProducer<IGenePool>> _producers =
        new(
            StringComparer.OrdinalIgnoreCase);

    public GenePoolFactory(Container container)
    {
        _container = container;
    }

    public IEnumerable<string> RemotePools => _producers.Keys.Where(x=> x!=GenePoolNames.Local);

    IGenePool IGenePoolFactory.CreateNew(string name)
    {
        var result = _producers[name].GetInstance();
        result.PoolName  = name;
        return result;
    }

    ILocalGenePool IGenePoolFactory.CreateLocal() =>
        (ILocalGenePool) _producers[GenePoolNames.Local].GetInstance();

    public void Register<TImplementation>(string name)
        where TImplementation : class, IGenePool
    {
        var producer = Lifestyle.Transient
            .CreateProducer<IGenePool, TImplementation>(_container);

        _producers.Add(name, producer);
    }

}