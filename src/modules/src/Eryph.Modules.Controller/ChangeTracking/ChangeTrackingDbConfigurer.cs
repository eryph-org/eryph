using System.Collections.Generic;
using System.Linq;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Eryph.Modules.Controller.ChangeTracking;

internal class ChangeTrackingDbConfigurer(
    IDbContextConfigurer<StateStoreContext> decoratedConfigurer,
    IEnumerable<IDbTransactionInterceptor> transactionInterceptors,
    IEnumerable<ISaveChangesInterceptor> saveChangesInterceptors)
    : IDbContextConfigurer<StateStoreContext>
{
    public void Configure(DbContextOptionsBuilder options)
    {
        decoratedConfigurer.Configure(options);
        options.AddInterceptors(saveChangesInterceptors.ToList());
        options.AddInterceptors(transactionInterceptors.ToList());
    }
}
