using System;
using System.Collections.Generic;
using System.Linq;
using SimpleInjector;

namespace Eryph.Modules.VmHostAgent.Images;


internal class ImageSourceFactory: IImageSourceFactory
{
    readonly Container _container;
    readonly Dictionary<string, InstanceProducer<IImageSource>> _producers =
        new(
            StringComparer.OrdinalIgnoreCase);

    public ImageSourceFactory(Container container)
    {
        _container = container;
    }

    public IEnumerable<string> RemoteSources => _producers.Keys.Where(x=> x!=ImagesSources.Local);

    IImageSource IImageSourceFactory.CreateNew(string name)
    {
        var result = _producers[name].GetInstance();
        result.SourceName  = name;
        return result;
    }

    ILocalImageSource IImageSourceFactory.CreateLocal() =>
        (ILocalImageSource) _producers[ImagesSources.Local].GetInstance();

    public void Register<TImplementation>(string name)
        where TImplementation : class, IImageSource
    {
        var producer = Lifestyle.Transient
            .CreateProducer<IImageSource, TImplementation>(_container);

        _producers.Add(name, producer);
    }

}