using SimpleInjector.Integration.ServiceCollection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.StateDb.InMemory;

public static class ContainerExtensions
{
    public static void RegisterInMemoryStateStore(this SimpleInjectorAddOptions options)
    {
        options.RegisterStateStore();
        options.Services.AddDbContext<StateStoreContext, InMemoryStateStoreContext>(
            (sp, dbOptions) =>
            {
                var configurer = sp.GetRequiredService<Container>()
                    .GetInstance<IStateStoreContextConfigurer>();
                configurer.Configure(dbOptions);
            });
    }
}
