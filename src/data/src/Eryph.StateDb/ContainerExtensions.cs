using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using Container = SimpleInjector.Container;

namespace Eryph.StateDb;

public static class ContainerExtensions
{
    public static SimpleInjectorAddOptions RegisterStateStore(this SimpleInjectorAddOptions options)
    {
        options.Services.AddDbContext<StateStoreContext>(
            (sp, dbOptions) => sp.GetRequiredService<Container>()
                .GetInstance<IDbContextConfigurer<StateStoreContext>>()
                .Configure(dbOptions));

        options.Container.Register(typeof(IReadonlyStateStoreRepository<>), typeof(ReadOnlyStateStoreRepository<>), Lifestyle.Scoped);
        options.Container.Register(typeof(IStateStoreRepository<>), typeof(StateStoreRepository<>), Lifestyle.Scoped);
        options.Container.Register<IStateStore, StateStore>(Lifestyle.Scoped);

        options.AddLogging();

        return options;
    }
}