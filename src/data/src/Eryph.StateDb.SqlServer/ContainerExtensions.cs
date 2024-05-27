using SimpleInjector.Integration.ServiceCollection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.StateDb.SqlServer;

public static class ContainerExtensions
{
    public static SimpleInjectorAddOptions RegisterSqlServerStateStore(
        this SimpleInjectorAddOptions options)
    {
        options.RegisterStateStore();
        options.Services.AddDbContext<StateStoreContext, SqlServerStateStoreContext>(
            (sp, dbOptions) => sp.GetRequiredService<Container>()
                .GetInstance<IStateStoreContextConfigurer>()
                .Configure(dbOptions));
        
        return options;
    }
}
