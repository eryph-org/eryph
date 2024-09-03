using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ardalis.Specification;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.StateDb;

public static class ContainerExtensions
{
    public static SimpleInjectorAddOptions RegisterStateStore(
        this SimpleInjectorAddOptions options)
    {
        options.Container.Register<IGeneRepository, GeneRepository>(Lifestyle.Scoped);

        options.Container.Register(
            typeof(IReadRepositoryBase<>),
            typeof(ReadOnlyStateStoreRepository<>),
            Lifestyle.Scoped);
        options.Container.Register(
            typeof(IReadonlyStateStoreRepository<>),
            typeof(ReadOnlyStateStoreRepository<>),
            Lifestyle.Scoped);
        options.Container.Register(
            typeof(IRepositoryBase<>),
            typeof(StateStoreRepository<>),
            Lifestyle.Scoped);
        options.Container.Register(
            typeof(IStateStoreRepository<>),
            typeof(StateStoreRepository<>),
            Lifestyle.Scoped);

        options.Container.Register<IStateStore, StateStore>(Lifestyle.Scoped);

        return options;
    }
}
