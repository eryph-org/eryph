using System;
using System.IO;
using Eryph.Runtime.Zero.Configuration;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Eryph.Runtime.Zero;

public class SqlLiteStateStoreContextConfigurer : IDbContextConfigurer<StateStoreContext>
{
    private readonly ZeroStateDbTransactionInterceptor _dbTransactionInterceptor;

    public SqlLiteStateStoreContextConfigurer()
    {
    }

    public void Configure(DbContextOptionsBuilder options)
    {
        var path = Path.Combine(ZeroConfig.GetPrivateConfigPath(), "state.db");
        options.UseSqlite($"Data Source=\"{path}\"");
        options.ConfigureWarnings(x => x.Ignore(RelationalEventId.AmbientTransactionWarning));
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
        //options.AddInterceptors(_dbTransactionInterceptor);
    }
}