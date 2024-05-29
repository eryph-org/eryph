using System.Collections.Generic;
using System.Linq;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Eryph.Modules.Controller.ChangeTracking;

internal class ChangeTrackingDbConfigurer(
    IStateStoreContextConfigurer decoratedConfigurer,
    IEnumerable<IDbTransactionInterceptor> interceptors)
    : IStateStoreContextConfigurer
{
    public void Configure(DbContextOptionsBuilder options)
    {
        decoratedConfigurer.Configure(options);
        options.AddInterceptors(interceptors.ToList());
    }
}
