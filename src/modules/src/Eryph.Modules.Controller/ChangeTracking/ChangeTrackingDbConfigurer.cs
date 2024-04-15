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
    private readonly ChangeTrackingInterceptorContext _interceptorContext;

    public ChangeTrackingDbConfigurer(
        IDbContextConfigurer<StateStoreContext> decoratedConfigurer,
        IEnumerable<IDbTransactionInterceptor> interceptors, 
        ChangeTrackingInterceptorContext interceptorContext)
    {
        _decoratedConfigurer = decoratedConfigurer;
        _interceptors = interceptors;
        _interceptorContext = interceptorContext;
    }

    public void Configure(DbContextOptionsBuilder options)
    {
        _decoratedConfigurer.Configure(options);
        if(_interceptorContext.EnableInterceptors)
            options.AddInterceptors(_interceptors.ToList());
    }
}
