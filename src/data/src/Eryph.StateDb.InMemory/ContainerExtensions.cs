using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.StateDb.InMemory;

public static class ContainerExtensions
{
    public static void RegisterInMemoryStateStore(this SimpleInjectorAddOptions options)
    {
        options.RegisterStateStore();
        options.Services.AddDbContext<StateStoreContext, InMemoryStateStoreContext>((sp, dbOptions) =>
        {
            var configurer = sp.GetRequiredService<Container>()
                .GetInstance<IStateStoreContextConfigurer>();
            configurer.Configure(dbOptions);
        });
    }
}
