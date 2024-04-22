using System.Collections.Generic;
using System.Linq;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Eryph.Modules.Controller.ChangeTracking;

internal class ChangeTrackingDbConfigurer : IDbContextConfigurer<StateStoreContext>
{
    private readonly IDbContextConfigurer<StateStoreContext> _decoratedConfigurer;
    private readonly IEnumerable<IDbTransactionInterceptor> _interceptors;

    public ChangeTrackingDbConfigurer(
        IDbContextConfigurer<StateStoreContext> decoratedConfigurer,
        IEnumerable<IDbTransactionInterceptor> interceptors)
    {
        _decoratedConfigurer = decoratedConfigurer;
        _interceptors = interceptors;
    }

    public void Configure(DbContextOptionsBuilder options)
    {
        _decoratedConfigurer.Configure(options);
        options.AddInterceptors(_interceptors.ToList());
    }
}
