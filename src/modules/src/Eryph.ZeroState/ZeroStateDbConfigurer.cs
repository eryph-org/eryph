using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SimpleInjector;

namespace Eryph.ZeroState;

internal class ZeroStateDbConfigurer : IDbContextConfigurer<StateStoreContext>
{
    private readonly IDbContextConfigurer<StateStoreContext> _decoratedConfigurer;
    private readonly IEnumerable<IDbTransactionInterceptor> _interceptors;

    public ZeroStateDbConfigurer(
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
