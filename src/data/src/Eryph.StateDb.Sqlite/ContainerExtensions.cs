using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.StateDb.Sqlite;

public static class ContainerExtensions
{
    public static SimpleInjectorAddOptions RegisterSqliteStateStore(
        this SimpleInjectorAddOptions options)
    {
        options.RegisterStateStore();
        options.Services.AddDbContext<StateStoreContext, SqliteStateStoreContext>(
            (sp, dbOptions) =>
            {
                var configurer = sp.GetRequiredService<Container>()
                    .GetInstance<IStateStoreContextConfigurer>();
                configurer.Configure(dbOptions);
            });

        return options;
    }
}
